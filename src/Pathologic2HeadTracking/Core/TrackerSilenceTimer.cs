namespace Pathologic2HeadTracking.Core
{
    /// <summary>
    /// Decides the one moment to say that no tracker datagram has ever arrived.
    ///
    /// Timed from the bind rather than from startup. A port another process still
    /// holds is retried for as long as it takes, and the retry loop reports that
    /// itself; a line about silence on a socket that does not exist yet names the
    /// wrong fault and would always beat the real one into the log.
    /// </summary>
    public sealed class TrackerSilenceTimer
    {
        private readonly long _warnAfterMs;

        private bool _warned;
        private bool _hasListeningSince;
        private long _listeningSinceMs;

        public TrackerSilenceTimer(long warnAfterMs)
        {
            _warnAfterMs = warnAfterMs;
        }

        /// <summary>
        /// Call every frame. True exactly once, on the first poll at least the warning
        /// interval after the socket was first seen bound, and never once any data has
        /// arrived.
        /// </summary>
        public bool Poll(bool everReceived, bool socketFailed, long nowMs)
        {
            if (_warned || everReceived) return false;

            if (socketFailed)
            {
                _hasListeningSince = false;
                return false;
            }

            if (!_hasListeningSince)
            {
                _hasListeningSince = true;
                _listeningSinceMs = nowMs;
                return false;
            }

            if (nowMs - _listeningSinceMs < _warnAfterMs) return false;

            _warned = true;
            return true;
        }
    }
}
