using System;
using System.IO;

namespace digital_wellbeing_app.Services
{
    /// <summary>
    /// Simple file-based logging service for diagnostics and crash reporting.
    /// Writes to %LocalAppData%\Digital Wellbeing\logs\
    /// Auto-rotates: keeps last 7 days of logs.
    /// </summary>
    public static class LogService
    {
        private static readonly object _logLock = new();
        private static string? _logFilePath;
        private static bool _initialized;

        private const string LogFolderName = "logs";
        private const int MaxLogAgeDays = 7;

        public enum LogLevel
        {
            Info,
            Warning,
            Error
        }

        /// <summary>
        /// Initialize the log service. Call once at app startup.
        /// </summary>
        public static void Initialize()
        {
            if (_initialized) return;

            try
            {
                var appDataFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Digital Wellbeing");
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

            try
            {
                var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {message}";

                lock (_logLock)
                {
                    File.AppendAllText(_logFilePath, line + Environment.NewLine);
                }
            }
            catch
            {
                // Logging should never crash the app
            }
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
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Digital Wellbeing",
                LogFolderName);
        }
    }
}
