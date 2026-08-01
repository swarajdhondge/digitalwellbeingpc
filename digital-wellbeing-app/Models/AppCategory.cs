using System;
using SQLite;

namespace digital_wellbeing_app.Models
{
    /// <summary>
    /// Categories for app classification in Focus Mode
    /// </summary>
    public enum AppCategoryType
    {
        Uncategorized = 0,
        Work = 1,
        Entertainment = 2
    }

    /// <summary>
    /// Where an AppCategory row's Category value came from. Manual always wins - an
    /// AutoSuggested row is a starting point the user hasn't confirmed or overridden yet.
    /// </summary>
    public enum CategorySource
    {
        Manual = 0,
        AutoSuggested = 1
    }

    /// <summary>
    /// Model for storing per-app category assignments
    /// </summary>
    [Table("AppCategory")]
    public class AppCategory
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        /// <summary>
        /// Unique identifier for the app (executable path or process name)
        /// </summary>
        [Indexed]
        public string AppIdentifier { get; set; } = string.Empty;

        /// <summary>
        /// Display name of the app
        /// </summary>
        public string AppName { get; set; } = string.Empty;

        /// <summary>
        /// Full path to the executable (for icon extraction)
        /// </summary>
        public string ExecutablePath { get; set; } = string.Empty;

        /// <summary>
        /// The category assigned to this app
        /// </summary>
        public AppCategoryType Category { get; set; } = AppCategoryType.Uncategorized;

        /// <summary>
        /// Whether Category was picked by the user or auto-suggested from a known-app rule.
        /// </summary>
        public CategorySource Source { get; set; } = CategorySource.Manual;

        /// <summary>
        /// When the category was last updated
        /// </summary>
        public DateTime LastUpdated { get; set; } = DateTime.Now;
    }
}

