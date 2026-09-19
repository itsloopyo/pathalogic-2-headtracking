using System;
using CameraUnlock.Core.Unity.Extensions;
using Pathologic2HeadTracking.Config;
using UnityEngine;

namespace Pathologic2HeadTracking.Core
{
    public sealed class InputHandler
    {
        private readonly ConfigManager _config;

        public event Action OnTogglePressed;
        public event Action OnCycleTrackingModePressed;
        public event Action OnToggleYawModePressed;
        public event Action OnToggleReticlePressed;

        public KeyCode ToggleKey { get { return _config.ToggleKey.Value; } }
        public KeyCode CycleTrackingModeKey { get { return _config.CycleTrackingModeKey.Value; } }
        public KeyCode YawModeKey { get { return _config.YawModeKey.Value; } }
        public KeyCode ToggleReticleKey { get { return _config.ToggleReticleKey.Value; } }

        public InputHandler(ConfigManager config)
        {
            _config = config;
        }

        public void CheckInput()
        {
            // Common case: nothing pressed this frame. Skip the GetKeyDown probes and
            // the ConfigEntry reads behind them.
            if (!Input.anyKeyDown)
                return;

            Dispatch(_config.ToggleKey.Value, ChordHotkeys.ToggleLetter, OnTogglePressed);
            Dispatch(_config.CycleTrackingModeKey.Value, ChordHotkeys.PositionLetter, OnCycleTrackingModePressed);
            Dispatch(_config.YawModeKey.Value, ChordHotkeys.FourthToggleLetter, OnToggleYawModePressed);
            Dispatch(_config.ToggleReticleKey.Value, ChordHotkeys.FifthToggleLetter, OnToggleReticlePressed);
        }

        private static void Dispatch(KeyCode primary, KeyCode chordLetter, Action handler)
        {
            if (ChordHotkeys.IsActionPressed(primary, chordLetter))
                handler?.Invoke();
        }
    }
}
