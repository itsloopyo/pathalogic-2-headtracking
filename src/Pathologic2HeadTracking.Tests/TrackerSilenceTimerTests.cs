using Pathologic2HeadTracking.Core;
using Xunit;

namespace Pathologic2HeadTracking.Tests
{
    public class TrackerSilenceTimerTests
    {
        private const long WarnAfterMs = 15000;

        [Fact]
        public void WarnsOnceAfterTheIntervalFromTheFirstBoundPoll()
        {
            var timer = new TrackerSilenceTimer(WarnAfterMs);

            Assert.False(timer.Poll(false, false, 1000));
            Assert.False(timer.Poll(false, false, 1000 + WarnAfterMs - 1));
            Assert.True(timer.Poll(false, false, 1000 + WarnAfterMs));
            Assert.False(timer.Poll(false, false, 1000 + WarnAfterMs * 10));
        }

        [Fact]
        public void NeverWarnsOnceDataHasArrived()
        {
            var timer = new TrackerSilenceTimer(WarnAfterMs);

            Assert.False(timer.Poll(false, false, 0));
            Assert.False(timer.Poll(true, false, WarnAfterMs * 2));
        }

        [Fact]
        public void AFailedSocketRestartsTheClockFromTheNextBoundPoll()
        {
            var timer = new TrackerSilenceTimer(WarnAfterMs);

            Assert.False(timer.Poll(false, false, 0));
            Assert.False(timer.Poll(false, true, WarnAfterMs));
            Assert.False(timer.Poll(false, false, WarnAfterMs + 500));
            Assert.False(timer.Poll(false, false, WarnAfterMs * 2 + 499));
            Assert.True(timer.Poll(false, false, WarnAfterMs * 2 + 500));
        }

        [Fact]
        public void TheFirstBoundPollOnlyStartsTheClock()
        {
            var timer = new TrackerSilenceTimer(WarnAfterMs);

            Assert.False(timer.Poll(false, false, WarnAfterMs * 100));
        }
    }
}
