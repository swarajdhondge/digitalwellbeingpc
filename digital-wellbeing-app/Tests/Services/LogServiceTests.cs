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
            Assert.Contains("Digital Wellbeing", dir);
            Assert.Contains("logs", dir);
        }
    }
}
