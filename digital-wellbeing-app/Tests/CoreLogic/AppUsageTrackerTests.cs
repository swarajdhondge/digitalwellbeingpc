using System;
using System.Linq;
using Xunit;
using digital_wellbeing_app.CoreLogic;
using digital_wellbeing_app.Models;
using digital_wellbeing_app.Services;

namespace digital_wellbeing_app.Tests.CoreLogic
{
    public class AppUsageTrackerTests : TestBase
    {
        [Theory]
        [InlineData(false, 0)]  // background audio or nothing playing: the user is away
        [InlineData(true, 1)]   // the foreground app is fullscreen or playing audio: keep crediting it
        public void Away_ClosesSessionUnlessForegroundIsBeingConsumed(bool consuming, int openSessions)
        {
            var now = DateTime.Today.AddHours(10);
            var idle = TimeSpan.Zero;
            using var tracker = new AppUsageTracker
            {
                Clock = () => now, IdleTimeProvider = () => idle, ForegroundConsumingProvider = () => consuming
            };
            SetField(tracker, "_currentSession", new AppUsageSession
            {
                AppName = "explorer", ExecutablePath = @"C:\Windows\explorer.exe", StartTime = now.AddMinutes(-10)
            });
            CallPrivate(tracker, "OnTick");
            now = now.AddSeconds(1);
            idle = TimeSpan.FromSeconds(301);
            CallPrivate(tracker, "OnTick");
            Assert.Equal(openSessions, tracker.CurrentSession == null ? 0 : 1);
        }

        [Fact]
        public void Away_IgnoresFocusChangesUntilTheUserReturns()
        {
            var now = DateTime.Today.AddHours(10);
            using var tracker = new AppUsageTracker
            {
                Clock = () => now, IdleTimeProvider = () => TimeSpan.FromMinutes(10), ForegroundConsumingProvider = () => false
            };
            CallPrivate(tracker, "OnTick");
            CallPrivate(tracker, "OnAppChanged", System.Diagnostics.Process.GetCurrentProcess());
            Assert.Null(tracker.CurrentSession);
        }

        [Fact]
        public void PeriodicSave_DoesNotChangeTheDisplayedTotal()
        {
            var now = DateTime.Today.AddHours(12);
            using var tracker = new AppUsageTracker { Clock = () => now };
            SetField(tracker, "_currentSession", new AppUsageSession
            {
                AppName = "notepad", ExecutablePath = @"C:\notepad.exe", StartTime = now.AddMinutes(-5)
            });
            SetField(tracker, "_lastSaved", now.AddMinutes(-5));
            var before = tracker.GetSessionsForRange(now.Date, now.Date).Sum(s => s.Duration.TotalSeconds);
            CallPrivate(tracker, "OnPeriodicSave", null, null);
            var after = tracker.GetSessionsForRange(now.Date, now.Date).Sum(s => s.Duration.TotalSeconds);
            Assert.Equal(300, before);
            Assert.Equal(before, after);
            now = now.AddSeconds(12);
            Assert.Equal(312, tracker.GetSessionsForRange(now.Date, now.Date).Sum(s => s.Duration.TotalSeconds));
        }

        [Fact]
        public void LiveRange_ClipsBothSidesOfMidnight()
        {
            var today = DateTime.Today;
            using var tracker = new AppUsageTracker { Clock = () => today.AddSeconds(20) };
            SetField(tracker, "_currentSession", new AppUsageSession { AppName = "notepad", StartTime = today.AddSeconds(-10) });
            Assert.Equal(10, Assert.Single(tracker.GetSessionsForRange(today.AddDays(-1), today.AddDays(-1))).Duration.TotalSeconds);
            Assert.Equal(20, Assert.Single(tracker.GetSessionsForRange(today, today)).Duration.TotalSeconds);
            Assert.Empty(tracker.GetSessionsForRange(today.AddDays(1), today.AddDays(1)));
        }

        [Fact]
        public void StopAfterMidnight_KeepsUsageInEachDay()
        {
            var today = DateTime.Today;
            using var tracker = new AppUsageTracker { Clock = () => today.AddSeconds(20) };
            SetField(tracker, "_currentSession", new AppUsageSession { AppName = "notepad", StartTime = today.AddSeconds(-10) });
            tracker.Stop();
            Assert.Equal(10, DatabaseService.GetAppUsageSessionsForDate(today.AddDays(-1)).Sum(s => s.Duration.TotalSeconds));
            Assert.Equal(20, DatabaseService.GetAppUsageSessionsForDate(today).Sum(s => s.Duration.TotalSeconds));
        }
    }
}
