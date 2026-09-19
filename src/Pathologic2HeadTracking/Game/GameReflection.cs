using System;
using System.Reflection;
using UnityEngine;

namespace Pathologic2HeadTracking.Game
{
    /// <summary>
    /// Every read this mod makes into Pathologic 2's own assemblies, resolved once
    /// and cached. Nothing here is called before <see cref="Initialize"/> has latched
    /// <see cref="Resolved"/>, which only happens once Assembly-CSharp has loaded its
    /// types - the BepInEx chainloader runs before that on this game, so the resolve
    /// is retried rather than attempted once.
    ///
    /// The mod reads these by preference over the game's own composite
    /// PlayerUtility.IsPlayerCanControlling. That property is the right answer but it
    /// is written to throw before the services it walks are initialised, and it
    /// dereferences three services that are simply absent in the main menu, so a mod
    /// polling it ten times a second would be building its gate out of caught
    /// exceptions. The pieces below are the same conditions read from members that
    /// answer at any point in the process's life.
    /// </summary>
    public static class GameReflection
    {
        private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        // GameCamera, the MonoBehaviour that owns the rendering camera.
        private static PropertyInfo _gameCameraInstance;
        private static PropertyInfo _gameCameraCamera;
        private static PropertyInfo _gameCameraTransform;
        private static FieldInfo _gameCameraAdditional;

        // CameraService, whose Kind says which controller is driving the camera.
        private static FieldInfo _cameraServiceKind;
        private static FieldInfo _cameraServiceInitialise;

        // UIService, whose Active window says which screen the player is looking at.
        private static PropertyInfo _uiServiceActive;
        private static PropertyInfo _uiServiceIsTransition;
        private static PropertyInfo _uiServiceIsInitialize;
        private static Type _hudWindowInterface;
        private static PropertyInfo _hudInteractable;

        private static MethodInfo _getService;

        // GetService's argument arrays, built once. The UI service is looked up from
        // OnGUI on every frame the aim dot is placed, and a fresh array per call
        // was the only allocation on that path.
        private static object[] _cameraServiceArgs;
        private static object[] _uiServiceArgs;

        // EngineApplication.IsPaused, which the pause menu and every modal screen set.
        private static PropertyInfo _engineApplicationInstance;
        private static PropertyInfo _engineApplicationIsPaused;

        // CursorService.Instance, free/visible whenever a screen owns the mouse.
        private static PropertyInfo _cursorInstance;
        private static PropertyInfo _cursorVisible;
        private static PropertyInfo _cursorFree;

        // GraphicsGameSettings.FieldOfView, the player's own un-zoomed FOV.
        private static PropertyInfo _graphicsSettingsInstance;
        private static FieldInfo _fieldOfViewValue;
        private static PropertyInfo _valueOfFloat;

        // The interaction icon at the centre of the HUD - the game's own reticle.
        // Serialized Unity members, looked up as both field and property so a repack
        // that promotes them does not silently lose the icon test and leave the mod's
        // own aim dot drawn over the game's marker.
        private static FieldInfo[] _iconFields;
        private static PropertyInfo[] _iconProperties;

        private static bool _resolved;
        private static bool _loggedFailure;

        /// <summary>True once every member below has been found.</summary>
        public static bool Resolved
        {
            get { return _resolved; }
        }

        /// <summary>
        /// Attempts the resolve. Cheap and idempotent once it has taken, so the caller
        /// polls it rather than ordering itself against the game's own startup.
        /// </summary>
        public static void Initialize(Action<string> logInfo, Action<string> logWarning)
        {
            if (_resolved) return;

            Type gameCamera = FindType("GameCamera");
            Type cameraService = FindType("Engine.Source.Services.CameraServices.CameraService");
            Type uiService = FindType("Engine.Impl.Services.UIService");
            Type hudWindow = FindType("Engine.Source.UI.IHudWindow");
            Type serviceLocator = FindType("Engine.Common.Services.ServiceLocator");
            Type engineApplication = FindType("Engine.Source.Commons.EngineApplication");
            Type instanceByRequest = FindType("Engine.Source.Commons.InstanceByRequest`1");
            Type cursorService = FindType("InputServices.CursorService");
            Type cursorController = FindType("InputServices.ICursorController");
            Type graphicsSettings = FindType("Engine.Source.Settings.GraphicsGameSettings");
            Type interactableWindow = FindType("InteractableWindow");

            if (gameCamera == null || cameraService == null || uiService == null
                || hudWindow == null || serviceLocator == null || engineApplication == null
                || instanceByRequest == null || cursorService == null || cursorController == null
                || graphicsSettings == null || interactableWindow == null)
            {
                return;
            }

            _gameCameraInstance = gameCamera.GetProperty("Instance", Static);
            _gameCameraCamera = gameCamera.GetProperty("Camera", Instance);
            _gameCameraTransform = gameCamera.GetProperty("CameraTransform", Instance);
            _gameCameraAdditional = gameCamera.GetField("additionalCameras", Instance);

            _cameraServiceArgs = new object[] { cameraService };
            _cameraServiceKind = cameraService.GetField("kind", Instance);
            _cameraServiceInitialise = cameraService.GetField("initialise", Instance);

            _uiServiceArgs = new object[] { uiService };
            _uiServiceActive = uiService.GetProperty("Active", Instance);
            _uiServiceIsTransition = uiService.GetProperty("IsTransition", Instance);
            _uiServiceIsInitialize = uiService.GetProperty("IsInitialize", Instance);
            _hudWindowInterface = hudWindow;
            _hudInteractable = hudWindow.GetProperty("InteractableInterface", Instance);

            _getService = serviceLocator.GetMethod("GetService", Static, null, new[] { typeof(Type) }, null);

            _engineApplicationInstance = ClosedInstanceProperty(instanceByRequest, engineApplication);
            _engineApplicationIsPaused = engineApplication.GetProperty("IsPaused", Instance);

            _cursorInstance = cursorService.GetProperty("Instance", Static);
            _cursorVisible = cursorController.GetProperty("Visible", Instance);
            _cursorFree = cursorController.GetProperty("Free", Instance);

            _graphicsSettingsInstance = ClosedInstanceProperty(instanceByRequest, graphicsSettings);
            _fieldOfViewValue = graphicsSettings.GetField("FieldOfView", Instance);
            if (_fieldOfViewValue != null)
                _valueOfFloat = _fieldOfViewValue.FieldType.GetProperty("Value", Instance);

            string[] iconNames = { "normalImage", "lockedImage", "blockedImage" };
            _iconFields = new FieldInfo[iconNames.Length];
            _iconProperties = new PropertyInfo[iconNames.Length];
            for (int i = 0; i < iconNames.Length; i++)
            {
                _iconFields[i] = interactableWindow.GetField(iconNames[i], Instance);
                _iconProperties[i] = interactableWindow.GetProperty(iconNames[i], Instance);
            }

            if (_gameCameraInstance == null || _gameCameraCamera == null || _gameCameraTransform == null
                || _cameraServiceKind == null || _cameraServiceInitialise == null
                || _uiServiceActive == null || _uiServiceIsTransition == null || _uiServiceIsInitialize == null
                || _hudInteractable == null || _getService == null
                || _engineApplicationInstance == null || _engineApplicationIsPaused == null
                || _cursorInstance == null || _cursorVisible == null || _cursorFree == null
                || _graphicsSettingsInstance == null || _fieldOfViewValue == null || _valueOfFloat == null)
            {
                if (!_loggedFailure)
                {
                    _loggedFailure = true;
                    logWarning("Pathologic 2's camera and UI members did not resolve. Head tracking "
                               + "stays off. This build is not one the mod knows.");
                }
                return;
            }

            _resolved = true;
            logInfo("Resolved Pathologic 2 camera, UI and settings members.");
        }

        /// <summary>The rendering camera. Null before a world is up.</summary>
        public static Camera MainCamera
        {
            get
            {
                object gameCamera = GameCameraInstance;
                if (gameCamera == null) return null;
                return _gameCameraCamera.GetValue(gameCamera, null) as Camera;
            }
        }

        /// <summary>
        /// The transform the game itself aims and fires along. PickingService builds its
        /// interaction ray from this, and RaycastAbilityProjectile fires every bullet
        /// from it, so this is the clean shot eye - never the mod's rendered basis,
        /// and never Camera.transform, which GameCamera holds as a separate serialized
        /// reference and is under no obligation to be the same object.
        /// </summary>
        public static Transform AimTransform
        {
            get
            {
                object gameCamera = GameCameraInstance;
                if (gameCamera == null) return null;
                return _gameCameraTransform.GetValue(gameCamera, null) as Transform;
            }
        }

        /// <summary>
        /// The secondary cameras GameCamera keeps in step with its own field of view.
        /// They render into the same frame, so the head basis has to reach them too or
        /// whatever they draw stays behind while the world turns.
        /// </summary>
        public static Camera[] AdditionalCameras
        {
            get
            {
                object gameCamera = GameCameraInstance;
                if (gameCamera == null || _gameCameraAdditional == null) return null;
                return _gameCameraAdditional.GetValue(gameCamera) as Camera[];
            }
        }

        /// <summary>
        /// Which controller CameraService has the camera on. Read off the backing field
        /// rather than the property, which throws until the service initialises.
        /// </summary>
        public static CameraKind CurrentCameraKind
        {
            get
            {
                object service = GetService(_cameraServiceArgs);
                if (service == null) return CameraKind.Unknown;
                if (!(bool)_cameraServiceInitialise.GetValue(service)) return CameraKind.Unknown;
                return (CameraKind)(int)_cameraServiceKind.GetValue(service);
            }
        }

        public static bool IsPaused
        {
            get
            {
                object application = _engineApplicationInstance.GetValue(null, null);
                if (application == null) return true;
                return (bool)_engineApplicationIsPaused.GetValue(application, null);
            }
        }

        /// <summary>
        /// True whenever a screen owns the mouse - inventory, map, mind map, dialogue,
        /// trade, the pause menu. The game's own control gate tests exactly this pair.
        /// </summary>
        public static bool CursorOwnsInput
        {
            get
            {
                object cursor = _cursorInstance.GetValue(null, null);
                if (cursor == null) return true;
                return (bool)_cursorVisible.GetValue(cursor, null)
                       || (bool)_cursorFree.GetValue(cursor, null);
            }
        }

        /// <summary>True while UIService is animating between two windows.</summary>
        public static bool IsUiTransitioning
        {
            get
            {
                object service = GetService(_uiServiceArgs);
                if (service == null) return false;
                return (bool)_uiServiceIsTransition.GetValue(service, null);
            }
        }

        /// <summary>
        /// The HUD window if it is the active one, otherwise null. Anything else being
        /// active means the player is in a screen rather than in the world.
        /// </summary>
        private static object ActiveHudWindow
        {
            get
            {
                object service = GetService(_uiServiceArgs);
                if (service == null) return null;
                if (!(bool)_uiServiceIsInitialize.GetValue(service, null)) return null;

                object active = _uiServiceActive.GetValue(service, null);
                if (active == null) return null;
                return _hudWindowInterface.IsInstanceOfType(active) ? active : null;
            }
        }

        public static bool IsHudActive
        {
            get { return ActiveHudWindow != null; }
        }

        /// <summary>The HUD's InteractableWindow, or null when the HUD is not up.</summary>
        private static object ActiveInteractable
        {
            get
            {
                object hud = ActiveHudWindow;
                return hud == null ? null : _hudInteractable.GetValue(hud, null);
            }
        }

        /// <summary>
        /// The GameObject of the centre-screen interaction prompt, or null when the HUD
        /// is not up. This is the game's own reticle: it marks what the aim ray is
        /// pointed at, so it is the element the mod moves rather than drawing over.
        /// </summary>
        public static GameObject InteractableWindowObject
        {
            get
            {
                var interactable = ActiveInteractable as Component;
                return interactable == null ? null : interactable.gameObject;
            }
        }

        /// <summary>
        /// True while the interaction prompt is showing one of its three icons. The
        /// mod's own aim dot stands down on those frames so the player is never given
        /// two markers on the same point.
        /// </summary>
        public static bool IsInteractionIconVisible
        {
            get
            {
                object interactable = ActiveInteractable;
                if (interactable == null) return false;

                for (int i = 0; i < _iconFields.Length; i++)
                {
                    object icon = _iconFields[i] != null
                        ? _iconFields[i].GetValue(interactable)
                        : (_iconProperties[i] != null ? _iconProperties[i].GetValue(interactable, null) : null);

                    var component = icon as Component;
                    if (component != null && component.gameObject.activeSelf) return true;
                }
                return false;
            }
        }

        /// <summary>
        /// The player's own field-of-view setting, in the same vertical degrees
        /// GameCamera.ApplyFov writes into Camera.fieldOfView. That shared origin is
        /// what makes the zoom factor exactly 1.0 in ordinary play.
        /// </summary>
        public static float PreferredFieldOfView
        {
            get
            {
                object settings = _graphicsSettingsInstance.GetValue(null, null);
                if (settings == null) return 0f;
                object value = _fieldOfViewValue.GetValue(settings);
                if (value == null) return 0f;
                return (float)_valueOfFloat.GetValue(value, null);
            }
        }

        private static object GameCameraInstance
        {
            get { return _resolved ? _gameCameraInstance.GetValue(null, null) : null; }
        }

        private static object GetService(object[] serviceTypeArgs)
        {
            return _getService.Invoke(null, serviceTypeArgs);
        }

        /// <summary>
        /// InstanceByRequest&lt;T&gt;.Instance for a given T. The game reaches every one of
        /// its settings singletons through this generic, so closing it here is the only
        /// way to read one at all.
        /// </summary>
        private static PropertyInfo ClosedInstanceProperty(Type openGeneric, Type argument)
        {
            return openGeneric.MakeGenericType(argument).GetProperty("Instance", Static);
        }

        private static Type FindType(string fullName)
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Type type = assemblies[i].GetType(fullName, false);
                if (type != null) return type;
            }
            return null;
        }
    }
}
