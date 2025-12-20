using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SQLite;
using digital_wellbeing_app.Models;

namespace digital_wellbeing_app.Services
{
    public static class DatabaseService
    {
        private static SQLiteConnection? _database;

        // folder & filename constants
        private const string AppFolderName = "Digital Wellbeing";
        private const string DbFileName = "digital_wellbeing.db";

        // full path to the DB, under %LocalAppData%
        private static string DbPath
        {
            get
            {
                var localAppData = Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData);
                var folder = Path.Combine(localAppData, AppFolderName);

                // ensure the folder exists
                if (!Directory.Exists(folder))
                    Directory.CreateDirectory(folder);

                return Path.Combine(folder, DbFileName);
            }
        }

        public static SQLiteConnection GetConnection()
        {
            if (_database is null)
            {
                _database = new SQLiteConnection(DbPath);
                InitializeTables();
            }
            return _database;
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
        }

        // --- App Usage ---
        public static void SaveAppUsageSession(AppUsageSession session)
        {
            GetConnection().Insert(session);
        }

        public static List<AppUsageSession> GetAppUsageSessionsForDate(DateTime date)
        {
            var conn = GetConnection();
            var dayStart = date.Date;
            var dayEnd = dayStart.AddDays(1);

            return conn.Table<AppUsageSession>()
                       .Where(s => s.StartTime >= dayStart && s.StartTime < dayEnd)
                       .ToList();
        }

        // --- Screen Time ---
        public static void SaveScreenTimePeriod(ScreenTimePeriod sts)
        {
            GetConnection().InsertOrReplace(sts);
        }

        public static ScreenTimePeriod? GetScreenTimePeriodForToday()
        {
            var todayKey = DateTime.Now.ToString("yyyy-MM-dd");
            return GetConnection()
                   .Table<ScreenTimePeriod>()
                   .FirstOrDefault(x => x.SessionDate == todayKey);
        }

        public static void SaveScreenTimeSession(ScreenTimeSession session)
        {
            GetConnection().Insert(session);
        }

        public static List<ScreenTimeSession> GetScreenTimeSessionsForDate(DateTime date)
        {
            var dateKey = date.ToString("yyyy-MM-dd");
            return GetConnection()
                   .Table<ScreenTimeSession>()
                   .Where(x => x.SessionDate == dateKey)
                   .ToList();
        }

        // --- Sound Usage ---
        public static void SaveSoundSession(SoundUsageSession session)
        {
            GetConnection().Insert(session);
        }

        public static List<SoundUsageSession> GetSoundSessionsForDate(DateTime date)
        {
            var conn = GetConnection();
            var dayStart = date.Date;
            var dayEnd = dayStart.AddDays(1);

            return conn.Table<SoundUsageSession>()
                       .Where(s => s.StartTime >= dayStart && s.StartTime < dayEnd)
                       .ToList();
        }

        // --- Focus Sessions ---
        public static void SaveFocusSession(FocusSession session)
        {
            GetConnection().InsertOrReplace(session);
        }

        public static List<FocusSession> GetFocusSessionsForDate(DateTime date)
        {
            var dateKey = date.ToString("yyyy-MM-dd");
            return GetConnection()
                   .Table<FocusSession>()
                   .Where(x => x.SessionDate == dateKey)
                   .ToList();
        }

        public static List<FocusSession> GetFocusSessionHistory(int days = 7)
        {
            var startDate = DateTime.Now.AddDays(-days).Date;
            return GetConnection()
                   .Table<FocusSession>()
                   .Where(x => x.StartTime >= startDate)
                   .OrderByDescending(x => x.StartTime)
                   .ToList();
        }

        // --- App Categories ---
        public static void SaveAppCategory(AppCategory category)
        {
            var conn = GetConnection();
            
            // Check if app already has a category
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

        public static List<AppCategory> GetAllAppCategories()
        {
            return GetConnection()
                   .Table<AppCategory>()
                   .ToList();
        }

        public static AppCategory? GetAppCategory(string appIdentifier)
        {
            return GetConnection()
                   .Table<AppCategory>()
                   .FirstOrDefault(x => x.AppIdentifier == appIdentifier);
        }
    }
}
