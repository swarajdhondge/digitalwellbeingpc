using System;
using System.Linq;
using Xunit;
using digital_wellbeing_app.Models;
using digital_wellbeing_app.Services;

namespace digital_wellbeing_app.Tests.Services
{
    /// <summary>
    /// Tests for the known-app category rule map and its startup backfill. The core invariant
    /// under test throughout: a suggestion may fill in a blank, but it must never touch a row
    /// that already exists - manual picks (and prior suggestions) always win.
    /// </summary>
    public class CategoryRuleServiceTests : TestBase
    {
        private static void SeedSession(string appName, string execPath, DateTime start, int minutes)
        {
            DatabaseService.SaveAppUsageSession(new AppUsageSession
            {
                AppName = appName,
                ExecutablePath = execPath,
                StartTime = start,
                EndTime = start.AddMinutes(minutes)
            });
        }

        [Theory]
        [InlineData("devenv", AppCategoryType.Work)]
        [InlineData("spotify", AppCategoryType.Entertainment)]
        public void TryGetSuggestedCategory_ReturnsExpectedCategory_ForKnownApps(string key, AppCategoryType expected)
        {
            var found = CategoryRuleService.TryGetSuggestedCategory(key, out var category);
            Assert.True(found);
            Assert.Equal(expected, category);
        }

        [Theory]
        [InlineData("chrome")]   // browsers are deliberately excluded - see class doc comment
        [InlineData("msedge")]
        [InlineData("firefox")]
        [InlineData("some_random_unknown_tool")]
        public void TryGetSuggestedCategory_ReturnsFalse_ForUnmappedApps(string key)
        {
            var found = CategoryRuleService.TryGetSuggestedCategory(key, out _);
            Assert.False(found);
        }

        [Fact]
        public void ApplyRulesToUncategorized_InsertsAutoSuggestedRow_ForRecentlySeenKnownApp()
        {
            DatabaseService.DeleteAllData();
            SeedSession("devenv", @"C:\VS\Common7\IDE\devenv.exe", DateTime.Now.AddDays(-1), 45);

            CategoryRuleService.ApplyRulesToUncategorized();

            var row = DatabaseService.GetAppCategory("devenv");
            Assert.NotNull(row);
            Assert.Equal(AppCategoryType.Work, row!.Category);
            Assert.Equal(CategorySource.AutoSuggested, row.Source);
        }

        [Fact]
        public void ApplyRulesToUncategorized_NeverOverwritesAnExistingManualRow()
        {
            DatabaseService.DeleteAllData();
            SeedSession("devenv", @"C:\VS\Common7\IDE\devenv.exe", DateTime.Now.AddDays(-1), 45);

            // User explicitly (and, for this test, deliberately "wrongly") set devenv to
            // Entertainment before the backfill ever ran.
            DatabaseService.SaveAppCategory(new AppCategory
            {
                AppIdentifier = "devenv",
                AppName = "devenv",
                ExecutablePath = @"C:\VS\Common7\IDE\devenv.exe",
                Category = AppCategoryType.Entertainment,
                Source = CategorySource.Manual,
                LastUpdated = DateTime.Now
            });

            CategoryRuleService.ApplyRulesToUncategorized();

            var row = DatabaseService.GetAppCategory("devenv");
            Assert.NotNull(row);
            Assert.Equal(AppCategoryType.Entertainment, row!.Category); // untouched
            Assert.Equal(CategorySource.Manual, row.Source);            // untouched
        }

        [Fact]
        public void ApplyRulesToUncategorized_IsIdempotent_SecondRunChangesNothing()
        {
            DatabaseService.DeleteAllData();
            SeedSession("spotify", @"C:\Users\test\AppData\Roaming\Spotify\Spotify.exe", DateTime.Now.AddDays(-2), 30);

            CategoryRuleService.ApplyRulesToUncategorized();
            var afterFirst = DatabaseService.GetAppCategory("spotify");
            Assert.NotNull(afterFirst);

            CategoryRuleService.ApplyRulesToUncategorized();
            var afterSecond = DatabaseService.GetAppCategory("spotify");

            Assert.Equal(afterFirst!.LastUpdated, afterSecond!.LastUpdated);
            Assert.Equal(afterFirst.Category, afterSecond.Category);
            Assert.Equal(afterFirst.Source, afterSecond.Source);
        }

        [Fact]
        public void ApplyRulesToUncategorized_IgnoresAppsOutsideTheLookbackWindow()
        {
            DatabaseService.DeleteAllData();
            // Seeded far outside the 30-day lookback the backfill uses.
            SeedSession("devenv", @"C:\VS\Common7\IDE\devenv.exe", DateTime.Now.AddDays(-90), 45);

            CategoryRuleService.ApplyRulesToUncategorized();

            Assert.Null(DatabaseService.GetAppCategory("devenv"));
        }

        [Fact]
        public void ApplyRulesToUncategorized_IgnoresAppsNotInTheRuleMap()
        {
            DatabaseService.DeleteAllData();
            SeedSession("some_random_unknown_tool", @"C:\Tools\some_random_unknown_tool.exe", DateTime.Now.AddDays(-1), 20);

            CategoryRuleService.ApplyRulesToUncategorized();

            Assert.Null(DatabaseService.GetAppCategory("some_random_unknown_tool"));
        }
    }
}
