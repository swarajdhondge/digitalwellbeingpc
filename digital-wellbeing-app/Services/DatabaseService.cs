using SQLite;
using System.IO;
using System;
using System.Collections.Generic;
using digital_wellbeing_app.Models;

namespace digital_wellbeing_app.Services
{
    public static class DatabaseService
    {
        private static SQLiteConnection? _database;

        public static SQLiteConnection GetConnection()
        {
            if (_database is null)
            {
                var folderPath = Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData);

                var dbPath = Path.Combine(folderPath, "digital_wellbeing.db");
                _database = new SQLiteConnection(dbPath);
                InitializeTables();
            }
            return _database;
        }

        private static void InitializeTables()
        {
            _database?.CreateTable<AppUsageSession>();
            _database?.CreateTable<ScreenTimePeriod>();
        }

        public static void SaveAppUsageSession(AppUsageSession session)
        {
            var conn = GetConnection();
            conn.Insert(session);
        }

        public static List<AppUsageSession> GetAppUsageSessionsForDate(DateTime date)
        {
            var conn = GetConnection();
            // Day range approach
            var dayStart = date.Date;
            var dayEnd = dayStart.AddDays(1);

            return conn.Table<AppUsageSession>()
                       .Where(s => s.StartTime >= dayStart && s.StartTime < dayEnd)
                       .ToList();
        }

        public static void SaveScreenTimePeriod(ScreenTimePeriod sts)
        {
            var conn = GetConnection();
            conn.InsertOrReplace(sts);
        }

        public static ScreenTimePeriod? GetScreenTimePeriodForToday()
        {
            var conn = GetConnection();
            var today = DateTime.Now.ToString("yyyy-MM-dd");
            return conn.Table<ScreenTimePeriod>()
                       .FirstOrDefault(x => x.SessionDate == today);
        }
    }
}
