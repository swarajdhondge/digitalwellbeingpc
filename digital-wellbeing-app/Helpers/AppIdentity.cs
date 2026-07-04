using System;
using System.IO;

namespace digital_wellbeing_app.Helpers
{
    /// <summary>
    /// Canonical identity for a tracked application.
    ///
    /// App references reach us in three shapes that must all resolve to one key:
    ///   - a process name        e.g. "chrome"        (AppUsageSession.AppName, Process.ProcessName)
    ///   - a file name           e.g. "Chrome.exe"
    ///   - a full executable path e.g. "C:\...\chrome.exe" (AppUsageSession.ExecutablePath)
    ///
    /// Historically category lookups keyed on <c>AppCategory.AppIdentifier</c> (a process name)
    /// but read code looked them up by <c>ExecutablePath</c> (a full path), so they never matched
    /// and everything showed as Uncategorized. <see cref="NormalizeKey"/> collapses all three
    /// shapes to a single lowercase, extension-less, directory-less key so writers and readers
    /// agree. It is idempotent: <c>NormalizeKey(NormalizeKey(x)) == NormalizeKey(x)</c>.
    /// </summary>
    public static class AppIdentity
    {
        /// <summary>
        /// Normalize a process name, file name, or full path to a canonical lookup key:
        /// the lowercase executable name without directory or extension.
        /// Returns <see cref="string.Empty"/> for null/blank input.
        /// </summary>
        public static string NormalizeKey(string? appNameOrPath)
        {
            if (string.IsNullOrWhiteSpace(appNameOrPath))
                return string.Empty;

            var value = appNameOrPath.Trim();

            // Strip any directory component. Path.GetFileName is safe on bare names in .NET 9.
            try
            {
                if (value.IndexOfAny(new[] { '\\', '/' }) >= 0)
                {
                    var fileName = Path.GetFileName(value);
                    if (!string.IsNullOrEmpty(fileName))
                        value = fileName;
                }
            }
            catch
            {
                // Malformed path characters — fall back to the raw value.
            }

            // Strip a trailing extension (.exe etc.), but leave leading-dot names intact.
            var dot = value.LastIndexOf('.');
            if (dot > 0)
                value = value.Substring(0, dot);

            return value.Trim().ToLowerInvariant();
        }

        /// <summary>
        /// Convenience overload: prefer the executable path when available, otherwise fall back
        /// to the app/process name. Both resolve to the same key via <see cref="NormalizeKey"/>.
        /// </summary>
        public static string NormalizeKey(string? executablePath, string? appName)
        {
            var fromPath = NormalizeKey(executablePath);
            return fromPath.Length > 0 ? fromPath : NormalizeKey(appName);
        }
    }
}
