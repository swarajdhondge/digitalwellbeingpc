using System;
using System.Runtime.InteropServices;

namespace digital_wellbeing_app.Platform.Windows
{
    internal static partial class NativeMethods
    {
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
    }
}
