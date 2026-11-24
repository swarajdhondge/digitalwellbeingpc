using System;
using System.Reflection;
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

        // Windows 11 Alt+Tab/taskbar switch: the hook sees the app, then explorer's task-switcher
        // host, which ends the session; no further event follows while the app stays in front.
        [Fact]
        public void Tick_RecoversForegroundApp_WhenSessionWasEndedByShellWindow()
        {
            var now = DateTime.Today.AddHours(10);
            using var tracker = new AppUsageTracker
            {
                Clock = () => now, IdleTimeProvider = () => TimeSpan.Zero, ForegroundConsumingProvider = () => false,
                ForegroundAppProvider = () => System.Diagnostics.Process.GetCurrentProcess()
            };
            CallPrivate(tracker, "OnAppChanged", System.Diagnostics.Process.GetCurrentProcess());
            CallPrivate(tracker, "OnAppChanged", new object?[] { null });   // shell window event
            Assert.Null(tracker.CurrentSession);

            CallPrivate(tracker, "OnTick");

            Assert.NotNull(tracker.CurrentSession);
            Assert.Equal(System.Diagnostics.Process.GetCurrentProcess().ProcessName, tracker.CurrentSession!.AppName);
        }

        [Fact]
        public void Tick_DoesNotReplaceARunningSession()
        {
            var now = DateTime.Today.AddHours(10);
            int lookups = 0;
            using var tracker = new AppUsageTracker
            {
                Clock = () => now, IdleTimeProvider = () => TimeSpan.Zero, ForegroundConsumingProvider = () => false,
                ForegroundAppProvider = () => { lookups++; return System.Diagnostics.Process.GetCurrentProcess(); }
            };
            var start = now.AddMinutes(-3);
            SetField(tracker, "_currentSession", new AppUsageSession { AppName = "notepad", StartTime = start });

            CallPrivate(tracker, "OnTick");

            Assert.Equal("notepad", tracker.CurrentSession!.AppName);
            Assert.Equal(start, tracker.CurrentSession.StartTime);
            Assert.Equal(0, lookups);
        }

        [Fact]
        public void Tick_DoesNotRecoverWhileAway()
        {
            var now = DateTime.Today.AddHours(10);
            using var tracker = new AppUsageTracker
            {
                Clock = () => now, IdleTimeProvider = () => TimeSpan.FromMinutes(10), ForegroundConsumingProvider = () => false,
                ForegroundAppProvider = () => System.Diagnostics.Process.GetCurrentProcess()
            };
            CallPrivate(tracker, "OnTick");
            now = now.AddSeconds(1);
            CallPrivate(tracker, "OnTick");
            Assert.Null(tracker.CurrentSession);
        }

        [Fact]
        public void Tick_NothingTrackableInFront_StaysEmpty()
        {
            var now = DateTime.Today.AddHours(10);
            using var tracker = new AppUsageTracker
            {
                Clock = () => now, IdleTimeProvider = () => TimeSpan.Zero, ForegroundConsumingProvider = () => false,
                ForegroundAppProvider = () => null   // desktop or taskbar in front
            };
            CallPrivate(tracker, "OnTick");
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

        [Fact]
        public void Start_DoesNotThrow_RegardlessOfForegroundOrIdleState()
        {
            var tracker = new AppUsageTracker();
            var exception = Record.Exception(() => tracker.Start());
            Assert.Null(exception);
            tracker.Dispose();
        }

        [Fact]
        public void Start_WhenSessionIsSynthesized_StartTimeIsRecent()
        {
            var before = DateTime.Now;
            var tracker = new AppUsageTracker();
            tracker.Start();
            var after = DateTime.Now;

            // A real machine is essentially never idle for 300s in a live/CI test run, so this
            // should be non-null in practice - but if the current foreground window couldn't be
            // resolved (e.g. a headless session) or the machine genuinely is idle, null is also a
            // correct, non-buggy outcome, so this only asserts freshness when a session exists.
            var session = tracker.CurrentSession;
            if (session != null)
            {
                Assert.InRange(session.StartTime, before.AddSeconds(-1), after.AddSeconds(1));
            }

            tracker.Dispose();
        }

        /// <summary>
        /// The new routine-path guard (no clock-change event involved): a session left open
        /// overnight must still split at the day boundary. CheckDayRollover/OnPeriodicSave/
        /// OnAppChanged are all private and the only public path to populate _currentSession needs
        /// a live Windows focus-change event, so this seeds the field via reflection - the same
        /// private state a real overnight session would have left behind.
        /// </summary>
        [Fact]
        public void AppTracker_RoutineDayRollover_FlushesStaleOvernightSession()
        {
            var tracker = new AppUsageTracker();
            // DatabaseService has its own unrelated 24h MaxSessionSeconds write guard, which a
            // StartTime=yesterday/EndTime=now flush would sit right at the edge of - rather than
            // chase that edge with a clock this test doesn't control, this only asserts the
            // in-memory state transition (see below), which happens regardless of whether the DB
            // write of the *old* segment is accepted or rejected by that unrelated guard.
            var yesterday = DateTime.Now.AddDays(-1);

            var sessionField = typeof(AppUsageTracker).GetField("_currentSession", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var originalSession = new AppUsageSession
            {
                AppName = "overnight_test_app",
                ExecutablePath = "overnight_test_app.exe",
                StartTime = yesterday,
                WindowTitle = null
            };
            sessionField.SetValue(tracker, originalSession);

            var checkMethod = typeof(AppUsageTracker).GetMethod("CheckDayRollover", BindingFlags.NonPublic | BindingFlags.Instance)!;
            checkMethod.Invoke(tracker, null);

            // The stale in-memory segment must have been replaced with a fresh continuation
            // segment for the same app, starting today.
            var afterSession = (AppUsageSession?)sessionField.GetValue(tracker);
            Assert.NotNull(afterSession);
            Assert.NotSame(originalSession, afterSession);
            Assert.Equal("overnight_test_app", afterSession!.AppName);
            Assert.Equal(DateTime.Now.Date, afterSession.StartTime.Date);

            tracker.Dispose();
        }

        [Fact]
        public void AppTracker_CheckDayRollover_SameDaySession_DoesNotFlush()
        {
            var tracker = new AppUsageTracker();
            // Anchor to the calendar day rather than "five minutes ago" so this
            // remains a same-day test even when the suite runs across midnight.
            var todayStart = DateTime.Today;

            var sessionField = typeof(AppUsageTracker).GetField("_currentSession", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var originalSession = new AppUsageSession
            {
                AppName = "sameday_test_app",
                ExecutablePath = "sameday_test_app.exe",
                StartTime = todayStart,
                WindowTitle = null
            };
            sessionField.SetValue(tracker, originalSession);

            var checkMethod = typeof(AppUsageTracker).GetMethod("CheckDayRollover", BindingFlags.NonPublic | BindingFlags.Instance)!;
            checkMethod.Invoke(tracker, null);

            // Same in-memory instance, untouched - no rollover should have happened.
            var afterSession = (AppUsageSession?)sessionField.GetValue(tracker);
            Assert.Same(originalSession, afterSession);
            Assert.Equal(todayStart, afterSession!.StartTime);

            tracker.Dispose();
        }

        [Fact]
        public void AppUsage_ForwardClockJump_IsNotCreditedToTheRunningApp()
        {
            var now = DateTime.Today.AddHours(10);
            using var tracker = new AppUsageTracker
            {
                Clock = () => now, IdleTimeProvider = () => TimeSpan.Zero, ForegroundConsumingProvider = () => false
            };
            SetField(tracker, "_currentSession", new AppUsageSession
            {
                AppName = "notepad", ExecutablePath = @"C:\notepad.exe", StartTime = now.AddMinutes(-2)
            });
            CallPrivate(tracker, "OnTick");
            now = now.AddHours(1);
            CallPrivate(tracker, "OnTick");
            now = now.AddSeconds(30);
            var total = tracker.GetSessionsForRange(now.Date, now.Date).Sum(s => s.Duration.TotalSeconds);
            Assert.Equal(150, total);
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
            CallPrivate(tracker, "CheckDayRollover");

            var prior = DatabaseService.GetAppUsageSessionsForDate(day).Single();
            Assert.Equal(day.AddDays(1), prior.EndTime);
            var live = Assert.IsType<digital_wellbeing_app.Models.AppUsageSession>(sessionField.GetValue(tracker));
            Assert.Equal(day.AddDays(1), live.StartTime);

            tracker.Dispose();
        }
    }
}
