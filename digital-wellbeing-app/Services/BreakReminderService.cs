using System;
using digital_wellbeing_app.Models;
using digital_wellbeing_app.Platform.Windows;

namespace digital_wellbeing_app.Services
{
    /// <summary>
    /// Service for managing break reminders (20-20-20 rule)
    /// Fires notifications at configurable intervals to remind users to take breaks
    /// </summary>
    public class BreakReminderService : IDisposable
    {
        private readonly System.Timers.Timer _breakTimer;
        private DateTime _lastBreakTime;
        private DateTime? _snoozeUntil = null;
        private bool _isDisposed = false;
        private bool _breakPending = false;  // Prevents duplicate notifications

        /// <summary>Fired when a break is due</summary>
        public event Action? BreakDue;
        
        /// <summary>Whether a break notification is currently pending (waiting for user response)</summary>
        public bool IsBreakPending => _breakPending;
        
        /// <summary>Fired when a break is dismissed</summary>
        public event Action? BreakDismissed;

        /// <summary>Interval between breaks in minutes (default: 20)</summary>
        public int IntervalMinutes { get; set; } = 20;
        
        /// <summary>TEST MODE: Use seconds instead of minutes for quick testing. Set to false for production.</summary>
        public bool TestMode { get; set; } = false;  // PRODUCTION MODE - Do not change for releases
        
        /// <summary>Whether break reminders are enabled</summary>
        public bool IsEnabled { get; set; } = false;
        
        /// <summary>Whether to play sound on break notification</summary>
        public bool SoundEnabled { get; set; } = true;
        
        /// <summary>Idle time threshold (in minutes) that counts as a break</summary>
        public int IdleThresholdMinutes { get; set; } = 5;
        
        /// <summary>Number of times user has snoozed current break</summary>
        public int SnoozeCount { get; private set; } = 0;
        
        /// <summary>Maximum snoozes before forcing a decision</summary>
        public int MaxSnoozeCount { get; set; } = 3;
        
        /// <summary>Time until next break</summary>
        public TimeSpan TimeUntilNextBreak
        {
            get
            {
                if (_snoozeUntil.HasValue && _snoozeUntil > DateTime.Now)
                    return _snoozeUntil.Value - DateTime.Now;
                
                var nextBreakTime = _lastBreakTime.AddMinutes(IntervalMinutes);
                var remaining = nextBreakTime - DateTime.Now;
                return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
            }
        }

        public BreakReminderService()
        {
            _lastBreakTime = DateTime.Now;
            
            // Check every 30 seconds in production (reduces CPU usage)
            _breakTimer = new System.Timers.Timer(30_000)
            {
                AutoReset = true
            };
            _breakTimer.Elapsed += OnTimerElapsed;
        }

        /// <summary>
        /// Start the break reminder service
        /// </summary>
        /// <param name="resetState">If true, reset timer and pending state (used on app start).
        /// If false, preserve state (used when reloading settings).</param>
        public void Start(bool resetState = true)
        {
            if (!IsEnabled)
                return;

            if (resetState)
            {
                _lastBreakTime = DateTime.Now;
                _snoozeUntil = null;
                _breakPending = false;  // Clear any pending state
            }
            _breakTimer.Start();
        }

        /// <summary>
        /// Stop the break reminder service
        /// </summary>
        public void Stop()
        {
            _breakTimer.Stop();
        }

        /// <summary>
        /// Snooze the current break reminder for specified minutes
        /// </summary>
        /// <param name="minutes">Minutes to snooze (default: 5)</param>
        /// <returns>True if snooze was allowed, false if max snoozes reached</returns>
        public bool Snooze(int minutes = 5)
        {
            // Check snooze limit
            if (SnoozeCount >= MaxSnoozeCount)
            {
                // Max snoozes reached - force dismiss instead
                Dismiss();
                return false;
            }
            
            SnoozeCount++;
            _breakPending = false;  // Clear pending state
            
            // In test mode, snooze uses seconds instead of minutes
            if (TestMode)
            {
                _snoozeUntil = DateTime.Now.AddSeconds(minutes);
            }
            else
            {
                _snoozeUntil = DateTime.Now.AddMinutes(minutes);
            }
            
            BreakDismissed?.Invoke();
            return true;
        }
        
        /// <summary>
        /// Check if snooze is still available
        /// </summary>
        public bool CanSnooze => SnoozeCount < MaxSnoozeCount;

        /// <summary>
        /// Dismiss the current break and reset timer
        /// </summary>
        public void Dismiss()
        {
            _breakPending = false;  // Clear pending state
            _lastBreakTime = DateTime.Now;  // Reset timer to start fresh
            _snoozeUntil = null;
            SnoozeCount = 0;  // Reset snooze counter for next break
            BreakDismissed?.Invoke();
        }

        /// <summary>
        /// Load settings from SettingsService and apply them
        /// </summary>
        public void LoadSettings()
        {
            var settingsService = new SettingsService();
            
            // Load from JSON settings
            IsEnabled = LoadBoolSetting(settingsService, BreakReminderKeys.IsEnabled, false);
            IntervalMinutes = LoadIntSetting(settingsService, BreakReminderKeys.IntervalMinutes, 20);
            SoundEnabled = LoadBoolSetting(settingsService, BreakReminderKeys.SoundEnabled, true);
        }

        /// <summary>
        /// Save current settings to SettingsService
        /// </summary>
        public void SaveSettings()
        {
            var settingsService = new SettingsService();
            settingsService.SaveBreakReminderEnabled(IsEnabled);
            settingsService.SaveBreakReminderInterval(IntervalMinutes);
            settingsService.SaveBreakReminderSound(SoundEnabled);
        }

        private void OnTimerElapsed(object? sender, System.Timers.ElapsedEventArgs e)
        {
            if (!IsEnabled)
                return;

            // Don't fire new notification if one is already pending
            if (_breakPending)
                return;

            // Check if currently snoozed
            if (_snoozeUntil.HasValue && DateTime.Now < _snoozeUntil.Value)
                return;

            // Check if user has been idle - idle time counts as a break!
            var idleThresholdSeconds = TestMode ? 20 : IdleThresholdMinutes * 60;
            if (WindowsIdleTimeHelper.IsUserIdle(idleThresholdSeconds))
            {
                // User is idle - reset timer, they're taking a natural break
                _lastBreakTime = DateTime.Now;
                _snoozeUntil = null;
                SnoozeCount = 0;
                _breakPending = false;
                return;
            }

            // Check if break interval has passed
            var timeSinceLastBreak = DateTime.Now - _lastBreakTime;
            
            // TEST MODE: Use seconds instead of minutes
            bool intervalPassed = TestMode 
                ? timeSinceLastBreak.TotalSeconds >= IntervalMinutes  // In test mode, IntervalMinutes = seconds
                : timeSinceLastBreak.TotalMinutes >= IntervalMinutes;
            
            if (intervalPassed)
            {
                // Mark break as pending - prevents duplicate notifications
                _breakPending = true;
                
                // Trigger break notification
                BreakDue?.Invoke();
            }
        }

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

        public void Dispose()
        {
            if (_isDisposed)
                return;

            _breakTimer?.Stop();
            _breakTimer?.Dispose();
            _isDisposed = true;
            GC.SuppressFinalize(this);
        }
    }
}

