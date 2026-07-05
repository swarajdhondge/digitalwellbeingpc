using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
using SQLite;
using digital_wellbeing_app.Helpers;
using digital_wellbeing_app.Models;

namespace digital_wellbeing_app.Services
{
    public static class DatabaseService
    {
        private static SQLiteConnection? _database;
        private static readonly object _dbLock = new();

        // folder & filename constants
        private const string AppFolderName = "Pulse";
        private const string DbFileName = "digital_wellbeing.db";

        // Optional override for the DB file path. Used by the test suite to isolate tests from
        // the user's real database; null in normal operation.
        private static string? _dbPathOverride;

        // full path to the DB, under %LocalAppData%
        private static string DbPath
        {
            get
            {
                if (_dbPathOverride != null)
                    return _dbPathOverride;

                var localAppData = Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData);
                var folder = Path.Combine(localAppData, AppFolderName);

                // ensure the folder exists
                if (!Directory.Exists(folder))
                {
                    Directory.CreateDirectory(folder);
                    RestrictDirectoryToCurrentUser(folder);
                }

                return Path.Combine(folder, DbFileName);
            }
        }

        /// <summary>
        /// Redirect the database to a specific file and reset the open connection. Intended for
        /// the test suite so it never touches the user's real database. Passing a path under a
        /// throwaway temp directory gives each test run an isolated, deterministic store.
        /// </summary>
        public static void SetDatabasePathForTesting(string path)
        {
            lock (_dbLock)
            {
                CloseConnection();
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                _dbPathOverride = path;
            }
        }

        /// <summary>
        /// Restricts a directory's ACL to the current user only.
        /// Removes inherited permissions and grants full control to the current user.
        /// </summary>
        private static void RestrictDirectoryToCurrentUser(string directoryPath)
        {
            try
            {
                var dirInfo = new DirectoryInfo(directoryPath);
                var security = dirInfo.GetAccessControl();

                // Disable inheritance and remove all inherited rules
                security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);

                // Remove all existing access rules
                foreach (FileSystemAccessRule rule in security.GetAccessRules(true, true, typeof(SecurityIdentifier)))
                {
                    security.RemoveAccessRule(rule);
                }

                // Add full control for current user only (with inheritance for child objects)
                var currentUser = WindowsIdentity.GetCurrent().User;
                if (currentUser != null)
                {
                    security.AddAccessRule(new FileSystemAccessRule(
                        currentUser,
                        FileSystemRights.FullControl,
                        InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                        PropagationFlags.None,
                        AccessControlType.Allow));
                }

                dirInfo.SetAccessControl(security);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DatabaseService] ACL restriction failed: {ex.Message}");
            }
        }

        public static SQLiteConnection GetConnection()
        {
            if (_database is null)
            {
                lock (_dbLock)
                {
                    if (_database is null)
                    {
                        _database = new SQLiteConnection(DbPath,
                            SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.FullMutex);
                        InitializeTables();
                    }
                }
            }
            return _database;
        }

        /// <summary>
        /// Close and release the database connection. Call on app exit.
        /// </summary>
        public static void CloseConnection()
        {
            lock (_dbLock)
            {
                if (_database != null)
                {
                    try
                    {
                        _database.Close();
                        _database.Dispose();
                    }
                    catch { }
                    _database = null;
                }
            }
        }

        private static void InitializeTables()
        {
            _database?.CreateTable<AppUsageSession>();
            _database?.CreateTable<ScreenTimePeriod>();
            _database?.CreateTable<ScreenTimeSession>();
            _database?.CreateTable<SoundUsageSession>();
            _database?.CreateTable<UserSettings>();
            _database?.CreateTable<FocusSession>();
            _database?.CreateTable<AppCategory>();

            // Create indexes for faster date-based queries on large tables
            try
            {
                _database?.Execute("CREATE INDEX IF NOT EXISTS idx_appusage_start ON AppUsageSession(StartTime)");
                _database?.Execute("CREATE INDEX IF NOT EXISTS idx_screenperiod_date ON ScreenTimePeriod(SessionDate)");
                _database?.Execute("CREATE INDEX IF NOT EXISTS idx_screensession_date ON ScreenTimeSession(SessionDate)");
                _database?.Execute("CREATE INDEX IF NOT EXISTS idx_sound_start ON SoundUsageSession(StartTime)");
                _database?.Execute("CREATE INDEX IF NOT EXISTS idx_focus_start ON FocusSession(StartTime)");
                _database?.Execute("CREATE INDEX IF NOT EXISTS idx_focus_date ON FocusSession(SessionDate)");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DatabaseService] Index creation error: {ex.Message}");
            }
        }

        // --- Data validation ---
        // One corrupt row (EndTime before StartTime, or an absurd duration from a clock change)
        // silently corrupts every total that sums (End - Start). Guard the write paths centrally
        // and defensively filter the reads.
        private const long MaxSessionSeconds = 24L * 60 * 60;

        /// <summary>
        /// True when a [start, end] interval is sane enough to persist. Rejects reversed intervals
        /// and durations beyond a single day; logs the reason. StartTime==EndTime is allowed (a
        /// zero-length row is harmless and filtered elsewhere by the &lt;30s noise rule).
        /// </summary>
        private static bool IsValidInterval(DateTime start, DateTime end, string kind)
        {
            if (end < start)
            {
                LogService.Warning($"Rejected {kind}: EndTime {end:o} precedes StartTime {start:o}");
                return false;
            }
            if ((end - start).TotalSeconds > MaxSessionSeconds)
            {
                LogService.Warning($"Rejected {kind}: duration {(end - start).TotalHours:F1}h exceeds 24h cap");
                return false;
            }
            return true;
        }

        // --- App Usage ---
        public static void SaveAppUsageSession(AppUsageSession session)
        {
            if (!IsValidInterval(session.StartTime, session.EndTime, nameof(AppUsageSession))) return;
            lock (_dbLock) { GetConnection().Insert(session); }
        }

        public static List<AppUsageSession> GetAppUsageSessionsForDate(DateTime date)
        {
            lock (_dbLock)
            {
                var conn = GetConnection();
                var dayStart = date.Date;
                var dayEnd = dayStart.AddDays(1);

                return conn.Table<AppUsageSession>()
                           .Where(s => s.StartTime >= dayStart && s.StartTime < dayEnd
                                       && s.EndTime >= s.StartTime)
                           .ToList();
            }
        }

        // --- Screen Time ---
        public static void SaveScreenTimePeriod(ScreenTimePeriod sts)
        {
            lock (_dbLock) { GetConnection().InsertOrReplace(sts); }
        }

        public static ScreenTimePeriod? GetScreenTimePeriodForToday()
        {
            lock (_dbLock)
            {
                var todayKey = DateTime.Now.ToString("yyyy-MM-dd");
                return GetConnection()
                       .Table<ScreenTimePeriod>()
                       .FirstOrDefault(x => x.SessionDate == todayKey);
            }
        }

        public static void SaveScreenTimeSession(ScreenTimeSession session)
        {
            if (session.DurationSeconds < 0 || session.DurationSeconds > MaxSessionSeconds)
            {
                LogService.Warning($"Rejected ScreenTimeSession: duration {session.DurationSeconds}s out of range");
                return;
            }
            lock (_dbLock) { GetConnection().Insert(session); }
        }

        public static List<ScreenTimeSession> GetScreenTimeSessionsForDate(DateTime date)
        {
            lock (_dbLock)
            {
                var dateKey = date.ToString("yyyy-MM-dd");
                return GetConnection()
                       .Table<ScreenTimeSession>()
                       .Where(x => x.SessionDate == dateKey)
                       .ToList();
            }
        }

        // --- Sound Usage ---
        public static void SaveSoundSession(SoundUsageSession session)
        {
            if (!IsValidInterval(session.StartTime, session.EndTime, nameof(SoundUsageSession))) return;
            lock (_dbLock) { GetConnection().Insert(session); }
        }

        public static List<SoundUsageSession> GetSoundSessionsForDate(DateTime date)
        {
            lock (_dbLock)
            {
                var conn = GetConnection();
                var dayStart = date.Date;
                var dayEnd = dayStart.AddDays(1);

                return conn.Table<SoundUsageSession>()
                           .Where(s => s.StartTime >= dayStart && s.StartTime < dayEnd
                                       && s.EndTime >= s.StartTime)
                           .ToList();
            }
        }

        // --- Focus Sessions ---
        public static void SaveFocusSession(FocusSession session)
        {
            lock (_dbLock) { GetConnection().InsertOrReplace(session); }
        }

        public static List<FocusSession> GetFocusSessionsForDate(DateTime date)
        {
            lock (_dbLock)
            {
                var dateKey = date.ToString("yyyy-MM-dd");
                return GetConnection()
                       .Table<FocusSession>()
                       .Where(x => x.SessionDate == dateKey)
                       .ToList();
            }
        }

        public static List<FocusSession> GetFocusSessionHistory(int days = 7)
        {
            lock (_dbLock)
            {
                var startDate = DateTime.Now.AddDays(-days).Date;
                return GetConnection()
                       .Table<FocusSession>()
                       .Where(x => x.StartTime >= startDate)
                       .OrderByDescending(x => x.StartTime)
                       .ToList();
            }
        }

        // --- App Categories ---
        public static void SaveAppCategory(AppCategory category)
        {
            lock (_dbLock)
            {
                var conn = GetConnection();

                var existing = conn.Table<AppCategory>()
                                  .FirstOrDefault(x => x.AppIdentifier == category.AppIdentifier);

                if (existing != null)
                {
                    existing.Category = category.Category;
                    existing.AppName = category.AppName;
                    existing.ExecutablePath = category.ExecutablePath;
                    existing.LastUpdated = DateTime.Now;
                    conn.Update(existing);
                }
                else
                {
                    conn.Insert(category);
                }
            }
        }

        public static List<AppCategory> GetAllAppCategories()
        {
            lock (_dbLock)
            {
                return GetConnection()
                       .Table<AppCategory>()
                       .ToList();
            }
        }

        public static AppCategory? GetAppCategory(string appIdentifier)
        {
            lock (_dbLock)
            {
                return GetConnection()
                       .Table<AppCategory>()
                       .FirstOrDefault(x => x.AppIdentifier == appIdentifier);
            }
        }

        /// <summary>
        /// One-time (idempotent) migration that rewrites each <see cref="AppCategory.AppIdentifier"/>
        /// to its canonical key (see <see cref="AppIdentity.NormalizeKey(string?)"/>). Legacy rows
        /// stored full paths or mixed casing, which never matched the process-name lookups used by
        /// the reports. Rows that collapse to the same key are merged, keeping the most recently
        /// updated category. Safe to run on every startup: a fully-normalized table produces no writes.
        /// </summary>
        public static void NormalizeAppCategoryKeys()
        {
            lock (_dbLock)
            {
                var conn = GetConnection();
                var rows = conn.Table<AppCategory>().ToList();
                if (rows.Count == 0) return;

                // Group by the canonical key; keep the newest row per key.
                var groups = rows
                    .GroupBy(r => AppIdentity.NormalizeKey(
                        !string.IsNullOrWhiteSpace(r.ExecutablePath) ? r.ExecutablePath : r.AppIdentifier));

                foreach (var group in groups)
                {
                    var key = group.Key;
                    var ordered = group.OrderByDescending(r => r.LastUpdated).ToList();
                    var winner = ordered[0];

                    // Delete any duplicate rows that collapse to the same key.
                    foreach (var dup in ordered.Skip(1))
                        conn.Delete(dup);

                    // Rewrite the surviving row's identifier if it isn't already canonical.
                    if (string.IsNullOrEmpty(key))
                    {
                        // Un-normalizable (blank) identifier — drop the orphan row.
                        conn.Delete(winner);
                    }
                    else if (winner.AppIdentifier != key)
                    {
                        winner.AppIdentifier = key;
                        conn.Update(winner);
                    }
                }
            }
        }

        // --- Multi-Day Queries (for Weekly Reports) ---

        /// <summary>
        /// Get all ScreenTimePeriod records for a date range (inclusive).
        /// Uses parameterized SQL for server-side filtering instead of loading all rows.
        /// </summary>
        public static List<ScreenTimePeriod> GetScreenTimePeriodsForRange(DateTime startDate, DateTime endDate)
        {
            lock (_dbLock)
            {
                var conn = GetConnection();
                var startKey = startDate.ToString("yyyy-MM-dd");
                var endKey = endDate.ToString("yyyy-MM-dd");

                return conn.Query<ScreenTimePeriod>(
                    "SELECT * FROM ScreenTimePeriod WHERE SessionDate >= ? AND SessionDate <= ?",
                    startKey, endKey);
            }
        }

        /// <summary>
        /// Get all AppUsageSessions for a date range (inclusive)
        /// </summary>
        public static List<AppUsageSession> GetAppUsageSessionsForRange(DateTime startDate, DateTime endDate)
        {
            lock (_dbLock)
            {
                var conn = GetConnection();
                var rangeStart = startDate.Date;
                var rangeEnd = endDate.Date.AddDays(1);

                return conn.Table<AppUsageSession>()
                           .Where(s => s.StartTime >= rangeStart && s.StartTime < rangeEnd
                                       && s.EndTime >= s.StartTime)
                           .ToList();
            }
        }

        /// <summary>
        /// Get all FocusSessions for a date range (inclusive)
        /// </summary>
        public static List<FocusSession> GetFocusSessionsForRange(DateTime startDate, DateTime endDate)
        {
            lock (_dbLock)
            {
                var conn = GetConnection();
                var rangeStart = startDate.Date;
                var rangeEnd = endDate.Date.AddDays(1);

                return conn.Table<FocusSession>()
                           .Where(x => x.StartTime >= rangeStart && x.StartTime < rangeEnd)
                           .ToList();
            }
        }

        // --- Export helpers (all rows, for CSV export) ---

        public static List<ScreenTimePeriod> GetAllScreenTimePeriods()
        {
            lock (_dbLock) { return GetConnection().Table<ScreenTimePeriod>().ToList(); }
        }

        public static List<AppUsageSession> GetAllAppUsageSessions()
        {
            lock (_dbLock) { return GetConnection().Table<AppUsageSession>().ToList(); }
        }

        public static List<SoundUsageSession> GetAllSoundUsageSessions()
        {
            lock (_dbLock) { return GetConnection().Table<SoundUsageSession>().ToList(); }
        }

        public static List<FocusSession> GetAllFocusSessions()
        {
            lock (_dbLock) { return GetConnection().Table<FocusSession>().ToList(); }
        }

        /// <summary>
        /// Delete all tracked data from the database. Preserves table structure.
        /// </summary>
        public static void DeleteAllData()
        {
            lock (_dbLock)
            {
                var conn = GetConnection();
                conn.DeleteAll<AppUsageSession>();
                conn.DeleteAll<ScreenTimePeriod>();
                conn.DeleteAll<ScreenTimeSession>();
                conn.DeleteAll<SoundUsageSession>();
                conn.DeleteAll<FocusSession>();
                conn.Execute("VACUUM");
            }
        }

        /// <summary>
        /// Delete all usage rows older than <paramref name="cutoff"/> and reclaim space. Used by
        /// the daily retention job. App-category preferences are intentionally preserved.
        /// </summary>
        public static void PurgeDataOlderThan(DateTime cutoff)
        {
            var cutoffKey = cutoff.ToString("yyyy-MM-dd");
            lock (_dbLock)
            {
                var conn = GetConnection();
                conn.Execute("DELETE FROM AppUsageSession WHERE StartTime < ?", cutoff);
                conn.Execute("DELETE FROM SoundUsageSession WHERE StartTime < ?", cutoff);
                conn.Execute("DELETE FROM FocusSession WHERE StartTime < ?", cutoff);
                conn.Execute("DELETE FROM ScreenTimeSession WHERE SessionDate < ?", cutoffKey);
                conn.Execute("DELETE FROM ScreenTimePeriod WHERE SessionDate < ?", cutoffKey);
                conn.Execute("VACUUM");
            }
        }

        /// <summary>
        /// Delete usage rows whose local day falls within [startDate, endDate] (inclusive) and
        /// reclaim space. Backs the Settings "delete a date range" option.
        /// </summary>
        public static void DeleteDataInRange(DateTime startDate, DateTime endDate)
        {
            var rangeStart = startDate.Date;
            var rangeEndExclusive = endDate.Date.AddDays(1);
            var startKey = rangeStart.ToString("yyyy-MM-dd");
            var endKey = endDate.Date.ToString("yyyy-MM-dd");
            lock (_dbLock)
            {
                var conn = GetConnection();
                conn.Execute("DELETE FROM AppUsageSession WHERE StartTime >= ? AND StartTime < ?", rangeStart, rangeEndExclusive);
                conn.Execute("DELETE FROM SoundUsageSession WHERE StartTime >= ? AND StartTime < ?", rangeStart, rangeEndExclusive);
                conn.Execute("DELETE FROM FocusSession WHERE StartTime >= ? AND StartTime < ?", rangeStart, rangeEndExclusive);
                conn.Execute("DELETE FROM ScreenTimeSession WHERE SessionDate >= ? AND SessionDate <= ?", startKey, endKey);
                conn.Execute("DELETE FROM ScreenTimePeriod WHERE SessionDate >= ? AND SessionDate <= ?", startKey, endKey);
                conn.Execute("VACUUM");
            }
        }

        /// <summary>
        /// Get the full path to the database file.
        /// </summary>
        public static string GetDatabaseFilePath() => DbPath;

        /// <summary>
        /// Get the size of the database file in bytes, or 0 if it doesn't exist.
        /// </summary>
        public static long GetDatabaseFileSize()
        {
            try
            {
                var info = new FileInfo(DbPath);
                return info.Exists ? info.Length : 0;
            }
            catch { return 0; }
        }
    }
}
