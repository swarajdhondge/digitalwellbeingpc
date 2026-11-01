using System;
using System.IO;
using System.Text.Json;

namespace digital_wellbeing_app.Services
{
    /// <summary>
    /// Today's not-yet-saved totals, written by the running app every 30 seconds to
    /// <c>live.json</c> next to the database so the command-line tool can show current numbers
    /// (the database itself is only written every few minutes). Holds no window titles.
    /// </summary>
    public sealed class LiveSnapshot
    {
        public const string FileName = "live.json";

        /// <summary>A snapshot older than this is ignored (Pulse closed or stuck).</summary>
        public static readonly TimeSpan MaxAge = TimeSpan.FromMinutes(2);

        public DateTime WrittenAt { get; set; }

        /// <summary>Today's active screen time including what is already saved.</summary>
        public int ActiveSeconds { get; set; }

        /// <summary>The app in front and when its unsaved segment started; null when none.</summary>
        public string? AppName { get; set; }
        public string? ExecutablePath { get; set; }
        public DateTime? AppStart { get; set; }

        public static string PathIn(string dataFolder) => Path.Combine(dataFolder, FileName);

        /// <summary>Writes atomically (temp file + replace) so a reader never sees half a file.</summary>
        public static void Write(string path, LiveSnapshot snapshot)
        {
            var temp = path + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(snapshot));
            File.Move(temp, path, overwrite: true);
        }

        /// <summary>The snapshot when it exists, parses, is from today and is recent; otherwise null.</summary>
        public static LiveSnapshot? ReadFresh(string path, DateTime now)
        {
            try
            {
                if (!File.Exists(path)) return null;
                var snapshot = JsonSerializer.Deserialize<LiveSnapshot>(File.ReadAllText(path));
                if (snapshot == null) return null;
                if (snapshot.WrittenAt.Date != now.Date) return null;
                if (snapshot.WrittenAt > now.AddSeconds(5) || now - snapshot.WrittenAt > MaxAge) return null;
                return snapshot;
            }
            catch
            {
                return null; // unreadable or mid-replace: fall back to saved data
            }
        }

        public static void Delete(string path)
        {
            try { File.Delete(path); } catch { /* best effort */ }
        }
    }
}
