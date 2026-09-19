using UnityEngine;

namespace Pathologic2HeadTracking.CameraRig
{
    /// <summary>
    /// Cuts a positional lean back to whatever the level leaves room for, so the
    /// rendered eye never ends up inside geometry. Rotation cannot cause this -
    /// rotation does not move the eye - so nothing here runs in rotation-only mode.
    ///
    /// Core owns this policy in C++ (cameraunlock::camera::LeanClamp) but has no C#
    /// equivalent, so the two halves are built here: this type is the policy, and
    /// the sweep it calls is the query.
    ///
    /// A zero-extent line, not a swept sphere. The camera sits inside the player's
    /// own collider, and any sweep with a volume reports an immediate overlap from
    /// there and refuses the lean in every direction including away from the wall.
    /// The standoff is therefore carried here rather than being a sweep radius, and
    /// the trace has to overreach the lean to see the surface it is about to come to
    /// rest against.
    /// </summary>
    public sealed class LeanClamp
    {
        private const float MinLeanToClamp = 1e-4f;

        private readonly int _mask;
        private float _allowance = 1f;

        private readonly float _releaseSmoothing;

        /// <summary>Standoff held off any surface, in meters.</summary>
        public float Margin { get; private set; }

        /// <summary>True while the last evaluated lean was cut back by geometry.</summary>
        public bool InContact { get; private set; }

        /// <summary>
        /// True when the sweep could not run. A clamp that has quietly stopped
        /// clamping looks exactly like one that never engaged, so this is reported
        /// separately from InContact rather than passing the lean through in silence.
        /// </summary>
        public bool LastQueryFailed { get; private set; }

        /// <summary>
        /// Distance the sweep found this frame, in meters. Zero when it found nothing,
        /// which includes the frames the release is still easing open: those are
        /// contact frames with no surface of their own to report.
        /// </summary>
        public float LastContactDistance { get; private set; }

        public bool HasMask { get { return _mask != 0; } }

        /// <param name="layerMask">Layers the sweep treats as solid.</param>
        /// <param name="margin">Standoff held off any surface, in meters.</param>
        /// <param name="releaseSmoothing">
        /// Smoothing applied when the allowance GROWS, on the fleet's 0-1 scale (0.9 is
        /// a 200ms time constant). Tightening is never smoothed; see
        /// <see cref="LeanAllowance.Advance"/>.
        /// </param>
        public LeanClamp(int layerMask, float margin, float releaseSmoothing)
        {
            _mask = layerMask;
            Margin = margin;
            _releaseSmoothing = releaseSmoothing;
        }

        /// <summary>
        /// Drops the allowance back to fully open. Call on any camera cut and on any
        /// frame that applies no lean at all, or the allowance carries the previous
        /// room's wall into the next one.
        /// </summary>
        public void Reset()
        {
            _allowance = 1f;
            InContact = false;
            LastQueryFailed = false;
            LastContactDistance = 0f;
        }

        /// <summary>
        /// Fraction of the requested lean that fits, in [0, 1].
        /// </summary>
        /// <param name="cleanEye">Where the game itself put the camera this frame.</param>
        /// <param name="requestedOffset">World-space lean the tracker is asking for.</param>
        /// <param name="nearClipPlane">The camera's near clip distance.</param>
        /// <param name="deltaTime">Frame time, so the release is frame-rate independent.</param>
        public float Evaluate(Vector3 cleanEye, Vector3 requestedOffset, float nearClipPlane,
            float deltaTime)
        {
            InContact = false;
            LastQueryFailed = false;
            LastContactDistance = 0f;

            if (_mask == 0)
            {
                LastQueryFailed = true;
                _allowance = 1f;
                return 1f;
            }

            float requested = requestedOffset.magnitude;
            if (requested < MinLeanToClamp)
            {
                _allowance = 1f;
                return 1f;
            }

            float standoff = LeanAllowance.Standoff(Margin, nearClipPlane);
            Vector3 direction = requestedOffset / requested;

            RaycastHit hit;
            float target;
            if (Physics.Raycast(cleanEye, direction, out hit,
                    LeanAllowance.TraceDistance(requested, standoff), _mask,
                    QueryTriggerInteraction.Ignore))
            {
                target = LeanAllowance.Target(
                    hit.distance, requested, Vector3.Dot(direction, -hit.normal), standoff);
                LastContactDistance = hit.distance;
            }
            else
            {
                target = 1f;
            }

            _allowance = LeanAllowance.Advance(_allowance, target, _releaseSmoothing, deltaTime);

            // Read off the allowance actually in force, not off this frame's sweep
            // answer. While the release is easing back open the clamp is still cutting
            // the lean, and calling that clear drops those frames from the account and
            // prints "leanAllow=0.35 contact=False", which reads as a contradiction on
            // the one line the question is asked from.
            InContact = _allowance < 1f;

            return _allowance;
        }
    }
}
