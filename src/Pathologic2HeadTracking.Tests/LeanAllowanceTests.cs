using Pathologic2HeadTracking.CameraRig;
using Xunit;

namespace Pathologic2HeadTracking.Tests
{
    public class LeanAllowanceTests
    {
        /// <summary>
        /// Geometry nearer the eye than the near plane is culled, so a standoff that
        /// does not clear it leaves the player seeing through the wall it was meant
        /// to hold them off. Pathologic 2's near plane is 0.025 m, so the configured
        /// margin is what decides the standoff here rather than the floor.
        /// </summary>
        [Fact]
        public void StandoffClearsTheNearPlane()
        {
            Assert.Equal(0.15f, LeanAllowance.Standoff(0.15f, 0.025f), 5);
            Assert.Equal(0.30f, LeanAllowance.Standoff(0.02f, 0.20f), 5);
        }

        /// <summary>
        /// A ray that stops where the lean stops cannot see the surface the lean is
        /// about to come to rest against.
        /// </summary>
        [Fact]
        public void TraceOverreachesTheRequestedLean()
        {
            float trace = LeanAllowance.TraceDistance(0.30f, 0.15f);
            Assert.True(trace > 0.30f + 0.15f);
        }

        [Fact]
        public void AnOpenRoomAllowsTheWholeLean()
        {
            Assert.Equal(1f, LeanAllowance.Target(5f, 0.30f, 1f, 0.15f));
        }

        [Fact]
        public void ASurfaceCutsTheLeanToWhatFitsInFrontOfIt()
        {
            // Wall 0.25 m away, head asking for 0.30 m, eye held 0.15 m off it.
            Assert.Equal((0.25f - 0.15f) / 0.30f, LeanAllowance.Target(0.25f, 0.30f, 1f, 0.15f), 5);
        }

        /// <summary>
        /// The allowance never reaches zero, and that is what keeps the clamp
        /// steerable: a zero offset carries no direction, so the sweep would be stuck
        /// re-testing the direction that blocked it and the lean would be refused in
        /// every direction including straight back out.
        /// </summary>
        [Fact]
        public void AWallAgainstTheEyeStillLeavesTheClampSteerable()
        {
            Assert.Equal(LeanAllowance.Minimum, LeanAllowance.Target(0.01f, 0.30f, 1f, 0.15f));
        }

        [Fact]
        public void AGrazingApproachDoesNotDivergeTheStandoff()
        {
            // cos below the floor is clamped, so the allowed distance stays finite.
            float grazing = LeanAllowance.Target(1f, 0.30f, 0.001f, 0.15f);
            float floored = LeanAllowance.Target(1f, 0.30f, LeanAllowance.MinApproachCosine, 0.15f);
            Assert.Equal(floored, grazing, 5);
        }

        /// <summary>
        /// Tightening is instant and only the release is smoothed. Easing INTO a
        /// smaller allowance leaves the eye inside the geometry for the duration of
        /// the ease, which is the whole bug the clamp exists to stop.
        /// </summary>
        [Fact]
        public void TighteningIsInstant()
        {
            Assert.Equal(0.4f, LeanAllowance.Advance(1f, 0.4f, 0.9f, 1f / 60f));
        }

        [Fact]
        public void ReleaseIsSmoothedAndThenSnaps()
        {
            float first = LeanAllowance.Advance(0.4f, 1f, 0.9f, 1f / 60f);
            Assert.True(first > 0.4f);
            Assert.True(first < 1f);

            // An exponential only approaches its target, and in float32 the step
            // underflows against the ulp of 1.0 long before it arrives. Without the
            // snap, everything that asks "is the lean being cut" reads yes for the
            // rest of the session after one brush against a doorframe.
            Assert.Equal(1f, LeanAllowance.Advance(0.99999f, 1f, 0.9f, 1f / 60f));
        }
    }
}
