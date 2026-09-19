using BepInEx.Logging;
using CameraUnlock.Core.Math;
using CameraUnlock.Core.Protocol;
using CameraUnlock.Core.Unity.UI;
using Pathologic2HeadTracking.Config;

namespace Pathologic2HeadTracking.Core
{
    /// <summary>
    /// Reports what the tracker connection is doing: connect and loss, which smoothing
    /// value the current source selects, and a tracker that has never sent anything.
    /// </summary>
    public sealed class TrackerConnectionMonitor
    {
        // How long the socket may stay silent after it binds before the log says so.
        // A tracker aimed at the wrong port, or stopped before it reaches this
        // process, binds and logs exactly like a working one and then never writes
        // another line, so without this the log of a session with no tracking at all
        // reads the same as the log of a good one.
        private const long NoDataWarningMs = 15000;

        private readonly OpenTrackReceiver _receiver;
        private readonly ConfigManager _config;
        private readonly NotificationUI _notificationUI;
        private readonly ManualLogSource _logger;
        private readonly TrackerSilenceTimer _silence = new TrackerSilenceTimer(NoDataWarningMs);

        // Monotonic and independent of Time.timeScale, which the pause menu takes to
        // zero. Time.unscaledTime would do the same job for the first hour and then
        // lose millisecond resolution to float precision.
        private readonly System.Diagnostics.Stopwatch _clock = System.Diagnostics.Stopwatch.StartNew();

        private bool _wasReceiving;
        private bool _everReceived;
        private bool _cachedIsRemoteConnection;
        private bool _hasCachedConnectionLocality;

        public TrackerConnectionMonitor(
            OpenTrackReceiver receiver,
            ConfigManager config,
            NotificationUI notificationUI,
            ManualLogSource logger)
        {
            _receiver = receiver;
            _config = config;
            _notificationUI = notificationUI;
            _logger = logger;
        }

        /// <summary>Call once per Update.</summary>
        public void Update()
        {
            MonitorConnectionState();
            MonitorConnectionLocality();
            WarnIfNoTrackerData();
        }

        private void MonitorConnectionState()
        {
            bool isReceiving = _receiver.IsReceiving;
            if (isReceiving == _wasReceiving) return;

            _logger.LogInfo(isReceiving
                ? "OpenTrack connection established"
                : "OpenTrack connection lost");

            if (_config.ShowConnectionNotifications.Value)
            {
                if (isReceiving) _notificationUI.ShowConnectionEstablished();
                else _notificationUI.ShowConnectionLost();
            }
            _wasReceiving = isReceiving;
            _everReceived |= isReceiving;
        }

        private void MonitorConnectionLocality()
        {
            bool isRemoteConnection = _receiver.IsRemoteConnection;
            if (_hasCachedConnectionLocality && isRemoteConnection == _cachedIsRemoteConnection)
                return;

            _cachedIsRemoteConnection = isRemoteConnection;
            _hasCachedConnectionLocality = true;

            float effective = SmoothingUtils.GetEffectiveSmoothing(
                _config.LocalSmoothing.Value, _config.RemoteSmoothing.Value, isRemoteConnection);
            _logger.LogInfo("Tracker source is " + (isRemoteConnection ? "remote" : "local")
                            + ", smoothing=" + effective.ToString("F2"));
        }

        /// <summary>
        /// One line, once, when no datagram has ever arrived. Silence here is the
        /// symptom of a tracker sending to a different port or to a different machine,
        /// and it is the one fault the rest of the log cannot show.
        /// </summary>
        private void WarnIfNoTrackerData()
        {
            if (!_silence.Poll(_everReceived, _receiver.IsFailed, _clock.ElapsedMilliseconds))
                return;

            _logger.LogWarning(string.Format(
                "No tracker data on UDP port {0} after {1}s. Send OpenTrack UDP to this "
                + "machine on that port, or set UDPPort under [Network] in "
                + "BepInEx/config/{2}.cfg to the port the tracker sends on.",
                _config.UDPPort.Value, NoDataWarningMs / 1000, HeadTrackingPlugin.PluginGUID));
        }
    }
}
