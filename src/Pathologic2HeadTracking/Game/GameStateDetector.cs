using System;
using UnityEngine;

namespace Pathologic2HeadTracking.Game
{
    /// <summary>
    /// Decides whether the frame being drawn is free-look gameplay. Polled on a fixed
    /// interval rather than every frame - every read here is a reflected member access,
    /// and the states it distinguishes last far longer than a frame.
    ///
    /// The conditions are the ones Pathologic 2's own PlayerUtility.IsPlayerCanControlling
    /// tests, read one at a time so nothing has to be caught. See
    /// <see cref="GameReflection"/> for why the composite property is not called directly.
    /// </summary>
    public sealed class GameStateDetector
    {
        private const float PollInterval = 0.1f;
        private const float WarmupSeconds = 1.5f;

        private float _nextPollTime;
        private float _worldEntryTime = -1f;
        private GameState _state = GameState.Loading;
        private CameraKind _kind = CameraKind.Unknown;

        public event Action<GameState> StateChanged;

        public GameState State
        {
            get { return _state; }
        }

        /// <summary>The camera controller the game had in play at the last poll.</summary>
        public CameraKind CameraKind
        {
            get { return _kind; }
        }

        /// <summary>
        /// True only in gameplay and only once the warmup has elapsed. The warmup
        /// covers the frames right after a level loads, where the camera bone chain is
        /// still snapping into place before the player has control.
        /// </summary>
        public bool IsGameplayActive
        {
            get { return _state == GameState.Gameplay && IsTrackingActive; }
        }

        public bool IsTrackingActive
        {
            get
            {
                if (!GameStateRules.AllowsTracking(_state)) return false;
                return _worldEntryTime >= 0f
                       && Time.realtimeSinceStartup - _worldEntryTime >= WarmupSeconds;
            }
        }

        public void Update()
        {
            // Rate-limited in gameplay only. Off gameplay the view is untracked, so every
            // frame between a screen closing and the poll noticing is drawn off where the
            // player's head is pointing, then jumps onto it.
            if (_state == GameState.Gameplay && Time.realtimeSinceStartup < _nextPollTime) return;
            _nextPollTime = Time.realtimeSinceStartup + PollInterval;

            GameState next = Evaluate();
            if (next == _state) return;

            GameState previous = _state;
            _state = next;

            // Timed from the world coming up rather than from entry into gameplay.
            // Every screen the player opens reports Menu, so arming it on the gameplay
            // edge would charge the full warmup - plus the controller's own fade in -
            // to closing the inventory, which in this game is opened constantly.
            if (!IsInWorld(next)) _worldEntryTime = -1f;
            else if (!IsInWorld(previous)) _worldEntryTime = Time.realtimeSinceStartup;

            StateChanged?.Invoke(next);
        }

        /// <summary>
        /// Whether a world is loaded with the flat camera in play. Menu and Cutscene
        /// both are; only Loading is not, so only coming back from a load re-arms the
        /// warmup.
        /// </summary>
        private static bool IsInWorld(GameState state)
        {
            return state != GameState.Loading;
        }

        private GameState Evaluate()
        {
            if (!GameReflection.Resolved) return GameState.Loading;

            // The one test for a world being up. Every screen in this game leaves the
            // camera in place and only changes what is drawn over it, so the camera
            // going away means the title screen or a level load and nothing else.
            Camera cam = GameReflection.MainCamera;
            if (cam == null || !cam.isActiveAndEnabled) return GameState.Loading;

            _kind = GameReflection.CurrentCameraKind;

            // Unknown is what every screen in this game sets the camera to on its way
            // in: the inventory, the map, the mind map, sleep, lock picking and the
            // pause menu all do it. Menu rather than Loading, and that distinction is
            // not cosmetic - Loading re-arms the 1.5s warmup, which would charge the
            // full wait plus the controller's fade to closing an inventory, and in
            // this game the inventory is opened constantly.
            if (_kind == CameraKind.Unknown) return GameState.Menu;

            if (!GameStateRules.IsPlayerControlled(_kind)) return GameState.Cutscene;

            if (GameReflection.IsPaused) return GameState.Menu;
            if (GameReflection.CursorOwnsInput) return GameState.Menu;
            if (GameReflection.IsUiTransitioning) return GameState.Menu;
            if (!GameReflection.IsHudActive) return GameState.Menu;

            return GameState.Gameplay;
        }
    }
}
