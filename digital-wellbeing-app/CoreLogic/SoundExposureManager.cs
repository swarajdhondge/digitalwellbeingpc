using System;
using digital_wellbeing_app.Models;
using digital_wellbeing_app.Services;

namespace digital_wellbeing_app.CoreLogic
{
    public class SoundExposureManager : IDisposable
    {
        private SoundUsageSession? _currentSession;
        private bool _alertRaised;
        private TimeSpan _continuousHarmfulDuration = TimeSpan.Zero;
        private readonly System.Timers.Timer _periodicSaveTimer;
        private DateTime _lastSaved = DateTime.Now;
        // HandleVolumeChange/HandleDeviceChange/CheckPlaybackActivity are called from
        // SoundMonitoringService's UI-thread DispatcherTimer, while OnPeriodicSave fires on this
        // class's own System.Timers.Timer (a ThreadPool thread) - _currentSession is genuinely
        // cross-thread, unlike AppUsageTracker/ScreenTimeTracker which already lock for this reason.
        private readonly object _sessionLock = new();

        // Save interval matches ScreenTimeTracker (5 minutes)
        private const int SaveIntervalMinutes = 5;

        public double ThresholdDb { get; set; } = 75.0;
        public TimeSpan ThresholdTime { get; set; } = TimeSpan.FromMinutes(30);

        public event EventHandler? OnThresholdExceeded;

        /// <summary>
        /// Expose the live session if it exists.
        /// </summary>
        public SoundUsageSession? CurrentSession => _currentSession;

        /// <summary>
        /// Source of "now" for every day-boundary/session decision after construction. Defaults
        /// to the real clock; overridable for long-running simulation tests. Mirrors
        /// ScreenTimeTracker.Clock/AppUsageTracker.Clock: the _lastSaved field initializer above
        /// intentionally still uses the real DateTime.Now (it runs before a caller can override
        /// this property).
        /// </summary>
        public Func<DateTime> Clock { get; set; } = () => DateTime.Now;

        public SoundExposureManager()
        {
            // Load threshold from settings
            var settingsService = new SettingsService();
            ThresholdDb = settingsService.LoadHarmfulThreshold();

            // Periodic save timer to prevent data loss on crash
            _periodicSaveTimer = new System.Timers.Timer(60_000) // Check every minute
            {
                AutoReset = true
            };
            _periodicSaveTimer.Elapsed += OnPeriodicSave;
            _periodicSaveTimer.Start();
        }

        public void HandleDeviceChange(string newDeviceName, string newDeviceType)
        {
            lock (_sessionLock)
            {
                EndCurrentSessionLocked();
                _currentSession = new SoundUsageSession
                {
                    StartTime = Clock(),
                    DeviceName = newDeviceName,
                    DeviceType = newDeviceType,
                    AvgVolume = 0.0,
                    EstimatedMaxSPL = 0.0,
                    WasHarmful = false,
                    HarmfulDuration = TimeSpan.Zero,
                    ActualListeningDuration = TimeSpan.Zero
                };
                _alertRaised = false;
            }
        }

        /// <summary>
        /// <paramref name="elapsed"/> is the real wall-clock time since the caller's last sample
        /// (whichever fired it - the periodic tick or an ad-hoc volume-change event), not assumed.
        /// Previously this hardcoded a separately-declared 1-second constant while the actual
        /// caller (SoundMonitoringService) polled every 10 real seconds - a real, shipping ~10x
        /// undercount of listening/harmful duration during any steady playback stretch. Requiring
        /// the caller to pass real elapsed time (rather than defaulting back to a constant) makes
        /// the two files structurally unable to drift apart again.
        /// </summary>
        public void HandleVolumeChange(double volumeScalar, string deviceName, string deviceType, float peakValue, TimeSpan elapsed)
        {
            lock (_sessionLock)
            {
                // HandleDeviceChange re-enters this same lock (Monitor.Enter is reentrant for the
                // owning thread), so this is safe despite already being inside the lock.
                if (_currentSession == null)
                    HandleDeviceChange(deviceName, deviceType);

                // Only count listening time when audio is actually playing
                if (peakValue <= 0.01f)
                    return;

                var now = Clock();
                var remainingElapsed = elapsed < TimeSpan.Zero ? TimeSpan.Zero : elapsed;

                // A sample can straddle midnight (the monitor polls every few seconds). Attribute
                // the portion before midnight to the old day and the remainder to the new day.
                if (_currentSession!.StartTime.Date != now.Date)
                {
                    var boundary = now.Date;
                    var sampleStart = now - remainingElapsed;
                    var beforeBoundary = boundary > sampleStart
                        ? boundary - sampleStart
                        : TimeSpan.Zero;
                    if (beforeBoundary > remainingElapsed) beforeBoundary = remainingElapsed;

                    ApplyListeningSample(_currentSession, volumeScalar, beforeBoundary);
                    SplitCurrentSessionLocked(boundary, resetExposure: true);
                    remainingElapsed -= beforeBoundary;
                }

                ApplyListeningSample(_currentSession!, volumeScalar, remainingElapsed);
            }
        }

        private void ApplyListeningSample(SoundUsageSession session, double volumeScalar, TimeSpan elapsed)
        {
            if (elapsed <= TimeSpan.Zero) return;

            var priorSeconds = session.ActualListeningDuration.TotalSeconds;
            var addedSeconds = elapsed.TotalSeconds;
            session.AvgVolume = priorSeconds <= 0
                ? volumeScalar
                : ((session.AvgVolume * priorSeconds) + (volumeScalar * addedSeconds))
                  / (priorSeconds + addedSeconds);
            session.ActualListeningDuration += elapsed;

            var estimatedSPL = volumeScalar * GetBaseSPL(session.DeviceType);
            session.EstimatedMaxSPL = Math.Max(session.EstimatedMaxSPL, estimatedSPL);

            if (estimatedSPL < ThresholdDb) return;

            session.WasHarmful = true;
            session.HarmfulDuration += elapsed;
            _continuousHarmfulDuration += elapsed;

            if (!_alertRaised && _continuousHarmfulDuration >= ThresholdTime)
            {
                _alertRaised = true;
                OnThresholdExceeded?.Invoke(this, EventArgs.Empty);
            }
        }

        public void CheckPlaybackActivity(float peakValue)
        {
            lock (_sessionLock)
            {
                if (_currentSession == null) return;
                if (peakValue < 0.01f)
                    EndCurrentSessionLocked();
            }
        }

        /// <summary>Caller must hold _sessionLock.</summary>
        private void EndCurrentSessionLocked()
        {
            if (_currentSession != null)
            {
                _currentSession.EndTime = Clock();
                DatabaseService.SaveSoundSession(_currentSession);
                _currentSession = null;
                _alertRaised = false;
                _continuousHarmfulDuration = TimeSpan.Zero;
            }
        }

        /// <summary>
        /// Routine-path day-boundary guard, mirroring AppUsageTracker.CheckDayRollover - runs on
        /// every periodic save so a session left open overnight on the same device still splits at
        /// the day boundary. Caller must hold _sessionLock.
        /// </summary>
        private void CheckDayRolloverLocked()
        {
            if (_currentSession != null && _currentSession.StartTime.Date != Clock().Date)
                SplitCurrentSessionLocked(Clock().Date, resetExposure: true);
        }

        /// <summary>Close the current chunk at an exact boundary and continue on the same device.</summary>
        private void SplitCurrentSessionLocked(DateTime boundary, bool resetExposure)
        {
            if (_currentSession == null) return;

            var prior = _currentSession;
            prior.EndTime = boundary;
            DatabaseService.SaveSoundSession(prior);

            _currentSession = new SoundUsageSession
            {
                StartTime = boundary,
                DeviceName = prior.DeviceName,
                DeviceType = prior.DeviceType,
                AvgVolume = 0.0,
                EstimatedMaxSPL = 0.0,
                WasHarmful = false,
                HarmfulDuration = TimeSpan.Zero,
                ActualListeningDuration = TimeSpan.Zero
            };

            if (resetExposure)
            {
                _alertRaised = false;
                _continuousHarmfulDuration = TimeSpan.Zero;
            }
        }

        /// <summary>Caller must hold _sessionLock.</summary>
        private void FlushCurrentSessionLocked()
        {
            if (_currentSession == null) return;
            SplitCurrentSessionLocked(Clock(), resetExposure: true);
        }

        /// <summary>
        /// Force-close the current listening segment and restart a fresh one for the same device
        /// at the new wall-clock time. Called on a system clock/timezone change, mirroring
        /// AppUsageTracker.FlushCurrentSession, so a single session cannot straddle two local-date
        /// buckets. Public (unlike the Locked-suffixed helpers above) since MainWindow calls this
        /// directly from OnSystemTimeChanged.
        /// </summary>
        public void FlushCurrentSession()
        {
            lock (_sessionLock)
            {
                FlushCurrentSessionLocked();
            }
        }

        /// <summary>
        /// Periodically saves the current session to prevent data loss on crash.
        /// Saves a completed segment and starts a new one for the same device.
        /// </summary>
        private void OnPeriodicSave(object? sender, System.Timers.ElapsedEventArgs e)
        {
            lock (_sessionLock)
            {
                CheckDayRolloverLocked();

                // This timer already ticks at a reasonable ~60s cadence, so no extra throttling
                // is needed here (unlike ScreenTimeTracker's 1s tick).
                TrackingHealthService.RecordHeartbeat(nameof(SoundExposureManager));

                var now = Clock();

                // Only save every SaveIntervalMinutes
                if ((now - _lastSaved).TotalMinutes < SaveIntervalMinutes)
                    return;

                if (_currentSession == null)
                    return;

                // Skip very short sessions (less than 30 seconds of actual listening)
                if (_currentSession.ActualListeningDuration.TotalSeconds < 30)
                    return;

                // Save the current session segment
                _currentSession.EndTime = now;
                DatabaseService.SaveSoundSession(_currentSession);

                // Start a new session segment for the same device (seamless continuation)
                _currentSession = new SoundUsageSession
                {
                    StartTime = now,
                    DeviceName = _currentSession.DeviceName,
                    DeviceType = _currentSession.DeviceType,
                    AvgVolume = 0.0,
                    EstimatedMaxSPL = 0.0,
                    WasHarmful = false,
                    HarmfulDuration = TimeSpan.Zero,
                    ActualListeningDuration = TimeSpan.Zero
                };

                _lastSaved = now;
            }
        }

        public void Dispose()
        {
            lock (_sessionLock)
            {
                EndCurrentSessionLocked();
            }
            _periodicSaveTimer.Stop();
            _periodicSaveTimer.Elapsed -= OnPeriodicSave;
            _periodicSaveTimer.Dispose();
        }

        private static double GetBaseSPL(string deviceType) => deviceType switch
        {
            "Headphones" => 100.0,
            "Earphones" => 102.0,
            "Headsets" => 98.0,
            "Speakers" => 90.0,
            _ => 95.0
        };
    }
}
