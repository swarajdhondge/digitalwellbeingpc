using System;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading;

namespace digital_wellbeing_app.Services
{
    /// <summary>
    /// Per-user names shared by the app and the command-line tool: the single-instance mutex
    /// (held while Pulse runs), the event that asks a running Pulse to quit, and the event a
    /// second launch uses to ask it to show its window.
    /// </summary>
    public static class PulseInstance
    {
        private static readonly string UserSid = GetCurrentUserSid();

        public static string SingleInstanceMutexName { get; } = $"DigitalWellbeingPC_SingleInstance_{UserSid}";

        public static string ExitRequestEventName { get; } = $"DigitalWellbeingPC_ExitRequest_{UserSid}";

        public static string ShowRequestEventName { get; } = $"DigitalWellbeingPC_ShowRequest_{UserSid}";

        /// <summary>
        /// Asks the running Pulse to show its window (the user opened Pulse again while it was in
        /// the notification area). Lets it take the foreground, which this launch was given.
        /// </summary>
        public static void RequestShow()
        {
            try
            {
                AllowSetForegroundWindow(AsfwAny);
                if (EventWaitHandle.TryOpenExisting(ShowRequestEventName, out var handle))
                    using (handle) handle.Set();
            }
            catch (Exception) { /* best effort: the running Pulse is still in the notification area */ }
        }

        private const int AsfwAny = -1;

        [DllImport("user32.dll")]
        private static extern bool AllowSetForegroundWindow(int processId);

        /// <summary>Whether Pulse is running for the current user.</summary>
        public static bool IsRunning()
        {
            try
            {
                if (!Mutex.TryOpenExisting(SingleInstanceMutexName, out var mutex)) return false;
                mutex.Dispose();
                return true;
            }
            catch (UnauthorizedAccessException)
            {
                return true; // exists, just not openable from here
            }
        }

        /// <summary>Asks the running Pulse to quit the same way as tray → Exit. False if it isn't listening.</summary>
        public static bool RequestExit()
        {
            try
            {
                if (!EventWaitHandle.TryOpenExisting(ExitRequestEventName, out var handle)) return false;
                using (handle) return handle.Set();
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

        private static string GetCurrentUserSid()
        {
            try
            {
                return WindowsIdentity.GetCurrent().User?.Value ?? "default";
            }
            catch
            {
                return "default";
            }
        }
    }
}
