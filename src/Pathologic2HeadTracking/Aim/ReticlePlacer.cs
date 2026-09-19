using CameraUnlock.Core.Unity.Tracking;
using CameraUnlock.Core.Unity.Utilities;
using Pathologic2HeadTracking.Game;
using UnityEngine;

namespace Pathologic2HeadTracking.Aim
{
    /// <summary>Where the reticle goes this frame, and what it was placed from.</summary>
    public struct ReticlePlacement
    {
        /// <summary>False when nothing should be placed at all.</summary>
        public bool Valid;

        /// <summary>Screen pixels, Unity convention.</summary>
        public Vector2 ScreenPosition;

        /// <summary>The same position as an offset from centre, +-1 at the frame edges.</summary>
        public Vector2 NdcOffset;

        /// <summary>False when the aim cast could not run at all.</summary>
        public bool Queried;

        /// <summary>True when a surface was found to mark, rather than a direction.</summary>
        public bool Hit;

        /// <summary>Distance to that surface. Only meaningful when Hit.</summary>
        public float Distance;

        /// <summary>World position of that surface. Only meaningful when Hit.</summary>
        public Vector3 AimPoint;
    }

    /// <summary>
    /// Places the aim marker on the surface a shot would stop on.
    ///
    /// The marker marks a POINT, not a direction: with the eye leaned off the shot
    /// axis the two project to different places, and the gap grows the closer the
    /// target. So the aim ray is cast from the CLEAN camera transform - the one
    /// PickingService and RaycastAbilityProjectile both read, so the one the game
    /// still aims and fires along - and its contact is projected through the matrices
    /// the frame was drawn with.
    ///
    /// A definite no-hit is a target at infinity and projects the aim direction, and
    /// so does a cast that could not run at all - the layers not resolved yet, or an
    /// allow-list this build defines none of, both of which are logged at startup.
    /// There is no fixed convergence distance anywhere in this path.
    ///
    /// Unity raises OnGUI more than once per frame (Layout and Repaint at minimum),
    /// and every input to the placement is fixed for the frame, so the result is
    /// cached on the frame counter.
    /// </summary>
    public sealed class ReticlePlacer
    {
        private readonly ViewMatrixTrackingController _cameraController;
        private readonly GameStateDetector _gameStateDetector;
        private readonly PerFrameCache<ReticlePlacement> _cache;

        private ReticlePlacement _last;

        public ReticlePlacer(
            ViewMatrixTrackingController cameraController,
            GameStateDetector gameStateDetector)
        {
            _cameraController = cameraController;
            _gameStateDetector = gameStateDetector;
            _cache = new PerFrameCache<ReticlePlacement>(Compute);
        }

        /// <summary>Whether the mod's own aim dot is switched on.</summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// The cast, once the physics layers have resolved. Null until then, and the
        /// marker falls back to the aim direction while it is.
        /// </summary>
        public AimTrace AimTrace { get; set; }

        /// <summary>This frame's placement, computed once however often it is asked for.</summary>
        public ReticlePlacement Current
        {
            get { return _cache.Get(); }
        }

        /// <summary>
        /// Computes this frame's placement if nothing has asked for it yet.
        ///
        /// Both the other callers are optional - IMGUIReticle skips its provider while
        /// the dot is switched off, and the geometry log is behind a config flag - and
        /// the canvas hook that moves the game's own prompt consumes whatever was last
        /// computed. Without a caller that always runs, switching the dot off froze the
        /// prompt at its last offset.
        /// </summary>
        public void Refresh()
        {
            _cache.Get();
        }

        /// <summary>
        /// The last placement <see cref="Compute"/> produced, which is the previous
        /// frame's when read from the canvas hook.
        ///
        /// The canvas update runs BEFORE the cameras render, so the view matrix on the
        /// camera at that moment is the one the PREVIOUS frame was drawn with while the
        /// aim transform has already moved on to this frame. Computing there would mix
        /// two frames, which is the one failure the projection cannot recover from.
        /// Taking the whole placement from one frame keeps it self-consistent and puts
        /// the game's own prompt one frame behind the picture instead - bounded,
        /// self-correcting, and with no drift in it. The mod's own dot is drawn from
        /// OnGUI, after the frame has rendered, and carries no lag at all.
        /// </summary>
        public ReticlePlacement LastPlacement
        {
            get { return _last; }
        }

        /// <summary>
        /// Whether the game's own interaction prompt is moved this frame. Separate
        /// from whether the mod's dot is drawn: the prompt is the game's reticle and
        /// it follows the aim point whether or not the player wants a dot as well.
        /// </summary>
        public bool ShouldMoveStockPrompt
        {
            get { return _gameStateDetector.IsGameplayActive && _last.Valid; }
        }

        /// <summary>
        /// Whether the mod's own dot is drawn this frame. It stands down while the
        /// game's prompt is showing an icon, which the mod has already moved to the
        /// same point - two markers on one point is worse than either alone.
        /// </summary>
        public bool ShouldDraw
        {
            get
            {
                return Enabled
                       && _gameStateDetector.IsGameplayActive
                       && !GameReflection.IsInteractionIconVisible;
            }
        }

        /// <summary><see cref="CameraUnlock.Core.Unity.Rendering.ReticlePositionProvider"/>.</summary>
        public bool TryGetScreenPosition(out float screenX, out float screenY)
        {
            ReticlePlacement placement = _cache.Get();
            screenX = placement.ScreenPosition.x;
            screenY = placement.ScreenPosition.y;
            return placement.Valid && ShouldDraw;
        }

        private ReticlePlacement Compute()
        {
            _last = ComputeNow();
            return _last;
        }

        private ReticlePlacement ComputeNow()
        {
            ReticlePlacement placement = default(ReticlePlacement);

            if (!_gameStateDetector.IsGameplayActive
                || !_cameraController.IsApplyingTracking)
            {
                return placement;
            }

            Camera cam = _cameraController.MainCamera;
            Transform aimTr = GameReflection.AimTransform;
            if (cam == null || aimTr == null) return placement;

            Vector3 shotEye = aimTr.position;
            Vector3 aimDirection = aimTr.forward;

            AimResult aim = AimTrace != null ? AimTrace.Cast(shotEye, aimDirection) : default(AimResult);
            placement.Queried = aim.Queried;
            placement.Hit = aim.Hit;

            Vector2 ndc;
            bool projected;
            if (aim.Hit)
            {
                placement.Distance = aim.Distance;
                placement.AimPoint = aim.Point;
                projected = AimProjection.TryProjectPointNdc(cam, aim.Point, out ndc);
            }
            else
            {
                projected = AimProjection.TryProjectDirectionNdc(cam, aimDirection, out ndc);
            }
            if (!projected) return placement;

            placement.Valid = true;
            placement.NdcOffset = ndc;
            placement.ScreenPosition = AimProjection.NdcToScreen(ndc);
            return placement;
        }
    }
}
