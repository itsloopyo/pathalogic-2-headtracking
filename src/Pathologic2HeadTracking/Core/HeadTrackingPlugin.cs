using System;
using BepInEx;
using CameraUnlock.Core.Data;
using CameraUnlock.Core.Processing;
using CameraUnlock.Core.Protocol;
using CameraUnlock.Core.Tracking;
using CameraUnlock.Core.Unity.Extensions;
using CameraUnlock.Core.Unity.Rendering;
using CameraUnlock.Core.Unity.Tracking;
using CameraUnlock.Core.Unity.UI;
using Pathologic2HeadTracking.Aim;
using Pathologic2HeadTracking.CameraRig;
using Pathologic2HeadTracking.Config;
using Pathologic2HeadTracking.Diagnostics;
using Pathologic2HeadTracking.Game;
using UnityEngine;

namespace Pathologic2HeadTracking.Core
{
    /// <summary>
    /// Nothing here depends on running after Pathologic 2's own camera update, which
    /// finishes in EngineUpdateBehaviour.LateUpdate -> UpdateService.LateUpdate ->
    /// CameraService.ComputeUpdate. LateUpdate here only advances the tracker
    /// pipeline; the camera transform is read in Camera.onPreCull and in OnGUI, both
    /// of which run after every LateUpdate in the frame. That matters because
    /// [DefaultExecutionOrder] cannot help: it is baked into a build's script
    /// execution order at build time, and an assembly loaded into a shipped game
    /// after the fact is never in that table.
    /// </summary>
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    public class HeadTrackingPlugin : BaseUnityPlugin
    {
        public const string PluginGUID = "com.cameraunlock.pathologic2.headtracking";
        public const string PluginName = "Pathologic 2 Head Tracking";
        public const string PluginVersion = "0.0.0";

        private const float StartupNotificationSeconds = 4f;
        private const float StatusNotificationSeconds = 1.5f;
        private const int ReticleBaseSizeAt1080p = 6;
        private const int ReticleOutlineWidthAt1080p = 2;
        private const int TrackingModeCount = 3;

        // The lookups that cannot be done at startup are polled on this interval
        // rather than every frame. Each one walks the loaded assemblies, and none is
        // worth a frame's cost: the slowest consequence of the delay is the lean
        // clamp arming half a second into a 1.5s gameplay warmup.
        private const float DeferredResolveIntervalSeconds = 0.5f;

        public bool TrackingEnabled { get; private set; }

        private ConfigManager _config;
        private OpenTrackReceiver _receiver;
        private TrackingProcessor _processor;
        private PoseInterpolator _interpolator;
        private PositionProcessor _positionProcessor;
        private PositionInterpolator _positionInterpolator;
        private ViewMatrixTrackingController _cameraController;
        private GameStateDetector _gameStateDetector;
        private InputHandler _inputHandler;
        private NotificationUI _notificationUI;
        private IMGUIReticle _aimReticle;
        private ReticlePlacer _reticlePlacer;
        private StockPrompt _stockPrompt;
        private AimTrace _aimTrace;
        private LeanClampController _leanClamp;
        private AdditionalCameraMirror _additionalCameras;
        private ZoomFactorTracker _zoom;
        private AimGeometryLog _aimGeometryLog;
        private SceneSurvey _sceneSurvey;
        private WindowCentering _windowCentering;
        private FrameTimingLog _frameTiming;
        private TrackerConnectionMonitor _connectionMonitor;

        private TrackingMode _trackingMode;
        private bool _initialized;

        private float _nextDeferredResolveTime;
        private Action<string> _logInfo;
        private Action<string> _logWarning;

        private void Awake()
        {
            Logger.LogInfo(PluginName + " v" + PluginVersion + " initializing...");

            _config = new ConfigManager();
            _config.Initialize(Config);
            if (_config.LogFrameTiming.Value) _frameTiming = new FrameTimingLog(Logger);

            // Held rather than written at each call site. The game reflection resolve
            // is retried until it takes, and a lambda there would allocate a fresh
            // pair of delegates on every attempt.
            _logInfo = msg => Logger.LogInfo(msg);
            _logWarning = msg => Logger.LogWarning(msg);
            GameReflection.Initialize(_logInfo, _logWarning);

            _windowCentering = new WindowCentering(Logger);
            _sceneSurvey = new SceneSurvey(Logger);
            _gameStateDetector = new GameStateDetector();
            _gameStateDetector.StateChanged += OnGameStateChanged;

            // Built before the pipeline that reads it, so the closure below can never
            // resolve a null. The camera is reached lazily for the same reason: the
            // controller that owns it does not exist yet.
            _zoom = new ZoomFactorTracker(
                () => _cameraController.MainCamera, _gameStateDetector, Logger);

            PositionSettings basePositionSettings = BuildPipeline();
            BuildCameraController();

            _leanClamp = new LeanClampController(
                _cameraController, _positionProcessor, basePositionSettings, Logger);

            BuildInput();
            BuildUI();

            _connectionMonitor = new TrackerConnectionMonitor(_receiver, _config, _notificationUI, Logger);

            _aimGeometryLog = new AimGeometryLog(
                Logger, _cameraController, _reticlePlacer, _leanClamp, _zoom);

            bool listening = _receiver.Start(_config.UDPPort.Value);
            TrackingEnabled = _config.EnabledOnStartup.Value;
            _initialized = true;

            Logger.LogInfo(PluginName + " initialized. Tracking "
                           + (TrackingEnabled ? "enabled" : "disabled"));
            // Only claim the socket when the bind took. A failed Start() leaves the
            // receiver polling for the port, and it has already logged why; printing
            // "Listening on ..." underneath that contradicts it and sends the player
            // looking at their tracker instead of the port.
            if (listening)
                Logger.LogInfo("Listening on UDP port " + _config.UDPPort.Value);

            if (_config.ShowStartupNotification.Value)
            {
                string status = TrackingEnabled ? "Head Tracking: ON" : "Head Tracking: OFF";
                _notificationUI.ShowNotification(status + "\n" + BuildHotkeyInfo(),
                    StartupNotificationSeconds);
            }
        }

        /// <summary>
        /// Builds the tracker pipeline and returns the position settings before the
        /// lean clamp scales them, which is what the clamp measures its allowance
        /// against.
        /// </summary>
        private PositionSettings BuildPipeline()
        {
            _receiver = new OpenTrackReceiver();
            _receiver.Log = _logInfo;

            _processor = new TrackingProcessor
            {
                LocalSmoothing = _config.LocalSmoothing.Value,
                RemoteSmoothing = _config.RemoteSmoothing.Value,
                // Pitch negated, yaw and roll passed through. Taken from the Unity
                // mods that drive the same ViewMatrixTrackingController rather than
                // re-derived - the-forest, easy-delivery-co, outer-wilds and
                // superliminal all ship this exact triple, and the-forest's header
                // records that deriving it from the tracker's documented wire frame
                // produced the opposite for yaw and roll and was wrong in game.
                //
                // Pitch is the one the engine explains: Unity's Euler angles are
                // positive-clockwise about +X, so ViewMatrixModifier's
                // Quaternion.Euler(pitch, ...) turns the view DOWN for a positive
                // pitch. Measured here, not assumed: holding a +20 wire pitch with no
                // inversion logged applied[turnU=-0.342], which is sin(-20 degrees).
                Sensitivity = new SensitivitySettings(
                    _config.YawSensitivity.Value,
                    _config.PitchSensitivity.Value,
                    _config.RollSensitivity.Value,
                    invertYaw: false,
                    invertPitch: true,
                    invertRoll: false),
                Deadzone = DeadzoneSettings.None
            };
            _interpolator = new PoseInterpolator();

            // x is inverted, y and z are not - the same triple eleven sibling Unity
            // mods ship, and inverted for the same reason yaw and roll are not: the
            // lateral axis arrives mirrored.
            //
            // z: the trackers' +Z points out the back of the head, which is the backward
            // lean, and the processor also calls negative z forward - so they agree
            // without a flip. Inverting here instead of at the camera would land ahead
            // of the processor's clamp and hand the forward lean the 0.10m backward
            // budget. ViewMatrixModifier does the transform-space flip itself.
            PositionSettings basePositionSettings = PositionSettings.Symmetric(
                _config.PositionSensitivityX.Value,
                _config.PositionSensitivityY.Value,
                _config.PositionSensitivityZ.Value,
                _config.PositionLimitX.Value,
                _config.PositionLimitY.Value,
                _config.PositionLimitZ.Value,
                _config.PositionLimitZBack.Value,
                _config.LocalSmoothing.Value,
                _config.RemoteSmoothing.Value,
                invertX: true, invertY: false, invertZ: false);

            _positionProcessor = new PositionProcessor
            {
                Settings = basePositionSettings,
                TrackerPivotForward = _config.TrackerPivotForward.Value
            };
            _positionInterpolator = new PositionInterpolator();

            return basePositionSettings;
        }

        private void BuildCameraController()
        {
            var trackingSource = new ZoomCompensatedSource(_receiver, () => _zoom.Factor, _logWarning);

            _cameraController = new ViewMatrixTrackingController(
                trackingSource, _processor, _interpolator,
                _positionProcessor, _positionInterpolator,
                () => GameReflection.MainCamera);
            _cameraController.WorldSpaceYaw = _config.WorldSpaceYaw.Value;

            SetTrackingMode(_config.PositionEnabled.Value
                ? TrackingMode.RotationAndPosition
                : TrackingMode.RotationOnly);
            _cameraController.Enable();

            // After the controller, so its callback is first on Camera.onPreCull.
            _additionalCameras = new AdditionalCameraMirror(_cameraController);
            _additionalCameras.Enable();
        }

        private void BuildInput()
        {
            _inputHandler = new InputHandler(_config);
            _inputHandler.OnTogglePressed += HandleToggle;
            _inputHandler.OnCycleTrackingModePressed += HandleCycleTrackingMode;
            _inputHandler.OnToggleYawModePressed += HandleToggleYawMode;
            _inputHandler.OnToggleReticlePressed += HandleToggleReticle;
        }

        private void BuildUI()
        {
            _notificationUI = new NotificationUI();

            _reticlePlacer = new ReticlePlacer(_cameraController, _gameStateDetector)
            {
                Enabled = _config.ShowReticle.Value
            };

            _stockPrompt = new StockPrompt(_reticlePlacer);
            _stockPrompt.Enable();

            _aimReticle = gameObject.AddComponent<IMGUIReticle>();
            _aimReticle.Style = ReticleStyle.Dot;
            _aimReticle.BaseSizeAt1080p = ReticleBaseSizeAt1080p;
            _aimReticle.OutlineWidthAt1080p = ReticleOutlineWidthAt1080p;
            _aimReticle.ReticleColor = Color.white;
            _aimReticle.OutlineColor = Color.black;
            _aimReticle.IsVisible = _reticlePlacer.Enabled;
            _aimReticle.Initialize(_reticlePlacer.TryGetScreenPosition);
        }

        private string BuildHotkeyInfo()
        {
            return "[" + _inputHandler.ToggleKey + "/Ctrl+Shift+" + ChordHotkeys.ToggleLetter + "] Toggle, "
                 + "[" + _inputHandler.CycleTrackingModeKey + "/Ctrl+Shift+" + ChordHotkeys.PositionLetter + "] Cycle Mode, "
                 + "[" + _inputHandler.YawModeKey + "/Ctrl+Shift+" + ChordHotkeys.FourthToggleLetter + "] Yaw, "
                 + "[" + _inputHandler.ToggleReticleKey + "/Ctrl+Shift+" + ChordHotkeys.FifthToggleLetter + "] Reticle";
        }

        private void Update()
        {
            if (!_initialized) return;

            _frameTiming?.BeginFrame();
            long started = _frameTiming == null ? 0 : System.Diagnostics.Stopwatch.GetTimestamp();

            _windowCentering.Update();
            _inputHandler.CheckInput();
            _gameStateDetector.Update();
            _notificationUI.Update();
            _connectionMonitor.Update();
            PollDeferredResolves();
            _leanClamp.FlushLog();
            _frameTiming?.EndUpdate(started);
        }

        /// <summary>
        /// The lookups that can only succeed once the game has loaded further than the
        /// chainloader takes it. Each latches on success, so this settles into two
        /// field tests a tick.
        /// </summary>
        private void PollDeferredResolves()
        {
            if (Time.realtimeSinceStartup < _nextDeferredResolveTime) return;
            _nextDeferredResolveTime = Time.realtimeSinceStartup + DeferredResolveIntervalSeconds;

            // The game types only exist once Assembly-CSharp has loaded its scene, so
            // the resolve attempt in Awake can legitimately come up empty.
            GameReflection.Initialize(_logInfo, _logWarning);
            EnsurePhysicsLayersResolved();
            if (_config.LogSceneSurvey.Value && _gameStateDetector.IsGameplayActive)
                _sceneSurvey.WriteOnce();
        }

        /// <summary>
        /// The layer allow-lists are resolved by name, and LayerMask.NameToLayer only
        /// answers once the scene's tag manager is live. Deferred to the first frame a
        /// world is loaded, and the full layer table is logged alongside so the
        /// configured names can be checked against what this build actually defines.
        /// </summary>
        private void EnsurePhysicsLayersResolved()
        {
            if (_aimTrace != null || !GameReflection.Resolved) return;
            if (GameReflection.MainCamera == null) return;

            _aimTrace = new AimTrace(_config.AimLayers.Value, _config.AimNpcHitLayers.Value);
            _reticlePlacer.AimTrace = _aimTrace;
            _aimGeometryLog.AimTrace = _aimTrace;

            LayerMaskResolver collisionLayers = LayerMaskResolver.Resolve(_config.CollisionLayers.Value);
            _leanClamp.Arm(
                collisionLayers.Mask,
                _config.CollisionMargin.Value,
                _config.CollisionReleaseSmoothing.Value);

            Logger.LogInfo("Physics layers in this build: " + LayerMaskResolver.DescribeAllLayers());
            Logger.LogInfo("Aim layers: " + _aimTrace.ResolvedLayerNames);
            Logger.LogInfo("Aim NPC hit layers: " + _aimTrace.ResolvedNpcHitLayerNames);
            Logger.LogInfo("Collision layers: " + collisionLayers.Resolved);
            if (_aimTrace.UnknownLayerNames.Length > 0)
                Logger.LogWarning("Aim layer names not defined by this build, skipped: " + _aimTrace.UnknownLayerNames);
            if (collisionLayers.Unknown.Length > 0)
                Logger.LogWarning("Collision layer names not defined by this build, skipped: " + collisionLayers.Unknown);
            if (!_aimTrace.HasMask)
                Logger.LogWarning("Aim layer mask is empty - the reticle falls back to the aim direction.");

            if (_config.CollisionEnabled.Value && !_leanClamp.HasMask)
                Logger.LogWarning("Collision layer mask is empty - the lean clamp cannot run.");
        }

        private void LateUpdate()
        {
            if (!_initialized) return;
            long started = _frameTiming == null ? 0 : System.Diagnostics.Stopwatch.GetTimestamp();

            _zoom.Update();

            bool shouldTrack = TrackingEnabled && _gameStateDetector.IsTrackingActive;

            // Gated on the receiver as well as the verdict, because the controller
            // applies no lean while the tracker is silent and the clamp has to reset
            // on any frame that applies none. Without it the allowance freezes at the
            // last leaned value, the sweep keeps running against a frozen direction,
            // and the log reports frames of contact while nothing at all is leaning.
            _leanClamp.Apply(
                shouldTrack && _receiver.IsReceiving,
                _config.CollisionEnabled.Value);

            _cameraController.ProcessFrame(shouldTrack);
            _frameTiming?.EndLateUpdate(started);
        }

        private void OnGUI()
        {
            long started = _frameTiming == null ? 0 : System.Diagnostics.Stopwatch.GetTimestamp();
            _notificationUI?.Draw();

            if (_initialized) _reticlePlacer.Refresh();

            if (_initialized && _config.LogAimGeometry.Value)
                _aimGeometryLog.Write();
            _frameTiming?.EndGUI(started);
        }

        private void OnDestroy()
        {
            Logger.LogInfo(PluginName + " shutting down...");

            if (_inputHandler != null)
            {
                _inputHandler.OnTogglePressed -= HandleToggle;
                _inputHandler.OnCycleTrackingModePressed -= HandleCycleTrackingMode;
                _inputHandler.OnToggleYawModePressed -= HandleToggleYawMode;
                _inputHandler.OnToggleReticlePressed -= HandleToggleReticle;
            }
            if (_gameStateDetector != null)
                _gameStateDetector.StateChanged -= OnGameStateChanged;

            _stockPrompt?.Disable();
            _additionalCameras?.Disable();
            _cameraController?.Disable();
            _receiver?.Dispose();
        }

        private void HandleToggle()
        {
            TrackingEnabled = !TrackingEnabled;
            if (TrackingEnabled)
            {
                _cameraController.OnTrackingEnabled();
                _notificationUI.ShowTrackingEnabled();
                Logger.LogInfo("Head tracking enabled");
            }
            else
            {
                _cameraController.OnTrackingDisabled();
                _stockPrompt.Restore();
                _notificationUI.ShowTrackingDisabled();
                Logger.LogInfo("Head tracking disabled");
            }
        }

        private void HandleCycleTrackingMode()
        {
            SetTrackingMode((TrackingMode)(((int)_trackingMode + 1) % TrackingModeCount));

            string label = "Tracking: " + _trackingMode.Description();
            _notificationUI.ShowNotification(label, NotificationType.Info, StatusNotificationSeconds);
            Logger.LogInfo(label);
        }

        private void SetTrackingMode(TrackingMode mode)
        {
            _trackingMode = mode;
            _cameraController.RotationEnabled = mode != TrackingMode.PositionOnly;
            _cameraController.PositionEnabled = mode != TrackingMode.RotationOnly;
        }

        private void HandleToggleYawMode()
        {
            _cameraController.WorldSpaceYaw = !_cameraController.WorldSpaceYaw;
            _notificationUI.ShowNotification(
                _cameraController.WorldSpaceYaw ? "Yaw: World-locked" : "Yaw: Camera-local",
                NotificationType.Info,
                StatusNotificationSeconds);
            Logger.LogInfo("Yaw mode: " + (_cameraController.WorldSpaceYaw ? "world-locked" : "camera-local"));
        }

        private void HandleToggleReticle()
        {
            _reticlePlacer.Enabled = !_reticlePlacer.Enabled;
            _aimReticle.IsVisible = _reticlePlacer.Enabled;

            // Only the mod's own dot. The game's interaction prompt keeps following the
            // aim point either way: it is the game's marker, and leaving it at the
            // centre of a decoupled view would point it at the wrong thing.
            string toast = _reticlePlacer.Enabled ? "Aim dot: ON" : "Aim dot: OFF";
            _notificationUI.ShowNotification(
                toast,
                _reticlePlacer.Enabled ? NotificationType.Success : NotificationType.Warning,
                StatusNotificationSeconds);
            Logger.LogInfo(toast);
        }

        private void OnGameStateChanged(GameState newState)
        {
            Logger.LogInfo("Game state: " + newState + " (camera=" + _gameStateDetector.CameraKind + ")");

            if (newState == GameState.Loading) _sceneSurvey.Rearm();

            // No fade back in. Every screen that ends gameplay here replaces the view
            // (map, inventory, dialogue camera), so the player never sees the untracked
            // frame the fade would start from - only the view drifting onto their head.
            if (!GameStateRules.AllowsTracking(newState))
            {
                _cameraController.ResetState(fadeInOnResume: false);
                _leanClamp.Reset();
                _stockPrompt.Restore();
            }
        }
    }
}
