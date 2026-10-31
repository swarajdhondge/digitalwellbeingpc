using System.Globalization;
using System.Security.Cryptography;
using digital_wellbeing_app.Cli;
using digital_wellbeing_app.Models;
using digital_wellbeing_app.Services;
using SQLite;
using Xunit;

// The commands read process-wide state (PULSE_DATA_DIR, culture, one shared DB connection).
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace PulseCli.Tests
{
    /// <summary>Runs `pulse` commands in-process against a throwaway data folder.</summary>
    public sealed class CliTests : IDisposable
    {
        private readonly string _dir = Path.Combine(Path.GetTempPath(), "pulse-cli-tests", Guid.NewGuid().ToString("N"));
        private string DbPath => Path.Combine(_dir, "digital_wellbeing.db");

        public CliTests()
        {
            Directory.CreateDirectory(_dir);
            Environment.SetEnvironmentVariable("PULSE_DATA_DIR", _dir);
            var english = CultureInfo.GetCultureInfo("en-US");
            CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = english;
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.DefaultThreadCurrentUICulture = english;
            LiveUsageProvider.LiveActiveTime = () => null;
            LiveUsageProvider.LiveAppSessionsForRange = (_, _) => null;
        }

        public void Dispose()
        {
            DatabaseService.CloseConnection();
            Environment.SetEnvironmentVariable("PULSE_DATA_DIR", null);
            try { Directory.Delete(_dir, true); } catch { }
        }

        private (int Code, string Out, string Err) Run(params string[] args)
        {
            var output = new StringWriter();
            var error = new StringWriter();
            var code = new CliApp(output, error, color: false).Run(args);
            return (code, output.ToString(), error.ToString());
        }

        private void Seed(Action<SQLiteConnection> fill)
        {
            using var db = new SQLiteConnection(DbPath);
            db.CreateTable<AppUsageSession>();
            db.CreateTable<ScreenTimePeriod>();
            db.CreateTable<ScreenTimeSession>();
            db.CreateTable<SoundUsageSession>();
            db.CreateTable<UserSettings>();
            db.CreateTable<FocusSession>();
            db.CreateTable<AppCategory>();
            db.CreateTable<AppLimit>();
            db.CreateTable<WebsiteUsageSession>();
            db.CreateTable<TrackerHeartbeat>();
            fill(db);
        }

        private static void AddScreen(SQLiteConnection db, DateTime day, int seconds)
            => db.Insert(new ScreenTimePeriod { SessionDate = day.ToString("yyyy-MM-dd"), AccumulatedActiveSeconds = seconds });

        private static void AddApp(SQLiteConnection db, string name, DateTime start, int seconds)
            => db.Insert(new AppUsageSession
            {
                AppName = name, ExecutablePath = $@"C:\Apps\{name}.exe", StartTime = start, EndTime = start.AddSeconds(seconds)
            });

        // ---------- basics ----------

        [Fact]
        public void Help_ListsEveryCommand()
        {
            var (code, output, _) = Run("help");
            Assert.Equal(CliApp.Ok, code);
            foreach (var command in new[] { "status", "today", "week", "apps", "start", "stop", "version", "help" })
                Assert.Contains($"  {command} ", output);
        }

        [Fact]
        public void NoArguments_ShowsHelp()
            => Assert.Equal(Run("help").Out, Run().Out);

        [Fact]
        public void UnknownCommand_FailsWithHint()
        {
            var (code, output, error) = Run("frobnicate");
            Assert.Equal(CliApp.Error, code);
            Assert.Equal("", output);
            Assert.Contains("frobnicate", error);
            Assert.Contains("pulse help", error);
        }

        [Fact]
        public void Version_PrintsVersion()
        {
            var (code, output, _) = Run("version");
            Assert.Equal(CliApp.Ok, code);
            Assert.StartsWith("pulse ", output);
        }

        [Theory]
        [InlineData("today", "--week")]
        [InlineData("apps", "--bogus")]
        [InlineData("apps", "-n", "zero")]
        public void BadOption_Fails(params string[] args)
            => Assert.Equal(CliApp.Error, Run(args).Code);

        [Fact]
        public void DataCommands_WithoutDatabase_ExplainAndReturnNoData()
        {
            foreach (var command in new[] { "today", "week", "apps" })
            {
                var (code, _, error) = Run(command);
                Assert.Equal(CliApp.NoData, code);
                Assert.Contains("hasn't recorded anything", error);
            }
            Assert.False(File.Exists(DbPath));   // never creates the database
        }

        // ---------- numbers ----------

        [Fact]
        public void Today_ShowsSavedScreenTimeAndAppsLongestFirst()
        {
            var today = DateTime.Today;
            Seed(db =>
            {
                AddScreen(db, today, 2 * 3600 + 5 * 60);
                AddApp(db, "firefox", today.AddHours(8), 35 * 60);
                AddApp(db, "Code", today.AddHours(9), 90 * 60);
            });

            var (code, output, _) = Run("today");

            Assert.Equal(CliApp.Ok, code);
            Assert.Contains("Screen time: 2h 5m", output);
            Assert.True(output.IndexOf("1h 30m") < output.IndexOf("35m"), output);
        }

        [Fact]
        public void Week_MatchesDailyTotals()
        {
            var monday = ReportService.GetWeekStart(DateTime.Today);
            var expected = 0;
            Seed(db =>
            {
                for (var day = monday; day <= DateTime.Today; day = day.AddDays(1))
                {
                    var seconds = 3600 + (int)(day - monday).TotalDays * 600;
                    AddScreen(db, day, seconds);
                    expected += seconds;
                }
            });

            var (code, output, _) = Run("week");

            Assert.Equal(CliApp.Ok, code);
            var total = TimeSpan.FromSeconds(expected);
            Assert.Contains($"Total {(int)total.TotalHours}h {total.Minutes}m", output);
            Assert.Equal(7, output.Split('\n').Count(l => l.StartsWith("  ")));
        }

        [Fact]
        public void Apps_RespectsCountAndWeek()
        {
            var today = DateTime.Today;
            Seed(db =>
            {
                AddApp(db, "alpha", today.AddHours(8), 3000);
                AddApp(db, "beta", today.AddHours(9), 2000);
                AddApp(db, "gamma", today.AddHours(10), 1000);
            });

            var one = Run("apps", "-n", "1").Out;
            Assert.Contains("alpha", one, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("beta", one, StringComparison.OrdinalIgnoreCase);

            var (code, week, _) = Run("apps", "--week");
            Assert.Equal(CliApp.Ok, code);
            Assert.Contains("gamma", week, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Status_WorksBeforeTheAppHasCreatedEveryTable()
        {
            using (var db = new SQLiteConnection(DbPath))
                db.CreateTable<ScreenTimePeriod>();   // e.g. data from an older version, no heartbeat table yet

            var (code, output, _) = Run("status");

            Assert.Equal(CliApp.Ok, code);
            Assert.Contains("Version", output);
        }

        [Fact]
        public void Commands_NeverModifyTheDatabase()
        {
            Seed(db => AddScreen(db, DateTime.Today, 600));
            var before = SHA256.HashData(File.ReadAllBytes(DbPath));

            foreach (var command in new[] { "status", "today", "week", "apps" })
                Run(command);

            Assert.Equal(before, SHA256.HashData(File.ReadAllBytes(DbPath)));
        }

        // ---------- live numbers (live.json) ----------

        [Fact]
        public void Today_AddsFreshLiveSnapshot()
        {
            var now = DateTime.Now;
            var appStart = now.AddMinutes(-20) < DateTime.Today ? DateTime.Today : now.AddMinutes(-20);
            Seed(db => AddScreen(db, DateTime.Today, 600));
            LiveSnapshot.Write(LiveSnapshot.PathIn(_dir), new LiveSnapshot
            {
                WrittenAt = now,
                ActiveSeconds = 3600,
                AppName = "Code",
                ExecutablePath = @"C:\Apps\Code.exe",
                AppStart = appStart
            });

            var output = Run("today").Out;

            Assert.Contains("Screen time: 1h 0m", output);
            Assert.Contains($"{(now - appStart).Minutes}m", output);
        }

        [Fact]
        public void Today_IgnoresStaleLiveSnapshot()
        {
            var old = DateTime.Now.AddMinutes(-10);
            Seed(db => AddScreen(db, DateTime.Today, 600));
            LiveSnapshot.Write(LiveSnapshot.PathIn(_dir), new LiveSnapshot { WrittenAt = old, ActiveSeconds = 7200 });

            Assert.Contains("Screen time: 10m", Run("today").Out);
        }

        [Fact]
        public void LiveSegment_SkipsSegmentAlreadySaved()
        {
            var start = DateTime.Today.AddHours(9);
            var snapshot = new LiveSnapshot { WrittenAt = start.AddMinutes(10), AppName = "Code", AppStart = start };
            var saved = new[] { new AppUsageSession { AppName = "Code", StartTime = start, EndTime = start.AddMinutes(5) } };

            Assert.Null(LiveData.LiveSegment(snapshot, saved, DateTime.Today, DateTime.Today));
            Assert.Equal(TimeSpan.FromMinutes(10),
                LiveData.LiveSegment(snapshot, Array.Empty<AppUsageSession>(), DateTime.Today, DateTime.Today)!.Duration);
        }

        [Fact]
        public void LiveSegment_ClipsToTheRequestedDays()
        {
            var start = DateTime.Today.AddMinutes(-30);   // began yesterday evening
            var snapshot = new LiveSnapshot { WrittenAt = DateTime.Today.AddMinutes(15), AppName = "Code", AppStart = start };

            var segment = LiveData.LiveSegment(snapshot, Array.Empty<AppUsageSession>(), DateTime.Today, DateTime.Today)!;

            Assert.Equal(DateTime.Today, segment.StartTime);
            Assert.Equal(TimeSpan.FromMinutes(15), segment.Duration);
        }

        [Fact]
        public void ReadFresh_RejectsCorruptOldOrOtherDaySnapshots()
        {
            var path = LiveSnapshot.PathIn(_dir);
            var now = DateTime.Now;

            File.WriteAllText(path, "{ not json");
            Assert.Null(LiveSnapshot.ReadFresh(path, now));

            LiveSnapshot.Write(path, new LiveSnapshot { WrittenAt = now - LiveSnapshot.MaxAge - TimeSpan.FromSeconds(1) });
            Assert.Null(LiveSnapshot.ReadFresh(path, now));

            LiveSnapshot.Write(path, new LiveSnapshot { WrittenAt = now.AddDays(-1) });
            Assert.Null(LiveSnapshot.ReadFresh(path, now));

            LiveSnapshot.Write(path, new LiveSnapshot { WrittenAt = now.AddSeconds(-20), ActiveSeconds = 5 });
            Assert.Equal(5, LiveSnapshot.ReadFresh(path, now)!.ActiveSeconds);
        }

        // ---------- language and layout ----------

        [Fact]
        public void FollowsTheAppsLanguageSetting()
        {
            File.WriteAllText(Path.Combine(_dir, "settings.json"), "{\"Language\":\"zh-Hans\"}");
            Assert.Contains("命令", Run("help").Out);
        }

        [Fact]
        public void WideCharacters_AreMeasuredAsTwoColumns()
        {
            Assert.Equal(4, CliApp.DisplayWidth("微信"));
            Assert.Equal("微信  ", CliApp.PadRight("微信", 6));
            Assert.Equal(5, CliApp.DisplayWidth(CliApp.Truncate("Visual Studio Code", 5)));
        }
    }
}
