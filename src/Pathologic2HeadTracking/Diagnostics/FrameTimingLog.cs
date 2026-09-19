using System;
using System.Diagnostics;
using BepInEx.Logging;

namespace Pathologic2HeadTracking.Diagnostics
{
    public sealed class FrameTimingLog
    {
        private const double SlowFrameMs = 50;
        private const double WindowMs = 10000;

        private readonly ManualLogSource _logger;
        private long _lastFrame;
        private long _windowStart;
        private int _collections;
        private int _frames;
        private int _lastCollections;
        private int _slowFrames;
        private int _gcSlowFrames;
        private double _gap;
        private double _update;
        private double _lateUpdate;
        private double _gui;

        public FrameTimingLog(ManualLogSource logger)
        {
            _logger = logger;
        }

        public void BeginFrame()
        {
            long now = Stopwatch.GetTimestamp();
            int collections = GC.CollectionCount(0);
            if (_lastFrame != 0)
            {
                double gap = Milliseconds(now - _lastFrame);
                _gap = Math.Max(_gap, gap);
                if (gap >= SlowFrameMs)
                {
                    _slowFrames++;
                    if (collections != _lastCollections) _gcSlowFrames++;
                }
            }
            _lastCollections = collections;
            _lastFrame = now;
            _frames++;
            if (_windowStart == 0)
            {
                _windowStart = now;
                _collections = GC.CollectionCount(0);
            }
            if (Milliseconds(now - _windowStart) < WindowMs) return;

            _logger.LogInfo(string.Format(
                "FRAME frames={0} maxGapMs={1:F2} updateMs={2:F2} lateUpdateMs={3:F2} guiMs={4:F2} gc={5} slowFrames={6} gcSlowFrames={7}",
                _frames, _gap, _update, _lateUpdate, _gui, collections - _collections,
                _slowFrames, _gcSlowFrames));
            _windowStart = Stopwatch.GetTimestamp();
            _collections = GC.CollectionCount(0);
            _frames = 0;
            _slowFrames = _gcSlowFrames = 0;
            _gap = _update = _lateUpdate = _gui = 0;
        }

        public void EndUpdate(long start) { _update = Math.Max(_update, Elapsed(start)); }
        public void EndLateUpdate(long start) { _lateUpdate = Math.Max(_lateUpdate, Elapsed(start)); }
        public void EndGUI(long start) { _gui = Math.Max(_gui, Elapsed(start)); }

        private static double Elapsed(long start)
        {
            return Milliseconds(Stopwatch.GetTimestamp() - start);
        }

        private static double Milliseconds(long ticks)
        {
            return ticks * 1000.0 / Stopwatch.Frequency;
        }
    }
}
