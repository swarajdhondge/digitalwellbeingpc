using System;
using Xunit;
using digital_wellbeing_app.Services;

namespace digital_wellbeing_app.Tests.Services
{
    public class GoalServiceTests
    {
        [Fact]
        public void SetAndGetDailyGoal_RoundTrips()
        {
            var svc = new GoalService();
            svc.SetDailyScreenTimeGoal(120); // 2 hours

            svc.InvalidateCache();
            var goal = svc.GetDailyScreenTimeGoal();
            Assert.Equal(120, goal);

            // Cleanup
            svc.SetDailyScreenTimeGoal(null);
        }

        [Fact]
        public void ClearGoal_ReturnsNull()
        {
            var svc = new GoalService();
            svc.SetDailyScreenTimeGoal(60);
            svc.SetDailyScreenTimeGoal(null);

            svc.InvalidateCache();
            Assert.Null(svc.GetDailyScreenTimeGoal());
        }

        [Fact]
        public void SetGoal_ClampsToBounds()
        {
            var svc = new GoalService();

            // Values should be clamped to 1-1440
            svc.SetDailyScreenTimeGoal(0);
            svc.InvalidateCache();
            var goal = svc.GetDailyScreenTimeGoal();
            Assert.True(goal == null || goal >= 1,
                $"Goal should be null or >= 1, got {goal}");

            svc.SetDailyScreenTimeGoal(2000);
            svc.InvalidateCache();
            goal = svc.GetDailyScreenTimeGoal();
            Assert.True(goal <= 1440, $"Goal should be <= 1440, got {goal}");

            // Cleanup
            svc.SetDailyScreenTimeGoal(null);
        }

        [Fact]
        public void GetGoalProgress_ReturnsCorrectRatio()
        {
            var svc = new GoalService();
            svc.SetDailyScreenTimeGoal(60); // 60 minutes

            // 30 minutes = 50%
            var progress = svc.GetGoalProgress(TimeSpan.FromMinutes(30));
            Assert.Equal(0.5, progress, 2);

            // 60 minutes = 100%
            progress = svc.GetGoalProgress(TimeSpan.FromMinutes(60));
            Assert.Equal(1.0, progress, 2);

            // 90 minutes = 150%
            progress = svc.GetGoalProgress(TimeSpan.FromMinutes(90));
            Assert.Equal(1.5, progress, 2);

            // Cleanup
            svc.SetDailyScreenTimeGoal(null);
        }

        [Fact]
        public void IsOverGoal_ReturnsTrueWhenExceeded()
        {
            var svc = new GoalService();
            svc.SetDailyScreenTimeGoal(60);

            Assert.False(svc.IsOverGoal(TimeSpan.FromMinutes(30)));
            Assert.False(svc.IsOverGoal(TimeSpan.FromMinutes(59)));
            Assert.True(svc.IsOverGoal(TimeSpan.FromMinutes(61)));

            // Cleanup
            svc.SetDailyScreenTimeGoal(null);
        }

        [Fact]
        public void GetTimeRemaining_ReturnsCorrectValue()
        {
            var svc = new GoalService();
            svc.SetDailyScreenTimeGoal(60);

            var remaining = svc.GetTimeRemaining(TimeSpan.FromMinutes(30));
            Assert.NotNull(remaining);
            Assert.Equal(30, remaining.Value.TotalMinutes, 1);

            // Over goal returns negative
            remaining = svc.GetTimeRemaining(TimeSpan.FromMinutes(90));
            Assert.NotNull(remaining);
            Assert.True(remaining.Value.TotalMinutes < 0);

            // Cleanup
            svc.SetDailyScreenTimeGoal(null);
        }

        [Fact]
        public void NoGoal_ProgressIsZero()
        {
            var svc = new GoalService();
            svc.SetDailyScreenTimeGoal(null);
            svc.InvalidateCache();

            Assert.Equal(0.0, svc.GetGoalProgress(TimeSpan.FromMinutes(30)));
            Assert.False(svc.IsOverGoal(TimeSpan.FromMinutes(30)));
            Assert.Null(svc.GetTimeRemaining(TimeSpan.FromMinutes(30)));
        }

        private static readonly DateTime Noon = new(2026, 11, 24, 12, 0, 0);

        [Fact]
        public void Reminder_SwitchOff_IsNeverDue()
        {
            Assert.False(GoalService.IsReminderDue(false, 0, null, Noon));
            Assert.False(GoalService.IsReminderDue(false, 15, Noon.AddHours(-5), Noon));
        }

        [Fact]
        public void Reminder_FirstTimeToday_IsDue()
        {
            Assert.True(GoalService.IsReminderDue(true, 0, null, Noon));
        }

        [Fact]
        public void Reminder_OnceADay_DoesNotRepeatSameDay()
        {
            Assert.False(GoalService.IsReminderDue(true, 0, Noon.AddHours(-3), Noon));
        }

        [Fact]
        public void Reminder_RepeatsAfterChosenInterval()
        {
            Assert.False(GoalService.IsReminderDue(true, 30, Noon.AddMinutes(-29), Noon));
            Assert.True(GoalService.IsReminderDue(true, 30, Noon.AddMinutes(-30), Noon));
        }

        [Fact]
        public void Reminder_NewDay_IsDueAgain()
        {
            var lateYesterday = new DateTime(2026, 11, 23, 23, 55, 0);
            var justAfterMidnight = new DateTime(2026, 11, 24, 0, 5, 0);
            Assert.True(GoalService.IsReminderDue(true, 0, lateYesterday, justAfterMidnight));
            Assert.True(GoalService.IsReminderDue(true, 60, lateYesterday, justAfterMidnight));
        }

        [Fact]
        public void ChangingGoal_ReArmsReminder()
        {
            var svc = new GoalService();
            svc.SetDailyScreenTimeGoal(60);
            svc.MarkReminded(Noon);
            Assert.NotNull(svc.LastReminder);

            svc.SetDailyScreenTimeGoal(90);
            Assert.Null(svc.LastReminder);

            // Cleanup
            svc.SetDailyScreenTimeGoal(null);
        }

        [Fact]
        public void ReminderSettings_DefaultOnceADayAndOn_ThenRoundTrip()
        {
            var svc = new GoalService();
            Assert.True(svc.GoalNotificationsEnabled);
            Assert.Equal(GoalService.DefaultRemindEveryMinutes, svc.RemindEveryMinutes);

            svc.RemindEveryMinutes = 30;
            svc.GoalNotificationsEnabled = false;
            Assert.Equal(30, svc.RemindEveryMinutes);
            Assert.False(svc.GoalNotificationsEnabled);

            // Cleanup
            svc.RemindEveryMinutes = GoalService.DefaultRemindEveryMinutes;
            svc.GoalNotificationsEnabled = true;
        }
    }
}
