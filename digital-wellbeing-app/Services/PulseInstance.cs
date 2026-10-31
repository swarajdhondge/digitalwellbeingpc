using System;
using System.Security.Principal;
using System.Threading;

namespace digital_wellbeing_app.Services
{
    /// <summary>
    /// Per-user names shared by the app and the command-line tool: the single-instance mutex
    /// (held while Pulse runs) and the event that asks a running Pulse to quit.
    /// </summary>
    public static class PulseInstance
    {
        private static readonly string UserSid = GetCurrentUserSid();

        public static string SingleInstanceMutexName { get; } = $"DigitalWellbeingPC_SingleInstance_{UserSid}";

        public static string ExitRequestEventName { get; } = $"DigitalWellbeingPC_ExitRequest_{UserSid}";

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
