using BepInEx.Logging;
using CameraUnlock.Core.Data;
using CameraUnlock.Core.Processing;
using CameraUnlock.Core.Unity.Tracking;
using UnityEngine;

namespace Pathologic2HeadTracking.CameraRig
{
    /// <summary>
    /// Drives <see cref="LeanClamp"/> from the frame: recovers the lean the tracker
    /// is asking for, sweeps for it, hands the surviving fraction to the position
    /// processor, and keeps the running account the log lines are written from.
    ///
    /// The clamp itself is the policy and knows nothing about the pipeline; this is
    /// the seat that connects the two, so the plugin owns neither.
    /// </summary>
    public sealed class LeanClampController
    {
        private const float LogIntervalSeconds = 10f;

        private const float MinRequestedLeanSquared = 1e-8f;

        private readonly ViewMatrixTrackingController _cameraController;
        private readonly PositionProcessor _positionProcessor;
        private readonly PositionSettings _baseSettings;
        private readonly ManualLogSource _logger;

        private LeanClamp _clamp;

        // Kept as counts rather than transition lines: a lean held against a
        // doorframe crosses the contact edge several times a second as the head
        // jitters, and one line per crossing would bury the shared BepInEx log.
        // Nothing is written while the room is open.
        private bool _loggedLive;
        private int _contactFrames;
        private int _queryFailFrames;
        private float _tightestAllowance = 1f;
        private float _closestContact;
        private float _nextLogTime;

        private float _allowance = 1f;
        private Vector3 _requestedLean;

        public LeanClampController(
            ViewMatrixTrackingController cameraController,
            PositionProcessor positionProcessor,
            PositionSettings baseSettings,
            ManualLogSource logger)
        {
            _cameraController = cameraController;
            _positionProcessor = positionProcessor;
            _baseSettings = baseSettings;
            _logger = logger;
        }

        /// <summary>Fraction of the requested lean in force, for the diagnostics line.</summary>
        public float Allowance
        {
            get { return _allowance; }
        }

        public bool InContact
        {
            get { return _clamp != null && _clamp.InContact; }
        }

        public bool LastQueryFailed
        {
            get { return _clamp != null && _clamp.LastQueryFailed; }
        }

        /// <summary>False until the physics layers have resolved and <see cref="Arm"/> has run.</summary>
        public bool IsArmed
        {
            get { return _clamp != null; }
        }

        public bool HasMask
        {
            get { return _clamp != null && _clamp.HasMask; }
        }

        /// <summary>
        /// Builds the clamp once the scene's tag manager can answer layer names.
        /// </summary>
        public void Arm(int layerMask, float margin, float releaseSmoothing)
        {
            _clamp = new LeanClamp(layerMask, margin, releaseSmoothing);
        }

        /// <summary>
        /// Sweeps from the clean eye toward where the head wants to go and scales the
        /// whole position offset to whatever fits. Call before the camera controller
        /// processes the frame, so the clamp lands on the offset before it is applied
        /// rather than after the eye is already inside the wall.
        /// </summary>
        public void Apply(bool shouldTrack, bool collisionEnabled)
        {
            bool active = shouldTrack
                          && collisionEnabled
                          && _clamp != null
                          && _cameraController.PositionEnabled;

            if (!active)
            {
                // Reset on every frame that applies no lean, or the allowance carries
                // the previous room's wall into the next one.
                if (_allowance != 1f || _requestedLean != Vector3.zero)
                {
                    Reset();
                    _requestedLean = Vector3.zero;
                }
                return;
            }

            Camera cam = _cameraController.MainCamera;
            if (cam == null) return;

            // The sweep is asked about the UNCLAMPED lean, recovered by undoing the
            // scale the clamp itself applied. Sweeping along the applied offset
            // instead produces a limit cycle the moment the clamp refuses a lean
            // outright: the offset goes to zero, a zero offset has no direction, the
            // allowance reopens, the lean comes back, and the eye chatters in and out
            // of the wall. LeanAllowance.Minimum is what keeps this exact rather than
            // needing a held fallback - the allowance never reaches zero, so the
            // direction is always the one the head is currently asking for.
            Vec3 applied = _cameraController.LastTrackingPosition;
            _requestedLean = new Vector3(applied.X, applied.Y, -applied.Z) / _allowance;

            if (_requestedLean.sqrMagnitude < MinRequestedLeanSquared)
            {
                if (_allowance != 1f) Reset();
                return;
            }

            // The camera pose is as of the last LateUpdate to run before this one,
            // which may or may not be after the game's own camera rig: a
            // runtime-loaded assembly has no place in the script execution order.
            // A frame stale at worst, over a query spanning 0.3m, while a sprinting
            // player covers under 0.1m in that time.
            Transform camTr = cam.transform;
            Vector3 requestedWorld = camTr.rotation * _requestedLean;
            float allowance = _clamp.Evaluate(
                camTr.position, requestedWorld, cam.nearClipPlane, Time.deltaTime);
            Record(allowance, cam.nearClipPlane);

            if (allowance == _allowance) return;
            _allowance = allowance;
            _positionProcessor.Settings = PositionScaling.Scale(_baseSettings, allowance);
        }

        /// <summary>
        /// Drops the allowance back to fully open. Call on any camera cut, or the
        /// allowance carries the previous room's wall into the next one.
        /// </summary>
        public void Reset()
        {
            if (_clamp != null) _clamp.Reset();
            _positionProcessor.Settings = _baseSettings;
            _allowance = 1f;
        }

        /// <summary>
        /// Accumulates what the sweep did this frame. The one-shot line is the answer
        /// to "is the clamp running at all" - a clamp that never engages and a clamp
        /// that is not being asked read identically from the summaries alone, and the
        /// two need different fixes.
        /// </summary>
        private void Record(float allowance, float nearClipPlane)
        {
            if (_clamp.LastQueryFailed)
            {
                _queryFailFrames++;
                return;
            }

            // Only once a sweep has actually run. Written above this check it claimed
            // the clamp was running on a build whose collision layer names resolved to
            // nothing, where every evaluation short-circuits and the clamp does not
            // run at all.
            if (!_loggedLive)
            {
                _loggedLive = true;
                _logger.LogInfo(string.Format(
                    "Lean clamp is running (margin {0:F2}m, near clip {1:F2}m)",
                    _clamp.Margin, nearClipPlane));
            }

            if (!_clamp.InContact) return;

            _contactFrames++;
            if (allowance < _tightestAllowance) _tightestAllowance = allowance;

            // Zero while the release is easing open with nothing in front of the eye,
            // which is a contact frame with no surface of its own to report.
            float contact = _clamp.LastContactDistance;
            if (contact > 0f && (_closestContact == 0f || contact < _closestContact))
                _closestContact = contact;
        }

        /// <summary>
        /// Call every frame; writes at most one pair of lines per interval, and
        /// nothing at all on the frames the lean fits. Flushed from Update rather
        /// than from the sweep so the last few contact frames still reach the log
        /// after the player straightens up and the sweep stops running.
        /// </summary>
        public void FlushLog()
        {
            if (_contactFrames == 0 && _queryFailFrames == 0) return;
            if (Time.realtimeSinceStartup < _nextLogTime) return;
            _nextLogTime = Time.realtimeSinceStartup + LogIntervalSeconds;

            if (_queryFailFrames > 0)
            {
                _logger.LogWarning("Lean clamp: the sweep could not run on "
                                   + _queryFailFrames
                                   + " frames, so the lean passed through unclamped.");
            }
            if (_contactFrames > 0)
            {
                _logger.LogInfo(string.Format(
                    "Lean clamp: cut the lean on {0} frames, tightest {1:F2} of the "
                    + "request, closest surface {2:F2}m",
                    _contactFrames, _tightestAllowance, _closestContact));
            }

            _contactFrames = 0;
            _queryFailFrames = 0;
            _tightestAllowance = 1f;
            _closestContact = 0f;
        }
    }
}
