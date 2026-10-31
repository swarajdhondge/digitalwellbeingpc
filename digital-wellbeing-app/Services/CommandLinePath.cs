using System;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace digital_wellbeing_app.Services
{
    /// <summary>
    /// Keeps the folder holding pulse.exe on the current user's PATH for the Setup.exe
    /// (Velopack) install, so `pulse` works in new terminals. Called from the install, update
    /// and uninstall hooks. The Store package uses an app execution alias instead.
    /// </summary>
    public static class CommandLinePath
    {
        private const string EnvironmentKey = "Environment";

        public static void Add(string folder) => Update(path => WithFolder(path, folder));

        public static void Remove(string folder) => Update(path => WithoutFolder(path, folder));

        /// <summary>The PATH value with <paramref name="folder"/> appended once (no duplicates).</summary>
        public static string WithFolder(string? path, string folder)
        {
            var entries = Split(path);
            return entries.Any(e => SameFolder(e, folder))
                ? path ?? string.Empty
                : string.Join(";", entries.Append(Normalize(folder)));
        }

        /// <summary>The PATH value without any entry for <paramref name="folder"/>.</summary>
        public static string WithoutFolder(string? path, string folder)
            => string.Join(";", Split(path).Where(e => !SameFolder(e, folder)));

        private static void Update(Func<string?, string> change)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(EnvironmentKey, writable: true);
                if (key == null) return;

                // Keep %VARIABLES% unexpanded and keep the value's type (usually REG_EXPAND_SZ).
                var current = key.GetValue("Path", null, RegistryValueOptions.DoNotExpandEnvironmentNames) as string;
                var kind = current == null ? RegistryValueKind.ExpandString : key.GetValueKind("Path");
                var updated = change(current);
                if (string.Equals(updated, current ?? string.Empty, StringComparison.Ordinal)) return;

                key.SetValue("Path", updated, kind);
                BroadcastEnvironmentChange();
            }
            catch (Exception ex)
            {
                LogService.Warning($"Could not update PATH for the command-line tool: {ex.Message}");
            }
        }

        private static string[] Split(string? path)
            => (path ?? string.Empty).Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        private static string Normalize(string folder) => folder.Trim().TrimEnd('\\', '/');

        private static bool SameFolder(string entry, string folder)
            => string.Equals(Normalize(entry), Normalize(folder), StringComparison.OrdinalIgnoreCase);

        // Tells Explorer (and terminals it starts) that environment variables changed.
        private static void BroadcastEnvironmentChange()
            => SendMessageTimeout(new IntPtr(0xFFFF), 0x001A, IntPtr.Zero, EnvironmentKey, 0x0002, 5000, out _);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint msg, IntPtr wParam, string lParam,
            uint flags, uint timeout, out IntPtr result);
    }
}
