using System;
using digital_wellbeing_app.Models;
using digital_wellbeing_app.Services;

namespace digital_wellbeing_app.CoreLogic
{
    public class SoundExposureManager
    {
        private SoundUsageSession? _currentSession;
        private bool _alertRaised;

        public double ThresholdDb { get; set; } = 75.0;
        public TimeSpan ThresholdTime { get; set; } = TimeSpan.FromMinutes(30);

        private readonly int _pollIntervalSeconds = 1;

        public event EventHandler? OnThresholdExceeded;

        /// <summary>
        /// Expose the live session if it exists.
        /// </summary>
        public SoundUsageSession? CurrentSession => _currentSession;

        public SoundExposureManager()
        {
            // Load threshold from settings
            var settingsService = new SettingsService();
            ThresholdDb = settingsService.LoadHarmfulThreshold();
        }

        public void HandleDeviceChange(string newDeviceName, string newDeviceType)
        {
            EndCurrentSession();
            _currentSession = new SoundUsageSession
            {
                StartTime = DateTime.Now,
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

        public void HandleVolumeChange(double volumeScalar, string deviceName, string deviceType, float peakValue)
        {
            if (_currentSession == null)
                HandleDeviceChange(deviceName, deviceType);

            // Only count listening time when audio is actually playing
            if (peakValue <= 0.01f)
                return;

            var session = _currentSession!;
            
            // Increment actual listening duration (only when audio is playing)
            session.ActualListeningDuration += TimeSpan.FromSeconds(_pollIntervalSeconds);
            
            session.AvgVolume = session.AvgVolume == 0.0
                ? volumeScalar
                : (session.AvgVolume + volumeScalar) / 2.0;

            double baseSPL = GetBaseSPL(session.DeviceType);
            double estimatedSPL = volumeScalar * baseSPL;
            session.EstimatedMaxSPL = Math.Max(session.EstimatedMaxSPL, estimatedSPL);

            // only accumulate harmful time **before** the alert
            if (estimatedSPL >= ThresholdDb && !_alertRaised)
            {
                session.WasHarmful = true;
                session.HarmfulDuration += TimeSpan.FromSeconds(_pollIntervalSeconds);

                if (session.HarmfulDuration >= ThresholdTime)
                {
                    _alertRaised = true;
                    OnThresholdExceeded?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public void CheckPlaybackActivity(float peakValue)
        {
            if (_currentSession == null) return;
            if (peakValue < 0.01f)
                EndCurrentSession();
        }

        private void EndCurrentSession()
        {
            if (_currentSession != null)
            {
                _currentSession.EndTime = DateTime.Now;
                DatabaseService.SaveSoundSession(_currentSession);
                _currentSession = null;
                _alertRaised = false;
            }
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
