using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace digital_wellbeing_app.Services
{
    /// <summary>
    /// Simple file-based logging service for diagnostics and crash reporting.
    /// Writes to %LocalAppData%\Pulse\logs\
    /// Auto-rotates: keeps last 7 days of logs.
    /// </summary>
    public static class LogService
    {
        private static readonly object _logLock = new();
        private static string? _logFilePath;
        private static bool _initialized;

        private const string LogFolderName = "logs";
        private const int MaxLogAgeDays = 7;

        // In-memory tail of recent lines, so the Settings Diagnostics card can show recent
        // activity without re-reading the log file from disk on every open.
        private const int RingBufferSize = 200;
        private static readonly object _ringLock = new();
        private static readonly Queue<string> _recentLines = new();

        /// <summary>
        /// Optional override for the Pulse app-data folder. Used by the test suite to isolate
        /// tests from the user's real log file; null in normal operation. Mirrors
        /// SettingsService.FolderOverride/DatabaseService.SetDatabasePathForTesting - before this,
        /// LogService was the only one of the three persistence mechanisms without test isolation,
        /// so every test run (including ones that deliberately trigger Warning/Error paths, e.g.
        /// DatabaseService's write-side validation rejections) wrote into the user's real log file.
        /// </summary>
        public static string? FolderOverride;

        public enum LogLevel
        {
            Info,
            Warning,
            Error
        }

        /// <summary>
        /// Reset initialization so the next Initialize() call picks up a new FolderOverride. Call
        /// from test setup before Initialize() (or before any Info/Warning/Error call, which
        /// no-ops silently if never initialized in production but would otherwise keep pointing at
        /// a previous test run's now-stale folder here).
        /// </summary>
        public static void ResetForTesting()
        {
            lock (_logLock)
            {
                _initialized = false;
                _logFilePath = null;
            }
        }

        /// <summary>
        /// Initialize the log service. Call once at app startup.
        /// </summary>
        public static void Initialize()
        {
            if (_initialized) return;

            try
            {
                var appDataFolder = FolderOverride ?? Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Pulse");
                var logFolder = Path.Combine(appDataFolder, LogFolderName);
                Directory.CreateDirectory(logFolder);

                // Log file per day
                var fileName = $"digitalwellbeing_{DateTime.Now:yyyy-MM-dd}.log";
                _logFilePath = Path.Combine(logFolder, fileName);

                _initialized = true;

                // Clean up old logs
                PurgeOldLogs(logFolder);

                Info("LogService initialized");
            }
            catch
            {
                // Logging should never crash the app
            }
        }

        public static void Info(string message)
        {
            WriteLog(LogLevel.Info, message);
        }

        public static void Warning(string message)
        {
            WriteLog(LogLevel.Warning, message);
        }

        public static void Error(string message, Exception? ex = null)
        {
            var fullMessage = ex != null ? $"{message}: {ex}" : message;
            WriteLog(LogLevel.Error, fullMessage);
        }

        private static void WriteLog(LogLevel level, string message)
        {
            if (!_initialized || _logFilePath == null) return;

            var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {message}";

            try
            {
                lock (_logLock)
                {
                    File.AppendAllText(_logFilePath, line + Environment.NewLine);
                }
            }
            catch
            {
                // Logging should never crash the app
            }

            lock (_ringLock)
            {
                _recentLines.Enqueue(line);
                while (_recentLines.Count > RingBufferSize)
                    _recentLines.Dequeue();
            }
        }

        /// <summary>Most recent log lines (oldest first), up to RingBufferSize - for the Settings
        /// Diagnostics card. In-memory only; resets on app restart, unlike the log file itself.</summary>
        public static IReadOnlyList<string> GetRecentLines()
        {
            lock (_ringLock) { return _recentLines.ToArray(); }
        }

        /// <summary>
        /// Delete log files older than MaxLogAgeDays
        /// </summary>
        private static void PurgeOldLogs(string logFolder)
        {
            try
            {
                var cutoff = DateTime.Now.AddDays(-MaxLogAgeDays);
                foreach (var file in Directory.GetFiles(logFolder, "digitalwellbeing_*.log"))
                {
                    var fileInfo = new FileInfo(file);
                    if (fileInfo.LastWriteTime < cutoff)
                    {
                        fileInfo.Delete();
                    }
                }
            }
            catch
            {
                // Non-critical - don't crash if cleanup fails
            }
        }

        /// <summary>
        /// Get the path to the current log file (for display in settings/about)
        /// </summary>
        public static string? GetCurrentLogPath() => _logFilePath;

        /// <summary>
        /// Get the log directory path
        /// </summary>
        public static string GetLogDirectory()
        {
            var appDataFolder = FolderOverride ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Pulse");
            return Path.Combine(appDataFolder, LogFolderName);
        }
    }
}
