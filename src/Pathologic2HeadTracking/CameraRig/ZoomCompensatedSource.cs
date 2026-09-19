using System;
using CameraUnlock.Core.Data;
using CameraUnlock.Core.Protocol;

namespace Pathologic2HeadTracking.CameraRig
{
    /// <summary>
    /// Wraps the tracker source and applies the FOV zoom factor to the pose on its
    /// way into the pipeline.
    ///
    /// This seat is chosen so that everything downstream agrees: the interpolator,
    /// the smoothing, the camera write, the reticle basis and the diagnostics all
    /// see the pose the camera was actually driven with. A compensation applied
    /// later - at the camera write alone - would leave the reticle projecting from
    /// a pose nobody is looking through, and the doctrine's rule that a log line
    /// must carry the APPLIED pose could not be satisfied.
    ///
    /// Yaw, pitch and position scale; roll does not. Roll rotates the image about
    /// the view axis, and ten degrees of head roll rolls the picture ten degrees at
    /// every field of view there is, so scaling it would flatten a tilt the player
    /// is holding and buy nothing.
    /// </summary>
    public sealed class ZoomCompensatedSource : ITrackingDataSource
    {
        private readonly ITrackingDataSource _inner;
        private readonly Func<float> _zoomFactor;
        private readonly Action<string> _logWarning;
        private bool _reportedOutOfBounds;

        /// <param name="logWarning">
        /// Told once, the first time a sample falls outside
        /// <see cref="TrackerInputBounds"/>. A working tracker never sends one, so
        /// that line is the only trace of a sender that is broken or hostile.
        /// </param>
        public ZoomCompensatedSource(ITrackingDataSource inner, Func<float> zoomFactor,
            Action<string> logWarning = null)
        {
            if (inner == null) throw new ArgumentNullException("inner");
            if (zoomFactor == null) throw new ArgumentNullException("zoomFactor");
            _inner = inner;
            _zoomFactor = zoomFactor;
            _logWarning = logWarning;
        }

        public bool IsReceiving { get { return _inner.IsReceiving; } }
        public bool IsRemoteConnection { get { return _inner.IsRemoteConnection; } }
        public bool IsFailed { get { return _inner.IsFailed; } }

        public bool IsDataFresh(int maxAgeMs = OpenTrackReceiver.DefaultMaxDataAgeMs)
        {
            return _inner.IsDataFresh(maxAgeMs);
        }

        public TrackingPose GetLatestPose()
        {
            TrackingPose pose = Bound(_inner.GetLatestPose());
            float factor = _zoomFactor();
            if (factor == 1f) return pose;

            return new TrackingPose(
                ZoomCompensation.ScaleAngle(pose.Yaw, factor),
                ZoomCompensation.ScaleAngle(pose.Pitch, factor),
                pose.Roll,
                pose.TimestampTicks);
        }

        public PositionData GetLatestPosition()
        {
            PositionData position = Bound(_inner.GetLatestPosition());
            float factor = _zoomFactor();
            if (factor == 1f) return position;

            // A head offset d seen at depth D lands at d / (2 * D * tan(fov/2)) of
            // the frame, so a lean scales linearly and exactly.
            return new PositionData(
                position.X * factor, position.Y * factor, position.Z * factor,
                position.TimestampTicks);
        }

        /// <summary>
        /// Deliberately NOT scaled. The raw rotation is the unprocessed tracker
        /// reading by definition, and its only callers are diagnostics that want to
        /// see what arrived rather than what was applied.
        /// </summary>
        public void GetRawRotation(out float yaw, out float pitch, out float roll)
        {
            _inner.GetRawRotation(out yaw, out pitch, out roll);
        }

        private TrackingPose Bound(TrackingPose pose)
        {
            bool clamped = false;
            float yaw = TrackerInputBounds.Clamp(pose.Yaw, TrackerInputBounds.MaxAngleDegrees, ref clamped);
            float pitch = TrackerInputBounds.Clamp(pose.Pitch, TrackerInputBounds.MaxAngleDegrees, ref clamped);
            float roll = TrackerInputBounds.Clamp(pose.Roll, TrackerInputBounds.MaxAngleDegrees, ref clamped);
            if (!clamped) return pose;

            ReportOutOfBounds();
            return new TrackingPose(yaw, pitch, roll, pose.TimestampTicks);
        }

        private PositionData Bound(PositionData position)
        {
            bool clamped = false;
            float x = TrackerInputBounds.Clamp(position.X, TrackerInputBounds.MaxPositionMeters, ref clamped);
            float y = TrackerInputBounds.Clamp(position.Y, TrackerInputBounds.MaxPositionMeters, ref clamped);
            float z = TrackerInputBounds.Clamp(position.Z, TrackerInputBounds.MaxPositionMeters, ref clamped);
            if (!clamped) return position;

            ReportOutOfBounds();
            return new PositionData(x, y, z, position.TimestampTicks);
        }

        private void ReportOutOfBounds()
        {
            if (_reportedOutOfBounds) return;
            _reportedOutOfBounds = true;
            if (_logWarning != null)
            {
                _logWarning(string.Format(
                    "Tracker sent a pose outside +-{0} degrees or +-{1} m; clamped. A working "
                    + "tracker never does this, so check what is sending to the UDP port.",
                    TrackerInputBounds.MaxAngleDegrees, TrackerInputBounds.MaxPositionMeters));
            }
        }

        public bool TryConsumeRecenterRequest()
        {
            return _inner.TryConsumeRecenterRequest();
        }

        public void Recenter()
        {
            _inner.Recenter();
        }
    }
}
