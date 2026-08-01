using System;
using System.IO;
using System.IO.Compression;
using Xunit;
using digital_wellbeing_app.Models;
using digital_wellbeing_app.Services;

namespace digital_wellbeing_app.Tests.Services
{
    /// <summary>
    /// TestBase's isolated per-test DB/settings path means CreateBackup/RestoreBackup here only
    /// ever touch a throwaway scratch file, never the user's real database.
    /// </summary>
    public class BackupServiceTests : TestBase
    {
        private static string NewScratchDir() =>
            Path.Combine(Path.GetTempPath(), "pulse-backup-test", Guid.NewGuid().ToString("N"));

        [Fact]
        public void CreateBackup_ProducesAZipFileContainingTheDatabase()
        {
            var dir = NewScratchDir();
            try
            {
                var zipPath = BackupService.CreateBackup(dir);

                Assert.True(File.Exists(zipPath));
                Assert.EndsWith(".zip", zipPath);

                using var archive = ZipFile.OpenRead(zipPath);
                Assert.NotNull(archive.GetEntry("digital_wellbeing.db"));
                Assert.NotNull(archive.GetEntry("manifest.json"));
            }
            finally { Directory.Delete(dir, true); }
        }

        [Fact]
        public void CreateBackup_ThenRestoreBackup_DataRoundTrips()
        {
            DatabaseService.SaveAppCategory(new AppCategory
            {
                AppIdentifier = "backuptest",
                AppName = "BackupTest",
                Category = AppCategoryType.Work
            });

            var dir = NewScratchDir();
            try
            {
                var zipPath = BackupService.CreateBackup(dir);

                // Mutate live data after the backup was taken.
                DatabaseService.SaveAppCategory(new AppCategory
                {
                    AppIdentifier = "backuptest",
                    AppName = "BackupTest",
                    Category = AppCategoryType.Entertainment
                });
                Assert.Equal(AppCategoryType.Entertainment, DatabaseService.GetAppCategory("backuptest")!.Category);

                BackupService.RestoreBackup(zipPath);

                var restored = DatabaseService.GetAppCategory("backuptest");
                Assert.NotNull(restored);
                Assert.Equal(AppCategoryType.Work, restored!.Category);
            }
            finally { Directory.Delete(dir, true); }
        }

        [Fact]
        public void CreateBackup_IncludesSettingsJsonWhenPresent()
        {
            // Force settings.json to exist by saving a setting through it.
            new SettingsService().SaveFirstRunCompleted(true);

            var dir = NewScratchDir();
            try
            {
                var zipPath = BackupService.CreateBackup(dir);

                using var archive = ZipFile.OpenRead(zipPath);
                Assert.NotNull(archive.GetEntry("settings.json"));
            }
            finally { Directory.Delete(dir, true); }
        }

        [Fact]
        public void ReadManifest_ReturnsCreationTimestamp()
        {
            var dir = NewScratchDir();
            try
            {
                var before = DateTime.UtcNow.AddSeconds(-2);
                var zipPath = BackupService.CreateBackup(dir);
                var after = DateTime.UtcNow.AddSeconds(2);

                var manifest = BackupService.ReadManifest(zipPath);

                Assert.NotNull(manifest);
                Assert.InRange(manifest!.CreatedUtc, before, after);
            }
            finally { Directory.Delete(dir, true); }
        }

        [Fact]
        public void ReadManifest_NonBackupFile_ReturnsNull()
        {
            var dir = NewScratchDir();
            Directory.CreateDirectory(dir);
            var notABackup = Path.Combine(dir, "not-a-backup.zip");
            try
            {
                ZipFile.Open(notABackup, ZipArchiveMode.Create).Dispose();

                Assert.Null(BackupService.ReadManifest(notABackup));
            }
            finally { Directory.Delete(dir, true); }
        }

        [Fact]
        public void RestoreBackup_MissingFile_Throws()
        {
            Assert.Throws<FileNotFoundException>(() =>
                BackupService.RestoreBackup(Path.Combine(Path.GetTempPath(), $"nonexistent-{Guid.NewGuid():N}.zip")));
        }

        [Fact]
        public void RestoreBackup_ArchiveMissingDatabase_ThrowsInvalidData()
        {
            var dir = NewScratchDir();
            Directory.CreateDirectory(dir);
            var badZip = Path.Combine(dir, "bad-backup.zip");
            try
            {
                using (var archive = ZipFile.Open(badZip, ZipArchiveMode.Create))
                {
                    var entry = archive.CreateEntry("manifest.json");
                    using var writer = new StreamWriter(entry.Open());
                    writer.Write("{}");
                }

                Assert.Throws<InvalidDataException>(() => BackupService.RestoreBackup(badZip));
            }
            finally { Directory.Delete(dir, true); }
        }
    }
}
