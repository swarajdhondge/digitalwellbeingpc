using System;
using System.Runtime.InteropServices;

namespace digital_wellbeing_app.Platform.Windows
{
    internal static partial class NativeMethods
    {
        // Window show constants
        internal const int SW_HIDE = 0;
        internal const int SW_MINIMIZE = 6;
        internal const int SW_FORCEMINIMIZE = 11;

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool UnhookWinEvent(IntPtr hWinEventHook);

        [LibraryImport("user32.dll")]
        internal static partial uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool SetWinEventHook(
            uint eventMin, uint eventMax,
            IntPtr hmodWinEventProc,
            FocusChangeListener.WinEventDelegate lpfnWinEventProc,
            uint idProcess, uint idThread, uint dwFlags);

        // Focus Session methods
        [DllImport("user32.dll")]
        internal static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        // Window resize
        [DllImport("user32.dll")]
        internal static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
    }
}
