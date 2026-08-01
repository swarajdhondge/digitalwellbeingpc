using System;
using System.IO;
using Xunit;
using digital_wellbeing_app.Services;

namespace digital_wellbeing_app.Tests.Services
{
    public class LogServiceTests
    {
        [Fact]
        public void Initialize_CreatesLogDirectory()
        {
            LogService.Initialize();

            var logDir = LogService.GetLogDirectory();
            Assert.True(Directory.Exists(logDir), $"Log directory should exist: {logDir}");
        }

        [Fact]
        public void Info_WritesToLogFile()
        {
            LogService.Initialize();
            LogService.Info("Test info message from unit test");

            var logPath = LogService.GetCurrentLogPath();
            Assert.NotNull(logPath);
            Assert.True(File.Exists(logPath), $"Log file should exist: {logPath}");

            var content = File.ReadAllText(logPath);
            Assert.Contains("Test info message from unit test", content);
        }

        [Fact]
        public void Warning_WritesToLogFile()
        {
            LogService.Initialize();
            LogService.Warning("Test warning message");

            var logPath = LogService.GetCurrentLogPath();
            Assert.NotNull(logPath);

            var content = File.ReadAllText(logPath);
            Assert.Contains("Test warning message", content);
            Assert.Contains("Warning", content);
        }

        [Fact]
        public void Error_WritesToLogFile_WithException()
        {
            LogService.Initialize();

            try
            {
                throw new InvalidOperationException("Test exception for logging");
            }
            catch (Exception ex)
            {
                LogService.Error("Test error occurred", ex);
            }

            var logPath = LogService.GetCurrentLogPath();
            Assert.NotNull(logPath);

            var content = File.ReadAllText(logPath);
            Assert.Contains("Test error occurred", content);
            Assert.Contains("Test exception for logging", content);
        }

        [Fact]
        public void GetLogDirectory_ReturnsValidPath()
        {
            var dir = LogService.GetLogDirectory();
            Assert.False(string.IsNullOrWhiteSpace(dir));
            Assert.Contains("Pulse", dir);
            Assert.Contains("logs", dir);
        }

        // --- Ring buffer (2026-07-17, backs the Settings Diagnostics card) ---
        // LogService is static and shared across the whole (serialized) test assembly, so the
        // buffer accumulates lines from other tests too - these assert containment/eviction
        // rather than an exact snapshot.

        [Fact]
        public void GetRecentLines_ContainsARecentlyWrittenLine()
        {
            LogService.Initialize();
            var marker = $"ring-buffer-marker-{Guid.NewGuid():N}";
            LogService.Info(marker);

            Assert.Contains(LogService.GetRecentLines(), line => line.Contains(marker));
        }

        [Fact]
        public void GetRecentLines_CapsAndEvictsOldestFirst()
        {
            LogService.Initialize();
            var prefix = Guid.NewGuid().ToString("N");
            var first = $"{prefix}-first";
            var last = $"{prefix}-last-000";

            LogService.Info(first);
            for (int i = 0; i < 249; i++)
                LogService.Info($"{prefix}-filler-{i}");
            LogService.Info(last);

            var recent = LogService.GetRecentLines();
            Assert.True(recent.Count <= 200, $"Ring buffer should cap at 200, had {recent.Count}");
            Assert.DoesNotContain(recent, line => line.Contains(first));
            Assert.Contains(recent, line => line.Contains(last));
        }
    }
}
