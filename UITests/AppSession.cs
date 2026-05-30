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
/// Launches the built Digital Wellbeing app once and exposes its main window for
/// UI-automation assertions. Shared across a test class via <see cref="ShellCollection"/>.
/// </summary>
public sealed class AppSession : IDisposable
{
    public Application App { get; }
    public UIA3Automation Automation { get; }
    public Window Window { get; }
    public string ShotDir { get; }

    public AppSession()
    {
        // The app uses a per-user single-instance mutex; make sure nothing is running.
        foreach (var p in Process.GetProcessesByName("DigitalWellbeing"))
        {
            try { p.Kill(true); p.WaitForExit(3000); } catch { /* ignore */ }
        }

        var exe = ResolveExe();
        ShotDir = Environment.GetEnvironmentVariable("DW_SHOT_DIR")
                  ?? Path.Combine(Path.GetTempPath(), "dw-uishots");
        Directory.CreateDirectory(ShotDir);

        // Force dark theme for comparison shots (the design's default). The app reads
        // theme.json from its working directory, so pin both to the exe folder.
        var exeDir = Path.GetDirectoryName(exe)!;
        try { File.WriteAllText(Path.Combine(exeDir, "theme.json"), "{\"Mode\":\"Dark\"}"); }
        catch { /* ignore */ }

        App = Application.Launch(new ProcessStartInfo(exe) { WorkingDirectory = exeDir });
        Automation = new UIA3Automation();
        Window = App.GetMainWindow(Automation, TimeSpan.FromSeconds(30))
                 ?? throw new InvalidOperationException("Main window did not appear within 30s.");

        // Maximize + foreground so screenshots are clean (not occluded). The content
        // caps + centers itself to ~1080 so it still matches the prototype when wide.
        try
        {
            Window.Patterns.Window.Pattern.SetWindowVisualState(WindowVisualState.Maximized);
            Window.SetForeground();
        }
        catch { /* ignore */ }

        // Give WPF a moment to settle the first view + theme.
        Thread.Sleep(900);
    }

    /// <summary>Find an element by AutomationId, retrying briefly for async UI.</summary>
    public AutomationElement Find(string automationId, int timeoutMs = 8000)
    {
        var result = Retry.WhileNull(
            () => Window.FindFirstDescendant(cf => cf.ByAutomationId(automationId)),
            TimeSpan.FromMilliseconds(timeoutMs),
            TimeSpan.FromMilliseconds(150)).Result;
        return result ?? throw new InvalidOperationException($"Element '{automationId}' not found.");
    }

    public AutomationElement? TryFind(string automationId, int timeoutMs = 2000)
    {
        return Retry.WhileNull(
            () => Window.FindFirstDescendant(cf => cf.ByAutomationId(automationId)),
            TimeSpan.FromMilliseconds(timeoutMs),
            TimeSpan.FromMilliseconds(150)).Result;
    }

    /// <summary>Click a rail nav item and wait for the view swap to settle.</summary>
    public void Nav(string automationId)
    {
        Find(automationId).Click();
        Thread.Sleep(500);
    }

    /// <summary>The current top-bar page title text.</summary>
    public string PageTitle() => Find("PageTitle").Name;

    /// <summary>Save a PNG of the whole window for visual inspection.</summary>
    public string Shot(string name)
    {
        try { Window.Focus(); Window.SetForeground(); } catch { /* best effort */ }
        Thread.Sleep(200);
        var path = Path.Combine(ShotDir, name + ".png");
        Capture.Element(Window).ToFile(path);
        return path;
    }

    private static string ResolveExe()
    {
        var env = Environment.GetEnvironmentVariable("DW_APP_EXE");
        if (!string.IsNullOrEmpty(env) && File.Exists(env)) return env;

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "digital-wellbeing-app", "bin", "Debug",
                "net9.0-windows10.0.19041.0", "DigitalWellbeing.exe");
            if (File.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
        throw new FileNotFoundException(
            "Could not locate DigitalWellbeing.exe. Build the app (Debug) or set DW_APP_EXE.");
    }

    public void Dispose()
    {
        try { Automation.Dispose(); } catch { /* ignore */ }
        try { App.Kill(); } catch { /* ignore */ }
        try { App.Dispose(); } catch { /* ignore */ }
    }
}

[CollectionDefinition("Shell")]
public sealed class ShellCollection : ICollectionFixture<AppSession> { }
