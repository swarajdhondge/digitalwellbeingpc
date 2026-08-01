using System.Diagnostics;
using Xunit;

namespace DigitalWellbeing.UITests;

/// <summary>
/// Opt-in soak tests (not part of the normal fast test run - see
/// .github/workflows/soak-test.yml, which is manually/scheduled-triggered, not on every push).
/// Launches the real built exe as a real OS process, starts a Focus session (so
/// FocusSessionService.StartSession's "persist immediately so crash recovery can find it" write
/// actually lands), then genuinely kills the process (Process.Kill(true), not a graceful Stop) and
/// relaunches - stressing the orphan-recovery path FocusSessionServiceTests/FocusRecoveryTests
/// only exercise against an in-process instance, never a real killed-and-relaunched app. Repeats
/// the cycle to accumulate soak coverage instead of a single kill/relaunch.
///
/// Deliberately does NOT use the shared ShellCollection/AppSession fixture other UITests classes
/// use - this needs to construct/kill/reconstruct the app repeatedly within one test method, not
/// share one long-lived instance across a whole test class.
/// </summary>
public class SoakTests
{
    private const int CycleCount = 3;

    [Fact]
    public void KillAndRelaunch_WithActiveFocusSession_RecoversWithoutCrashOrHang()
    {
        for (int cycle = 1; cycle <= CycleCount; cycle++)
        {
            // AppSession's own constructor throws if the main window doesn't appear within 30s -
            // on cycles 2+, that's the real assertion: it did NOT hang or crash on startup despite
            // an orphaned session left behind by the previous cycle's hard kill.
            var session = new AppSession();
            try
            {
                session.Nav("NavFocus");
                session.Find("StartFocusButton").Click();

                // Let the click handler + FocusSessionService.StartSession's immediate DB write
                // land before the kill.
                Thread.Sleep(1500);

                KillHard(session.App.ProcessId);
            }
            finally
            {
                // AppSession.Dispose() already wraps App.Kill()/App.Dispose()/Automation.Dispose()
                // in try/catch each, so calling it on an already-hard-killed process is a safe
                // no-op cleanup of the FlaUI-side wrapper objects.
                session.Dispose();
            }
        }

        // One final relaunch to confirm the app is left in a clean, responsive state after the
        // last kill - not just "the loop above didn't throw".
        using var finalSession = new AppSession();
        finalSession.Nav("NavDashboard");
        Assert.False(string.IsNullOrWhiteSpace(finalSession.PageTitle()));
    }

    private static void KillHard(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(5000);
            }
        }
        catch
        {
            // Already exited, or couldn't be found - either way, the point (it's gone) holds.
        }
    }
}
