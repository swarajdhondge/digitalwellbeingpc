using System;
using System.Collections.Generic;
using System.Linq;
using digital_wellbeing_app.Helpers;
using digital_wellbeing_app.Models;

namespace digital_wellbeing_app.Services
{
    /// <summary>
    /// Suggests a Focus-Mode category for well-known apps so most installs don't start with
    /// everything Uncategorized. Suggestions are never allowed to touch a row that already
    /// exists - manual picks (and previously-applied suggestions) always win.
    ///
    /// Deliberately conservative: only apps with an unambiguous work/entertainment identity
    /// are mapped. Browsers are intentionally excluded (their usage is not app-level - see the
    /// browser-tracking roadmap item) and anything context-dependent (Discord's gaming vs. work
    /// use, design tools used professionally vs. as a hobby) is left for the user to decide.
    /// </summary>
    public static class CategoryRuleService
    {
        private static readonly Dictionary<string, AppCategoryType> Rules = new(StringComparer.OrdinalIgnoreCase)
        {
            // Work - office & productivity
            { "winword", AppCategoryType.Work },
            { "excel", AppCategoryType.Work },
            { "powerpnt", AppCategoryType.Work },
            { "outlook", AppCategoryType.Work },
            { "onenote", AppCategoryType.Work },
            { "msteams", AppCategoryType.Work },
            { "teams", AppCategoryType.Work },
            { "slack", AppCategoryType.Work },
            { "zoom", AppCategoryType.Work },
            { "notion", AppCategoryType.Work },
            { "obsidian", AppCategoryType.Work },
            { "todoist", AppCategoryType.Work },
            { "trello", AppCategoryType.Work },

            // Work - development
            { "code", AppCategoryType.Work },
            { "cursor", AppCategoryType.Work },
            { "devenv", AppCategoryType.Work },
            { "rider", AppCategoryType.Work },
            { "idea64", AppCategoryType.Work },
            { "pycharm64", AppCategoryType.Work },
            { "webstorm64", AppCategoryType.Work },
            { "phpstorm64", AppCategoryType.Work },
            { "githubdesktop", AppCategoryType.Work },
            { "sourcetree", AppCategoryType.Work },

            // Entertainment
            { "spotify", AppCategoryType.Entertainment },
            { "vlc", AppCategoryType.Entertainment },
            { "netflix", AppCategoryType.Entertainment },
            { "steam", AppCategoryType.Entertainment },
            { "epicgameslauncher", AppCategoryType.Entertainment },
            { "discord", AppCategoryType.Entertainment },
            { "minecraft", AppCategoryType.Entertainment },
            { "valorant", AppCategoryType.Entertainment },
            { "fortnite", AppCategoryType.Entertainment },
            { "leagueclient", AppCategoryType.Entertainment },
        };

        /// <summary>
        /// Look up a suggested category for an already-normalized app identifier
        /// (see <see cref="AppIdentity.NormalizeKey(string?)"/>). Returns false for anything
        /// not in the rule map, including all browsers by design.
        /// </summary>
        public static bool TryGetSuggestedCategory(string normalizedAppIdentifier, out AppCategoryType category)
            => Rules.TryGetValue(normalizedAppIdentifier, out category);

        /// <summary>
        /// One-time, idempotent startup backfill: for apps seen in usage history over the last
        /// 30 days that have no AppCategory row yet, insert an AutoSuggested row from the rule
        /// map above. Never touches an app that already has a row, manual or previously
        /// suggested - re-running this is always a no-op once every known app has been seen once.
        /// </summary>
        public static void ApplyRulesToUncategorized()
        {
            try
            {
                var since = DateTime.Now.AddDays(-30);
                var recentSessions = DatabaseService.GetAppUsageSessionsForRange(since, DateTime.Now);

                var seenApps = recentSessions
                    .Select(s => new
                    {
                        Key = AppIdentity.NormalizeKey(s.ExecutablePath, s.AppName),
                        s.AppName,
                        s.ExecutablePath
                    })
                    .Where(x => x.Key.Length > 0)
                    .GroupBy(x => x.Key)
                    .Select(g => g.First());

                foreach (var app in seenApps)
                {
                    if (!Rules.TryGetValue(app.Key, out var suggested))
                        continue;

                    // Never overwrite an existing row - manual picks and prior suggestions both win.
                    if (DatabaseService.GetAppCategory(app.Key) != null)
                        continue;

                    DatabaseService.SaveAppCategory(new AppCategory
                    {
                        AppIdentifier = app.Key,
                        AppName = app.AppName,
                        ExecutablePath = app.ExecutablePath,
                        Category = suggested,
                        Source = CategorySource.AutoSuggested,
                        LastUpdated = DateTime.Now
                    });
                }
            }
            catch (Exception ex)
            {
                LogService.Warning($"Category rule backfill skipped: {ex.Message}");
            }
        }
    }
}
