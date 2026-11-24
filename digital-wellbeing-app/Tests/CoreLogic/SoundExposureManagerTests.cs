using System;
using System.Linq;
using System.Threading.Tasks;
using digital_wellbeing_app.Models;
using digital_wellbeing_app.Services;
using System.Reflection;
using Xunit;
using digital_wellbeing_app.CoreLogic;

namespace digital_wellbeing_app.Tests.CoreLogic
{
    /// <summary>
    /// Regression coverage for the 2026-07-17 fix to a real, shipping ~10x undercount:
    /// SoundMonitoringService polls every 10 real seconds, but HandleVolumeChange used to
    /// unconditionally add a separately-declared, hardcoded 1-second constant regardless of how
    /// much wall-clock time actually passed. HandleVolumeChange now requires the caller to pass
    /// real elapsed time, which these tests control directly - no live audio device or timer
    /// needed, since the bug (and the fix) is entirely in how the passed-in value is used.
    /// This class had zero dedicated test coverage before this fix.
    /// </summary>
    public class SoundExposureManagerTests : TestBase
    {
        [Fact]
        public void HandleVolumeChange_UsesProvidedElapsedTime_NotAHardcodedConstant()
        {
            var mgr = new SoundExposureManager();

            // The old bug: this would have recorded 1 second regardless of the 10 passed here.
            mgr.HandleVolumeChange(0.3, "TestDevice", "Headphones", 0.5f, TimeSpan.FromSeconds(10));

            Assert.NotNull(mgr.CurrentSession);
            Assert.Equal(TimeSpan.FromSeconds(10), mgr.CurrentSession!.ActualListeningDuration);

            mgr.Dispose();
        }

        [Fact]
        public void HandleVolumeChange_AccumulatesElapsedAcrossMultipleCalls()
        {
            var mgr = new SoundExposureManager();

            mgr.HandleVolumeChange(0.3, "TestDevice", "Headphones", 0.5f, TimeSpan.FromSeconds(10));
            mgr.HandleVolumeChange(0.3, "TestDevice", "Headphones", 0.5f, TimeSpan.FromSeconds(7));
            mgr.HandleVolumeChange(0.3, "TestDevice", "Headphones", 0.5f, TimeSpan.FromSeconds(12));

            Assert.Equal(TimeSpan.FromSeconds(29), mgr.CurrentSession!.ActualListeningDuration);

            mgr.Dispose();
        }

        [Fact]
        public void HandleVolumeChange_SilentPeak_DoesNotAccumulateDuration()
        {
            var mgr = new SoundExposureManager();

            // peakValue at/below the 0.01 floor means "not actually playing" - must not accumulate
            // even though a full 10s "elapsed" was passed.
            mgr.HandleVolumeChange(0.3, "TestDevice", "Headphones", 0.0f, TimeSpan.FromSeconds(10));

            Assert.NotNull(mgr.CurrentSession);
            Assert.Equal(TimeSpan.Zero, mgr.CurrentSession!.ActualListeningDuration);

            mgr.Dispose();
        }

        [Fact]
        public void HandleVolumeChange_HarmfulDuration_UsesElapsedTime_NotAHardcodedConstant()
        {
            var mgr = new SoundExposureManager();
            mgr.ThresholdDb = 75.0;

            // Headphones base SPL is 100.0 (GetBaseSPL) - volumeScalar 0.9 => estimatedSPL 90,
            // comfortably over the 75 dB threshold, so this should count as harmful.
            mgr.HandleVolumeChange(0.9, "TestDevice", "Headphones", 0.8f, TimeSpan.FromSeconds(15));

            Assert.NotNull(mgr.CurrentSession);
            Assert.True(mgr.CurrentSession!.WasHarmful);
            Assert.Equal(TimeSpan.FromSeconds(15), mgr.CurrentSession.HarmfulDuration);

            mgr.Dispose();
        }

        [Fact]
        public void HandleVolumeChange_QuietVolume_DoesNotAccumulateHarmfulDuration()
        {
            var mgr = new SoundExposureManager();
            mgr.ThresholdDb = 75.0;

            // volumeScalar 0.1 => estimatedSPL 10, well under threshold.
            mgr.HandleVolumeChange(0.1, "TestDevice", "Headphones", 0.5f, TimeSpan.FromSeconds(10));

            Assert.NotNull(mgr.CurrentSession);
            Assert.False(mgr.CurrentSession!.WasHarmful);
            Assert.Equal(TimeSpan.Zero, mgr.CurrentSession.HarmfulDuration);

            mgr.Dispose();
        }

        [Fact]
        public void HarmfulThreshold_ContinuesAcrossCrashSafetyChunks()
        {
            var now = new DateTime(2026, 7, 31, 9, 0, 0);
            var mgr = new SoundExposureManager
            {
                Clock = () => now,
                ThresholdDb = 75.0,
                ThresholdTime = TimeSpan.FromMinutes(30)
            };
            var alerts = 0;
            mgr.OnThresholdExceeded += (_, _) => alerts++;
            var split = typeof(SoundExposureManager).GetMethod(
                "SplitCurrentSessionLocked", BindingFlags.NonPublic | BindingFlags.Instance)!;

            for (var i = 0; i < 6; i++)
            {
                now = now.AddMinutes(5);
                mgr.HandleVolumeChange(0.9, "TestDevice", "Headphones", 0.8f, TimeSpan.FromMinutes(5));
                if (i < 5)
                    split.Invoke(mgr, new object[] { now, false });
            }

            Assert.Equal(1, alerts);
            mgr.Dispose();
        }

        [Fact]
        public void AverageVolume_IsWeightedByListeningDuration()
        {
            var mgr = new SoundExposureManager();

            mgr.HandleVolumeChange(0.2, "TestDevice", "Headphones", 0.5f, TimeSpan.FromSeconds(10));
            mgr.HandleVolumeChange(0.8, "TestDevice", "Headphones", 0.5f, TimeSpan.FromSeconds(30));

            Assert.Equal(0.65, mgr.CurrentSession!.AvgVolume, 6);
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
                        periodicSaveMethod.Invoke(mgr, new object[] { null, null });
                }
            })).ToArray();

            var exception = Record.Exception(() => Task.WaitAll(tasks, TimeSpan.FromSeconds(30)));

            Assert.Null(exception);
            mgr.Dispose();
        }
    }
}
