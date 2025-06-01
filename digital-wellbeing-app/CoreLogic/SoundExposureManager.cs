using System;
using digital_wellbeing_app.Models;
using digital_wellbeing_app.Services;

namespace digital_wellbeing_app.CoreLogic
{
    public class SoundExposureManager
    {
        private SoundUsageSession? _currentSession;
        private bool _alertRaised;

        // dB threshold (20.0 for testing; change to 85.0 for real use)
        public double ThresholdDb { get; set; } = 85.0;

        // Duration above threshold before firing alert (10s for testing)
        public TimeSpan ThresholdTime { get; set; } = TimeSpan.FromHours(4);

        private readonly int _pollIntervalSeconds = 10;


        public event EventHandler? OnThresholdExceeded;

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
                HarmfulDuration = TimeSpan.Zero
            };
            _alertRaised = false;
        }

        public void HandleVolumeChange(
            double volumeScalar,
            string deviceName,
            string deviceType,
            float peakValue
        )
        {
            if (_currentSession == null)
            {
                HandleDeviceChange(deviceName, deviceType);
            }

            bool isActivePlayback = peakValue > 0.01f;
            if (!isActivePlayback) return;

            var session = _currentSession!;
            session.AvgVolume = session.AvgVolume == 0.0
                ? volumeScalar
                : (session.AvgVolume + volumeScalar) / 2.0;

            double baseSPL = GetBaseSPL(session.DeviceType);
            double estimatedSPL = volumeScalar * baseSPL;
            session.EstimatedMaxSPL = Math.Max(session.EstimatedMaxSPL, estimatedSPL);

            if (estimatedSPL >= ThresholdDb)
            {
                session.WasHarmful = true;
                session.HarmfulDuration += TimeSpan.FromSeconds(_pollIntervalSeconds);

                if (!_alertRaised && session.HarmfulDuration >= ThresholdTime)
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
            {
                EndCurrentSession();
            }
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

        private static double GetBaseSPL(string deviceType)
        {
            return deviceType switch
            {
                "Headphones" => 100.0,
                "Earphones" => 102.0,
                "Headsets" => 98.0,
                "Speakers" => 90.0,
                _ => 95.0
            };
        }
    }
}
