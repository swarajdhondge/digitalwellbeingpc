// Pulse fixture seeder — populates the SQLite DB with a believable ~2 weeks of
// activity so the screenshot pipeline captures rich (not empty) views.
//
// Reuses the app's own Models + DB path resolver via a project reference, and
// opens its own SQLiteConnection with the SAME flags the app uses
// (ReadWrite | Create | FullMutex, storeDateTimeAsTicks default = true) so the
// on-disk representation matches exactly.
//
// Usage:
//   dotnet run --project tools/FixtureSeeder
//   dotnet run --project tools/FixtureSeeder -- --db "C:\tmp\fixture.db"
//
// Environment override (lower precedence than --db):
//   PULSE_FIXTURE_DB   path to the DB file to seed
//
// It is idempotent: it deletes the rows it manages before re-inserting, so it
// can be run repeatedly without piling up duplicates.

using SQLite;
using digital_wellbeing_app.Models;
using digital_wellbeing_app.Services;

// ---- Resolve the target DB path -------------------------------------------------
// Precedence: --db <path>  >  PULSE_FIXTURE_DB  >  the app's real DB path.
string? dbPath = null;
for (int i = 0; i < args.Length - 1; i++)
{
    if (args[i] is "--db" or "-d") { dbPath = args[i + 1]; break; }
}
dbPath ??= Environment.GetEnvironmentVariable("PULSE_FIXTURE_DB");
dbPath ??= DatabaseService.GetDatabaseFilePath(); // %LocalAppData%\Pulse\digital_wellbeing.db

var dbDir = Path.GetDirectoryName(dbPath)!;
Directory.CreateDirectory(dbDir);

Console.WriteLine($"[FixtureSeeder] Seeding: {dbPath}");

// Match the app's connection settings exactly (see DatabaseService.GetConnection).
var db = new SQLiteConnection(dbPath,
    SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.FullMutex);

db.CreateTable<AppUsageSession>();
db.CreateTable<ScreenTimePeriod>();
db.CreateTable<ScreenTimeSession>();
db.CreateTable<SoundUsageSession>();
db.CreateTable<FocusSession>();
db.CreateTable<AppCategory>();

// Idempotency: clear the tables we own before reseeding.
db.DeleteAll<AppUsageSession>();
db.DeleteAll<ScreenTimePeriod>();
db.DeleteAll<ScreenTimeSession>();
db.DeleteAll<SoundUsageSession>();
db.DeleteAll<FocusSession>();
db.DeleteAll<AppCategory>();

// Deterministic RNG so screenshots are reproducible run-to-run.
var rng = new Random(20260705);

// ---- App catalogue (drives categories + usage) ---------------------------------
var apps = new AppDef[]
{
    new("Code.exe",    "Visual Studio Code", @"C:\Users\Alex\AppData\Local\Programs\Microsoft VS Code\Code.exe", AppCategoryType.Work,          Weight: 6),
    new("chrome.exe",  "Google Chrome",      @"C:\Program Files\Google\Chrome\Application\chrome.exe",           AppCategoryType.Uncategorized, Weight: 5),
    new("Teams.exe",   "Microsoft Teams",    @"C:\Users\Alex\AppData\Local\Microsoft\Teams\Teams.exe",          AppCategoryType.Work,          Weight: 3),
    new("slack.exe",   "Slack",              @"C:\Users\Alex\AppData\Local\slack\slack.exe",                    AppCategoryType.Work,          Weight: 2),
    new("Notion.exe",  "Notion",             @"C:\Users\Alex\AppData\Local\Programs\Notion\Notion.exe",         AppCategoryType.Work,          Weight: 2),
    new("Figma.exe",   "Figma",              @"C:\Users\Alex\AppData\Local\Figma\Figma.exe",                    AppCategoryType.Work,          Weight: 2),
    new("Spotify.exe", "Spotify",            @"C:\Users\Alex\AppData\Roaming\Spotify\Spotify.exe",              AppCategoryType.Entertainment, Weight: 3),
    new("Discord.exe", "Discord",            @"C:\Users\Alex\AppData\Local\Discord\app-1.0\Discord.exe",        AppCategoryType.Entertainment, Weight: 2),
    new("steam.exe",   "Steam",              @"C:\Program Files (x86)\Steam\steam.exe",                         AppCategoryType.Entertainment, Weight: 2),
    new("vlc.exe",     "VLC media player",   @"C:\Program Files\VideoLAN\VLC\vlc.exe",                          AppCategoryType.Entertainment, Weight: 1),
};

foreach (var a in apps)
{
    db.Insert(new AppCategory
    {
        AppIdentifier = a.Process,
        AppName = a.Name,
        ExecutablePath = a.Path,
        Category = a.Category,
        LastUpdated = DateTime.Now,
    });
}

// ---- Sound devices --------------------------------------------------------------
var devices = new (string Name, string Type)[]
{
    ("Sony WH-1000XM4", "Headphones"),
    ("Realtek High Definition Audio", "Speakers"),
    ("Galaxy Buds Pro", "Earphones"),
};

// ---- Generate ~2 weeks of data (13 days back + today) ---------------------------
const int DaysBack = 13;
var today = DateTime.Now.Date;

for (int d = DaysBack; d >= 0; d--)
{
    var day = today.AddDays(-d);
    bool isToday = d == 0;
    bool isWeekend = day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

    // Target active screen time for the day (weekends lighter, some variance).
    int baseMinutes = isWeekend ? 150 : 320;
    int targetMinutes = baseMinutes + rng.Next(-40, 60);

    // Day starts mid-morning (weekends later) and runs into the evening.
    var cursor = day.AddHours(isWeekend ? 10 : 8).AddMinutes(rng.Next(0, 45));
    var dayEnd = day.AddHours(22);
    if (isToday)
    {
        // Seed today as a believable partial day up to mid-afternoon, independent of the
        // actual wall-clock time the seeder runs (so screenshots are never empty when captured
        // early in the morning). The dashboard reads today's total live, so this drives its hero.
        dayEnd = day.AddHours(15).AddMinutes(rng.Next(0, 50));
        targetMinutes = 195 + rng.Next(-20, 40); // ~3h of "so far" activity
    }

    int accumulatedSeconds = 0;
    string dateKey = day.ToString("yyyy-MM-dd");

    while (cursor < dayEnd && accumulatedSeconds < targetMinutes * 60)
    {
        var app = PickWeighted(apps, rng);
        int blockMinutes = rng.Next(8, 55);
        var start = cursor;
        var end = start.AddMinutes(blockMinutes);
        if (end > dayEnd) end = dayEnd;
        int blockSeconds = (int)(end - start).TotalSeconds;
        if (blockSeconds < 60) break;

        // App usage session.
        db.Insert(new AppUsageSession
        {
            AppName = app.Name,
            ExecutablePath = app.Path,
            WindowTitle = WindowTitleFor(app, rng),
            StartTime = start,
            EndTime = end,
        });

        // Matching screen-time (unlock) session — used by timeline/hourly views.
        db.Insert(new ScreenTimeSession
        {
            SessionDate = dateKey,
            StartTime = start,
            DurationSeconds = blockSeconds,
        });

        accumulatedSeconds += blockSeconds;

        // Occasional listening block on top of the current app.
        if (rng.NextDouble() < 0.35)
        {
            var dev = devices[rng.Next(devices.Length)];
            double avgVol = 0.35 + rng.NextDouble() * 0.45;         // 0.35–0.80
            double peakSpl = 68 + rng.NextDouble() * 24;            // 68–92 dB
            bool harmful = peakSpl > 80;
            var listen = TimeSpan.FromSeconds(blockSeconds * (0.6 + rng.NextDouble() * 0.4));
            db.Insert(new SoundUsageSession
            {
                StartTime = start,
                EndTime = end,
                AvgVolume = Math.Round(avgVol, 2),
                EstimatedMaxSPL = Math.Round(peakSpl, 1),
                DeviceName = dev.Name,
                DeviceType = dev.Type,
                WasHarmful = harmful,
                HarmfulDuration = harmful ? TimeSpan.FromSeconds(blockSeconds * 0.3) : TimeSpan.Zero,
                ActualListeningDuration = listen,
            });
        }

        // Gap between activity blocks (short breaks).
        cursor = end.AddMinutes(rng.Next(2, 18));
    }

    // Per-day screen-time summary.
    db.Insert(new ScreenTimePeriod
    {
        SessionDate = dateKey,
        SessionStartTime = day.AddHours(isWeekend ? 10 : 8).ToString("HH:mm:ss"),
        LastRecordedTime = dayEnd.ToString("HH:mm:ss"),
        AccumulatedActiveSeconds = accumulatedSeconds,
    });

    // Focus sessions: 1–3 on weekdays, 0–1 on weekends.
    int focusCount = isWeekend ? rng.Next(0, 2) : rng.Next(1, 4);
    for (int f = 0; f < focusCount; f++)
    {
        int planned = new[] { 25, 45, 60, 90 }[rng.Next(4)];
        var fStart = day.AddHours(9 + f * 3).AddMinutes(rng.Next(0, 40));
        if (isToday && fStart > DateTime.Now) continue;
        bool completed = rng.NextDouble() < 0.75;
        int actual = completed ? planned : rng.Next(8, planned);
        var fEnd = fStart.AddMinutes(actual);
        if (isToday && fEnd > DateTime.Now) fEnd = DateTime.Now;

        db.Insert(new FocusSession
        {
            StartTime = fStart,
            EndTime = fEnd,
            PlannedDurationMinutes = planned,
            EnforcementLevel = rng.NextDouble() < 0.5 ? FocusEnforcementLevel.Warn : FocusEnforcementLevel.Block,
            Completed = completed,
            DistractionWarnings = rng.Next(0, 5),
            DistractionOverrides = rng.Next(0, 2),
            SessionDate = dateKey,
        });
    }
}

// ---- Mark first-run complete so the Welcome overlay does not block capture ------
// The app reads settings.json from %LocalAppData%\Pulse (see SettingsService). We
// write it next to the resolved DB folder, which is that folder in the default case.
try
{
    var settingsPath = Path.Combine(dbDir, "settings.json");
    var settings = new Dictionary<string, object>();
    if (File.Exists(settingsPath))
    {
        try
        {
            settings = System.Text.Json.JsonSerializer
                .Deserialize<Dictionary<string, object>>(File.ReadAllText(settingsPath)) ?? new();
        }
        catch { settings = new(); }
    }
    settings["FirstRunCompleted"] = true;
    File.WriteAllText(settingsPath,
        System.Text.Json.JsonSerializer.Serialize(settings));
    Console.WriteLine($"[FixtureSeeder] FirstRunCompleted=true -> {settingsPath}");
}
catch (Exception ex)
{
    Console.WriteLine($"[FixtureSeeder] WARN could not write settings.json: {ex.Message}");
}

// ---- Report --------------------------------------------------------------------
Console.WriteLine($"[FixtureSeeder] Done. Rows: " +
    $"AppUsage={db.Table<AppUsageSession>().Count()}, " +
    $"ScreenPeriods={db.Table<ScreenTimePeriod>().Count()}, " +
    $"ScreenSessions={db.Table<ScreenTimeSession>().Count()}, " +
    $"Sound={db.Table<SoundUsageSession>().Count()}, " +
    $"Focus={db.Table<FocusSession>().Count()}, " +
    $"Categories={db.Table<AppCategory>().Count()}");

db.Close();

// ---- helpers -------------------------------------------------------------------
static AppDef PickWeighted(AppDef[] apps, Random rng)
{
    int total = apps.Sum(a => a.Weight);
    int roll = rng.Next(total);
    foreach (var a in apps)
    {
        roll -= a.Weight;
        if (roll < 0) return a;
    }
    return apps[0];
}

static string WindowTitleFor(AppDef app, Random rng) => app.Process switch
{
    "Code.exe"    => new[] { "Program.cs — pulse", "MainWindow.xaml — pulse", "DatabaseService.cs — pulse" }[rng.Next(3)],
    "chrome.exe"  => new[] { "Inbox (12) - Gmail", "Stack Overflow", "GitHub - pull requests" }[rng.Next(3)],
    "Teams.exe"   => "Daily standup | Microsoft Teams",
    "slack.exe"   => "#engineering | Slack",
    "Spotify.exe" => "Deep Focus — Spotify",
    "Discord.exe" => "#general | Discord",
    "steam.exe"   => "Steam",
    "vlc.exe"     => "Episode 4 — VLC media player",
    "Figma.exe"   => "Pulse UI — Figma",
    "Notion.exe"  => "Sprint planning — Notion",
    _ => app.Name,
};

// App catalogue entry.
internal readonly record struct AppDef(
    string Process, string Name, string Path, AppCategoryType Category, int Weight);
