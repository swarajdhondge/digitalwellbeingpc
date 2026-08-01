using System;
using System.Collections.Generic;
using digital_wellbeing_app.CoreLogic;
using digital_wellbeing_app.Helpers;
using digital_wellbeing_app.Models;
using digital_wellbeing_app.Platform.Windows;

namespace digital_wellbeing_app.Services
{
    /// <summary>
    /// Enforces per-app daily time caps and time-of-day restrictions. Independent of Focus
    /// Sessions - a limit applies all the time, not just while a session is running.
    ///
    /// Checked immediately when the foreground app changes and every 30 seconds while it remains
    /// open, so a daily cap is enforced without requiring the user to switch away and back. The
    /// low-frequency timer adds negligible load and includes the not-yet-persisted live segment.
    /// </summary>
    public class AppLimitService : IDisposable
    {
        private readonly AppUsageTracker _appTracker;
        private readonly Dictionary<string, AppLimit> _limits = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _appsFailedToBlock = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, DateTime> _lastActionTime = new(StringComparer.OrdinalIgnoreCase);
        private readonly System.Timers.Timer _limitCheckTimer = new(30_000) { AutoReset = true };
        private static readonly TimeSpan ActionCooldown = TimeSpan.FromSeconds(30);
        private bool _isDisposed;
        private bool _isStarted;

        /// <summary>Fired when a limited app is detected over its cap/schedule.</summary>
        public event Action<string, string, FocusEnforcementLevel>? LimitReached; // appName, executablePath, level

        public AppLimitService(AppUsageTracker appTracker)
        {
            _appTracker = appTracker ?? throw new ArgumentNullException(nameof(appTracker));
            _limitCheckTimer.Elapsed += (_, _) => CheckCurrentApp();
        }

        public void Start()
        {
            ReloadLimits();
            if (_isStarted) return;
            _appTracker.OnAppSwitched += OnAppSwitched;
            _limitCheckTimer.Start();
            _isStarted = true;
        }

        public void Stop()
        {
            if (!_isStarted) return;
            _appTracker.OnAppSwitched -= OnAppSwitched;
            _limitCheckTimer.Stop();
            _isStarted = false;
        }

        /// <summary>Re-read limits from the DB. Call after any add/edit/remove from the UI.</summary>
        public void ReloadLimits()
        {
            var fresh = new Dictionary<string, AppLimit>(StringComparer.OrdinalIgnoreCase);
            try
            {
                foreach (var limit in DatabaseService.GetAllAppLimits())
                {
                    if (limit.AppIdentifier.Length > 0)
                        fresh[limit.AppIdentifier] = limit;
                }
            }
            catch (Exception ex)
            {
                LogService.Warning($"AppLimit reload skipped: {ex.Message}");
                return;
            }

            lock (_limits)
            {
                _limits.Clear();
                foreach (var kv in fresh) _limits[kv.Key] = kv.Value;
            }
        }

        private void OnAppSwitched()
            => CheckCurrentApp();

        private void CheckCurrentApp()
        {
            try
            {
                var session = _appTracker.CurrentSession;
                if (session == null) return;

                var key = AppIdentity.NormalizeKey(session.ExecutablePath, session.AppName);
                if (key.Length == 0) return;

                AppLimit? limit;
                lock (_limits)
                {
                    if (!_limits.TryGetValue(key, out limit) || !limit.IsEnabled)
                        return;
                }

                var now = DateTime.Now;
                var liveSeconds = session.StartTime.Date == now.Date && now > session.StartTime
                    ? (int)(now - session.StartTime).TotalSeconds
                    : 0;
                if (!IsLimitExceeded(limit, now, liveSeconds)) return;

                if (_lastActionTime.TryGetValue(key, out var last) && DateTime.Now - last < ActionCooldown)
                    return;
                _lastActionTime[key] = DateTime.Now;

                if (limit.EnforcementLevel != FocusEnforcementLevel.Warn && !_appsFailedToBlock.Contains(key))
                {
                    var handle = NativeMethods.GetForegroundWindow();
                    if (handle != IntPtr.Zero && !WindowEnforcement.TryMinimizeWindow(handle))
                        _appsFailedToBlock.Add(key);
                }

                LimitReached?.Invoke(session.AppName, session.ExecutablePath, limit.EnforcementLevel);
            }
            catch (Exception ex)
            {
                LogService.Warning($"AppLimit check failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Whether <paramref name="limit"/> is currently exceeded - either its time-of-day
        /// restriction is active, or today's usage has reached its daily cap. Pure function of the
        /// limit row and a point in time (plus, for the daily-cap branch, today's persisted usage),
        /// so it's testable without a live tracker.
        /// </summary>
        public static bool IsLimitExceeded(AppLimit limit, DateTime now, int liveUsageSeconds = 0)
        {
            if (limit.ScheduleEnabled && IsTimeInSchedule(limit, now))
                return true;

            if (limit.DailyLimitMinutes > 0)
            {
                var usedSeconds = DatabaseService.GetAppUsageSecondsForDate(limit.AppIdentifier, now.Date)
                                  + Math.Max(0, liveUsageSeconds);
                if (usedSeconds >= limit.DailyLimitMinutes * 60)
                    return true;
            }

            return false;
        }

        /// <summary>Ported from WindDownService.IsTimeInWindDownPeriod - same overnight-aware math.</summary>
        private static bool IsTimeInSchedule(AppLimit limit, DateTime time)
        {
            var currentMinutes = time.Hour * 60 + time.Minute;
            var startMinutes = limit.ScheduleStartHour * 60 + limit.ScheduleStartMinute;
            var endMinutes = limit.ScheduleEndHour * 60 + limit.ScheduleEndMinute;

            return startMinutes <= endMinutes
                ? currentMinutes >= startMinutes && currentMinutes < endMinutes
                : currentMinutes >= startMinutes || currentMinutes < endMinutes;
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            Stop();
            _limitCheckTimer.Dispose();
            _isDisposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
