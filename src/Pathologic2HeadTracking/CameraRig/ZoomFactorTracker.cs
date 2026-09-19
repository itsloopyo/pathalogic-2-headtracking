using System;
using BepInEx.Logging;
using Pathologic2HeadTracking.Game;
using UnityEngine;

namespace Pathologic2HeadTracking.CameraRig
{
    /// <summary>
    /// Re-reads the rendered FOV against the player's own setting every frame and
    /// keeps the factor the pose is scaled by.
    ///
    /// GameCamera.ApplyFov renders at the player's FOV setting plus AdditionalFov, or
    /// at a cutscene FOV outright. The blueprint FovEffectNode drives AdditionalFov
    /// during scripted effects, and those run in ordinary gameplay. Without this the
    /// head would move the picture further for as long as one is running, which a
    /// player reads as the mod's sensitivity changing on its own.
    ///
    /// The base is the player's own FOV setting rather than a constant, so a slider
    /// moved in the game's options is picked up on the next frame and leaves the
    /// factor at 1.0 where it belongs.
    /// </summary>
    public sealed class ZoomFactorTracker
    {
        private readonly Func<Camera> _camera;
        private readonly GameStateDetector _gameStateDetector;
        private readonly ManualLogSource _logger;

        private bool _loggedBasis;

        public ZoomFactorTracker(
            Func<Camera> camera,
            GameStateDetector gameStateDetector,
            ManualLogSource logger)
        {
            _camera = camera;
            _gameStateDetector = gameStateDetector;
            _logger = logger;
            Factor = 1f;
        }

        /// <summary>1.0 whenever the game is at its own un-zoomed FOV, which is ordinary play.</summary>
        public float Factor { get; private set; }

        /// <summary>Call once per LateUpdate, before the pose is read.</summary>
        public void Update()
        {
            Camera cam = _camera();
            if (cam == null || !GameReflection.Resolved)
            {
                Factor = 1f;
                return;
            }

            float live = cam.fieldOfView;
            float baseFov = GameReflection.PreferredFieldOfView;
            Factor = ZoomCompensation.Factor(live, baseFov);

            // Logged off the camera, not off a tracker sample, so the basis is visible
            // without a tracker connected. Held until the game is in ordinary gameplay,
            // because the gate on this line is that it reads factor=1.0000, and a line
            // written during the intro proves nothing either way.
            if (!_loggedBasis && _gameStateDetector.State == GameState.Gameplay)
            {
                _loggedBasis = true;

                // An unreadable base scales nothing, and a factor of 1.0000 is also
                // what a correct basis reads, so the two have to be told apart on the
                // line itself rather than by their factor.
                if (baseFov <= 0f)
                {
                    _logger.LogWarning(
                        "ZOOMBASIS GraphicsGameSettings.FieldOfView is unreadable on this "
                        + "build, so no zoom compensation is applied. Head tracking will "
                        + "feel exaggerated while the game narrows its field of view.");
                    return;
                }

                _logger.LogInfo(string.Format(
                    "ZOOMBASIS live={0:F2}deg vertical base={1:F2}deg vertical "
                    + "aspect={2:F4} factor={3:F4}",
                    live, baseFov, cam.aspect, Factor));
            }
        }
    }
}
