using System;
using System.Threading;

namespace digital_wellbeing_app.Platform.Windows
{
    /// <summary>
    /// Minimize-and-verify retry logic shared by every feature that reacts to a "Block" enforcement
    /// level (Focus Sessions, App Limits). Extracted from FocusSessionService so the two can't drift.
    /// </summary>
    internal static class WindowEnforcement
    {
        /// <summary>
        /// Try to minimize a window, verifying it actually left the foreground before giving up.
        /// Returns false if the app resisted (admin-elevated, full-screen, some UWP surfaces, etc.) -
        /// callers that want to stop retrying a known-resistant app should track that themselves.
        /// </summary>
        internal static bool TryMinimizeWindow(IntPtr windowHandle)
        {
            try
            {
                NativeMethods.ShowWindow(windowHandle, NativeMethods.SW_MINIMIZE);
                Thread.Sleep(200);

                if (NativeMethods.GetForegroundWindow() == windowHandle)
                {
                    NativeMethods.ShowWindow(windowHandle, NativeMethods.SW_FORCEMINIMIZE);
                    Thread.Sleep(200);

                    if (NativeMethods.GetForegroundWindow() == windowHandle)
                        return false;
                }

                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
