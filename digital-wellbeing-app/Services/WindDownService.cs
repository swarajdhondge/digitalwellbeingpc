using System;
using digital_wellbeing_app.Models;

namespace digital_wellbeing_app.Services
{
    /// <summary>
    /// Service for managing Wind Down mode - subtle end-of-day awareness.
    /// Provides scheduled quiet hours with visual cues and optional notifications.
    /// Non-blocking: This feature only provides awareness, never blocks anything.
    /// </summary>
    public class WindDownService : IDisposable
    {
        private readonly System.Timers.Timer _checkTimer;
        private bool _isDisposed = false;
        private bool _wasActive = false;  // Track state to fire events only on transitions
        private bool _notificationShownThisSession = false;  // Prevent repeated notifications
        private DateTime _lastNotificationTime = DateTime.MinValue;  // Throttle notifications

        /// <summary>Fired when Wind Down mode becomes active</summary>
        public event Action? WindDownStarted;

        /// <summary>Fired when Wind Down mode ends</summary>
        public event Action? WindDownEnded;

        /// <summary>Fired every check interval while Wind Down is active (for UI updates)</summary>
        public event Action? WindDownTick;

        // Settings
        public bool IsEnabled { get; set; } = false;
        public int StartHour { get; set; } = 21;  // 9 PM
        public int StartMinute { get; set; } = 0;
        public int EndHour { get; set; } = 7;     // 7 AM
        public int EndMinute { get; set; } = 0;
        public bool ShowNotification { get; set; } = true;
        public bool ShowVisualCue { get; set; } = true;
        public WindDownVisualStyle VisualStyle { get; set; } = WindDownVisualStyle.Amber;
        public double VisualOpacity { get; set; } = 0.3;

        /// <summary>
        /// Whether Wind Down mode is currently active based on the schedule
        /// </summary>
        public bool IsWindDownActive
        {
            get
            {
                if (!IsEnabled) return false;
                return IsTimeInWindDownPeriod(DateTime.Now);
            }
        }

        /// <summary>
        /// Time remaining until Wind Down ends (if active) or starts (if not active)
        /// </summary>
        public TimeSpan TimeUntilTransition
        {
            get
            {
                var now = DateTime.Now;
                if (IsWindDownActive)
                {
                    // Time until Wind Down ends
                    return GetTimeUntilEnd(now);
                }
                else
                {
                    // Time until Wind Down starts
                    return GetTimeUntilStart(now);
                }
            }
        }

        public WindDownService()
        {
            // Check every 30 seconds for time-based transitions
            _checkTimer = new System.Timers.Timer(30_000)
            {
                AutoReset = true
            };
            _checkTimer.Elapsed += OnTimerElapsed;
        }

        /// <summary>
        /// Start the Wind Down service
        /// </summary>
        /// <param name="resetNotification">If true, reset notification flag (used on app start). 
        /// If false, preserve notification state (used when reloading settings).</param>
        public void Start(bool resetNotification = true)
        {
            _wasActive = IsWindDownActive;
            if (resetNotification)
            {
                _notificationShownThisSession = false;
            }
            _checkTimer.Start();

            // If already in Wind Down period when starting, fire the event
            // But only if this is a fresh start, not a settings reload
            if (_wasActive && IsEnabled && resetNotification)
            {
                WindDownStarted?.Invoke();
            }
        }

        /// <summary>
        /// Stop the Wind Down service
        /// </summary>
        public void Stop()
        {
            _checkTimer.Stop();
            
            // If we were active, notify that we're ending
            if (_wasActive)
            {
                _wasActive = false;
                WindDownEnded?.Invoke();
            }
        }

        /// <summary>
        /// Load settings from SettingsService
        /// </summary>
        public void LoadSettings()
        {
            var settingsService = new SettingsService();
            
            IsEnabled = LoadBoolSetting(settingsService, WindDownKeys.IsEnabled, false);
            StartHour = LoadIntSetting(settingsService, WindDownKeys.StartHour, 21);
            StartMinute = LoadIntSetting(settingsService, WindDownKeys.StartMinute, 0);
            EndHour = LoadIntSetting(settingsService, WindDownKeys.EndHour, 7);
            EndMinute = LoadIntSetting(settingsService, WindDownKeys.EndMinute, 0);
            ShowNotification = LoadBoolSetting(settingsService, WindDownKeys.ShowNotification, true);
            ShowVisualCue = LoadBoolSetting(settingsService, WindDownKeys.ShowVisualCue, true);
            VisualStyle = (WindDownVisualStyle)LoadIntSetting(settingsService, WindDownKeys.VisualStyle, 0);
            VisualOpacity = LoadDoubleSetting(settingsService, WindDownKeys.VisualOpacity, 0.3);

            System.Diagnostics.Debug.WriteLine($"[WindDown] Settings loaded: enabled={IsEnabled}, schedule={StartHour}:{StartMinute:D2}-{EndHour}:{EndMinute:D2}, showVisual={ShowVisualCue}, showNotif={ShowNotification}");
        }

        /// <summary>
        /// Save current settings to SettingsService
        /// </summary>
        public void SaveSettings()
        {
            var settingsService = new SettingsService();
            settingsService.SaveWindDownEnabled(IsEnabled);
            settingsService.SaveWindDownStartTime(StartHour, StartMinute);
            settingsService.SaveWindDownEndTime(EndHour, EndMinute);
            settingsService.SaveWindDownShowNotification(ShowNotification);
            settingsService.SaveWindDownShowVisualCue(ShowVisualCue);
            settingsService.SaveWindDownVisualStyle((int)VisualStyle);
            settingsService.SaveWindDownVisualOpacity(VisualOpacity);
        }

        /// <summary>
        /// Get a formatted string of the schedule
        /// </summary>
        public string GetScheduleText()
        {
            var startTime = new TimeSpan(StartHour, StartMinute, 0);
            var endTime = new TimeSpan(EndHour, EndMinute, 0);
            
            return $"{FormatTime(startTime)} - {FormatTime(endTime)}";
        }

        private string FormatTime(TimeSpan time)
        {
            var hours = time.Hours;
            var minutes = time.Minutes;
            var ampm = hours >= 12 ? "PM" : "AM";
            var displayHours = hours > 12 ? hours - 12 : (hours == 0 ? 12 : hours);
            return minutes > 0 ? $"{displayHours}:{minutes:D2} {ampm}" : $"{displayHours} {ampm}";
        }

        private void OnTimerElapsed(object? sender, System.Timers.ElapsedEventArgs e)
        {
            if (!IsEnabled) return;

            bool isNowActive = IsWindDownActive;

            // Check for state transitions
            if (isNowActive && !_wasActive)
            {
                // Just entered Wind Down period
                _wasActive = true;
                // Don't reset notification flag here - let HasNotificationBeenShown handle throttling
                WindDownStarted?.Invoke();
            }
            else if (!isNowActive && _wasActive)
            {
                // Just exited Wind Down period
                _wasActive = false;
                // Reset notification flag when Wind Down ends, so next session can show notification
                _notificationShownThisSession = false;
                WindDownEnded?.Invoke();
            }
            else if (isNowActive)
            {
                // Still in Wind Down - fire tick for UI updates
                WindDownTick?.Invoke();
            }
        }

        /// <summary>
        /// Check if the given time falls within the Wind Down period.
        /// Handles overnight schedules (e.g., 9 PM to 7 AM).
        /// </summary>
        private bool IsTimeInWindDownPeriod(DateTime time)
        {
            var currentMinutes = time.Hour * 60 + time.Minute;
            var startMinutes = StartHour * 60 + StartMinute;
            var endMinutes = EndHour * 60 + EndMinute;

            bool isActive;
            string scheduleType;
            
            if (startMinutes <= endMinutes)
            {
                // Same day schedule (e.g., 9 AM to 5 PM)
                isActive = currentMinutes >= startMinutes && currentMinutes < endMinutes;
                scheduleType = "same-day";
            }
            else
            {
                // Overnight schedule (e.g., 9 PM to 7 AM)
                isActive = currentMinutes >= startMinutes || currentMinutes < endMinutes;
                scheduleType = "overnight";
            }

            System.Diagnostics.Debug.WriteLine($"[WindDown] Time check: now={time:HH:mm} ({currentMinutes}min), schedule={StartHour}:{StartMinute:D2}-{EndHour}:{EndMinute:D2} ({scheduleType}), active={isActive}");
            
            return isActive;
        }

        private TimeSpan GetTimeUntilStart(DateTime now)
        {
            var today = now.Date;
            var startTime = today.AddHours(StartHour).AddMinutes(StartMinute);
            
            if (startTime <= now)
            {
                // Start time has passed today, get tomorrow's start
                startTime = startTime.AddDays(1);
            }
            
            return startTime - now;
        }

        private TimeSpan GetTimeUntilEnd(DateTime now)
        {
            var today = now.Date;
            var endTime = today.AddHours(EndHour).AddMinutes(EndMinute);
            
            // For overnight schedules, end time is the next day
            if (EndHour * 60 + EndMinute <= StartHour * 60 + StartMinute)
            {
                // Overnight schedule - if we're before midnight, end is tomorrow
                if (now.Hour >= StartHour || now.Hour < EndHour)
                {
                    if (now.Hour >= StartHour)
                    {
                        endTime = today.AddDays(1).AddHours(EndHour).AddMinutes(EndMinute);
                    }
                }
            }
            
            if (endTime <= now)
            {
                endTime = endTime.AddDays(1);
            }
            
            return endTime - now;
        }

        /// <summary>
        /// Check if notification has been shown this Wind Down session.
        /// Also prevents showing notification more than once per hour.
        /// </summary>
        public bool HasNotificationBeenShown
        {
            get
            {
                // Prevent notification if already shown this session
                if (_notificationShownThisSession) return true;
                
                // Also prevent notification if shown within the last hour (safety throttle)
                if ((DateTime.Now - _lastNotificationTime).TotalMinutes < 60) return true;
                
                return false;
            }
        }

        /// <summary>
        /// Mark that the notification has been shown for this Wind Down session
        /// </summary>
        public void MarkNotificationShown()
        {
            _notificationShownThisSession = true;
            _lastNotificationTime = DateTime.Now;
        }

        #region Settings Helpers

        private bool LoadBoolSetting(SettingsService service, string key, bool defaultValue)
        {
            try
            {
                var dict = service.GetAllSettings();
                if (dict.TryGetValue(key, out var value))
                {
                    if (value is bool b)
                        return b;
                    if (value is System.Text.Json.JsonElement je && je.ValueKind == System.Text.Json.JsonValueKind.True)
                        return true;
                    if (value is System.Text.Json.JsonElement je2 && je2.ValueKind == System.Text.Json.JsonValueKind.False)
                        return false;
                }
            }
            catch { }
            return defaultValue;
        }

        private int LoadIntSetting(SettingsService service, string key, int defaultValue)
        {
            try
            {
                var dict = service.GetAllSettings();
                if (dict.TryGetValue(key, out var value))
                {
                    if (value is int i)
                        return i;
                    if (value is System.Text.Json.JsonElement je && je.TryGetInt32(out var intVal))
                        return intVal;
                }
            }
            catch { }
            return defaultValue;
        }

        private double LoadDoubleSetting(SettingsService service, string key, double defaultValue)
        {
            try
            {
                var dict = service.GetAllSettings();
                if (dict.TryGetValue(key, out var value))
                {
                    if (value is double d)
                        return d;
                    if (value is System.Text.Json.JsonElement je && je.TryGetDouble(out var dVal))
                        return dVal;
                }
            }
            catch { }
            return defaultValue;
        }

        #endregion

        public void Dispose()
        {
            if (_isDisposed) return;

            _checkTimer?.Stop();
            _checkTimer?.Dispose();
            _isDisposed = true;
            GC.SuppressFinalize(this);
        }
    }
}

