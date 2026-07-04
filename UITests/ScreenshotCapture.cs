using System.Diagnostics;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Capturing;
using FlaUI.Core.Definitions;
using FlaUI.Core.Tools;
using FlaUI.UIA3;
using Xunit;

namespace DigitalWellbeing.UITests;

/// <summary>
/// Phase 3 "screenshot pipeline": launches the built Pulse app at a fixed
/// 1600x1000 window, walks every main section via the icon-rail automation IDs,
/// and captures each section in BOTH the Dark and Light themes to the canonical
/// PNG filenames the README expects (e.g. <c>dashboard.png</c> for dark and
/// <c>dashboard-light.png</c> for light).
///
/// This is intentionally self-contained (it does NOT reuse the shared
/// <see cref="AppSession"/> fixture) because it needs precise control over the
/// window size and the starting theme — the shared fixture maximizes and pins
/// dark. It follows the same FlaUI patterns as AppSession/ShellTests.
///
/// Run it opt-in, after building the app and seeding a fixture DB:
///     dotnet test UITests\UITests.csproj \
///         --filter "FullyQualifiedName~ScreenshotCapture"
///
/// Environment overrides:
///   PULSE_SHOTS_DIR  output directory (default: &lt;repo&gt;\.github\screenshots)
///   PULSE_APP_EXE    full path to DigitalWellbeing.exe (default: newest build
///                    found under digital-wellbeing-app\bin\{Release,Debug})
///   DW_APP_EXE       legacy fallback, same meaning as PULSE_APP_EXE
/// </summary>
public sealed class ScreenshotCapture
{
    // Fixed capture window size — large enough that the content area (which caps
    // + centers itself around ~1080px) renders at its intended wide layout.
    private const int WinWidth = 1600;
    private const int WinHeight = 1000;

    /// <summary>
    /// Rail automation ID -> canonical screenshot base name. The base name is the
    /// DARK-theme file (e.g. "dashboard" => dashboard.png); the light variant is
    /// written as "&lt;base&gt;-light.png". These base names are taken verbatim from
    /// the README screenshot section so the generated files drop straight in.
    ///
    /// NOTE: "Welcome" has no rail nav button or automation ID — it is only shown
    /// on first run (see MainWindow first-run branch / Views/Welcome/WelcomeView).
    /// TODO(integrator): if a Welcome shot is needed, drive it by clearing the
    /// "FirstRunCompleted" settings flag before launch; there is no rail ID to nav to.
    /// </summary>
    private static readonly (string NavId, string BaseName)[] Sections =
    {
        ("NavDashboard", "dashboard"),
        ("NavScreen",    "screentime"),
        ("NavApps",      "appusage"),
        ("NavSound",     "sound"),
        ("NavFocus",     "focusmode"),
        ("NavReports",   "weeklyreport"),
        ("NavSettings",  "settings"),
        ("NavHelp",      "helpsection"),
    };

    [Fact]
    public void Capture_all_sections_in_both_themes()
    {
        // The app is single-instance (per-user mutex); make sure nothing is running.
        foreach (var p in Process.GetProcessesByName("DigitalWellbeing"))
        {
            try { p.Kill(true); p.WaitForExit(3000); } catch { /* ignore */ }
        }

        var exe = ResolveExe();
        var exeDir = Path.GetDirectoryName(exe)!;

        var shotDir = Environment.GetEnvironmentVariable("PULSE_SHOTS_DIR")
                      ?? Path.Combine(RepoRoot(), ".github", "screenshots");
        Directory.CreateDirectory(shotDir);

        // Pin the starting theme to Dark. The app reads theme.json from its working
        // directory (see ThemeService), so write it into the exe folder and launch
        // there. We flip to Light later via the ThemeToggle button.
        try { File.WriteAllText(Path.Combine(exeDir, "theme.json"), "{\"Mode\":\"Dark\"}"); }
        catch { /* best effort */ }

        // Drop a flag file under %LocalAppData%\Pulse (the app's known data dir) so it enters
        // screenshot mode (edge-to-edge opaque window) — captures then have zero desktop bleed and
        // need no cropping. A fixed absolute path is reliable where env vars / exe-dir are not.
        var pulseDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Pulse");
        Directory.CreateDirectory(pulseDir);
        var screenshotFlag = Path.Combine(pulseDir, ".screenshot-mode");
        try { File.WriteAllText(screenshotFlag, "1"); } catch { /* best effort */ }

        var app = Application.Launch(new ProcessStartInfo(exe) { WorkingDirectory = exeDir });
        using var automation = new UIA3Automation();
        try
        {
            var window = app.GetMainWindow(automation, TimeSpan.FromSeconds(30))
                         ?? throw new InvalidOperationException("Main window did not appear within 30s.");

            SizeWindow(window);

            // Let WPF settle the first view + theme + the initial layout pass.
            Thread.Sleep(1200);

            // --- Dark pass (base filenames) ---
            CaptureAll(window, shotDir, suffix: "");

            // Flip to Light via the top-bar ThemeToggle, then re-capture everything.
            ToggleTheme(window);
            Thread.Sleep(700);

            // --- Light pass (-light filenames) ---
            CaptureAll(window, shotDir, suffix: "-light");
        }
        finally
        {
            try { app.Close(); } catch { /* ignore */ }
            try { app.Kill(); } catch { /* ignore */ }
            try { app.Dispose(); } catch { /* ignore */ }
            try { File.Delete(screenshotFlag); } catch { /* ignore */ }
        }
    }

    private static void CaptureAll(Window window, string shotDir, string suffix)
    {
        foreach (var (navId, baseName) in Sections)
        {
            Nav(window, navId);
            Shot(window, Path.Combine(shotDir, baseName + suffix + ".png"));
        }
    }

    /// <summary>Resize + reposition to a fixed, on-screen 1600x1000 rectangle (Normal state).</summary>
    private static void SizeWindow(Window window)
    {
        try
        {
            // Normal state keeps the content capped/centered exactly like the design;
            // we avoid the transparent shadow margin by capturing the opaque WindowBorder
            // element (see Shot) rather than the window bounds.
            window.Patterns.Window.PatternOrDefault?.SetWindowVisualState(WindowVisualState.Normal);
            Thread.Sleep(150);

            var transform = window.Patterns.Transform.PatternOrDefault;
            if (transform != null)
            {
                if (transform.CanMove) transform.Move(40, 30);
                if (transform.CanResize) transform.Resize(WinWidth, WinHeight);
            }
            window.SetForeground();
        }
        catch { /* best effort — a slightly different size still yields usable shots */ }
        Thread.Sleep(300);
    }

    /// <summary>Click a rail nav item and wait for the animated view swap to settle.</summary>
    private static void Nav(Window window, string automationId)
    {
        Find(window, automationId).Click();
        // The content host uses an animated ContentControl; give it time to finish
        // the transition + any async data load before capturing.
        Thread.Sleep(650);
    }

    private static void ToggleTheme(Window window)
    {
        // ThemeToggle lives in the top bar (AutomationId="ThemeToggle"); one click
        // swaps Dark<->Light for the whole window and persists it.
        Find(window, "ThemeToggle").Click();
    }

    [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr hwnd, int attr, out RECT rect, int size);

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    private const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;

    private static void Shot(Window window, string path)
    {
        try { window.Focus(); window.SetForeground(); } catch { /* best effort */ }
        Thread.Sleep(250);

        // FlaUI's Capture.Element uses the UIA BoundingRectangle, which for this WPF
        // AllowsTransparency window extends beyond the visible window and composites the desktop
        // into the edges. Capture the TRUE visible bounds from DWM instead (in screenshot mode the
        // window is opaque edge-to-edge, so this is exactly the app with no bleed and no content
        // cut). Fall back to element capture if DWM bounds are unavailable.
        var hwnd = window.Properties.NativeWindowHandle.ValueOrDefault;
        if (hwnd != IntPtr.Zero &&
            DwmGetWindowAttribute(hwnd, DWMWA_EXTENDED_FRAME_BOUNDS, out var r, System.Runtime.InteropServices.Marshal.SizeOf<RECT>()) == 0 &&
            r.Right > r.Left && r.Bottom > r.Top)
        {
            var rect = new System.Drawing.Rectangle(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top);
            Capture.Rectangle(rect).ToFile(path);
        }
        else
        {
            Capture.Element(window).ToFile(path);
        }
    }

    private static AutomationElement Find(Window window, string automationId, int timeoutMs = 8000)
    {
        var result = Retry.WhileNull(
            () => window.FindFirstDescendant(cf => cf.ByAutomationId(automationId)),
            TimeSpan.FromMilliseconds(timeoutMs),
            TimeSpan.FromMilliseconds(150)).Result;
        return result ?? throw new InvalidOperationException($"Element '{automationId}' not found.");
    }

    private static string ResolveExe()
    {
        var env = Environment.GetEnvironmentVariable("PULSE_APP_EXE")
                  ?? Environment.GetEnvironmentVariable("DW_APP_EXE");
        if (!string.IsNullOrEmpty(env) && File.Exists(env)) return env;

        // Walk up to the repo, then take the newest DigitalWellbeing.exe under
        // bin\Release first (this pipeline builds Release), falling back to Debug.
        var appDir = Path.Combine(RepoRoot(), "digital-wellbeing-app");
        foreach (var config in new[] { "Release", "Debug" })
        {
            var binDir = Path.Combine(appDir, "bin", config);
            if (!Directory.Exists(binDir)) continue;

            var exe = Directory.GetFiles(binDir, "DigitalWellbeing.exe", SearchOption.AllDirectories)
                               .OrderByDescending(File.GetLastWriteTimeUtc)
                               .FirstOrDefault();
            if (exe != null) return exe;
        }

        throw new FileNotFoundException(
            "Could not locate DigitalWellbeing.exe. Build the app (Release) or set PULSE_APP_EXE.");
    }

    /// <summary>Walk up from the test binary to the repo root (the dir holding digital-wellbeing-app).</summary>
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "digital-wellbeing-app")))
                return dir.FullName;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("Could not locate the repo root from " + AppContext.BaseDirectory);
    }
}
