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

        /// <summary>
        /// The PATH value with <paramref name="folder"/> appended once (no duplicates). Every
        /// other entry is left exactly as it was.
        /// </summary>
        public static string WithFolder(string? path, string folder)
        {
            var current = path ?? string.Empty;
            if (current.Split(';').Any(e => SameFolder(e, folder))) return current;
            if (current.Trim().Length == 0) return Normalize(folder);
            return current.EndsWith(';') ? current + Normalize(folder) : current + ";" + Normalize(folder);
        }

        /// <summary>
        /// The PATH value without any entry for <paramref name="folder"/>. The same text when
        /// there is none, so other entries are never rewritten.
        /// </summary>
        public static string WithoutFolder(string? path, string folder)
        {
            var current = path ?? string.Empty;
            var entries = current.Split(';');
            return entries.Any(e => SameFolder(e, folder))
                ? string.Join(";", entries.Where(e => !SameFolder(e, folder)))
                : current;
        }

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

        private static string Normalize(string folder) => folder.Trim().TrimEnd('\\', '/');

        // Also matches the same folder written with %VARIABLES% or in quotes.
        private static bool SameFolder(string entry, string folder)
        {
            var expanded = Environment.ExpandEnvironmentVariables(entry.Trim().Trim('"'));
            return expanded.Length > 0
                && string.Equals(Normalize(expanded), Normalize(folder), StringComparison.OrdinalIgnoreCase);
        }

        // Tells Explorer (and terminals it starts) that environment variables changed.
        private static void BroadcastEnvironmentChange()
            => SendMessageTimeout(new IntPtr(0xFFFF), 0x001A, IntPtr.Zero, EnvironmentKey, 0x0002, 5000, out _);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint msg, IntPtr wParam, string lParam,
            uint flags, uint timeout, out IntPtr result);
    }
}
