using BepInEx.Configuration;
using CameraUnlock.Core.Data;
using CameraUnlock.Core.Math;
using UnityEngine;

namespace Pathologic2HeadTracking.Config
{
    public sealed class ConfigManager
    {
        // General
        public ConfigEntry<bool> EnabledOnStartup { get; private set; }
        public ConfigEntry<bool> ShowStartupNotification { get; private set; }
        public ConfigEntry<bool> WorldSpaceYaw { get; private set; }

        // UI
        public ConfigEntry<bool> ShowConnectionNotifications { get; private set; }
        public ConfigEntry<bool> ShowReticle { get; private set; }

        // Keybindings
        public ConfigEntry<KeyCode> ToggleKey { get; private set; }
        public ConfigEntry<KeyCode> CycleTrackingModeKey { get; private set; }
        public ConfigEntry<KeyCode> YawModeKey { get; private set; }
        public ConfigEntry<KeyCode> ToggleReticleKey { get; private set; }

        // Network
        public ConfigEntry<int> UDPPort { get; private set; }

        // Sensitivity
        public ConfigEntry<float> YawSensitivity { get; private set; }
        public ConfigEntry<float> PitchSensitivity { get; private set; }
        public ConfigEntry<float> RollSensitivity { get; private set; }

        // Smoothing
        public ConfigEntry<float> LocalSmoothing { get; private set; }
        public ConfigEntry<float> RemoteSmoothing { get; private set; }

        // Position
        public ConfigEntry<bool> PositionEnabled { get; private set; }
        public ConfigEntry<float> PositionSensitivityX { get; private set; }
        public ConfigEntry<float> PositionSensitivityY { get; private set; }
        public ConfigEntry<float> PositionSensitivityZ { get; private set; }
        public ConfigEntry<float> PositionLimitX { get; private set; }
        public ConfigEntry<float> PositionLimitY { get; private set; }
        public ConfigEntry<float> PositionLimitZ { get; private set; }
        public ConfigEntry<float> PositionLimitZBack { get; private set; }
        public ConfigEntry<float> TrackerPivotForward { get; private set; }

        // Camera collision
        public ConfigEntry<bool> CollisionEnabled { get; private set; }
        public ConfigEntry<float> CollisionMargin { get; private set; }
        public ConfigEntry<float> CollisionReleaseSmoothing { get; private set; }
        public ConfigEntry<string> CollisionLayers { get; private set; }

        // Aim
        public ConfigEntry<string> AimLayers { get; private set; }
        public ConfigEntry<string> AimNpcHitLayers { get; private set; }

        // Diagnostics
        public ConfigEntry<bool> LogAimGeometry { get; private set; }
        public ConfigEntry<bool> LogFrameTiming { get; private set; }
        public ConfigEntry<bool> LogSceneSurvey { get; private set; }

        /// <summary>
        /// Binds every setting, one method per ini section. This is bind order, not the
        /// order the file ends up in: BepInEx groups by section and sorts the groups
        /// alphabetically when it writes, so the shipped cfg opens on [Aim].
        /// </summary>
        public void Initialize(ConfigFile config)
        {
            BindGeneral(config);
            BindUi(config);
            BindKeybindings(config);
            BindNetwork(config);
            BindSensitivity(config);
            BindSmoothing(config);
            BindPosition(config);
            BindCollision(config);
            BindAim(config);
            BindDiagnostics(config);
        }

        private void BindGeneral(ConfigFile config)
        {
            EnabledOnStartup = config.Bind(
                "General", "EnabledOnStartup", true,
                "Whether head tracking is enabled when the game starts");

            ShowStartupNotification = config.Bind(
                "General", "ShowStartupNotification", true,
                "Whether to show a notification when the plugin initializes");

            WorldSpaceYaw = config.Bind(
                "General", "WorldSpaceYaw", true,
                "Yaw mode: true = horizon-locked yaw (default), false = camera-local");
        }

        private void BindUi(ConfigFile config)
        {
            ShowConnectionNotifications = config.Bind(
                "UI", "ShowConnectionNotifications", true,
                "Whether to show notifications when the OpenTrack connection is lost or restored");

            ShowReticle = config.Bind(
                "UI", "ShowReticle", true,
                "Show a dot where the line of the shot stops. Pathologic 2 draws no "
                + "crosshair of its own, and its interaction prompt only appears when "
                + "something in front of you can be used, so with the view decoupled "
                + "from aim there is otherwise nothing marking where a revolver, rifle "
                + "or shotgun is pointed. The dot stands down whenever the game's own "
                + "prompt is on screen, which the mod moves to the same point, so there "
                + "are never two markers at once.");
        }

        private void BindKeybindings(ConfigFile config)
        {
            ToggleKey = config.Bind(
                "Keybindings", "ToggleKey", KeyCode.End,
                "Key to toggle head tracking on/off");

            CycleTrackingModeKey = config.Bind(
                "Keybindings", "CycleTrackingModeKey", KeyCode.PageUp,
                "Key to cycle tracking mode (full -> rotation only -> position only -> full)");

            YawModeKey = config.Bind(
                "Keybindings", "YawModeKey", KeyCode.PageDown,
                "Key to toggle world-locked vs camera-local yaw");

            ToggleReticleKey = config.Bind(
                "Keybindings", "ToggleReticleKey", KeyCode.Insert,
                "Key to toggle the aim dot on/off");
        }

        private void BindNetwork(ConfigFile config)
        {
            UDPPort = config.Bind(
                "Network", "UDPPort", 4242,
                new ConfigDescription(
                    "UDP port to listen for OpenTrack data",
                    new AcceptableValueRange<int>(1024, 65535)));
        }

        private void BindSensitivity(ConfigFile config)
        {
            YawSensitivity = config.Bind(
                "Sensitivity", "YawSensitivity", 1.0f,
                new ConfigDescription(
                    "Multiplier for horizontal head rotation (left/right)",
                    new AcceptableValueRange<float>(0.1f, 3.0f)));

            PitchSensitivity = config.Bind(
                "Sensitivity", "PitchSensitivity", 1.0f,
                new ConfigDescription(
                    "Multiplier for vertical head rotation (up/down)",
                    new AcceptableValueRange<float>(0.1f, 3.0f)));

            RollSensitivity = config.Bind(
                "Sensitivity", "RollSensitivity", 1.0f,
                new ConfigDescription(
                    "Multiplier for head tilt (ear to shoulder)",
                    new AcceptableValueRange<float>(0.1f, 3.0f)));
        }

        private void BindSmoothing(ConfigFile config)
        {
            LocalSmoothing = config.Bind(
                "Smoothing", "LocalSmoothing", SmoothingUtils.DefaultLocalSmoothing,
                new ConfigDescription(
                    "Smoothing applied when the tracker runs on this machine (loopback). 0 = none, 1 = heavy.",
                    new AcceptableValueRange<float>(0f, 1f)));

            RemoteSmoothing = config.Bind(
                "Smoothing", "RemoteSmoothing", SmoothingUtils.DefaultRemoteSmoothing,
                new ConfigDescription(
                    "Smoothing applied when the tracker is a remote device on the network. 0 = none, 1 = heavy.",
                    new AcceptableValueRange<float>(0f, 1f)));
        }

        private void BindPosition(ConfigFile config)
        {
            PositionEnabled = config.Bind(
                "Position", "PositionEnabled", true,
                "Enable positional tracking (lean in/out/side-to-side)");

            PositionSensitivityX = config.Bind(
                "Position", "PositionSensitivityX", 1.0f,
                new ConfigDescription(
                    "Multiplier for lateral (left/right) position",
                    new AcceptableValueRange<float>(0f, 5.0f)));

            PositionSensitivityY = config.Bind(
                "Position", "PositionSensitivityY", 1.0f,
                new ConfigDescription(
                    "Multiplier for vertical (up/down) position",
                    new AcceptableValueRange<float>(0f, 5.0f)));

            PositionSensitivityZ = config.Bind(
                "Position", "PositionSensitivityZ", 1.0f,
                new ConfigDescription(
                    "Multiplier for depth (forward/back) position",
                    new AcceptableValueRange<float>(0f, 5.0f)));

            PositionLimitX = config.Bind(
                "Position", "PositionLimitX", PositionSettings.Default.LimitX,
                new ConfigDescription(
                    "Maximum lateral displacement in meters",
                    new AcceptableValueRange<float>(0.01f, 0.5f)));

            PositionLimitY = config.Bind(
                "Position", "PositionLimitY", PositionSettings.Default.LimitY,
                new ConfigDescription(
                    "Maximum vertical displacement in meters",
                    new AcceptableValueRange<float>(0.01f, 0.5f)));

            PositionLimitZ = config.Bind(
                "Position", "PositionLimitZ", PositionSettings.Default.LimitZ,
                new ConfigDescription(
                    "Maximum forward displacement in meters",
                    new AcceptableValueRange<float>(0.01f, 0.5f)));

            PositionLimitZBack = config.Bind(
                "Position", "PositionLimitZBack", PositionSettings.Default.LimitZBack,
                new ConfigDescription(
                    "Maximum backward displacement in meters",
                    new AcceptableValueRange<float>(0.01f, 0.5f)));

            // Off by default, matching core and its schema. The correct arm length is a
            // property of the TRACKER, not of this game, and the trackers this mod's
            // axis conventions are pinned against already remove the arc themselves.
            // Shipping a non-zero value would subtract that arc a second time.
            TrackerPivotForward = config.Bind(
                "Position", "TrackerPivotForward", 0.0f,
                new ConfigDescription(
                    "Distance from the neck pivot to the tracked face point, in meters. "
                    + "Compensates the lateral arc head yaw puts into the position data. "
                    + "Leave at 0 unless your tracker sends a neck-pivoted position it "
                    + "does not already correct.",
                    new AcceptableValueRange<float>(0f, 0.20f)));
        }

        private void BindCollision(ConfigFile config)
        {
            // Off, because the sweep has not yet been seen cutting a lean against a
            // real wall. What has been confirmed in game is that it runs - the log
            // writes "Lean clamp is running" with the margin and the near clip, and
            // the collision layer names all resolve on this build - and the policy
            // arithmetic is covered by LeanAllowanceTests. What is missing is a
            // contact: every lean tried from the opening Theatre reported
            // allow=1.00 contact=False even at the maximum 0.6 m margin, because the
            // player stands in open floor there. Turn this on once a log from a
            // tight interior shows it engaging at the right distance.
            CollisionEnabled = config.Bind(
                "Collision", "CollisionEnabled", false,
                "Cut a lean back to whatever the level leaves room for, so the view "
                + "never ends up inside a wall. No effect in rotation-only mode. Off "
                + "until the sweep has been seen engaging on a real wall in this game.");

            CollisionMargin = config.Bind(
                "Collision", "CollisionMargin", 0.15f,
                new ConfigDescription(
                    "Distance in meters the eye is held off a surface. Raised at runtime "
                    + "if it does not clear the camera's near clip plane, since anything "
                    + "closer than that is culled and the player sees through the wall "
                    + "anyway.",
                    new AcceptableValueRange<float>(0.02f, 0.6f)));

            CollisionReleaseSmoothing = config.Bind(
                "Collision", "CollisionReleaseSmoothing", 0.9f,
                new ConfigDescription(
                    "Smoothing applied as the lean opens back up after an obstruction "
                    + "clears. 0.9 is a 200ms time constant. Tightening is never smoothed.",
                    new AcceptableValueRange<float>(0f, 1f)));

            CollisionLayers = config.Bind(
                "Collision", "CollisionLayers",
                "Default,Doors,Buildings,Indoor,Indoor Isolated,Key Building,Terrain,InvisibleWall",
                "Physics layers the lean sweep treats as solid, by name. Names this "
                + "build of the game does not define are reported in the log and skipped, "
                + "and the whole layer table this build declares is logged next to them. "
                + "InvisibleWall is on the list because it stops the player, so leaning "
                + "through it would put the eye somewhere the level was never dressed for.");
        }

        private void BindAim(ConfigFile config)
        {
            AimLayers = config.Bind(
                "Aim", "AimLayers",
                "Default,Doors,Buildings,Indoor_Transparant_Object,Dynamic,Target,Indoor,"
                + "Indoor Isolated,Dynamic_Sunless,Ragdoll,Key Building,Unimportant Object,"
                + "Pond,Terrain",
                "Physics layers a shot is treated as stopping on, by name. This sets the "
                + "depth the reticle is drawn at, so it is an allow-list rather than a "
                + "blacklist - the world is full of trigger volumes a ray would otherwise "
                + "stop on, collapsing the depth onto whatever the player is standing in. "
                + "Player, Triggers, Regions, TriggerInteract, Fog Portals and Reflection "
                + "Probe are the layers deliberately left off. The mod logs every layer "
                + "this build declares, and what each one answers for the current aim, "
                + "when Diagnostics/LogAimGeometry is on.");

            AimNpcHitLayers = config.Bind(
                "Aim", "AimNpcHitLayers",
                "Npc Hit Colliders",
                "Layers whose TRIGGER colliders also stop a shot. Pathologic 2 hangs its "
                + "NPC hit boxes on triggers, and RaycastAbilityProjectile keeps exactly "
                + "the trigger hits on this layer while discarding every other one. "
                + "Without it the reticle marks the wall behind a man rather than the man.");
        }

        private void BindDiagnostics(ConfigFile config)
        {
            LogFrameTiming = config.Bind(
                "Diagnostics", "LogFrameTiming", false,
                "Log peak frame gaps, mod callback durations and garbage collections every ten seconds.");

            LogAimGeometry = config.Bind(
                "Diagnostics", "LogAimGeometry", false,
                "Log one line per second carrying the applied pose, lean, aim distance "
                + "and reticle offset together, so a projection fault can be settled by "
                + "arithmetic rather than by playing.");

            LogSceneSurvey = config.Bind(
                "Diagnostics", "LogSceneSurvey", false,
                "Log the camera set, the physics layer table and the HUD element the "
                + "reticle is moved through, once per world load.");
        }
    }
}
