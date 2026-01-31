using System;
using System.IO;
using Xunit;
using digital_wellbeing_app.Services;

namespace digital_wellbeing_app.Tests.Services
{
    public class DataExportServiceTests : IDisposable
    {
        private readonly string _testDir;

        public DataExportServiceTests()
        {
            _testDir = Path.Combine(Path.GetTempPath(), $"DWTest_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_testDir);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_testDir))
                    Directory.Delete(_testDir, true);
            }
            catch { }
        }

        [Fact]
        public void ExportAllToCsv_CreatesFiles()
        {
            var count = DataExportService.ExportAllToCsv(_testDir);

            // Should create at least some CSV files
            Assert.True(count > 0, $"Expected at least 1 file, got {count}");

            // Check CSV files exist
            var csvFiles = Directory.GetFiles(_testDir, "*.csv");
            Assert.True(csvFiles.Length > 0, "Expected CSV files in output directory");
        }

        [Fact]
        public void ExportAllToCsv_CreatesExpectedFileNames()
        {
            DataExportService.ExportAllToCsv(_testDir);

            // Should have these specific files
            Assert.True(File.Exists(Path.Combine(_testDir, "ScreenTimePeriods.csv")));
            Assert.True(File.Exists(Path.Combine(_testDir, "AppUsageSessions.csv")));
            Assert.True(File.Exists(Path.Combine(_testDir, "SoundUsageSessions.csv")));
            Assert.True(File.Exists(Path.Combine(_testDir, "FocusSessions.csv")));
        }

        [Fact]
        public void ExportedCsv_HasHeaders()
        {
            DataExportService.ExportAllToCsv(_testDir);

            // Check ScreenTimePeriods.csv has the correct header
            var screenTimePath = Path.Combine(_testDir, "ScreenTimePeriods.csv");
            if (File.Exists(screenTimePath))
            {
                var lines = File.ReadAllLines(screenTimePath);
                Assert.True(lines.Length >= 1, "CSV should have at least a header line");
                Assert.Contains("Id", lines[0]);
                Assert.Contains("SessionDate", lines[0]);
                Assert.Contains("AccumulatedActiveSeconds", lines[0]);
            }
        }

        [Fact]
        public void ExportAllToCsv_CreatesDirectoryIfMissing()
        {
            var nestedDir = Path.Combine(_testDir, "nested", "export");
            var count = DataExportService.ExportAllToCsv(nestedDir);
            Assert.True(count > 0);
            Assert.True(Directory.Exists(nestedDir));
        }
    }
}
