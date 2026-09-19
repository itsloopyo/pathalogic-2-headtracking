using CameraUnlock.Core.Data;
using CameraUnlock.Core.Processing;
using CameraUnlock.Core.Protocol;
using Pathologic2HeadTracking.CameraRig;
using Xunit;

namespace Pathologic2HeadTracking.Tests
{
    /// <summary>
    /// The UDP socket takes datagrams from any host on the network, and the receiver
    /// only rejects values that are not finite as a float. A finite value near
    /// float.MaxValue still overflows the first subtraction or multiply it meets, and
    /// the smoothing stages then hold a NaN for the rest of the session.
    /// </summary>
    public class TrackerInputBoundsTests
    {
        private sealed class ScriptedSource : ITrackingDataSource
        {
            public TrackingPose Pose;
            public PositionData Position;

            public bool IsReceiving { get { return true; } }
            public bool IsRemoteConnection { get { return true; } }
            public bool IsFailed { get { return false; } }
            public bool IsDataFresh(int maxAgeMs = OpenTrackReceiver.DefaultMaxDataAgeMs) { return true; }
            public TrackingPose GetLatestPose() { return Pose; }
            public PositionData GetLatestPosition() { return Position; }
            public void GetRawRotation(out float yaw, out float pitch, out float roll)
            {
                yaw = Pose.Yaw; pitch = Pose.Pitch; roll = Pose.Roll;
            }
            public bool TryConsumeRecenterRequest() { return false; }
            public void Recenter() { }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        [Fact]
        public void ExtremeFiniteAnglesNeverReachThePipelineAsNaN()
        {
            var inner = new ScriptedSource();
            var source = new ZoomCompensatedSource(inner, () => 1f);
            var interpolator = new PoseInterpolator();
            var processor = new TrackingProcessor();

            float[] pitches = { float.MaxValue, -float.MaxValue, float.MaxValue, 10f, 0f };
            TrackingPose processed = default(TrackingPose);
            for (int i = 0; i < pitches.Length; i++)
            {
                inner.Pose = new TrackingPose(float.MaxValue, pitches[i], -float.MaxValue, i + 1);
                for (int frame = 0; frame < 3; frame++)
                    processed = processor.Process(interpolator.Update(source.GetLatestPose(), 1f / 60f), 1f / 60f);
            }

            Assert.True(IsFinite(processed.Yaw));
            Assert.True(IsFinite(processed.Pitch));
            Assert.True(IsFinite(processed.Roll));
        }

        [Fact]
        public void ExtremeFinitePositionSurvivesAZoomFactor()
        {
            var inner = new ScriptedSource
            {
                Position = new PositionData(float.MaxValue, -float.MaxValue, float.MaxValue, 1)
            };
            var source = new ZoomCompensatedSource(inner, () => 1000f);

            PositionData position = source.GetLatestPosition();

            Assert.True(IsFinite(position.X));
            Assert.True(IsFinite(position.Y));
            Assert.True(IsFinite(position.Z));
        }

        [Fact]
        public void ExtremeFinitePositionNeverReachesTheLeanAsNaN()
        {
            var inner = new ScriptedSource();
            var source = new ZoomCompensatedSource(inner, () => 1f);
            var interpolator = new PositionInterpolator();
            var processor = new PositionProcessor();

            float[] xs = { float.MaxValue, -float.MaxValue, float.MaxValue, 0.1f };
            Vec3 lean = Vec3.Zero;
            for (int i = 0; i < xs.Length; i++)
            {
                inner.Position = new PositionData(xs[i], 0f, 0f, i + 1);
                for (int frame = 0; frame < 3; frame++)
                    lean = processor.Process(
                        interpolator.Update(source.GetLatestPosition(), 1f / 60f),
                        Quat4.Identity, 1f / 60f);
            }

            Assert.True(IsFinite(lean.X));
        }

        [Fact]
        public void InRangePoseIsPassedThroughUntouched()
        {
            var inner = new ScriptedSource
            {
                Pose = new TrackingPose(-179.5f, 89f, 180f, 1),
                Position = new PositionData(0.3f, -0.2f, -0.4f, 1)
            };
            var source = new ZoomCompensatedSource(inner, () => 1f);

            TrackingPose pose = source.GetLatestPose();
            PositionData position = source.GetLatestPosition();

            Assert.Equal(-179.5f, pose.Yaw);
            Assert.Equal(89f, pose.Pitch);
            Assert.Equal(180f, pose.Roll);
            Assert.Equal(0.3f, position.X);
            Assert.Equal(-0.2f, position.Y);
            Assert.Equal(-0.4f, position.Z);
        }

        [Fact]
        public void OutOfRangeValuesAreClampedAndReportedOnce()
        {
            var inner = new ScriptedSource
            {
                Pose = new TrackingPose(1e30f, -500f, 0f, 1),
                Position = new PositionData(0f, 1e30f, 0f, 1)
            };
            int reports = 0;
            var source = new ZoomCompensatedSource(inner, () => 1f, _ => reports++);

            TrackingPose pose = source.GetLatestPose();
            PositionData position = source.GetLatestPosition();
            source.GetLatestPose();

            Assert.Equal(TrackerInputBounds.MaxAngleDegrees, pose.Yaw);
            Assert.Equal(-TrackerInputBounds.MaxAngleDegrees, pose.Pitch);
            Assert.Equal(TrackerInputBounds.MaxPositionMeters, position.Y);
            Assert.Equal(1, reports);
        }
    }
}
