using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text;
using digital_wellbeing_app.Helpers;
using digital_wellbeing_app.Models;
using digital_wellbeing_app.Services;

namespace digital_wellbeing_app.Cli
{
    /// <summary>
    /// The `pulse` commands. Reads the data the Pulse app records; never writes it. The only
    /// actions that affect the app are `start` (launch it in the notification area) and `stop`
    /// (ask it to save and quit, exactly like tray → Exit).
    /// </summary>
    public sealed class CliApp
    {
        public const int Ok = 0, Error = 1, NoData = 2;

        private const int BarWidth = 24;
        private static readonly TimeSpan StartStopTimeout = TimeSpan.FromSeconds(15);

        /// <summary>Whether the Pulse app is running; replaceable in tests.</summary>
        internal static Func<bool> IsAppRunning = PulseInstance.IsRunning;

        private readonly TextWriter _out;
        private readonly TextWriter _err;
        private readonly bool _color;

        public CliApp(TextWriter output, TextWriter error, bool color)
        {
            _out = output;
            _err = error;
            _color = color;
        }

        /// <summary>%LocalAppData%\Pulse, or PULSE_DATA_DIR when set (same rule as the app).</summary>
        public static string DataFolder
            => Environment.GetEnvironmentVariable("PULSE_DATA_DIR") is { Length: > 0 } dir
                ? dir
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Pulse");

        private static string DatabasePath => Path.Combine(DataFolder, "digital_wellbeing.db");

        public int Run(string[] args)
        {
            DatabaseService.OpenReadOnly = true;
            var command = args.Length == 0 ? "help" : args[0].ToLowerInvariant();
            var options = args.Skip(1).ToArray();
            try
            {
                if (Directory.Exists(DataFolder))
                    Loc.Apply(new SettingsService().LoadLanguage());

                // `pulse today --help` and the like show the help instead of an error.
                if (options.Any(o => o is "-h" or "--help" or "/?"))
                    return Help();

                return command switch
                {
                    "help" or "-h" or "--help" or "/?" => Help(),
                    "version" or "--version" or "-v" => Version(),
                    "status" => NoOptions(command, options) ?? Status(),
                    "today" => NoOptions(command, options) ?? WithData(Today),
                    "week" => NoOptions(command, options) ?? WithData(Week),
                    "apps" => Apps(options),
                    "start" => NoOptions(command, options) ?? Start(),
                    "stop" => NoOptions(command, options) ?? Stop(),
                    _ => Fail(Loc.Format("Cli_UnknownCommand", args[0]))
                };
            }
            catch (SQLite.SQLiteException ex) when (ex.Result is SQLite.SQLite3.Result.Busy or SQLite.SQLite3.Result.Locked)
            {
                return Fail(Loc.Get("Cli_DataBusy"));
            }
            catch (SQLite.SQLiteException)
            {
                // Data from a newer app version, or left mid-write by a crash: the app fixes it on its next start.
                return Fail(Loc.Get("Cli_DataNeedsApp"));
            }
            catch (Exception ex)
            {
                return Fail(Loc.Format("Cli_Failed", ex.Message));
            }
            finally
            {
                DatabaseService.CloseConnection();
            }
        }

        // ---------------- commands ----------------

        private int Help()
        {
            _out.WriteLine(Loc.Get("Cli_Usage"));
            _out.WriteLine();
            _out.WriteLine(Loc.Get("Cli_CommandsHeader"));
            foreach (var (name, text) in new[]
            {
                ("status", Loc.Get("Cli_Help_Status")), ("today", Loc.Get("Cli_Help_Today")),
                ("week", Loc.Get("Cli_Help_Week")), ("apps", Loc.Get("Cli_Help_Apps")),
                ("start", Loc.Get("Cli_Help_Start")), ("stop", Loc.Get("Cli_Help_Stop")),
                ("version", Loc.Get("Cli_Help_Version")), ("help", Loc.Get("Cli_Help_Help")),
            })
            {
                _out.WriteLine($"  {name,-9}{text}");
            }
            _out.WriteLine();
            _out.WriteLine(Loc.Get("Cli_HelpFooter"));
            return Ok;
        }

        private int Version()
        {
            _out.WriteLine($"pulse {AppVersion}");
            return Ok;
        }

        private int Status()
        {
            bool running = IsAppRunning();
            WriteColored(Loc.Get(running ? "Cli_StatusRecording" : "Cli_StatusNotRunning"),
                running ? ConsoleColor.Green : ConsoleColor.Yellow);
            _out.WriteLine(Loc.Format("Settings_VersionFormat", AppVersion));
            _out.WriteLine(Loc.Format("Cli_DataFolder", DataFolder));

            if (File.Exists(DatabasePath) && LastSaved() is DateTime saved)
                _out.WriteLine(Loc.Format("Cli_LastSaved", FormatWhen(saved)));

            if (!running)
                _out.WriteLine(Loc.Get("Cli_StartHint"));
            return Ok;
        }

        private int Today()
        {
            var today = DateTime.Today;
            WriteTitle($"{Loc.Get("Common_Today")} · {today.ToString("D", CultureInfo.CurrentCulture)}");

            var screen = LiveUsageProvider.GetTodayActiveTime();
            _out.WriteLine($"{Loc.Get("Common_ScreenTime")}: {TimeFormatHelper.FormatCompact(screen)}");

            var goal = new GoalService();
            if (goal.GetDailyScreenTimeGoal() is > 0)
                _out.WriteLine(goal.FormatProgressText(screen));

            _out.WriteLine();
            WriteTitle(Loc.Get("Cli_TopAppsToday"));
            var apps = LiveUsageProvider.GetTodayAppEntries().Take(5)
                .Select(a => (AppNameService.GetDisplayName(a.AppName, a.ExecutablePath), a.Duration))
                .ToList();
            WriteAppRows(apps);

            WriteNotRecordingHint();
            return Ok;
        }

        private int Week()
        {
            var report = new ReportService();
            var weekStart = ReportService.GetWeekStart(DateTime.Today);
            var weekEnd = ReportService.GetWeekEnd(DateTime.Today);
            WriteTitle($"{Loc.Get("Cli_ThisWeek")} · {FormatRange(weekStart, weekEnd)}");

            // Same source and arithmetic as the Dashboard's "This week" card.
            var trend = report.GetDailyScreenTimeTrend(weekStart, weekEnd);
            var max = Math.Max(1, trend.Count > 0 ? trend.Max(d => d.TotalSeconds) : 1);
            var labelWidth = trend.Max(d => DisplayWidth(d.Date.ToString("ddd", CultureInfo.CurrentCulture)));
            foreach (var day in trend)
            {
                var label = PadRight(day.Date.ToString("ddd", CultureInfo.CurrentCulture), labelWidth);
                var value = TimeFormatHelper.FormatCompact(TimeSpan.FromSeconds(day.TotalSeconds));
                _out.Write($"  {label}  {value,-8} ");
                WriteBar(day.TotalSeconds / (double)max, day.Date == DateTime.Today);
                _out.WriteLine();
            }

            var totalMinutes = trend.Sum(d => d.TotalSeconds / 60.0);
            _out.WriteLine();
            _out.WriteLine(Loc.Format("Cli_WeekTotal",
                TimeFormatHelper.FormatCompact(TimeSpan.FromMinutes(totalMinutes)),
                TimeFormatHelper.FormatCompact(TimeSpan.FromMinutes(totalMinutes / 7.0))));

            // Nothing recorded last week (e.g. the first week with Pulse): there is nothing to compare.
            var comparison = report.GetWeekOverWeekComparison(weekStart);
            if (comparison.LastWeekScreenTime > TimeSpan.Zero)
            {
                var changePercent = comparison.ScreenTimeChangePercent;
                _out.WriteLine(Math.Abs(changePercent) < 1
                    ? Loc.Get("Dashboard_SameAsLastWeek")
                    : Loc.Format("Dashboard_WeeklyChange", comparison.ScreenTimeImproved ? "↓" : "↑",
                        Math.Abs(changePercent).ToString("F0")));
            }

            WriteNotRecordingHint();
            return Ok;
        }

        private int Apps(string[] options)
        {
            bool week = false;
            int count = 10;
            for (int i = 0; i < options.Length; i++)
            {
                var option = options[i].ToLowerInvariant();
                if (option is "--week" or "-w") week = true;
                else if (option is "-n" or "--top")
                {
                    if (i + 1 >= options.Length
                        || !int.TryParse(options[i + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var n)
                        || n < 1)
                        return Fail(Loc.Format("Cli_BadNumber", options[i]));
                    count = Math.Min(n, 100);
                    i++;
                }
                else return Fail(Loc.Format("Cli_BadOption", options[i], "apps"));
            }

            return WithData(() =>
            {
                List<(string Name, TimeSpan Duration)> apps;
                if (week)
                {
                    var report = new ReportService();
                    var start = ReportService.GetWeekStart(DateTime.Today);
                    var end = ReportService.GetWeekEnd(DateTime.Today);
                    WriteTitle($"{Loc.Get("Cli_TopAppsWeek")} · {FormatRange(start, end)}");
                    apps = report.GetTopAppsForPeriod(start, end, count)
                        .Select(a => (a.AppName, TimeSpan.FromSeconds(a.TotalSeconds))).ToList();
                }
                else
                {
                    WriteTitle(Loc.Get("Cli_TopAppsToday"));
                    apps = LiveUsageProvider.GetTodayAppEntries().Take(count)
                        .Select(a => (AppNameService.GetDisplayName(a.AppName, a.ExecutablePath), a.Duration))
                        .ToList();
                }
                WriteAppRows(apps);
                WriteNotRecordingHint();
                return Ok;
            });
        }

        private int Start()
        {
            if (IsAppRunning())
            {
                _out.WriteLine(Loc.Get("Cli_AlreadyRunning"));
                return Ok;
            }

            var exe = Path.Combine(AppContext.BaseDirectory, "DigitalWellbeing.exe");
            if (!File.Exists(exe))
                return Fail(Loc.Get("Cli_AppNotFound"));

            _out.WriteLine(Loc.Get("Cli_Starting"));
            try
            {
                // --startup = the same quiet launch Windows uses at sign-in: tray only, no window.
                // Shell launch so the app doesn't inherit this console's handles: an inherited
                // output pipe stays open while the app runs, so `$x = pulse start` never returned.
                using var _ = Process.Start(new ProcessStartInfo(exe, "--startup")
                {
                    UseShellExecute = true,
                    WorkingDirectory = AppContext.BaseDirectory
                });
            }
            catch (Exception ex)
            {
                return Fail(Loc.Format("Cli_StartFailed", ex.Message));
            }

            if (!WaitUntil(IsAppRunning, StartStopTimeout))
                return Fail(Loc.Format("Cli_StartTimeout", (int)StartStopTimeout.TotalSeconds));

            WriteColored(Loc.Get("Cli_StatusRecording"), ConsoleColor.Green);
            return Ok;
        }

        private int Stop()
        {
            if (!IsAppRunning())
            {
                _out.WriteLine(Loc.Get("Cli_StatusNotRunning"));
                return Ok;
            }

            _out.WriteLine(Loc.Get("Cli_Stopping"));
            // A Pulse that is still starting up isn't listening yet, so keep asking for a while.
            if (!WaitUntil(() => PulseInstance.RequestExit() || !IsAppRunning(), StartStopTimeout))
                return Fail(Loc.Get("Cli_StopUnsupported"));

            if (!WaitUntil(() => !IsAppRunning(), StartStopTimeout))
                return Fail(Loc.Format("Cli_StopTimeout", (int)StartStopTimeout.TotalSeconds));

            _out.WriteLine(Loc.Get("Cli_Stopped"));
            return Ok;
        }

        // ---------------- helpers ----------------

        /// <summary>Last tracker heartbeat (local time); null if none or the app never created the table.</summary>
        private static DateTime? LastSaved()
        {
            try
            {
                return DatabaseService.GetAllTrackerHeartbeats()
                    .Select(h => (DateTime?)h.LastSeenUtc.ToLocalTime()).Max();
            }
            catch (SQLite.SQLiteException)
            {
                return null;
            }
        }

        /// <summary>Runs a data command when Pulse has recorded something; wires in live numbers.</summary>
        private int WithData(Func<int> command)
        {
            if (!File.Exists(DatabasePath))
            {
                _err.WriteLine(Loc.Get("Cli_NoData"));
                return NoData;
            }
            // live.json from a Pulse that crashed holds time that was never saved; skip it.
            if (IsAppRunning())
                LiveData.Connect(DataFolder, DateTime.Now);
            return command();
        }

        private int? NoOptions(string command, string[] options)
            => options.Length == 0 ? null : Fail(Loc.Format("Cli_BadOption", options[0], command));

        private int Fail(string message)
        {
            _err.WriteLine(message);
            return Error;
        }

        private void WriteNotRecordingHint()
        {
            if (IsAppRunning()) return;
            _out.WriteLine();
            WriteColored(Loc.Get("Cli_NotRecordingHint"), ConsoleColor.Yellow);
        }

        /// <summary>
        /// App names come from other programs' file descriptions, so they could carry terminal
        /// control sequences; print them as plain text.
        /// </summary>
        internal static string Printable(string text)
            => string.Concat(text.Select(c => char.IsControl(c) ? '?' : c));

        private void WriteAppRows(List<(string Name, TimeSpan Duration)> apps)
        {
            if (apps.Count == 0)
            {
                _out.WriteLine($"  {Loc.Get("Cli_NoAppTime")}");
                return;
            }

            apps = apps.Select(a => (Printable(a.Name), a.Duration)).ToList();
            var nameWidth = Math.Min(28, apps.Max(a => DisplayWidth(a.Name)));
            var max = Math.Max(1, apps.Max(a => a.Duration.TotalSeconds));
            foreach (var (name, duration) in apps)
            {
                _out.Write($"  {PadRight(Truncate(name, nameWidth), nameWidth)}  {TimeFormatHelper.FormatCompact(duration),-8} ");
                WriteBar(duration.TotalSeconds / max, highlight: false);
                _out.WriteLine();
            }
        }

        private void WriteBar(double fraction, bool highlight)
        {
            var cells = (int)Math.Round(Math.Clamp(fraction, 0, 1) * BarWidth);
            if (fraction > 0 && cells == 0) cells = 1;
            if (_color) Console.ForegroundColor = highlight ? ConsoleColor.Cyan : ConsoleColor.DarkCyan;
            _out.Write(new string('█', cells));
            if (_color) Console.ResetColor();
        }

        private void WriteTitle(string text)
        {
            if (_color) Console.ForegroundColor = ConsoleColor.White;
            _out.WriteLine(text);
            if (_color) Console.ResetColor();
        }

        private void WriteColored(string text, ConsoleColor color)
        {
            if (_color) Console.ForegroundColor = color;
            _out.WriteLine(text);
            if (_color) Console.ResetColor();
        }

        private static string FormatRange(DateTime start, DateTime end)
        {
            var pattern = CultureInfo.CurrentCulture.DateTimeFormat.MonthDayPattern;
            return $"{start.ToString(pattern, CultureInfo.CurrentCulture)} – {end.ToString(pattern, CultureInfo.CurrentCulture)}";
        }

        private static string FormatWhen(DateTime local)
            => local.Date == DateTime.Today
                ? local.ToString("t", CultureInfo.CurrentCulture)
                : local.ToString("g", CultureInfo.CurrentCulture);

        private static bool WaitUntil(Func<bool> condition, TimeSpan timeout)
        {
            var sw = Stopwatch.StartNew();
            while (sw.Elapsed < timeout)
            {
                if (condition()) return true;
                Thread.Sleep(250);
            }
            return condition();
        }

        public static string AppVersion
        {
            get
            {
                var version = typeof(CliApp).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                              ?? typeof(CliApp).Assembly.GetName().Version?.ToString() ?? "?";
                var plus = version.IndexOf('+');
                return plus > 0 ? version[..plus] : version;
            }
        }

        // Console column width: East Asian wide characters take two cells.
        internal static int DisplayWidth(string text)
            => text.Sum(c => IsWide(c) ? 2 : 1);

        private static bool IsWide(char c)
            => c >= 0x1100 && (c <= 0x115F || (c >= 0x2E80 && c <= 0xA4CF) || (c >= 0xAC00 && c <= 0xD7A3)
                || (c >= 0xF900 && c <= 0xFAFF) || (c >= 0xFE30 && c <= 0xFE4F) || (c >= 0xFF00 && c <= 0xFF60)
                || (c >= 0xFFE0 && c <= 0xFFE6));

        internal static string PadRight(string text, int width)
            => text + new string(' ', Math.Max(0, width - DisplayWidth(text)));

        internal static string Truncate(string text, int width)
        {
            if (DisplayWidth(text) <= width) return text;
            var sb = new StringBuilder();
            foreach (var c in text)
            {
                if (DisplayWidth(sb.ToString()) + (IsWide(c) ? 2 : 1) > width - 1) break;
                sb.Append(c);
            }
            return sb.Append('…').ToString();
        }
    }
}
