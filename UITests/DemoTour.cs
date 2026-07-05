using System.Diagnostics;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Tools;
using FlaUI.UIA3;
using Xunit;

namespace DigitalWellbeing.UITests;

/// <summary>
/// Automated demo-video driver: launches the built Pulse app in its NORMAL window
/// (nice corner-pinned chrome — NOT screenshot mode), starts an FFmpeg gdigrab
/// screen-recording of the "Pulse" window, then walks a timed tour of every main
/// section (Dashboard → Screen Time → App Usage +Week → Hearing → Focus → Insights
/// → Settings + a theme toggle). When the tour finishes it stops FFmpeg gracefully
/// (sends "q" to its stdin so the MP4 is finalized) and closes the app.
///
/// This is the video analogue of <see cref="ScreenshotCapture"/> and follows the
/// same self-contained FlaUI patterns. It is a LOCAL pre-release step driven by
/// <c>scripts/record-demo.ps1</c> — it cannot run on hosted CI (no GUI to capture).
///
/// It is OPT-IN: the [Fact] no-ops unless <c>PULSE_DEMO_OUT</c> is set, so a plain
/// <c>dotnet test</c> never tries to record. Drive it with:
///     PULSE_DEMO_OUT=...\demo.mp4 dotnet test UITests\UITests.csproj \
///         --filter "FullyQualifiedName~DemoTour"
///
/// Environment overrides:
///   PULSE_DEMO_OUT   output MP4 path (REQUIRED — also gates the test)
///   PULSE_FFMPEG     path to ffmpeg.exe (default: "ffmpeg" on PATH)
///   PULSE_APP_EXE    full path to DigitalWellbeing.exe (default: newest build
///                    found under digital-wellbeing-app\bin\{Release,Debug})
///   PULSE_DEMO_FPS   capture framerate (default: 30)
/// </summary>
public sealed class DemoTour
{
    // Match the app's default / the screenshot pipeline so chrome + layout are identical.
    private const int WinWidth = 1220;
    private const int WinHeight = 870;

    /// <summary>One "beat" of the tour: navigate to a rail section, then dwell so the
    /// viewer can take it in. Dwell is in milliseconds. Kept DELIBERATELY SHORT — the
    /// demo's whole job is "here's what the app is" in ~15s, not a slow feature crawl.
    /// The Dashboard opens the video and gets the longest hold (the hero / "what is this");
    /// every other section is a quick, scrolled flash so a short attention span never checks out.
    ///
    /// SMART / FUTURE-PROOF: the tour is NOT a hardcoded section list. It discovers the rail's
    /// nav buttons at runtime (every RadioButton whose AutomationId starts with "Nav"), tours
    /// them in visual top-to-bottom order, and auto-scrolls any page whose content overflows.
    /// So a NEW feature page added later (its own Nav* rail button) is included in the demo with
    /// no change here. Only the framework pages below are treated specially by id.</summary>

    // The hero (opens the video, longest hold). If absent, the first discovered nav item is used.
    private const string HeroNavId = "NavDashboard";
    // Help is docs, not a feature — skipped in the tour.
    private const string HelpNavId = "NavHelp";

    private const int DwellMs = 1200;      // base per-section hold
    private const int HeroDwellMs = 2400;  // opening hold on the hero

    [Fact]
    public void Record_demo_tour()
    {
        var outPath = Environment.GetEnvironmentVariable("PULSE_DEMO_OUT");
        if (string.IsNullOrWhiteSpace(outPath))
        {
            // Opt-in only: without an output path this is a no-op so `dotnet test`
            // (no filter) doesn't launch a recording. record-demo.ps1 sets it.
            return;
        }
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outPath))!);

        var ffmpeg = Environment.GetEnvironmentVariable("PULSE_FFMPEG") ?? "ffmpeg";
        var fps = Environment.GetEnvironmentVariable("PULSE_DEMO_FPS") ?? "30";

        // Single-instance app (per-user mutex) — make sure nothing is running.
        foreach (var p in Process.GetProcessesByName("DigitalWellbeing"))
        {
            try { p.Kill(true); p.WaitForExit(3000); } catch { /* ignore */ }
        }

        var exe = ResolveExe();
        var exeDir = Path.GetDirectoryName(exe)!;

        // Pin the OPENING theme to Dark so the demo is deterministic (open dark -> flip to
        // light at the end, every run). ThemeService reads/writes %LocalAppData%\Pulse\theme.json
        // as {"Mode":"Dark"} — NOT the exe dir — and our own end-of-tour ThemeToggle persists the
        // flip there, so without this the app would open in whatever the previous run left.
        try
        {
            var pulseDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Pulse");
            Directory.CreateDirectory(pulseDir);
            File.WriteAllText(Path.Combine(pulseDir, "theme.json"), "{\"Mode\":\"Dark\"}");
        }
        catch { /* best effort */ }

        // NOTE: deliberately do NOT write %LocalAppData%\Pulse\.screenshot-mode — the demo
        // shows the real, corner-pinned window chrome (screenshot mode is edge-to-edge opaque).

        var app = Application.Launch(new ProcessStartInfo(exe) { WorkingDirectory = exeDir });
        using var automation = new UIA3Automation();
        Process? rec = null;
        try
        {
            var window = app.GetMainWindow(automation, TimeSpan.FromSeconds(30))
                         ?? throw new InvalidOperationException("Main window did not appear within 30s.");

            SizeWindow(window);

            // Glide the cursor between targets instead of teleporting — reads like a real
            // person using the app (a YouTube-style walkthrough), and makes clicks visible.
            FlaUI.Core.Input.Mouse.MovePixelsPerMillisecond = 6;

            // Discover the rail's nav buttons NOW (order top-to-bottom). Future feature pages
            // appear here automatically. Content beats = everything except the hero (which opens
            // the video) and Help (docs, skipped). Settings IS toured — it shows the privacy card.
            var navIds = DiscoverNavIds(window);
            var heroId = navIds.Contains(HeroNavId) ? HeroNavId : navIds.FirstOrDefault();
            var contentIds = navIds
                .Where(id => id != heroId && id != HelpNavId)
                .ToList();

            // Land on the hero and let WPF settle the first view + theme + layout BEFORE
            // recording starts, so the video opens on a clean, stable frame.
            if (heroId != null) Nav(window, heroId, settleMs: 400);
            Thread.Sleep(1000);

            // Capture the window's TRUE visible screen rect (DWM frame bounds, physical px)
            // and record THAT region of the desktop. gdigrab's window-title capture uses GDI
            // BitBlt, which returns solid black for this WPF transparent/GPU-composited window;
            // desktop-region capture composites correctly (same trick as the screenshot pipeline).
            var rect = GetWindowRect(window);
            rec = StartRecording(ffmpeg, fps, outPath, rect);

            // Opening hold on the hero — the "what is this app" shot — then a gentle browse-scroll.
            Thread.Sleep(HeroDwellMs);
            SmartScroll(window, rect);

            // --- Content sections (auto-discovered) ---
            foreach (var navId in contentIds)
            {
                Nav(window, navId);
                Thread.Sleep(DwellMs);

                // Any page exposing the App Usage range toggle gets a Today -> Week flip.
                if (ClickIfPresent(window, "AppRangeWeek")) Thread.Sleep(1200);

                // Auto-scroll if (and only if) the page overflows — works for future pages too.
                SmartScroll(window, rect);
            }

            // --- Outro: return to the colorful hero and flip the theme live (Dark -> Light).
            // The whole app recolors — a strong closer — and it avoids ending on the Settings
            // page, whose segmented Theme control doesn't track the top-bar quick-toggle (so it
            // would read the wrong mode). Flipping on the hero keeps the ending consistent.
            if (heroId != null) { Nav(window, heroId); Thread.Sleep(700); }
            ClickIfPresent(window, "ThemeToggle");
            Thread.Sleep(1600);

            // Hold the final (Light) frame for a beat, then wrap.
            Thread.Sleep(700);
        }
        finally
        {
            StopRecording(rec);
            try { app.Close(); } catch { /* ignore */ }
            try { app.Kill(); } catch { /* ignore */ }
            try { app.Dispose(); } catch { /* ignore */ }
        }

        Assert.True(File.Exists(outPath) && new FileInfo(outPath).Length > 0,
            $"FFmpeg did not produce a demo video at {outPath}.");
    }

    // --- FFmpeg control -----------------------------------------------------------

    /// <summary>Start an FFmpeg gdigrab recording of the desktop region occupied by the
    /// Pulse window (physical-pixel rect). Video is H.264 yuv420p (broadly compatible) at
    /// a visually-lossless CRF; dimensions are even (yuv420p requirement). stdin is kept
    /// open so we can stop it with "q".</summary>
    private static Process StartRecording(string ffmpeg, string fps, string outPath, RECT rect)
    {
        // gdigrab offset/size are in physical pixels. Force even width/height for yuv420p.
        var w = (rect.Right - rect.Left) & ~1;
        var h = (rect.Bottom - rect.Top) & ~1;

        var args =
            $"-y -hide_banner -loglevel warning " +
            $"-f gdigrab -framerate {fps} " +
            $"-offset_x {rect.Left} -offset_y {rect.Top} -video_size {w}x{h} -i desktop " +
            $"-c:v libx264 -preset veryfast -crf 20 -pix_fmt yuv420p " +
            $"-movflags +faststart \"{outPath}\"";

        var psi = new ProcessStartInfo(ffmpeg, args)
        {
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        var proc = Process.Start(psi)
                   ?? throw new InvalidOperationException("Failed to start ffmpeg.");
        // Drain stderr so the pipe buffer can't fill and stall ffmpeg on a long capture.
        proc.ErrorDataReceived += (_, _) => { };
        proc.BeginErrorReadLine();

        if (proc.HasExited)
            throw new InvalidOperationException(
                $"ffmpeg exited immediately (code {proc.ExitCode}); check the ffmpeg path and the 'Pulse' window title.");
        return proc;
    }

    /// <summary>Stop FFmpeg gracefully by sending "q" on stdin so it flushes and finalizes
    /// the MP4 (killing it would leave a truncated/unplayable file). Falls back to Kill.</summary>
    private static void StopRecording(Process? rec)
    {
        if (rec == null) return;
        try
        {
            if (!rec.HasExited)
            {
                rec.StandardInput.Write("q");
                rec.StandardInput.Flush();
                if (!rec.WaitForExit(15000))
                {
                    try { rec.Kill(); } catch { /* ignore */ }
                }
            }
        }
        catch { try { rec.Kill(); } catch { /* ignore */ } }
        finally { try { rec.Dispose(); } catch { /* ignore */ } }
    }

    // --- Window geometry (physical-pixel bounds for the desktop-region capture) ----

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr hwnd, int attr, out RECT rect, int size);

    private const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int cx, int cy, uint flags);

    private static readonly IntPtr HWND_TOPMOST = new(-1);
    private const uint SWP_NOMOVE = 0x0002, SWP_NOSIZE = 0x0001, SWP_NOACTIVATE = 0x0010;

    /// <summary>The window's TRUE visible bounds in physical pixels (DWM frame bounds, which
    /// exclude the transparent drop-shadow margin) — exactly the region to screen-record.</summary>
    private static RECT GetWindowRect(Window window)
    {
        var hwnd = window.Properties.NativeWindowHandle.ValueOrDefault;
        if (hwnd != IntPtr.Zero &&
            DwmGetWindowAttribute(hwnd, DWMWA_EXTENDED_FRAME_BOUNDS, out var r,
                System.Runtime.InteropServices.Marshal.SizeOf<RECT>()) == 0 &&
            r.Right > r.Left && r.Bottom > r.Top)
        {
            return r;
        }
        throw new InvalidOperationException("Could not read the window's DWM frame bounds for capture.");
    }

    // --- Window / navigation helpers (mirror ScreenshotCapture) --------------------

    /// <summary>Resize + reposition to a fixed, on-screen 1220x870 rectangle (Normal state),
    /// then pin it topmost so nothing can occlude the recorded desktop region mid-tour.</summary>
    private static void SizeWindow(Window window)
    {
        try
        {
            window.Patterns.Window.PatternOrDefault?.SetWindowVisualState(WindowVisualState.Normal);
            Thread.Sleep(150);

            var transform = window.Patterns.Transform.PatternOrDefault;
            if (transform != null)
            {
                if (transform.CanMove) transform.Move(40, 30);
                if (transform.CanResize) transform.Resize(WinWidth, WinHeight);
            }
            window.SetForeground();

            var hwnd = window.Properties.NativeWindowHandle.ValueOrDefault;
            if (hwnd != IntPtr.Zero)
                SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        }
        catch { /* best effort */ }
        Thread.Sleep(300);
    }

    private static void Nav(Window window, string automationId, int settleMs = 650)
    {
        Find(window, automationId).Click();
        Thread.Sleep(settleMs);
    }

    /// <summary>Discover the rail's nav buttons in visual top-to-bottom order: every RadioButton
    /// whose AutomationId starts with "Nav". This is what makes the tour future-proof — a new
    /// feature page's rail button is picked up automatically with no edit here.</summary>
    private static List<string> DiscoverNavIds(Window window)
    {
        var buttons = Retry.WhileEmpty(
            () => window.FindAllDescendants(cf => cf.ByControlType(ControlType.RadioButton)),
            TimeSpan.FromSeconds(8), TimeSpan.FromMilliseconds(200)).Result ?? Array.Empty<AutomationElement>();

        return buttons
            .Select(b => (Id: b.Properties.AutomationId.ValueOrDefault ?? "", Top: SafeTop(b)))
            .Where(x => x.Id.StartsWith("Nav", StringComparison.Ordinal))
            .OrderBy(x => x.Top)
            .Select(x => x.Id)
            .Distinct()
            .ToList();
    }

    private static double SafeTop(AutomationElement e)
    {
        try { return e.BoundingRectangle.Top; } catch { return double.MaxValue; }
    }

    /// <summary>Click an element if it's present; returns whether it was clicked. Used for
    /// optional controls (Week toggle, theme toggle) so a missing id degrades gracefully.</summary>
    private static bool ClickIfPresent(Window window, string automationId)
    {
        var el = Retry.WhileNull(
            () => window.FindFirstDescendant(cf => cf.ByAutomationId(automationId)),
            TimeSpan.FromMilliseconds(2500),
            TimeSpan.FromMilliseconds(150)).Result;
        if (el == null) return false;
        try { el.Click(); return true; } catch { return false; }
    }

    /// <summary>Natural browse-scroll: if the current page's content overflows its viewport,
    /// glide the cursor over it and mouse-wheel gently down (several small steps so it eases
    /// rather than jumps), then ease part-way back up. No-op when the page fits. Content-length
    /// agnostic, so future longer pages scroll too.</summary>
    private static void SmartScroll(Window window, RECT rect)
    {
        var scroll = FindScrollable(window);
        if (scroll == null) return;

        // Park the cursor over the content column (right of the ~70px rail), mid-height.
        int x = rect.Left + (int)((rect.Right - rect.Left) * 0.60);
        int y = rect.Top + (int)((rect.Bottom - rect.Top) * 0.55);
        FlaUI.Core.Input.Mouse.MoveTo(x, y);
        Thread.Sleep(200);

        // Down: a few gentle notches. WPF scrolls by lines per notch, so small steps + delays
        // read as a smooth glide on the recording. Then ease back up so the end frame isn't cut off.
        for (int i = 0; i < 4 && CanScrollDown(scroll); i++)
        {
            FlaUI.Core.Input.Mouse.Scroll(-1);
            Thread.Sleep(130);
        }
        Thread.Sleep(350);
        for (int i = 0; i < 2; i++)
        {
            FlaUI.Core.Input.Mouse.Scroll(1);
            Thread.Sleep(130);
        }
        Thread.Sleep(250);
    }

    /// <summary>The first descendant that is vertically scrollable right now (a ScrollPattern
    /// whose view is smaller than 100%). Returns null if the page fits on screen.</summary>
    private static AutomationElement? FindScrollable(Window window)
    {
        try
        {
            // Scan descendants for a usable, currently-scrollable ScrollPattern (a ScrollViewer
            // whose vertical view is smaller than 100% == content overflows).
            foreach (var el in window.FindAllDescendants())
            {
                var sp = el.Patterns.Scroll.PatternOrDefault;
                if (sp != null && sp.VerticallyScrollable.ValueOrDefault &&
                    sp.VerticalViewSize.ValueOrDefault is > 0 and < 99)
                    return el;
            }
        }
        catch { /* best effort */ }
        return null;
    }

    private static bool CanScrollDown(AutomationElement scroll)
    {
        try
        {
            var sp = scroll.Patterns.Scroll.PatternOrDefault;
            return sp != null && sp.VerticalScrollPercent.ValueOrDefault is >= 0 and < 99;
        }
        catch { return true; }
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
