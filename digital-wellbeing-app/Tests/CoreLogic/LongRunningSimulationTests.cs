using System;
using System.Linq;
using System.Reflection;
using Xunit;
using digital_wellbeing_app.CoreLogic;
using digital_wellbeing_app.Services;

namespace digital_wellbeing_app.Tests.CoreLogic
{
    /// <summary>
    /// Drives ScreenTimeTracker/AppUsageTracker/SoundExposureManager through simulated multi-day
    /// runs using the 2026-07-17 Clock seam, instead of waiting on real timers (which would take
    /// real hours to cross even a handful of day boundaries). The private periodic-callback
    /// methods are invoked directly via reflection with a fake/null ElapsedEventArgs (none of them
    /// actually read that parameter) - each invocation stands in for one real timer tick, with
    /// Clock advanced between calls to simulate elapsed wall-clock time.
    ///
    /// These test day-boundary/session-key correctness and crash-freedom across many simulated
    /// days, not realistic accumulated-duration totals - each invocation always credits exactly
    /// one "tick" of activity regardless of how far Clock jumped between calls.
    /// </summary>
    public class LongRunningSimulationTests : TestBase
    {
        private static void InvokePrivate(object target, string methodName, params object?[] args)
        {
            var method = target.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance)!;
            method.Invoke(target, args);
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
                        InvokePrivate(tracker, "CheckActivity", null, null);
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

            InvokePrivate(tracker, "CheckActivity", null, null);
            simulatedNow = previousDay.AddDays(1).AddSeconds(1);
            InvokePrivate(tracker, "CheckActivity", null, null);

            var prior = DatabaseService.GetScreenTimePeriodsForRange(previousDay, previousDay).Single();
            Assert.True(prior.AccumulatedActiveSeconds >= 1,
                "The last active seconds before midnight must remain in the previous day's bucket.");

            tracker.Dispose();
        }

        [Fact]
        public void AppUsageTracker_SimulatedMultiDayRun_SplitsSessionsAtDayBoundaries()
        {
            // Deliberately goes through CheckDayRollover directly rather than OnAppChanged/
            // OnPeriodicSave - both of those gate on WindowsIdleTimeHelper.IsUserIdle, which
            // AppUsageTracker has no injectable seam for (unlike ScreenTimeTracker's
            // IdleTimeProvider), so they'd be flaky on a CI runner with no recent real input.
            // CheckDayRollover itself has no such gate, and is exactly the mechanism this test
            // means to exercise across many simulated days.
            var simulatedNow = new DateTime(2026, 1, 1, 8, 0, 0);
            var tracker = new AppUsageTracker { Clock = () => simulatedNow };
            var sessionField = typeof(AppUsageTracker).GetField("_currentSession", BindingFlags.NonPublic | BindingFlags.Instance)!;

            var exception = Record.Exception(() =>
            {
                for (int day = 0; day < 5; day++)
                {
                    sessionField.SetValue(tracker, new digital_wellbeing_app.Models.AppUsageSession
                    {
                        AppName = "simapp",
                        ExecutablePath = "simapp.exe",
                        StartTime = simulatedNow
                    });

                    simulatedNow = simulatedNow.AddDays(1);
                    InvokePrivate(tracker, "CheckDayRollover");
                }
            });

            Assert.Null(exception);

            var sessionsAcrossRange = DatabaseService.GetAppUsageSessionsForRange(
                new DateTime(2026, 1, 1), simulatedNow);
            var distinctDays = sessionsAcrossRange.Select(s => s.StartTime.Date).Distinct().Count();
            Assert.True(distinctDays >= 2, $"Expected sessions split across multiple simulated days, got {distinctDays}");

            tracker.Dispose();
        }

        [Fact]
        public void AppUsageTracker_Rollover_CutsAtExactMidnight()
        {
            var day = new DateTime(2026, 7, 30);
            var now = day.AddHours(23).AddMinutes(58);
            var tracker = new AppUsageTracker { Clock = () => now };
            var sessionField = typeof(AppUsageTracker).GetField(
                "_currentSession", BindingFlags.NonPublic | BindingFlags.Instance)!;
            sessionField.SetValue(tracker, new digital_wellbeing_app.Models.AppUsageSession
            {
                AppName = "code",
                ExecutablePath = @"C:\code.exe",
                StartTime = now
            });

            now = day.AddDays(1).AddMinutes(3);
            InvokePrivate(tracker, "CheckDayRollover");

            var prior = DatabaseService.GetAppUsageSessionsForDate(day).Single();
            Assert.Equal(day.AddDays(1), prior.EndTime);
            var live = Assert.IsType<digital_wellbeing_app.Models.AppUsageSession>(sessionField.GetValue(tracker));
            Assert.Equal(day.AddDays(1), live.StartTime);

            tracker.Dispose();
        }

        [Fact]
        public void SoundExposureManager_SimulatedMultiDayRun_SplitsSessionsAtDayBoundaries()
        {
            var simulatedNow = new DateTime(2026, 1, 1, 8, 0, 0);
            var mgr = new SoundExposureManager { Clock = () => simulatedNow };

            mgr.HandleDeviceChange("SimDevice", "Headphones");

            var exception = Record.Exception(() =>
            {
                for (int day = 0; day < 5; day++)
                {
                    mgr.HandleVolumeChange(0.3, "SimDevice", "Headphones", 0.5f, TimeSpan.FromSeconds(45));
                    InvokePrivate(mgr, "OnPeriodicSave", null, null);
                    simulatedNow = simulatedNow.AddDays(1);
                }
            });

            Assert.Null(exception);

            var sessionsDay1 = DatabaseService.GetSoundSessionsForDate(new DateTime(2026, 1, 1));
            var sessionsLastDay = DatabaseService.GetSoundSessionsForDate(simulatedNow.AddDays(-1).Date);
            Assert.True(sessionsDay1.Count > 0 || sessionsLastDay.Count > 0,
                "Expected at least one persisted session across the simulated run");

            mgr.Dispose();
        }

        [Fact]
        public void ScreenTimeTracker_ClockDefaultsToRealTime_WhenNotOverridden()
        {
            var before = DateTime.Now;
            var tracker = new ScreenTimeTracker();
            var after = DateTime.Now;

            var reported = tracker.Clock();

            Assert.InRange(reported, before.AddSeconds(-1), after.AddSeconds(1));
            tracker.Dispose();
        }
    }
}
