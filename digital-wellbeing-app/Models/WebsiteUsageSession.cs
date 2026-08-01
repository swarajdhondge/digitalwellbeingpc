using System;
using SQLite;

namespace digital_wellbeing_app.Models
{
    /// <summary>
    /// A contiguous stretch of time a hostname was showing in a browser's address bar while that
    /// browser was the foreground window. Hostname only - never a full URL, path, or query string.
    /// Opt-in (see SettingsService.LoadWebsiteTrackingEnabled), off by default.
    /// </summary>
    [Table("WebsiteUsageSession")]
    public class WebsiteUsageSession
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        /// <summary>Process name of the browser (e.g. "chrome", "msedge", "firefox").</summary>
        public string BrowserProcessName { get; set; } = string.Empty;

        /// <summary>Registrable domain only (e.g. "youtube.com", "docs.google.com") - see
        /// BrowserTabInspector.ExtractHostname. Never a path, query string, or full URL.</summary>
        [Indexed]
        public string Hostname { get; set; } = string.Empty;

        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }

        [Ignore]
        public TimeSpan Duration => EndTime - StartTime;
    }
}
