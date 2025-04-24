using System;
using System.Runtime.InteropServices;

namespace digital_wellbeing_app.Platform.Windows
{
    internal static class WindowsIdleTimeHelper
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct LASTINPUTINFO
        {
            public uint cbSize;
            public uint dwTime;
        }

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

        public static TimeSpan GetIdleTime()
        {
            LASTINPUTINFO lastInputInfo = new()
            {
                cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>()
            };

            GetLastInputInfo(ref lastInputInfo);

            uint idleTimeMs = (uint)Environment.TickCount - lastInputInfo.dwTime;
            return TimeSpan.FromMilliseconds(idleTimeMs);
        }

        public static bool IsUserIdle(int thresholdInSeconds)
        {
            TimeSpan idleTime = GetIdleTime();
            return idleTime.TotalSeconds > thresholdInSeconds;
        }
    }
}
