using System;
using System.Reflection;
using System.Threading;
using System.Linq;
using digital_wellbeing_app.Models;
using digital_wellbeing_app.Services;
using Xunit;
using digital_wellbeing_app.CoreLogic;

namespace digital_wellbeing_app.Tests.CoreLogic
{
    public class ScreenTimeTrackerTests : TestBase
    {
        [Fact]
        public void ForgetToday_StartsFromZeroAndKeepsSaving()
        {
            DatabaseService.DeleteAllData();
            var key = DateTime.Now.ToString("yyyy-MM-dd");
            DatabaseService.SaveScreenTimePeriod(new ScreenTimePeriod
            {
                SessionDate = key, SessionStartTime = DateTime.Today.ToString("o"),
                LastRecordedTime = DateTime.Now.ToString("o"), AccumulatedActiveSeconds = 3600
            });
            var tracker = new ScreenTimeTracker();
            Assert.True(tracker.CurrentActiveTime.TotalMinutes >= 59);

            // the user deletes today: the record is gone, then the tracker starts over
            DatabaseService.DeleteDataInRange(DateTime.Today, DateTime.Today);
            tracker.ForgetToday();

            Assert.True(tracker.CurrentActiveTime.TotalSeconds < 5);
            var row = DatabaseService.GetConnection().Table<ScreenTimePeriod>().FirstOrDefault(x => x.SessionDate == key);
            Assert.NotNull(row);
            Assert.Equal(0, row!.AccumulatedActiveSeconds);
        }

        [Fact]
        public void State_ReflectsCurrentTracking()
        {
            var tracker = new ScreenTimeTracker();

            // Before start, state should not be Paused
            tracker.Start();

            // After start, state should be Active or Idle (depending on user activity)
            var state = tracker.State;
            Assert.True(state == TrackingState.Active || state == TrackingState.Idle,
                $"After Start(), state should be Active or Idle, got {state}");

            tracker.Pause();
            Assert.Equal(TrackingState.Paused, tracker.State);

            tracker.Resume();
            Assert.NotEqual(TrackingState.Paused, tracker.State);

            tracker.Stop();
        }

        [Fact]
        public void ReturningToPreviouslyRecordedDate_PreservesItsTotal()
        {
            var day = DateTime.Today;
            DatabaseService.SaveScreenTimePeriod(new ScreenTimePeriod
            {
                SessionDate = day.ToString("yyyy-MM-dd"), AccumulatedActiveSeconds = 3600,
                SessionStartTime = day.AddHours(9).ToString("o")
            });
            var now = day.AddHours(12);
            using var tracker = new ScreenTimeTracker { Clock = () => now };
            now = day.AddDays(1).AddHours(1);
            tracker.HandleTimeChanged();
            now = day.AddHours(13);
            tracker.HandleTimeChanged();
            Assert.Equal(TimeSpan.FromHours(1), tracker.CurrentActiveTime);
            Assert.Equal(3600, DatabaseService.GetScreenTimePeriodsForRange(day, day).Single().AccumulatedActiveSeconds);
        }

        [Fact]
        public void IdleThresholdBeforeMidnight_DoesNotCreditTheIdleGap()
        {
            var day = DateTime.Today;
            var now = day.AddHours(23).AddMinutes(59).AddSeconds(50);
            var idle = TimeSpan.FromSeconds(300);
            using var tracker = new ScreenTimeTracker
            {
                Clock = () => now, IdleTimeProvider = () => idle, PassiveConsumptionProvider = () => false
            };
            CallPrivate(tracker, "CheckActivity", null, null);
            var before = tracker.CurrentActiveTime.TotalSeconds;
            now = day.AddDays(1).AddSeconds(20);
            idle = TimeSpan.FromSeconds(330);
            CallPrivate(tracker, "CheckActivity", null, null);
            var prior = DatabaseService.GetScreenTimePeriodsForRange(day, day).Single();
            Assert.Equal((int)before, prior.AccumulatedActiveSeconds);
            Assert.Equal(TimeSpan.Zero, tracker.CurrentActiveTime);
        }

        [Fact]
        public void DelayedTimer_CountsElapsedTimeInsteadOfCallbacks()
        {
            var now = DateTime.Today.AddHours(10);
            using var tracker = new ScreenTimeTracker { Clock = () => now, IdleTimeProvider = () => TimeSpan.Zero };
            CallPrivate(tracker, "CheckActivity", null, null);
            var before = tracker.CurrentActiveTime;
            now = now.AddSeconds(10);
            CallPrivate(tracker, "CheckActivity", null, null);
            Assert.Equal(TimeSpan.FromSeconds(10), tracker.CurrentActiveTime - before);
            tracker.Pause();
            now = now.AddHours(1);
            before = tracker.CurrentActiveTime;
            CallPrivate(tracker, "CheckActivity", null, null);
            Assert.Equal(before, tracker.CurrentActiveTime);
        }

        [Theory]
        [InlineData(false, 10)]
        [InlineData(true, 20)]
        public void IdleTransition_StopsAtThresholdUnlessMediaIsPlaying(bool media, int expectedSeconds)
        {
            var now = DateTime.Today.AddHours(10);
            var idle = TimeSpan.Zero;
            using var tracker = new ScreenTimeTracker
            {
                Clock = () => now, IdleTimeProvider = () => idle, PassiveConsumptionProvider = () => media
            };
            CallPrivate(tracker, "CheckActivity", null, null);
            var before = tracker.CurrentActiveTime;
            now = now.AddSeconds(20);
            idle = TimeSpan.FromSeconds(310);
            CallPrivate(tracker, "CheckActivity", null, null);
            Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), tracker.CurrentActiveTime - before);
            Assert.Equal(media ? TrackingState.Active : TrackingState.Idle, tracker.State);
        }

        [Fact]
        public void Start_Accumulates_Time()
        {
            // Arrange
            var tracker = new ScreenTimeTracker();
            // Force the "active" path regardless of real machine input, so accumulation is
            // deterministic on idle dev machines and headless CI.
            tracker.IdleTimeProvider = () => TimeSpan.Zero;
            var beforeStartTime = tracker.CurrentActiveTime; // likely zero on fresh day

            // Act
            tracker.Start();
            // Simulate user activity for 2 seconds
            Thread.Sleep(2000);
            tracker.Stop();
            var afterStopTime = tracker.CurrentActiveTime;

            // Assert
            Assert.True(afterStopTime > beforeStartTime, "Active time should increase after Start/Stop.");
        }

        [Fact]
        public void ScreenTracker_HandleTimeChanged_PreservesDayTotal_AndRestartsSegment()
        {
            var tracker = new ScreenTimeTracker { IdleTimeProvider = () => TimeSpan.Zero };
            tracker.Start();
            Thread.Sleep(1100); // accumulate ~1s of active time
            var before = tracker.CurrentActiveTime;

            tracker.HandleTimeChanged();

            var after = tracker.CurrentActiveTime;
            tracker.Stop();

            Assert.True(after >= before, "Day total must not be reset by a clock change.");
            Assert.NotNull(tracker.CurrentSessionStart); // a fresh segment was started
        }

        [Fact]
        public void ScreenTime_ForwardClockJump_CreditsOneTickNotTheJump()
        {
            var now = DateTime.Today.AddHours(10);
            using var tracker = new ScreenTimeTracker
            {
                Clock = () => now, IdleTimeProvider = () => TimeSpan.Zero, PassiveConsumptionProvider = () => false
            };
            tracker.Start();
            var before = tracker.CurrentActiveTime;
            now = now.AddHours(1).AddSeconds(1); // DST spring-forward / time-zone change
            CallPrivate(tracker, "CheckActivity", null, null);
            Assert.Equal(TimeSpan.FromSeconds(1), tracker.CurrentActiveTime - before);
            tracker.Stop();
        }

        [Fact]
        public void ScreenTimeTracker_SimulatedSevenDayRun_RolloverAtEachDayBoundary_NoCorruption()
        {
            var simulatedNow = new DateTime(2026, 1, 1, 8, 0, 0);
            var tracker = new ScreenTimeTracker
            {
                Clock = () => simulatedNow,
                IdleTimeProvider = () => TimeSpan.Zero // always "active" - deterministic, no dependency on real machine idle state
            };
            tracker.Start();

            var exception = Record.Exception(() =>
            {
                for (int day = 0; day < 7; day++)
                {
                    for (int tick = 0; tick < 5; tick++)
                    {
                        CallPrivate(tracker, "CheckActivity", null, null);
                        simulatedNow = simulatedNow.AddHours(4);
                    }
                }
            });

            Assert.Null(exception);
            // After 7 simulated days, the tracker's own bookkeeping must agree with the final
            // simulated date, not the real one.
            Assert.Equal(simulatedNow.Date, tracker.SessionStartTime.Date);

            var periods = DatabaseService.GetScreenTimePeriodsForRange(new DateTime(2026, 1, 1), simulatedNow.Date);
            var distinctDays = periods.Select(p => p.SessionDate).Distinct().Count();
            Assert.True(distinctDays >= 2, $"Expected multiple distinct day rows across a 7-day simulation, got {distinctDays}");

            tracker.Stop();
            tracker.Dispose();
        }

        /// <summary>
        /// Focused regression test for the bug the test above found: ScreenTimePeriod's PK is an
        /// AutoIncrement Id, defaulted to 0 on every freshly-constructed row inside
        /// CheckDayRollover - a bare InsertOrReplace(entry) therefore *replaced* the same Id=0 row
        /// on every rollover after the first, instead of inserting one row per day. Only
        /// manifested on a process that stayed running across multiple real midnights without
        /// restarting (LoadSessionData's own startup path uses a plain Insert, which doesn't have
        /// this problem - so a daily restart habit would have masked it entirely).
        /// </summary>
        [Fact]
        public void ScreenTimeTracker_CheckDayRollover_AcrossThreeDays_CreatesThreeDistinctRows()
        {
            var simulatedNow = new DateTime(2026, 2, 1, 12, 0, 0);
            var tracker = new ScreenTimeTracker { Clock = () => simulatedNow };
            var rolloverMethod = typeof(ScreenTimeTracker).GetMethod("CheckDayRollover", BindingFlags.NonPublic | BindingFlags.Instance)!;

            rolloverMethod.Invoke(tracker, null); // day 1 (real construction date -> Feb 1)
            simulatedNow = simulatedNow.AddDays(1);
            rolloverMethod.Invoke(tracker, null); // day 2
            simulatedNow = simulatedNow.AddDays(1);
            rolloverMethod.Invoke(tracker, null); // day 3

            var periods = DatabaseService.GetScreenTimePeriodsForRange(new DateTime(2026, 2, 1), new DateTime(2026, 2, 3));
            var distinctIds = periods.Select(p => p.Id).Distinct().Count();
            var distinctDates = periods.Select(p => p.SessionDate).Distinct().Count();

            Assert.Equal(3, distinctDates);
            Assert.Equal(3, distinctIds); // each day must be its own row, not one row repeatedly overwritten

            tracker.Dispose();
        }

        [Fact]
        public void ScreenTimeTracker_Rollover_PersistsFinalSecondsToPreviousDay()
        {
            var previousDay = DateTime.Today;
            var simulatedNow = previousDay.AddHours(23).AddMinutes(59);
            var tracker = new ScreenTimeTracker
            {
                Clock = () => simulatedNow,
                IdleTimeProvider = () => TimeSpan.Zero
            };

            CallPrivate(tracker, "CheckActivity", null, null);
            simulatedNow = previousDay.AddDays(1).AddSeconds(1);
            CallPrivate(tracker, "CheckActivity", null, null);

            var prior = DatabaseService.GetScreenTimePeriodsForRange(previousDay, previousDay).Single();
            Assert.True(prior.AccumulatedActiveSeconds >= 1,
                "The last active seconds before midnight must remain in the previous day's bucket.");

            tracker.Dispose();
        }
    }
}
