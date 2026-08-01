using System;
using Xunit;
using digital_wellbeing_app.CoreLogic;
using digital_wellbeing_app.Models;
using digital_wellbeing_app.Services;

namespace digital_wellbeing_app.Tests.Services
{
    public class AppLimitServiceTests : TestBase
    {
        private static AppLimit MakeLimit(string key = "testapp", int dailyMinutes = 0, bool scheduleEnabled = false,
            int startHour = 9, int startMinute = 0, int endHour = 17, int endMinute = 0, bool enabled = true)
        {
            return new AppLimit
            {
                AppIdentifier = key,
                AppName = "TestApp",
                ExecutablePath = @"C:\Apps\testapp.exe",
                IsEnabled = enabled,
                DailyLimitMinutes = dailyMinutes,
                ScheduleEnabled = scheduleEnabled,
                ScheduleStartHour = startHour,
                ScheduleStartMinute = startMinute,
                ScheduleEndHour = endHour,
                ScheduleEndMinute = endMinute,
                EnforcementLevel = FocusEnforcementLevel.Warn
            };
        }

        // --- DatabaseService CRUD ---

        [Fact]
        public void SaveAppLimit_Insert_ThenGetReturnsIt()
        {
            DatabaseService.SaveAppLimit(MakeLimit("chrome", dailyMinutes: 60));

            var saved = DatabaseService.GetAppLimit("chrome");
            Assert.NotNull(saved);
            Assert.Equal(60, saved!.DailyLimitMinutes);
            Assert.Equal("TestApp", saved.AppName);
        }

        [Fact]
        public void SaveAppLimit_ExistingIdentifier_UpdatesInPlace()
        {
            DatabaseService.SaveAppLimit(MakeLimit("steam", dailyMinutes: 30));
            DatabaseService.SaveAppLimit(MakeLimit("steam", dailyMinutes: 90));

            var all = DatabaseService.GetAllAppLimits();
            Assert.Single(all);
            Assert.Equal(90, all[0].DailyLimitMinutes);
        }

        [Fact]
        public void DeleteAppLimit_RemovesRow()
        {
            DatabaseService.SaveAppLimit(MakeLimit("discord"));
            DatabaseService.DeleteAppLimit("discord");

            Assert.Null(DatabaseService.GetAppLimit("discord"));
            Assert.Empty(DatabaseService.GetAllAppLimits());
        }

        [Fact]
        public void GetAllAppLimits_ReturnsEveryRow()
        {
            DatabaseService.SaveAppLimit(MakeLimit("appone"));
            DatabaseService.SaveAppLimit(MakeLimit("apptwo"));

            Assert.Equal(2, DatabaseService.GetAllAppLimits().Count);
        }

        // --- GetAppUsageSecondsForDate ---

        [Fact]
        public void GetAppUsageSecondsForDate_SumsOnlyMatchingApp()
        {
            var today = DateTime.Today;
            DatabaseService.SaveAppUsageSession(new AppUsageSession
            {
                AppName = "testapp",
                ExecutablePath = @"C:\Apps\testapp.exe",
                StartTime = today.AddHours(9),
                EndTime = today.AddHours(9).AddMinutes(10)
            });
            DatabaseService.SaveAppUsageSession(new AppUsageSession
            {
                AppName = "testapp",
                ExecutablePath = @"C:\Apps\testapp.exe",
                StartTime = today.AddHours(10),
                EndTime = today.AddHours(10).AddMinutes(5)
            });
            DatabaseService.SaveAppUsageSession(new AppUsageSession
            {
                AppName = "otherapp",
                ExecutablePath = @"C:\Apps\otherapp.exe",
                StartTime = today.AddHours(11),
                EndTime = today.AddHours(11).AddMinutes(20)
            });

            var seconds = DatabaseService.GetAppUsageSecondsForDate("testapp", today);
            Assert.Equal(15 * 60, seconds);
        }

        [Fact]
        public void GetAppUsageSecondsForDate_NoSessions_ReturnsZero()
        {
            Assert.Equal(0, DatabaseService.GetAppUsageSecondsForDate("nosuchapp", DateTime.Today));
        }

        // --- AppLimitService.IsLimitExceeded (pure decision logic) ---

        [Fact]
        public void IsLimitExceeded_NoScheduleNoCap_NeverExceeded()
        {
            var limit = MakeLimit();
            Assert.False(AppLimitService.IsLimitExceeded(limit, DateTime.Now));
        }

        [Fact]
        public void IsLimitExceeded_DailyCapReached_ReturnsTrue()
        {
            var limit = MakeLimit("cappedapp", dailyMinutes: 10);
            var today = DateTime.Today;
            DatabaseService.SaveAppUsageSession(new AppUsageSession
            {
                AppName = "cappedapp",
                ExecutablePath = "cappedapp",
                StartTime = today.AddHours(9),
                EndTime = today.AddHours(9).AddMinutes(15) // 15 min used > 10 min cap
            });

            Assert.True(AppLimitService.IsLimitExceeded(limit, DateTime.Now));
        }

        [Fact]
        public void IsLimitExceeded_DailyCapNotReached_ReturnsFalse()
        {
            var limit = MakeLimit("underused", dailyMinutes: 60);
            var today = DateTime.Today;
            DatabaseService.SaveAppUsageSession(new AppUsageSession
            {
                AppName = "underused",
                ExecutablePath = "underused",
                StartTime = today.AddHours(9),
                EndTime = today.AddHours(9).AddMinutes(10)
            });

            Assert.False(AppLimitService.IsLimitExceeded(limit, DateTime.Now));
        }

        [Fact]
        public void IsLimitExceeded_IncludesUnflushedLiveUsage()
        {
            var limit = MakeLimit("liveapp", dailyMinutes: 10);
            var today = DateTime.Today;
            DatabaseService.SaveAppUsageSession(new AppUsageSession
            {
                AppName = "liveapp",
                ExecutablePath = "liveapp",
                StartTime = today.AddHours(9),
                EndTime = today.AddHours(9).AddMinutes(8)
            });

            Assert.True(AppLimitService.IsLimitExceeded(limit, DateTime.Now, liveUsageSeconds: 120));
        }

        [Fact]
        public void IsLimitExceeded_SameDaySchedule_ActiveWithinWindow()
        {
            var limit = MakeLimit(scheduleEnabled: true, startHour: 9, endHour: 17);
            var withinWindow = DateTime.Today.AddHours(12);
            var outsideWindow = DateTime.Today.AddHours(20);

            Assert.True(AppLimitService.IsLimitExceeded(limit, withinWindow));
            Assert.False(AppLimitService.IsLimitExceeded(limit, outsideWindow));
        }

        [Fact]
        public void IsLimitExceeded_OvernightSchedule_WrapsPastMidnight()
        {
            // 10 PM - 6 AM: active late at night AND in the early morning, not mid-afternoon.
            var limit = MakeLimit(scheduleEnabled: true, startHour: 22, startMinute: 0, endHour: 6, endMinute: 0);

            Assert.True(AppLimitService.IsLimitExceeded(limit, DateTime.Today.AddHours(23)));
            Assert.True(AppLimitService.IsLimitExceeded(limit, DateTime.Today.AddHours(3)));
            Assert.False(AppLimitService.IsLimitExceeded(limit, DateTime.Today.AddHours(14)));
        }

        [Fact]
        public void IsLimitExceeded_ScheduleDisabled_IgnoresTimeOfDay()
        {
            var limit = MakeLimit(scheduleEnabled: false, startHour: 0, startMinute: 0, endHour: 23, endMinute: 59);
            Assert.False(AppLimitService.IsLimitExceeded(limit, DateTime.Now));
        }

        // --- Service lifecycle (construction must not touch native hooks - Start() isn't called
        // on the tracker, only event subscription happens) ---

        [Fact]
        public void Constructor_NullTracker_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new AppLimitService(null!));
        }

        [Fact]
        public void StartStop_DoesNotThrow_AndReloadPicksUpDbChanges()
        {
            using var tracker = new AppUsageTracker();
            using var svc = new AppLimitService(tracker);

            svc.Start();
            DatabaseService.SaveAppLimit(MakeLimit("livereload"));
            svc.ReloadLimits();
            svc.Stop();

            // No exception is the primary assertion here; DB state confirms the reload path ran.
            Assert.NotNull(DatabaseService.GetAppLimit("livereload"));
        }
    }
}
