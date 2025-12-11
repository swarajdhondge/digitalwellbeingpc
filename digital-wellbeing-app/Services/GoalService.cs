using System;
using digital_wellbeing_app.Models;

namespace digital_wellbeing_app.Services
{
    /// <summary>
    /// Service for managing and tracking screen time goals.
    /// </summary>
    public class GoalService
    {
        private int? _cachedGoal;
        private bool _cacheValid;

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
            
            // Notify all subscribers that the goal has changed
            GoalChanged?.Invoke(this, EventArgs.Empty);
        }

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
            var goalHours = goal.Value / 60;
            var goalMins = goal.Value % 60;

            string goalText = goalMins > 0 
                ? $"{goalHours}h {goalMins}m" 
                : $"{goalHours}h";

            if (progress > 1.0)
            {
                var overTime = currentTime - TimeSpan.FromMinutes(goal.Value);
                var overHours = (int)overTime.TotalHours;
                var overMins = overTime.Minutes;
                string overText = overHours > 0 
                    ? $"{overHours}h {overMins}m" 
                    : $"{overMins}m";
                return $"{percent}% - Over goal by {overText}";
            }

            return $"{percent}% of {goalText} goal";
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

