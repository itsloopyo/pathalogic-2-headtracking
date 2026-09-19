// SPDX-License-Identifier: MIT
// Copyright (c) 2026 itsloopyo

using System;
using System.Runtime.InteropServices;
using System.Text;
using BepInEx.Logging;
using UnityEngine;

namespace Pathologic2HeadTracking.Core
{
    /// <summary>
    /// Places the game's render window once per launch: a windowed game that
    /// opened off-centre is centred on the work area of the monitor it is already
    /// on. A fullscreen game, a window that already fills the work area, and a
    /// window the game centred itself are all left where they are.
    ///
    /// The window is found by class name rather than by process id alone. BepInEx
    /// opens a console that belongs to the same process, is visible, is unowned
    /// and is comfortably larger than any size floor, so a search that only filters
    /// on the process can return the console instead of the game - screenshots come
    /// back full of log text and keypresses go to the console. The render window is
    /// the one whose class is UnityWndClass.
    ///
    /// The work area, not the monitor bounds: centring against the full monitor
    /// puts the title bar behind a top-docked taskbar, and the window cannot then
    /// be dragged back.
    /// </summary>
    public sealed class WindowCentering
    {
        private const string UnityWindowClass = "UnityWndClass";

        private const float PollIntervalSeconds = 0.25f;

        // Generous because the polls below are frame-gated, and the frames are
        // scarce exactly when this is running: the whole settle takes about 14
        // seconds of wall time while the game loads its title screen, against the
        // 3 seconds the same poll count would cost at a playable frame rate.
        private const float TimeoutSeconds = 120f;

        // The rect has to hold still before it is worth acting on. The window is up
        // well before the engine has finished sizing and placing it, and when the
        // engine stops moving it has not been measured here, so the wait is on an
        // unchanged rect rather than on a fixed delay that would be a guess.
        private const int SettlePolls = 12;

        // A game that centres its own window rounds the odd half-pixel up where the
        // integer maths here rounds it down, so an exact comparison would move the
        // window one pixel and report that as a fix.
        private const int CentredTolerancePixels = 2;

        private const uint MONITOR_DEFAULTTONEAREST = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_NOACTIVATE = 0x0010;

        private readonly ManualLogSource _logger;
        private readonly float _deadline;

        private float _nextPollTime;
        private IntPtr _window;
        private NativeRect _previousRect;
        private bool _hasPreviousRect;
        private int _stablePolls;
        private bool _finished;

        public WindowCentering(ManualLogSource logger)
        {
            _logger = logger;
            _deadline = Time.unscaledTime + TimeoutSeconds;
        }

        /// <summary>
        /// Polls for a settled window and places it. Call every frame; it stops
        /// doing anything once it has made its one decision, so a window the player
        /// drags afterwards stays where they put it.
        /// </summary>
        public void Update()
        {
            if (_finished) return;

            float now = Time.unscaledTime;
            if (now < _nextPollTime) return;
            _nextPollTime = now + PollIntervalSeconds;

            if (now >= _deadline)
            {
                _finished = true;
                _logger.LogWarning("Window: nothing settled within " + (int)TimeoutSeconds
                                   + "s, leaving its placement alone");
                return;
            }

            // Enumerated once and then held. A handle that stops answering - the
            // engine recreating its window during startup - is dropped and found again.
            if (_window == IntPtr.Zero) _window = FindRenderWindow();
            NativeRect rect;
            if (_window == IntPtr.Zero || !GetWindowRect(_window, out rect))
            {
                _window = IntPtr.Zero;
                _hasPreviousRect = false;
                _stablePolls = 0;
                return;
            }

            if (_hasPreviousRect && SameRect(rect, _previousRect))
            {
                if (++_stablePolls < SettlePolls) return;
                _finished = true;
                Place(_window, rect);
                return;
            }

            _previousRect = rect;
            _hasPreviousRect = true;
            _stablePolls = 0;
        }

        private void Place(IntPtr window, NativeRect rect)
        {
            if (Screen.fullScreen)
            {
                _logger.LogInfo("Window: game is running fullscreen, leaving it alone");
                return;
            }

            var info = new MonitorInfo { Size = Marshal.SizeOf(typeof(MonitorInfo)) };
            if (!GetMonitorInfo(MonitorFromWindow(window, MONITOR_DEFAULTTONEAREST), ref info))
            {
                _logger.LogWarning("Window: GetMonitorInfo failed ("
                                   + Marshal.GetLastWin32Error()
                                   + "), leaving its placement alone");
                return;
            }

            int width = rect.Right - rect.Left;
            int height = rect.Bottom - rect.Top;
            int workWidth = info.Work.Right - info.Work.Left;
            int workHeight = info.Work.Bottom - info.Work.Top;

            // A window at least as large as the work area cannot be centred within
            // it, and moving it anyway would push its title bar out of reach.
            if (width >= workWidth || height >= workHeight)
            {
                _logger.LogInfo("Window: " + width + "x" + height + " fills the work area "
                                + workWidth + "x" + workHeight + ", leaving it in place");
                return;
            }

            // Either reading counts as centred. The game centres on the monitor, this
            // centres on the work area, and the two differ by half the taskbar. Moving
            // a window that is already centred trades a visible jump for nothing.
            if (IsCentredOn(rect, info.Work) || IsCentredOn(rect, info.Monitor))
            {
                _logger.LogInfo("Window: " + width + "x" + height + " at ("
                                + rect.Left + ", " + rect.Top
                                + ") is already centred, leaving it alone");
                return;
            }

            int x = CentredOrigin(info.Work.Left, workWidth, width);
            int y = CentredOrigin(info.Work.Top, workHeight, height);
            if (!SetWindowPos(window, IntPtr.Zero, x, y, 0, 0,
                              SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE))
            {
                _logger.LogWarning("Window: SetWindowPos failed ("
                                   + Marshal.GetLastWin32Error()
                                   + "), leaving it at (" + rect.Left + ", " + rect.Top + ")");
                return;
            }

            _logger.LogInfo("Window: centred the " + width + "x" + height + " window at ("
                            + x + ", " + y + "), was at (" + rect.Left + ", " + rect.Top
                            + ") on work area " + workWidth + "x" + workHeight);
        }

        /// <summary>
        /// The render window, or <c>IntPtr.Zero</c> while the engine has not brought
        /// one up yet.
        /// </summary>
        private static IntPtr FindRenderWindow()
        {
            uint processId = GetCurrentProcessId();
            IntPtr found = IntPtr.Zero;
            var className = new StringBuilder(64);

            EnumWindows((hWnd, ignored) =>
            {
                uint owner;
                GetWindowThreadProcessId(hWnd, out owner);
                if (owner != processId || !IsWindowVisible(hWnd)) return true;

                className.Length = 0;
                GetClassName(hWnd, className, className.Capacity);
                if (className.ToString() != UnityWindowClass) return true;

                found = hWnd;
                return false;
            }, IntPtr.Zero);

            return found;
        }

        private static int CentredOrigin(int areaStart, int areaExtent, int windowExtent)
        {
            return areaStart + (areaExtent - windowExtent) / 2;
        }

        private static bool IsCentredOn(NativeRect window, NativeRect area)
        {
            int dx = window.Left - CentredOrigin(
                area.Left, area.Right - area.Left, window.Right - window.Left);
            int dy = window.Top - CentredOrigin(
                area.Top, area.Bottom - area.Top, window.Bottom - window.Top);
            return Mathf.Abs(dx) <= CentredTolerancePixels
                   && Mathf.Abs(dy) <= CentredTolerancePixels;
        }

        private static bool SameRect(NativeRect a, NativeRect b)
        {
            return a.Left == b.Left && a.Top == b.Top && a.Right == b.Right && a.Bottom == b.Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MonitorInfo
        {
            public int Size;
            public NativeRect Monitor;
            public NativeRect Work;
            public uint Flags;
        }

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetClassNameW")]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder buffer, int bufferSize);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetWindowRect(IntPtr hWnd, out NativeRect rect);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hWnd, uint flags);

        [DllImport("user32.dll", SetLastError = true, EntryPoint = "GetMonitorInfoW")]
        private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr insertAfter,
                                                int x, int y, int cx, int cy, uint flags);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentProcessId();
    }
}
