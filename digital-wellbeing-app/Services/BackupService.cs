using System;
using System.IO;
using System.IO.Compression;
using System.Text.Json;

namespace digital_wellbeing_app.Services
{
    /// <summary>
    /// Manual (v1 scope cut - no scheduled/automatic backups) backup and restore of Pulse's local
    /// data: the SQLite database (via DatabaseService.BackupTo, SQLite's native online backup, not
    /// a raw file copy) plus settings.json, bundled into a single .zip with a small manifest -
    /// same "whole app-state folder" precedent DataMigrationService.MigrateDataFolder() already
    /// established for DB+logs+settings living together.
    /// </summary>
    public static class BackupService
    {
        private const string ManifestFileName = "manifest.json";
        private const string DbFileNameInBackup = "digital_wellbeing.db";
        private const string SettingsFileNameInBackup = "settings.json";

        public class BackupManifest
        {
            public string AppVersion { get; set; } = string.Empty;
            public DateTime CreatedUtc { get; set; }
        }

        /// <summary>Creates a timestamped .zip in <paramref name="destinationDirectory"/> and
        /// returns its full path.</summary>
        public static string CreateBackup(string destinationDirectory)
        {
            Directory.CreateDirectory(destinationDirectory);
            var zipPath = Path.Combine(destinationDirectory, $"pulse-backup-{DateTime.Now:yyyy-MM-dd_HHmmss}.zip");

            var stagingDir = Path.Combine(Path.GetTempPath(), "pulse-backup-staging", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(stagingDir);

            try
            {
                DatabaseService.BackupTo(Path.Combine(stagingDir, DbFileNameInBackup));

                var settingsSourcePath = GetLiveSettingsPath();
                if (File.Exists(settingsSourcePath))
                    File.Copy(settingsSourcePath, Path.Combine(stagingDir, SettingsFileNameInBackup), overwrite: true);

                var manifest = new BackupManifest
                {
                    AppVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown",
                    CreatedUtc = DateTime.UtcNow
                };
                File.WriteAllText(Path.Combine(stagingDir, ManifestFileName), JsonSerializer.Serialize(manifest));

                if (File.Exists(zipPath)) File.Delete(zipPath);
                ZipFile.CreateFromDirectory(stagingDir, zipPath);

                return zipPath;
            }
            finally
            {
                TryDeleteDirectory(stagingDir);
            }
        }

        /// <summary>
        /// Restores the database and settings from a backup .zip, overwriting the live files.
        /// Closes the live DB connection first. Requires an app restart afterward - other running
        /// services hold in-memory state derived from the pre-restore data, so this deliberately
        /// does not attempt to reopen/reload anything live; callers must restart the process.
        /// </summary>
        public static void RestoreBackup(string zipPath)
        {
            if (!File.Exists(zipPath))
                throw new FileNotFoundException("Backup file not found.", zipPath);

            var stagingDir = Path.Combine(Path.GetTempPath(), "pulse-restore-staging", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(stagingDir);

            try
            {
                ZipFile.ExtractToDirectory(zipPath, stagingDir, overwriteFiles: true);

                var extractedDb = Path.Combine(stagingDir, DbFileNameInBackup);
                if (!File.Exists(extractedDb))
                    throw new InvalidDataException("Backup archive is missing the database file.");

                // Close before touching the live file - CloseConnection() is safe to call even if
                // no connection is currently open.
                DatabaseService.CloseConnection();

                File.Copy(extractedDb, DatabaseService.GetDatabaseFilePath(), overwrite: true);

                var extractedSettings = Path.Combine(stagingDir, SettingsFileNameInBackup);
                if (File.Exists(extractedSettings))
                    File.Copy(extractedSettings, GetLiveSettingsPath(), overwrite: true);
            }
            finally
            {
                TryDeleteDirectory(stagingDir);
            }
        }

        /// <summary>Reads just the manifest from a backup .zip, for display before restoring (e.g.
        /// "Backup from 2026-07-10" in a confirmation prompt). Returns null if unreadable.</summary>
        public static BackupManifest? ReadManifest(string zipPath)
        {
            try
            {
                using var archive = ZipFile.OpenRead(zipPath);
                var entry = archive.GetEntry(ManifestFileName);
                if (entry == null) return null;

                using var stream = entry.Open();
                return JsonSerializer.Deserialize<BackupManifest>(stream);
            }
            catch
            {
                return null;
            }
        }

        private static string GetLiveSettingsPath()
        {
            var dbFolder = Path.GetDirectoryName(DatabaseService.GetDatabaseFilePath())!;
            return Path.Combine(dbFolder, "settings.json");
        }

        private static void TryDeleteDirectory(string path)
        {
            try { Directory.Delete(path, recursive: true); } catch { /* best-effort cleanup */ }
        }
    }
}
