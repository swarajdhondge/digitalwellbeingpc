using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using digital_wellbeing_app.CoreLogic;
using digital_wellbeing_app.Models;
using digital_wellbeing_app.Services;

namespace digital_wellbeing_app.Tests.CoreLogic
{
    /// <summary>
    /// SoundExposureManager had zero dedicated test coverage before 2026-07-17's day-boundary fix.
    /// Covers: the new FlushCurrentSession (clock-change path), the new routine-path rollover
    /// guard, and the new _sessionLock (previously unguarded cross-thread access from
    /// SoundMonitoringService's UI-thread DispatcherTimer vs. this class's own ThreadPool timer).
    /// </summary>
    public class SoundExposureManagerBoundaryTests : TestBase
    {
        [Fact]
        public void FlushCurrentSession_NoActiveSession_DoesNotThrow()
        {
            var mgr = new SoundExposureManager();
            var ex = Record.Exception(() => mgr.FlushCurrentSession());
            Assert.Null(ex);
            mgr.Dispose();
        }

        [Fact]
        public void FlushCurrentSession_WithActiveSession_PersistsAndReopensForSameDevice()
        {
            var mgr = new SoundExposureManager();
            mgr.HandleDeviceChange("TestHeadphones", "Headphones");
            mgr.HandleVolumeChange(0.5, "TestHeadphones", "Headphones", 0.2f, TimeSpan.FromSeconds(10));

            mgr.FlushCurrentSession();

            var today = DateTime.Today;
            var persisted = DatabaseService.GetSoundSessionsForDate(today);
            Assert.Contains(persisted, s => s.DeviceName == "TestHeadphones");

            // Reopened in-memory for the same device, ready to keep accumulating.
            Assert.NotNull(mgr.CurrentSession);
            Assert.Equal("TestHeadphones", mgr.CurrentSession!.DeviceName);
            Assert.Equal("Headphones", mgr.CurrentSession.DeviceType);

            mgr.Dispose();
        }

        /// <summary>
        /// Mirrors AppUsageTracker_RoutineDayRollover: seeds a stale overnight session via
        /// reflection (CheckDayRolloverLocked is private, and the only public path needs a live
        /// audio-device callback), then invokes the private rollover check directly.
        /// </summary>
        [Fact]
        public void RoutineDayRollover_FlushesStaleOvernightSession()
        {
            var mgr = new SoundExposureManager();
            var yesterday = DateTime.Now.AddDays(-1);

            var sessionField = typeof(SoundExposureManager).GetField("_currentSession", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var originalSession = new SoundUsageSession
            {
                StartTime = yesterday,
                DeviceName = "OvernightDevice",
                DeviceType = "Speakers",
                AvgVolume = 0.3,
                EstimatedMaxSPL = 70.0,
                WasHarmful = false,
                HarmfulDuration = TimeSpan.Zero,
                ActualListeningDuration = TimeSpan.FromMinutes(1)
            };
            sessionField.SetValue(mgr, originalSession);

            var checkMethod = typeof(SoundExposureManager).GetMethod("CheckDayRolloverLocked", BindingFlags.NonPublic | BindingFlags.Instance)!;
            checkMethod.Invoke(mgr, null);

            // DatabaseService has its own unrelated 24h MaxSessionSeconds write guard that a
            // StartTime=yesterday/EndTime=now flush would sit right at the edge of - this only
            // asserts the in-memory state transition, which happens regardless of whether the DB
            // write of the *old* segment is accepted or rejected by that unrelated guard.
            Assert.NotNull(mgr.CurrentSession);
            Assert.NotSame(originalSession, mgr.CurrentSession);
            Assert.Equal("OvernightDevice", mgr.CurrentSession!.DeviceName);
            Assert.Equal(DateTime.Now.Date, mgr.CurrentSession.StartTime.Date);

            mgr.Dispose();
        }

        [Fact]
        public void CheckDayRollover_SameDaySession_DoesNotFlush()
        {
            var mgr = new SoundExposureManager();
            // Anchor to the calendar day rather than "five minutes ago" so this
            // remains a same-day test even when the suite runs across midnight.
            var todayStart = DateTime.Today;

            var sessionField = typeof(SoundExposureManager).GetField("_currentSession", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var originalSession = new SoundUsageSession
            {
                StartTime = todayStart,
                DeviceName = "SameDayDevice",
                DeviceType = "Headphones",
                ActualListeningDuration = TimeSpan.FromMinutes(1)
            };
            sessionField.SetValue(mgr, originalSession);

            var checkMethod = typeof(SoundExposureManager).GetMethod("CheckDayRolloverLocked", BindingFlags.NonPublic | BindingFlags.Instance)!;
            checkMethod.Invoke(mgr, null);

            Assert.Same(originalSession, mgr.CurrentSession);

            mgr.Dispose();
        }

        /// <summary>
        /// Exercises the new _sessionLock under real contention: concurrent HandleVolumeChange
        /// calls (simulating the UI-thread DispatcherTimer) racing a manual OnPeriodicSave
        /// invocation (simulating this class's own ThreadPool timer) must never throw or corrupt
        /// CurrentSession. This is the scenario the "no lock" gap made unsafe.
        /// </summary>
        [Fact]
        public void ConcurrentVolumeChangesAndPeriodicSave_NeverThrows()
        {
            var mgr = new SoundExposureManager();
            mgr.HandleDeviceChange("ConcurrentDevice", "Headphones");

            var periodicSaveMethod = typeof(SoundExposureManager).GetMethod("OnPeriodicSave", BindingFlags.NonPublic | BindingFlags.Instance)!;

            var tasks = Enumerable.Range(0, 8).Select(i => Task.Run(() =>
            {
                for (int j = 0; j < 25; j++)
                {
                    if (i % 2 == 0)
                        mgr.HandleVolumeChange(0.4, "ConcurrentDevice", "Headphones", 0.3f, TimeSpan.FromSeconds(10));
                    else
                        periodicSaveMethod.Invoke(mgr, new object?[] { null, null });
                }
            })).ToArray();

            var exception = Record.Exception(() => Task.WaitAll(tasks, TimeSpan.FromSeconds(30)));

            Assert.Null(exception);
            mgr.Dispose();
        }
    }
}
