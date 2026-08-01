using System;
using System.Runtime.InteropServices;

namespace digital_wellbeing_app.Platform.Windows
{
    /// <summary>
    /// Provides lightweight Win32 checks for audio playback and fullscreen app detection.
    /// Used by ScreenTimeTracker to determine if user is passively consuming content.
    /// </summary>
    public static class ActivityDetector
    {
        #region Fullscreen Detection

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        [DllImport("user32.dll")]
        private static extern IntPtr GetShellWindow();

        [DllImport("user32.dll")]
        private static extern IntPtr GetDesktopWindow();

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left, Top, Right, Bottom;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }

        private const uint MONITOR_DEFAULTTONEAREST = 2;

        /// <summary>
        /// Checks if a fullscreen application is currently active.
        /// Excludes desktop and shell windows.
        /// Execution time: ~0.1ms
        /// </summary>
        /// <returns>True if a fullscreen app (excluding desktop/shell) is active</returns>
        public static bool IsFullscreenAppActive()
        {
            try
            {
                IntPtr foreground = GetForegroundWindow();
                if (foreground == IntPtr.Zero)
                    return false;

                // Exclude desktop and shell windows
                IntPtr desktop = GetDesktopWindow();
                IntPtr shell = GetShellWindow();
                if (foreground == desktop || foreground == shell)
                    return false;

                // Get window rectangle
                if (!GetWindowRect(foreground, out RECT windowRect))
                    return false;

                // Get monitor info for the window's monitor
                IntPtr monitor = MonitorFromWindow(foreground, MONITOR_DEFAULTTONEAREST);
                MONITORINFO monitorInfo = new() { cbSize = Marshal.SizeOf<MONITORINFO>() };
                
                if (!GetMonitorInfo(monitor, ref monitorInfo))
                    return false;

                // Check if window covers entire monitor
                RECT monitorRect = monitorInfo.rcMonitor;
                return windowRect.Left <= monitorRect.Left &&
                       windowRect.Top <= monitorRect.Top &&
                       windowRect.Right >= monitorRect.Right &&
                       windowRect.Bottom >= monitorRect.Bottom;
            }
            catch
            {
                // Fail gracefully - assume not fullscreen if detection fails
                return false;
            }
        }

        #endregion

        #region Combined Detection

        /// <summary>
        /// Checks if user is passively consuming content (audio playing OR fullscreen app).
        /// Use this to avoid marking user as idle during video/music playback.
        /// </summary>
        /// <returns>True if user is likely watching/listening to content</returns>
        public static bool IsPassivelyConsuming()
        {
            return IsAudioCurrentlyPlaying() || IsFullscreenAppActive();
        }

        /// <summary>
        /// Consults SoundExposureManager - the app's single source of truth for "is audio
        /// playing" - instead of a second, independent Core Audio COM query. The two previously
        /// disagreed: this class checked only the default render device via its own raw interop,
        /// while SoundExposureManager (via SoundMonitoringService) already tracks device changes
        /// properly. CurrentSession is refreshed at least every 10s by SoundMonitoringService's
        /// poll, which is fresh enough for idle detection. Falls back to false (never throws) when
        /// no App/SoundExposureMgr is available, e.g. unit tests or design-time.
        /// </summary>
        private static bool IsAudioCurrentlyPlaying()
        {
            return (System.Windows.Application.Current as App)?.SoundExposureMgr?.CurrentSession != null;
        }

        #endregion
    }
}

