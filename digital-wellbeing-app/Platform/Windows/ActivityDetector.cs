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
        #region Audio Detection

        // Windows Core Audio API interfaces
        [ComImport]
        [Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
        private class MMDeviceEnumerator { }

        [ComImport]
        [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDeviceEnumerator
        {
            int NotImpl1();
            [PreserveSig]
            int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice ppDevice);
        }

        [ComImport]
        [Guid("D666063F-1587-4E43-81F1-B948E807363F")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDevice
        {
            [PreserveSig]
            int Activate(ref Guid iid, int dwClsCtx, IntPtr pActivationParams, out IAudioMeterInformation ppInterface);
        }

        [ComImport]
        [Guid("C02216F6-8C67-4B5B-9D00-D008E73E0064")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioMeterInformation
        {
            [PreserveSig]
            int GetPeakValue(out float pfPeak);
        }

        private static readonly Guid IID_IAudioMeterInformation = new("C02216F6-8C67-4B5B-9D00-D008E73E0064");

        /// <summary>
        /// Checks if system audio is currently playing (peak level > threshold).
        /// Execution time: ~0.2ms
        /// </summary>
        /// <param name="threshold">Audio level threshold (0.0 to 1.0). Default 0.01 (1%)</param>
        /// <returns>True if audio is playing above threshold</returns>
        public static bool IsSystemAudioPlaying(float threshold = 0.01f)
        {
            try
            {
                var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumerator();
                
                // eRender = 0, eMultimedia = 1
                int hr = enumerator.GetDefaultAudioEndpoint(0, 1, out IMMDevice device);
                if (hr != 0 || device == null)
                    return false;

                Guid iid = IID_IAudioMeterInformation;
                hr = device.Activate(ref iid, 1, IntPtr.Zero, out IAudioMeterInformation meter);
                if (hr != 0 || meter == null)
                {
                    Marshal.ReleaseComObject(device);
                    return false;
                }

                hr = meter.GetPeakValue(out float peak);
                
                Marshal.ReleaseComObject(meter);
                Marshal.ReleaseComObject(device);
                Marshal.ReleaseComObject(enumerator);

                return hr == 0 && peak > threshold;
            }
            catch
            {
                // Fail gracefully - assume no audio if detection fails
                return false;
            }
        }

        #endregion

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
            return IsSystemAudioPlaying() || IsFullscreenAppActive();
        }

        #endregion
    }
}

