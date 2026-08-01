using System;
using System.Collections.Generic;
using System.Linq;
using digital_wellbeing_app.Models;

namespace digital_wellbeing_app.Services
{
    /// <summary>
    /// Traffic-light health status per tracker, built from the heartbeats each tracker writes on
    /// its own throttled cadence. Diagnostic aid for troubleshooting, not a strict alerting system:
    /// a "Stale"/"Down" tracker can be entirely normal right after a screen lock or sleep, since
    /// ScreenTimeTracker/AppUsageTracker pause then (SoundExposureManager currently doesn't - see
    /// the open note in docs/roadmap-handoff.md).
    /// </summary>
    public static class TrackingHealthService
    {
        public enum HealthStatus
        {
            /// <summary>No heartbeat has ever been recorded for this tracker.</summary>
            Unknown,
            Healthy,
            Stale,
            Down
        }

        public record TrackerHealthInfo(string TrackerName, HealthStatus Status, DateTime? LastSeenUtc, long TickCount, string? LastError);

        /// <summary>The three trackers with a general-purpose heartbeat (FocusSessionService keeps
        /// its own separate, session-scoped crash-recovery heartbeat - a different mechanism for a
        /// different purpose, not migrated onto this table).</summary>
        public static readonly IReadOnlyList<string> KnownTrackers = new[]
        {
            "ScreenTimeTracker", "AppUsageTracker", "SoundExposureManager"
        };

        private static readonly TimeSpan HealthyWindow = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan StaleWindow = TimeSpan.FromMinutes(15);

        /// <summary>
        /// Record that <paramref name="trackerName"/> is alive. Callers are expected to throttle
        /// their own call frequency (e.g. ScreenTimeTracker's 1s tick does NOT call this every
        /// tick) - this method itself does no throttling, so it stays a simple, predictable write.
        /// </summary>
        public static void RecordHeartbeat(string trackerName, string? error = null)
        {
            try
            {
                var heartbeat = DatabaseService.GetTrackerHeartbeat(trackerName) ?? new TrackerHeartbeat
                {
                    TrackerName = trackerName
                };

                heartbeat.LastSeenUtc = DateTime.UtcNow;
                heartbeat.TickCount++;
                if (error != null) heartbeat.LastError = error;

                DatabaseService.SaveTrackerHeartbeat(heartbeat);
            }
            catch (Exception ex)
            {
                LogService.Warning($"TrackingHealthService.RecordHeartbeat({trackerName}) failed: {ex.Message}");
            }
        }

        /// <summary>Status for every known tracker, in KnownTrackers order. A tracker with no
        /// heartbeat row yet (e.g. app just started) reports Unknown, not Down.</summary>
        public static List<TrackerHealthInfo> GetHealthStatuses()
        {
            var rows = DatabaseService.GetAllTrackerHeartbeats()
                .ToDictionary(h => h.TrackerName, StringComparer.OrdinalIgnoreCase);
            var now = DateTime.UtcNow;

            return KnownTrackers.Select(name =>
            {
                if (!rows.TryGetValue(name, out var hb))
                    return new TrackerHealthInfo(name, HealthStatus.Unknown, null, 0, null);

                var age = now - hb.LastSeenUtc;
                var status = age <= HealthyWindow ? HealthStatus.Healthy
                           : age <= StaleWindow ? HealthStatus.Stale
                           : HealthStatus.Down;

                return new TrackerHealthInfo(name, status, hb.LastSeenUtc, hb.TickCount, hb.LastError);
            }).ToList();
        }
    }
}
