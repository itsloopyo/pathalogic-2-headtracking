using BepInEx.Logging;
using CameraUnlock.Core.Data;
using CameraUnlock.Core.Unity.Tracking;
using Pathologic2HeadTracking.Aim;
using Pathologic2HeadTracking.CameraRig;
using Pathologic2HeadTracking.Game;
using UnityEngine;

namespace Pathologic2HeadTracking.Diagnostics
{
    /// <summary>
    /// Rotation, lean, distance and reticle offset on one line and on the same frame.
    /// Reading the distance off one log line and the offset off another makes a
    /// distance fault and a sign fault read alike.
    ///
    /// Written from OnGUI rather than from LateUpdate, and this is load-bearing. The
    /// line reads the reticle placement, which is cached on the frame counter; asking
    /// for it in LateUpdate would compute it before Camera.onPreCull had written this
    /// frame's view matrix and then serve that stale answer to the reticle itself.
    /// Turning diagnostics on would have quietly put the reticle a frame behind.
    /// </summary>
    public sealed class AimGeometryLog
    {
        private const float LogIntervalSeconds = 1f;

        private readonly ManualLogSource _logger;
        private readonly ViewMatrixTrackingController _cameraController;
        private readonly ReticlePlacer _reticlePlacer;
        private readonly LeanClampController _leanClamp;
        private readonly ZoomFactorTracker _zoom;

        private float _nextLogTime;

        public AimGeometryLog(
            ManualLogSource logger,
            ViewMatrixTrackingController cameraController,
            ReticlePlacer reticlePlacer,
            LeanClampController leanClamp,
            ZoomFactorTracker zoom)
        {
            _logger = logger;
            _cameraController = cameraController;
            _reticlePlacer = reticlePlacer;
            _leanClamp = leanClamp;
            _zoom = zoom;
        }

        /// <summary>
        /// The cast, once the physics layers have resolved. Its per-layer histogram is
        /// what the aim allow-list gets checked against.
        /// </summary>
        public AimTrace AimTrace { get; set; }

        /// <summary>Call every OnGUI; writes at most one line per interval.</summary>
        public void Write()
        {
            if (Time.realtimeSinceStartup < _nextLogTime) return;
            if (!_cameraController.IsApplyingTracking || AimTrace == null) return;

            Camera cam = _cameraController.MainCamera;
            Transform aimTr = GameReflection.AimTransform;
            if (cam == null || aimTr == null) return;

            _nextLogTime = Time.realtimeSinceStartup + LogIntervalSeconds;

            Vector3 eye = aimTr.position;
            Vector3 forward = aimTr.forward;
            Vec3 lean = _cameraController.LastTrackingPosition;
            ReticlePlacement placement = _reticlePlacer.Current;
            AppliedBasis basis = AppliedBasis.Read(cam);

            _logger.LogInfo(string.Format(
                "AIMGEO rot=({0:F2},{1:F2},{2:F2}) lean=({3:F3},{4:F3},{5:F3}) "
                + "queried={6} hit={7} dist={8:F2} reticle={9} valid={10} drawn={11} "
                + "prompt={12} leanAllow={13:F2} contact={14} queryFail={15} "
                + "nearclip={16:F3} fov={17:F1} zoom={18:F4} "
                + "eye=({19:F3},{20:F3},{21:F3}) fwd=({22:F4},{23:F4},{24:F4}) "
                + "enginedelta={25:F2}px "
                + "applied[turnR={26:F3} turnU={27:F3} tiltR={28:F3} "
                + "leanR={29:F3} leanU={30:F3} leanF={31:F3}] layers[{32}]",
                _cameraController.LastTrackingYaw, _cameraController.LastTrackingPitch,
                _cameraController.LastTrackingRoll,
                lean.X, lean.Y, lean.Z,
                placement.Queried, placement.Hit, placement.Distance,
                DescribeOffset(placement), placement.Valid, _reticlePlacer.ShouldDraw,
                _reticlePlacer.ShouldMoveStockPrompt,
                _leanClamp.Allowance, _leanClamp.InContact, _leanClamp.LastQueryFailed,
                cam.nearClipPlane, cam.fieldOfView, _zoom.Factor,
                eye.x, eye.y, eye.z,
                forward.x, forward.y, forward.z,
                EngineProjectionDelta(cam, placement),
                basis.TurnedRight, basis.TurnedUp, basis.TiltedRight,
                basis.LeanedRight, basis.LeanedUp, basis.LeanedForward,
                AimTrace.DescribeContacts(eye, forward)));
        }

        /// <summary>
        /// The reticle's offset from screen centre, or "none" when nothing was placed.
        /// Subtracting half the screen from an unset position printed a large, entirely
        /// plausible offset - (-960,-540) at 1080p - on every frame the placement was
        /// invalid, which is a fabricated parallax figure sitting in the field a fault
        /// is read out of.
        /// </summary>
        private static string DescribeOffset(ReticlePlacement placement)
        {
            if (!placement.Valid) return "none";

            return string.Format("({0:F0},{1:F0})px",
                placement.ScreenPosition.x - Screen.width * 0.5f,
                placement.ScreenPosition.y - Screen.height * 0.5f);
        }

        /// <summary>
        /// How far this mod's projection of the aim point lands from the engine's own
        /// Camera.WorldToScreenPoint for the same point, in pixels.
        ///
        /// They agree only if the engine's projection honours the view-matrix override,
        /// and that is what decides whether anything the game projects through the
        /// camera - HUDQuestMarker reads the aim transform rather than the matrix, but
        /// a future element need not - is already compensated. A large delta says the
        /// opposite, and says it every second rather than once.
        /// </summary>
        private static float EngineProjectionDelta(Camera cam, ReticlePlacement placement)
        {
            if (!placement.Hit || !placement.Valid) return -1f;

            Vector3 engine = cam.WorldToScreenPoint(placement.AimPoint);
            float dx = placement.ScreenPosition.x - engine.x;
            float dy = placement.ScreenPosition.y - engine.y;
            return Mathf.Sqrt(dx * dx + dy * dy);
        }
    }
}
