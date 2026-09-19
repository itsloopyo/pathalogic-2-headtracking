using CameraUnlock.Core.Data;
using Pathologic2HeadTracking.CameraRig;
using Xunit;

namespace Pathologic2HeadTracking.Tests
{
    public class PositionScalingTests
    {
        private static PositionSettings Base()
        {
            return PositionSettings.Symmetric(
                sensitivityX: 1f, sensitivityY: 1f, sensitivityZ: 1f,
                limitX: 0.30f, limitY: 0.20f, limitZ: 0.40f, limitZBack: 0.10f,
                localSmoothing: 0f, remoteSmoothing: 0.15f,
                invertX: true, invertY: false, invertZ: false);
        }

        /// <summary>
        /// Scaling the sensitivities and the limits by the same fraction scales the
        /// offset uniformly, because clamp(raw * s * f, +-L * f) == f * clamp(raw * s,
        /// +-L). That identity is what lets the lean clamp hand its allowance to the
        /// position processor as a settings block rather than post-multiplying an
        /// offset the processor has already clamped.
        /// </summary>
        [Fact]
        public void EveryMagnitudeScalesTogether()
        {
            PositionSettings scaled = PositionScaling.Scale(Base(), 0.5f);

            Assert.Equal(0.5f, scaled.SensitivityX, 6);
            Assert.Equal(0.5f, scaled.SensitivityY, 6);
            Assert.Equal(0.5f, scaled.SensitivityZ, 6);
            Assert.Equal(0.15f, scaled.LimitX, 6);
            Assert.Equal(0.10f, scaled.LimitY, 6);
            Assert.Equal(0.10f, scaled.LimitYDown, 6);
            Assert.Equal(0.20f, scaled.LimitZ, 6);
            Assert.Equal(0.05f, scaled.LimitZBack, 6);
        }

        /// <summary>
        /// The smoothing pair and the inversion flags are conversions rather than
        /// distances, so the clamp must carry them across untouched. Scaling smoothing
        /// with the allowance would change the filter every time the player brushed a
        /// doorframe.
        /// </summary>
        [Fact]
        public void ConversionsSurviveScalingUnchanged()
        {
            PositionSettings scaled = PositionScaling.Scale(Base(), 0.25f);

            Assert.Equal(0f, scaled.LocalSmoothing);
            Assert.Equal(0.15f, scaled.RemoteSmoothing);
            Assert.True(scaled.InvertX);
            Assert.False(scaled.InvertY);
            Assert.False(scaled.InvertZ);
        }

        /// <summary>
        /// The asymmetric z budget is the one the doctrine calls out: the generous
        /// 0.40 m belongs to the forward lean and must stay there through any scale.
        /// </summary>
        [Fact]
        public void TheForwardBudgetStaysTheGenerousOne()
        {
            PositionSettings scaled = PositionScaling.Scale(Base(), 0.75f);
            Assert.True(scaled.LimitZ > scaled.LimitZBack);
        }
    }
}
