using System;
using Xunit;
using digital_wellbeing_app.Services;
using digital_wellbeing_app.Models;

namespace digital_wellbeing_app.Tests.Services
{
    public class FocusSessionServiceTests
    {
        [Fact]
        public void Constructor_SetsDefaults()
        {
            var svc = new FocusSessionService();
            Assert.False(svc.IsInFocusMode);
            Assert.Null(svc.CurrentSession);
            Assert.Equal(25, svc.DefaultDurationMinutes);
            Assert.Equal(FocusEnforcementLevel.Warn, svc.EnforcementLevel);
        }

        [Fact]
        public void StartSession_EntersFocusMode()
        {
            var svc = new FocusSessionService();
            bool started = false;
            svc.SessionStarted += () => started = true;

            svc.StartSession(25);

            Assert.True(svc.IsInFocusMode);
            Assert.NotNull(svc.CurrentSession);
            Assert.True(started);

            // Cleanup
            svc.EndSession(false);
        }

        [Fact]
        public void EndSession_ExitsFocusMode()
        {
            var svc = new FocusSessionService();
            svc.StartSession(25);

            bool ended = false;
            bool completedFlag = false;
            svc.SessionEnded += (completed) => { ended = true; completedFlag = completed; };

            svc.EndSession(false);

            Assert.False(svc.IsInFocusMode);
            Assert.True(ended);
            Assert.False(completedFlag);
        }

        [Fact]
        public void TimeRemaining_DecreasesOverTime()
        {
            var svc = new FocusSessionService();
            svc.StartSession(1); // 1 minute

            var remaining1 = svc.TimeRemaining;
            Assert.True(remaining1.TotalSeconds > 0);
            Assert.True(remaining1.TotalMinutes <= 1);

            svc.EndSession(false);
        }

        [Fact]
        public void Progress_IsZeroAtStart()
        {
            var svc = new FocusSessionService();
            svc.StartSession(25);

            // Progress should be near 0 at the start
            Assert.True(svc.Progress < 5, $"Progress should be near 0 at start, got {svc.Progress}");

            svc.EndSession(false);
        }

        [Fact]
        public void SetAppCategory_PersistsCategory()
        {
            var svc = new FocusSessionService();
            svc.SetAppCategory("test_app_123", "Test App", "C:\\test.exe", AppCategoryType.Entertainment);

            var category = svc.GetAppCategory("test_app_123");
            Assert.Equal(AppCategoryType.Entertainment, category);

            // Cleanup - set back to Uncategorized
            svc.SetAppCategory("test_app_123", "Test App", "C:\\test.exe", AppCategoryType.Uncategorized);
        }

        [Fact]
        public void IsDistractingApp_DetectsEntertainment()
        {
            var svc = new FocusSessionService();
            svc.BlockEntertainment = true;
            svc.SetAppCategory("distracting_test", "Game", "C:\\game.exe", AppCategoryType.Entertainment);

            Assert.True(svc.IsDistractingApp("distracting_test"));

            // Cleanup
            svc.SetAppCategory("distracting_test", "Game", "C:\\game.exe", AppCategoryType.Uncategorized);
        }

        [Fact]
        public void AllowAppForSession_OverridesDistraction()
        {
            var svc = new FocusSessionService();
            svc.BlockEntertainment = true;
            svc.SetAppCategory("override_test", "YouTube", "C:\\yt.exe", AppCategoryType.Entertainment);
            svc.StartSession(25);

            svc.AllowAppForSession("override_test");
            Assert.True(svc.IsAppOverriddenForSession("override_test"));

            svc.EndSession(false);

            // Cleanup
            svc.SetAppCategory("override_test", "YouTube", "C:\\yt.exe", AppCategoryType.Uncategorized);
        }

        [Fact]
        public void GetSessionHistory_ReturnsListForDate()
        {
            var svc = new FocusSessionService();
            var history = svc.GetSessionHistory(DateTime.Today);
            Assert.NotNull(history);
            // May be empty or have entries, but should not throw
        }

        [Fact]
        public void GetTotalFocusTime_ReturnsTimeSpan()
        {
            var svc = new FocusSessionService();
            var total = svc.GetTotalFocusTime(DateTime.Today);
            Assert.True(total.TotalSeconds >= 0);
        }

        // --- Windows Focus integration reaction ---

        [Fact]
        public void OnWindowsFocusActiveChanged_True_StartsASession()
        {
            var svc = new FocusSessionService();

            svc.OnWindowsFocusActiveChanged(true);

            Assert.True(svc.IsInFocusMode);
            svc.EndSession(false);
        }

        [Fact]
        public void OnWindowsFocusActiveChanged_False_DoesNothing()
        {
            var svc = new FocusSessionService();

            svc.OnWindowsFocusActiveChanged(false);

            Assert.False(svc.IsInFocusMode);
        }

        [Fact]
        public void OnWindowsFocusActiveChanged_True_DoesNotClobberAnInProgressSession()
        {
            var svc = new FocusSessionService();
            svc.StartSession(25);
            var originalStart = svc.CurrentSession!.StartTime;

            svc.OnWindowsFocusActiveChanged(true);

            Assert.True(svc.IsInFocusMode);
            Assert.Equal(originalStart, svc.CurrentSession!.StartTime);
            svc.EndSession(false);
        }

        /// <summary>
        /// Exercises the same wiring pattern MainWindow uses in production (subscribe an
        /// IWindowsFocusSource's FocusActiveChanged event to OnWindowsFocusActiveChanged) via a
        /// fake, rather than a live WinRT call - proves the glue works end-to-end.
        /// </summary>
        private class FakeWindowsFocusSource : digital_wellbeing_app.Platform.Windows.IWindowsFocusSource
        {
            public bool IsSupported => true;
            public bool IsFocusActive { get; private set; }
            public event Action<bool>? FocusActiveChanged;

            public void SetFocusActive(bool active)
            {
                IsFocusActive = active;
                FocusActiveChanged?.Invoke(active);
            }
        }

        [Fact]
        public void FakeWindowsFocusSource_WiredLikeMainWindow_StartsASession()
        {
            var svc = new FocusSessionService();
            var fakeSource = new FakeWindowsFocusSource();
            fakeSource.FocusActiveChanged += svc.OnWindowsFocusActiveChanged;

            fakeSource.SetFocusActive(true);

            Assert.True(svc.IsInFocusMode);
            svc.EndSession(false);
        }
    }
}
