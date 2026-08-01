using System;
using System.Runtime.InteropServices;
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
        private DateTime _lastVolumeSampleTime = DateTime.Now;

        public SoundMonitoringService(SoundExposureManager exposureManager)
        {
            _exposureManager = exposureManager;
            // Threshold-exceeded notification is wired from MainWindow (tray-balloon pattern,
            // matching Goal/WindDown/Break) rather than here - this constructor used to show a
            // synchronous, blocking MessageBox.Show, the only alert in the app that worked that
            // way instead of a balloon.

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
                    peak,
                    ConsumeElapsedSinceLastSample()
                );
            }
            catch (COMException)
            {
                // Device disconnected, ignore
            }
        }

        /// <summary>
        /// Real elapsed time since the last volume sample, whichever caller (the 10s periodic tick
        /// or this ad-hoc volume-change event) triggered it last - see
        /// SoundExposureManager.HandleVolumeChange for why this must be measured, not assumed.
        /// Clamped because, unlike ScreenTracker/AppTracker/WebsiteUsageSvc, this service isn't
        /// paused on system lock/sleep - an unclamped gap after waking from sleep would otherwise
        /// be misread as that many minutes of continuous (and possibly "harmful") listening.
        /// </summary>
        private TimeSpan ConsumeElapsedSinceLastSample()
        {
            var now = DateTime.Now;
            var elapsed = now - _lastVolumeSampleTime;
            _lastVolumeSampleTime = now;

            return elapsed > TimeSpan.Zero && elapsed <= TimeSpan.FromSeconds(30)
                ? elapsed
                : TimeSpan.FromSeconds(10); // fall back to the nominal tick interval
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
                    peakVal,
                    ConsumeElapsedSinceLastSample()
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

        /// <summary>
        /// A manual Settings override (SettingsService.LoadDeviceTypeOverride) always wins over
        /// the friendly-name guess below - the guess is inherently unreliable for anything OEMs
        /// don't literally name "headphone"/"earphone"/etc.
        /// </summary>
        private static string IdentifyDeviceType(string friendlyName)
        {
            var overrideType = new SettingsService().LoadDeviceTypeOverride();
            if (!string.IsNullOrEmpty(overrideType)) return overrideType;

            string name = friendlyName.ToLowerInvariant();

            if (name.Contains("headphone") || name.Contains("beats") ||
                name.Contains(" wh-") || name.StartsWith("wh-"))
                return "Headphones";

            if (name.Contains("earphone") || name.Contains("earbud") || name.Contains("airpods") ||
                name.Contains("buds") || name.Contains(" wf-") || name.StartsWith("wf-"))
                return "Earphones";

            if (name.Contains("headset"))
                return "Headsets";

            if (name.Contains("speaker") || name.Contains("soundbar"))
                return "Speakers";

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
