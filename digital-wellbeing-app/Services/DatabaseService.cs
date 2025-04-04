using SQLite;
using System.IO;

namespace digital_wellbeing_app.Services
{
    public static class DatabaseService
    {
        private static SQLiteConnection? _database;

        public static SQLiteConnection GetConnection()
        {
            if (_database == null)
            {
                var folderPath = System.Environment.GetFolderPath(
                    System.Environment.SpecialFolder.LocalApplicationData);

                var databasePath = Path.Combine(folderPath, "digital_wellbeing.db");
                _database = new SQLiteConnection(databasePath);
                InitializeTables();
            }
            return _database;
        }

        private static void InitializeTables()
        {
            _database?.CreateTable<CoreLogic.ScreenTimeSession>();
        }
    }
}
