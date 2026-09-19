using Pathologic2HeadTracking.CameraRig;
using Xunit;

namespace Pathologic2HeadTracking.Tests
{
    public class ZoomCompensationTests
    {
        /// <summary>
        /// The gate the doctrine puts on this whole conversion: ordinary play must
        /// read exactly 1.0, or the mod is quietly scaling every frame.
        /// </summary>
        [Fact]
        public void OrdinaryPlayIsExactlyUnity()
        {
            Assert.Equal(1f, ZoomCompensation.Factor(60f, 60f), 6);
        }

        [Fact]
        public void NarrowingTheViewShrinksTheFactor()
        {
            float factor = ZoomCompensation.Factor(40f, 60f);
            Assert.True(factor < 1f);
            Assert.Equal(
                (float)(System.Math.Tan(20.0 * System.Math.PI / 180.0)
                        / System.Math.Tan(30.0 * System.Math.PI / 180.0)),
                factor, 5);
        }

        [Fact]
        public void WideningTheViewGrowsTheFactor()
        {
            Assert.True(ZoomCompensation.Factor(100f, 60f) > 1f);
        }

        /// <summary>
        /// A field of view the mod cannot read applies no compensation rather than a
        /// guessed one - this is the boundary check, so the only safe answer is 1.0.
        /// </summary>
        [Theory]
        [InlineData(0f, 60f)]
        [InlineData(60f, 0f)]
        [InlineData(float.NaN, 60f)]
        [InlineData(60f, float.PositiveInfinity)]
        [InlineData(200f, 60f)]
        public void AnUnreadableFieldOfViewScalesNothing(float live, float baseFov)
        {
            Assert.Equal(1f, ZoomCompensation.Factor(live, baseFov));
        }

        /// <summary>
        /// The tangent round trip is what makes the rescale exact rather than a plain
        /// multiply: the rescaled angle displaces the image by as much at the live FOV
        /// as the original did at the base one.
        /// </summary>
        [Fact]
        public void ScaledAngleHoldsTheSameScreenDisplacement()
        {
            const float live = 40f;
            const float baseFov = 60f;
            float factor = ZoomCompensation.Factor(live, baseFov);
            float scaled = ZoomCompensation.ScaleAngle(20f, factor);

            double displacementBefore = System.Math.Tan(20.0 * System.Math.PI / 180.0)
                                        / System.Math.Tan(baseFov * 0.5 * System.Math.PI / 180.0);
            double displacementAfter = System.Math.Tan(scaled * System.Math.PI / 180.0)
                                       / System.Math.Tan(live * 0.5 * System.Math.PI / 180.0);

            Assert.Equal(displacementBefore, displacementAfter, 6);
        }

        [Fact]
        public void AUnityFactorLeavesTheAngleAlone()
        {
            Assert.Equal(17.5f, ZoomCompensation.ScaleAngle(17.5f, 1f));
        }
    }
}
