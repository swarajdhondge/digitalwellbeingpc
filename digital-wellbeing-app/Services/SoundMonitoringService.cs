using System;
using System.Data;
using System.Windows;
using System.Windows.Threading;
using digital_wellbeing_app.CoreLogic;
using NAudio.CoreAudioApi;

namespace digital_wellbeing_app.Services
{
    public class SoundMonitoringService : IDisposable
    {
        private readonly MMDeviceEnumerator _deviceEnumerator;
        private MMDevice _currentDevice;
        private readonly DispatcherTimer _dispatcherTimer;
        private readonly SoundExposureManager _exposureManager;
        private string _lastDeviceId;

        public SoundMonitoringService()
        {
            _exposureManager = new SoundExposureManager();
            _exposureManager.OnThresholdExceeded += (_, __) =>
            {
                MessageBox.Show(
                    "You have been listening above the threshold.\n" +
                    "Lower your volume to protect your hearing.",
                    "Hearing Alert",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
            };

            _deviceEnumerator = new MMDeviceEnumerator();
            _currentDevice = _deviceEnumerator.GetDefaultAudioEndpoint(
                DataFlow.Render, Role.Multimedia);
            _lastDeviceId = _currentDevice.ID;
            SubscribeToDevice(_currentDevice);

            _dispatcherTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(10)
            };
            _dispatcherTimer.Tick += OnDispatcherTimerTick;
            _dispatcherTimer.Start();
        }

        private void SubscribeToDevice(MMDevice device)
        {
            _exposureManager.HandleDeviceChange(
                device.FriendlyName,
                IdentifyDeviceType(device.FriendlyName)
            );
            device.AudioEndpointVolume.OnVolumeNotification += OnVolumeNotification;
        }

        private void UnsubscribeFromDevice(MMDevice device)
        {
            device.AudioEndpointVolume.OnVolumeNotification -= OnVolumeNotification;
        }

        private void OnVolumeNotification(AudioVolumeNotificationData data)
        {
            float peak = _currentDevice.AudioMeterInformation.MasterPeakValue;
            double volumeScalar = _currentDevice.AudioEndpointVolume.MasterVolumeLevelScalar;

            _exposureManager.HandleVolumeChange(
                volumeScalar,
                _currentDevice.FriendlyName,
                IdentifyDeviceType(_currentDevice.FriendlyName),
                peak
            );
        }

        private void OnDispatcherTimerTick(object? sender, EventArgs e)
        {
            var defaultDevice = _deviceEnumerator.GetDefaultAudioEndpoint(
                DataFlow.Render, Role.Multimedia);

            if (defaultDevice.ID != _lastDeviceId)
            {
                UnsubscribeFromDevice(_currentDevice);
                _exposureManager.HandleDeviceChange(
                    _currentDevice.FriendlyName,
                    IdentifyDeviceType(_currentDevice.FriendlyName)
                );
                _currentDevice = defaultDevice;
                _lastDeviceId = _currentDevice.ID;
                SubscribeToDevice(_currentDevice);
            }

            double currentVolume = _currentDevice.AudioEndpointVolume.MasterVolumeLevelScalar;
            float peakVal = _currentDevice.AudioMeterInformation.MasterPeakValue;

            _exposureManager.HandleVolumeChange(
                currentVolume,
                _currentDevice.FriendlyName,
                IdentifyDeviceType(_currentDevice.FriendlyName),
                peakVal
            );

            _exposureManager.CheckPlaybackActivity(peakVal);
        }

        private static string IdentifyDeviceType(string friendlyName)
        {
            string name = friendlyName.ToLowerInvariant();
            if (name.Contains("headphone")) return "Headphones";
            if (name.Contains("earphone")) return "Earphones";
            if (name.Contains("headset")) return "Headsets";
            if (name.Contains("speaker")) return "Speakers";
            return "Unknown";
        }

        public void Dispose()
        {
            _dispatcherTimer.Tick -= OnDispatcherTimerTick;
            _dispatcherTimer.Stop();

            UnsubscribeFromDevice(_currentDevice);
            _exposureManager.HandleDeviceChange(
                _currentDevice.FriendlyName,
                IdentifyDeviceType(_currentDevice.FriendlyName)
            );

            _deviceEnumerator.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
