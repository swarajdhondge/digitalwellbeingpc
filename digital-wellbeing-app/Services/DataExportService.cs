using System;
using System.IO;
using System.Text;

namespace digital_wellbeing_app.Services
{
    /// <summary>
    /// Exports all tracked data to CSV files in a user-chosen folder.
    /// </summary>
    public static class DataExportService
    {
        /// <summary>
        /// Export all data tables as CSV files to the specified directory.
        /// Returns the number of files written.
        /// </summary>
        public static int ExportAllToCsv(string outputDirectory)
        {
            Directory.CreateDirectory(outputDirectory);
            int filesWritten = 0;

            try
            {
                ExportScreenTimePeriods(outputDirectory);
                filesWritten++;
            }
            catch (Exception ex)
            {
                LogService.Warning($"Failed to export ScreenTimePeriods: {ex.Message}");
            }

            try
            {
                ExportAppUsageSessions(outputDirectory);
                filesWritten++;
            }
            catch (Exception ex)
            {
                LogService.Warning($"Failed to export AppUsageSessions: {ex.Message}");
            }

            try
            {
                ExportSoundUsageSessions(outputDirectory);
                filesWritten++;
            }
            catch (Exception ex)
            {
                LogService.Warning($"Failed to export SoundUsageSessions: {ex.Message}");
            }

            try
            {
                ExportFocusSessions(outputDirectory);
                filesWritten++;
            }
            catch (Exception ex)
            {
                LogService.Warning($"Failed to export FocusSessions: {ex.Message}");
            }

            return filesWritten;
        }

        private static void ExportScreenTimePeriods(string dir)
        {
            var rows = DatabaseService.GetAllScreenTimePeriods();
            var path = Path.Combine(dir, "ScreenTimePeriods.csv");
            var sb = new StringBuilder();
            sb.AppendLine("Id,SessionDate,AccumulatedActiveSeconds");
            foreach (var r in rows)
            {
                sb.AppendLine($"{r.Id},{Escape(r.SessionDate)},{r.AccumulatedActiveSeconds}");
            }
            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        }

        private static void ExportAppUsageSessions(string dir)
        {
            var rows = DatabaseService.GetAllAppUsageSessions();
            var path = Path.Combine(dir, "AppUsageSessions.csv");
            var sb = new StringBuilder();
            sb.AppendLine("Id,AppName,ExecutablePath,StartTime,EndTime");
            foreach (var r in rows)
            {
                sb.AppendLine($"{r.Id},{Escape(r.AppName)},{Escape(r.ExecutablePath)},{r.StartTime:O},{r.EndTime:O}");
            }
            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        }

        private static void ExportSoundUsageSessions(string dir)
        {
            var rows = DatabaseService.GetAllSoundUsageSessions();
            var path = Path.Combine(dir, "SoundUsageSessions.csv");
            var sb = new StringBuilder();
            // StartTime..EndTime spans wall-clock time (silence included); the UI reports
            // ActualListeningDuration/HarmfulDuration instead. Export both explicitly so a naive
            // (End-Start) can't be mistaken for actual listening time.
            sb.AppendLine("Id,StartTime,EndTime,ActualListeningSeconds,HarmfulSeconds,AvgVolume,EstimatedMaxSPL,DeviceName,DeviceType,WasHarmful");
            foreach (var r in rows)
            {
                sb.AppendLine($"{r.Id},{r.StartTime:O},{r.EndTime:O},{r.ActualListeningDuration.TotalSeconds:F0},{r.HarmfulDuration.TotalSeconds:F0},{r.AvgVolume:F4},{r.EstimatedMaxSPL:F1},{Escape(r.DeviceName)},{Escape(r.DeviceType)},{r.WasHarmful}");
            }
            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        }

        private static void ExportFocusSessions(string dir)
        {
            var rows = DatabaseService.GetAllFocusSessions();
            var path = Path.Combine(dir, "FocusSessions.csv");
            var sb = new StringBuilder();
            sb.AppendLine("Id,SessionDate,StartTime,EndTime,PlannedDurationMinutes,EnforcementLevel,Completed,DistractionWarnings,DistractionOverrides");
            foreach (var r in rows)
            {
                sb.AppendLine($"{r.Id},{Escape(r.SessionDate)},{r.StartTime:O},{r.EndTime?.ToString("O") ?? ""},{r.PlannedDurationMinutes},{r.EnforcementLevel},{r.Completed},{r.DistractionWarnings},{r.DistractionOverrides}");
            }
            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        }

        /// <summary>
        /// Escape a string value for CSV (wrap in quotes if it contains comma, quote, or newline).
        /// </summary>
        private static string Escape(string? value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
            {
                return "\"" + value.Replace("\"", "\"\"") + "\"";
            }
            return value;
        }
    }
}
