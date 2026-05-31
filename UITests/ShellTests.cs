using FlaUI.Core.AutomationElements;
using Xunit;

namespace DigitalWellbeing.UITests;

/// <summary>
/// Smoke tests for the Pulse shell: window chrome, icon rail navigation between every
/// view, per-section top-bar title, and the top-bar controls (theme toggle, privacy pill).
/// </summary>
[Collection("Shell")]
public class ShellTests
{
    private readonly AppSession _app;

    public ShellTests(AppSession app) => _app = app;

    [Fact]
    public void Window_opens_with_correct_title()
    {
        Assert.Equal("Pulse", _app.Window.Title);
        _app.Shot("00-launch");
    }

    [Fact]
    public void Rail_has_all_nav_items()
    {
        foreach (var id in new[]
        {
            "NavDashboard", "NavScreen", "NavApps", "NavSound", "NavFocus",
            "NavReports", "NavSettings", "NavHelp",
        })
        {
            Assert.NotNull(_app.TryFind(id));
        }
    }

    [Fact]
    public void Topbar_has_controls()
    {
        Assert.NotNull(_app.TryFind("ThemeToggle"));
        Assert.NotNull(_app.TryFind("PrivatePill"));
        Assert.NotNull(_app.TryFind("PageTitle"));
    }

    [Theory]
    [InlineData("NavScreen", "Screen Time")]
    [InlineData("NavApps", "App Usage")]
    [InlineData("NavSound", "Hearing")]
    [InlineData("NavFocus", "Focus")]
    [InlineData("NavReports", "Insights")]
    [InlineData("NavSettings", "Settings")]
    [InlineData("NavHelp", "Help")]
    public void Navigating_updates_page_title(string navId, string expectedTitle)
    {
        _app.Nav(navId);
        Assert.Equal(expectedTitle, _app.PageTitle());
        _app.Shot("nav-" + navId);
    }

    [Fact]
    public void Dashboard_title_is_a_greeting()
    {
        _app.Nav("NavDashboard");
        Assert.StartsWith("Good ", _app.PageTitle());
        _app.Shot("nav-NavDashboard");
    }

    [Fact]
    public void Privacy_pill_navigates_to_settings()
    {
        _app.Nav("NavDashboard");
        _app.Find("PrivatePill").Click();
        Thread.Sleep(500);
        Assert.Equal("Settings", _app.PageTitle());
    }

    [Fact]
    public void Theme_toggle_is_clickable()
    {
        var toggle = _app.Find("ThemeToggle");
        toggle.Click();
        Thread.Sleep(400);
        _app.Shot("theme-toggled");
        // toggle back so the session ends in the saved theme
        _app.Find("ThemeToggle").Click();
        Thread.Sleep(400);
    }
}
