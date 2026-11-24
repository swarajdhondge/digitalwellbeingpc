using System;
using digital_wellbeing_app.Helpers;
using digital_wellbeing_app.Models;

namespace digital_wellbeing_app.Services
{
    /// <summary>
    /// Service for managing and tracking screen time goals.
    /// </summary>
    public class GoalService
    {
        private static int? _cachedGoal;
        private static bool _cacheValid;

        /// <summary>
        /// Static event fired when the goal is changed from any GoalService instance.
        /// Subscribers can use this to refresh their goal state.
        /// </summary>
        public static event EventHandler? GoalChanged;

        /// <summary>
        /// Gets the daily screen time goal in minutes.
        /// Returns null if no goal is set.
        /// </summary>
        public int? GetDailyScreenTimeGoal()
        {
            if (_cacheValid)
                return _cachedGoal;

            var db = DatabaseService.GetConnection();
            var setting = db.Table<UserSettings>()
                           .FirstOrDefault(x => x.Key == SettingsKeys.ScreenTimeGoal);

            if (setting != null && int.TryParse(setting.Value, out int minutes) && minutes > 0)
            {
                _cachedGoal = minutes;
            }
            else
            {
                _cachedGoal = null;
            }
            
            _cacheValid = true;
            return _cachedGoal;
        }

        /// <summary>
        /// Sets the daily screen time goal.
        /// </summary>
        /// <param name="minutes">Goal in minutes. Pass null or 0 to disable.</param>
        public void SetDailyScreenTimeGoal(int? minutes)
        {
            var db = DatabaseService.GetConnection();
            var setting = db.Table<UserSettings>()
                           .FirstOrDefault(x => x.Key == SettingsKeys.ScreenTimeGoal);

            if (minutes == null || minutes <= 0)
            {
                // Remove goal
                if (setting != null)
                    db.Delete(setting);
                _cachedGoal = null;
            }
            else
            {
                // Clamp to valid range: 1 minute to 24 hours
                minutes = Math.Clamp(minutes.Value, 1, 1440);

                if (setting == null)
                {
                    setting = new UserSettings { Key = SettingsKeys.ScreenTimeGoal };
                    setting.Value = minutes.Value.ToString();
                    db.Insert(setting);
                }
                else
                {
                    setting.Value = minutes.Value.ToString();
                    db.Update(setting);
                }
                _cachedGoal = minutes;
            }
            
            _cacheValid = true;

            // A new goal starts over: the reminder can fire again for it today
            DeleteSetting(SettingsKeys.GoalLastReminder);

            // Notify all subscribers that the goal has changed
            GoalChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>Default for "remind again": once a day.</summary>
        public const int DefaultRemindEveryMinutes = 0;

        /// <summary>Whether Pulse tells the user when the goal is reached. Default on.</summary>
        public bool GoalNotificationsEnabled
        {
            get => GetSetting(SettingsKeys.GoalNotificationsEnabled) != bool.FalseString;
            set => SetSetting(SettingsKeys.GoalNotificationsEnabled, value.ToString());
        }

        /// <summary>Minutes between reminders once the goal is passed; 0 means once a day.</summary>
        public int RemindEveryMinutes
        {
            get => int.TryParse(GetSetting(SettingsKeys.GoalRemindEveryMinutes), out var m) && m >= 0
                ? m : DefaultRemindEveryMinutes;
            set => SetSetting(SettingsKeys.GoalRemindEveryMinutes, Math.Max(0, value).ToString());
        }

        /// <summary>When the reminder last fired, or null if it has not fired since the goal was set.</summary>
        public DateTime? LastReminder =>
            DateTime.TryParse(GetSetting(SettingsKeys.GoalLastReminder), null,
                System.Globalization.DateTimeStyles.RoundtripKind, out var t) ? t : null;

        /// <summary>Saves the time of a reminder, so a restart does not repeat it.</summary>
        public void MarkReminded(DateTime now) =>
            SetSetting(SettingsKeys.GoalLastReminder, now.ToString("o"));

        /// <summary>
        /// Whether a goal reminder should show now, given the user has passed the goal. A new day
        /// always starts over; within a day the reminder repeats as often as the user chose
        /// (0 = only the first time that day).
        /// </summary>
        public static bool IsReminderDue(bool enabled, int remindEveryMinutes, DateTime? lastReminder, DateTime now)
        {
            if (!enabled) return false;
            if (lastReminder == null || lastReminder.Value.Date != now.Date) return true;
            return remindEveryMinutes > 0
                && now - lastReminder.Value >= TimeSpan.FromMinutes(remindEveryMinutes);
        }

        private static string? GetSetting(string key) =>
            DatabaseService.GetConnection().Table<UserSettings>().FirstOrDefault(x => x.Key == key)?.Value;

        private static void SetSetting(string key, string value) =>
            DatabaseService.GetConnection().InsertOrReplace(new UserSettings { Key = key, Value = value });

        private static void DeleteSetting(string key) =>
            DatabaseService.GetConnection().Delete<UserSettings>(key);

        /// <summary>
        /// Gets the current progress toward the daily goal.
        /// </summary>
        /// <param name="currentTime">Current screen time today</param>
        /// <returns>Progress as 0.0 to 1.0+. Returns 0 if no goal is set.</returns>
        public double GetGoalProgress(TimeSpan currentTime)
        {
            var goal = GetDailyScreenTimeGoal();
            if (goal == null || goal <= 0)
                return 0;

            return currentTime.TotalMinutes / goal.Value;
        }

        /// <summary>
        /// Gets how much time is remaining until goal is reached.
        /// </summary>
        /// <param name="currentTime">Current screen time today</param>
        /// <returns>Remaining time (negative if over goal). Null if no goal set.</returns>
        public TimeSpan? GetTimeRemaining(TimeSpan currentTime)
        {
            var goal = GetDailyScreenTimeGoal();
            if (goal == null)
                return null;

            var goalTime = TimeSpan.FromMinutes(goal.Value);
            return goalTime - currentTime;
        }

        /// <summary>
        /// Checks if the user has exceeded their goal.
        /// </summary>
        public bool IsOverGoal(TimeSpan currentTime)
        {
            var goal = GetDailyScreenTimeGoal();
            if (goal == null)
                return false;

            return currentTime.TotalMinutes > goal.Value;
        }

        /// <summary>
        /// Formats the goal progress for display.
        /// </summary>
        public string FormatProgressText(TimeSpan currentTime)
        {
            var goal = GetDailyScreenTimeGoal();
            if (goal == null)
                return string.Empty;

            var progress = GetGoalProgress(currentTime);
            var percent = (int)(progress * 100);
            var goalText = TimeFormatHelper.FormatCompact(TimeSpan.FromMinutes(goal.Value));

            if (progress > 1.0)
            {
                var overTime = currentTime - TimeSpan.FromMinutes(goal.Value);
                return Loc.Format("Goal_ProgressOver", percent, TimeFormatHelper.FormatCompact(overTime));
            }

            return Loc.Format("Goal_ProgressOf", percent, goalText);
        }

        /// <summary>
        /// Invalidates the cache (call when settings might have changed externally)
        /// </summary>
        public void InvalidateCache()
        {
            _cacheValid = false;
        }
    }
}

