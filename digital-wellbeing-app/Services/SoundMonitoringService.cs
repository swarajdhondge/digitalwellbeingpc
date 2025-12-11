using System;
using System.Data;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using digital_wellbeing_app.CoreLogic;
using NAudio.CoreAudioApi;

namespace digital_wellbeing_app.Services
{
    public class SoundMonitoringService : IDisposable
    {
        private readonly MMDeviceEnumerator _deviceEnumerator;
        private MMDevice? _currentDevice;
        private readonly DispatcherTimer _dispatcherTimer;
        private readonly SoundExposureManager _exposureManager;
        private string? _lastDeviceId;
        private bool _hasAudioDevice;

        public SoundMonitoringService()
        {
            _exposureManager = new SoundExposureManager();
            _exposureManager.OnThresholdExceeded += (_, __) =>
            {
                System.Windows.MessageBox.Show(
                    "You have been listening above the threshold.\n" +
                    "Lower your volume to protect your hearing.",
                    "Hearing Alert",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
            };

            _deviceEnumerator = new MMDeviceEnumerator();
            
            // Try to get default audio device (may not exist)
            try
            {
                _currentDevice = _deviceEnumerator.GetDefaultAudioEndpoint(
                    DataFlow.Render, Role.Multimedia);
                _lastDeviceId = _currentDevice.ID;
                SubscribeToDevice(_currentDevice);
                _hasAudioDevice = true;
            }
            catch (COMException)
            {
                // No audio device available
                _currentDevice = null;
                _lastDeviceId = null;
                _hasAudioDevice = false;
            }

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
            if (_currentDevice == null) return;

            try
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
            catch (COMException)
            {
                // Device disconnected, ignore
            }
        }

        private void OnDispatcherTimerTick(object? sender, EventArgs e)
        {
            try
            {
                MMDevice? defaultDevice = null;
                try
                {
                    defaultDevice = _deviceEnumerator.GetDefaultAudioEndpoint(
                        DataFlow.Render, Role.Multimedia);
                }
                catch (COMException)
                {
                    // No audio device available
                    if (_hasAudioDevice && _currentDevice != null)
                    {
                        // Device was just disconnected
                        UnsubscribeFromDevice(_currentDevice);
                        _currentDevice = null;
                        _lastDeviceId = null;
                        _hasAudioDevice = false;
                    }
                    return;
                }

                if (defaultDevice == null) return;

                // Check if device changed
                if (defaultDevice.ID != _lastDeviceId)
                {
                    if (_currentDevice != null)
                    {
                        UnsubscribeFromDevice(_currentDevice);
                        _exposureManager.HandleDeviceChange(
                            _currentDevice.FriendlyName,
                            IdentifyDeviceType(_currentDevice.FriendlyName)
                        );
                    }
                    _currentDevice = defaultDevice;
                    _lastDeviceId = _currentDevice.ID;
                    SubscribeToDevice(_currentDevice);
                    _hasAudioDevice = true;
                }

                if (_currentDevice == null) return;

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
            catch (COMException)
            {
                // Device disconnected mid-operation, will retry on next tick
                _currentDevice = null;
                _lastDeviceId = null;
                _hasAudioDevice = false;
            }
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

            if (_currentDevice != null)
            {
                try
                {
                    UnsubscribeFromDevice(_currentDevice);
                    _exposureManager.HandleDeviceChange(
                        _currentDevice.FriendlyName,
                        IdentifyDeviceType(_currentDevice.FriendlyName)
                    );
                }
                catch (COMException)
                {
                    // Device already disconnected, ignore
                }
            }

            _deviceEnumerator.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
