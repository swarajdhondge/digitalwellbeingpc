using System.Threading.Tasks;
using Velopack;
using Velopack.Locators;
using Velopack.Sources;

namespace digital_wellbeing_app.Services
{
    /// <summary>
    /// Handles checking for and applying application updates via Velopack.
    /// </summary>
    public class UpdateService
    {
        private const string GitHubRepoUrl = "https://github.com/swarajdhondge/digitalwellbeingpc";
        private readonly UpdateManager? _updateManager;

        public UpdateService()
        {
            if (PackagedAppInfo.IsPackaged)
            {
                UnavailableReason = "Updates are managed automatically by Microsoft Store.";
                return;
            }

            if (!VelopackLocator.IsCurrentSet)
            {
                UnavailableReason = "Update checks are available in the installed Pulse app, not this development build.";
                return;
            }

            try
            {
                _updateManager = new UpdateManager(new GithubSource(GitHubRepoUrl, null, false));
                if (!_updateManager.IsInstalled)
                {
                    _updateManager = null;
                    UnavailableReason = "Update checks are available in the installed Pulse app, not this portable build.";
                }
            }
            catch (System.Exception ex)
            {
                UnavailableReason = "Pulse could not initialize the updater for this installation.";
                LastError = ex.Message;
                LogService.Warning($"Updater initialization failed: {ex.Message}");
            }
        }

        public bool IsAvailable => _updateManager != null;
        public string? UnavailableReason { get; }
        public string? LastError { get; private set; }

        /// <summary>
        /// Gets the current application version.
        /// </summary>
        public string? CurrentVersion => _updateManager?.CurrentVersion?.ToString();

        /// <summary>
        /// Checks for updates and returns info if available.
        /// </summary>
        public async Task<UpdateInfo?> CheckForUpdatesAsync()
        {
            try
            {
                if (_updateManager == null) return null;
                LastError = null;
                return await _updateManager.CheckForUpdatesAsync();
            }
            catch (System.Exception ex)
            {
                LastError = ex.Message;
                LogService.Warning($"Update check failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Downloads and applies the update, then restarts the app.
        /// </summary>
        public async Task<bool> DownloadAndApplyAsync(UpdateInfo update)
        {
            try
            {
                if (_updateManager == null) return false;
                await _updateManager.DownloadUpdatesAsync(update);
                _updateManager.ApplyUpdatesAndRestart(update);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Convenience method: checks for update and prompts user.
        /// Returns true if an update was found and applied.
        /// </summary>
        public async Task<bool> CheckAndPromptUpdateAsync()
        {
            var update = await CheckForUpdatesAsync();
            if (update == null)
                return false;

            var result = System.Windows.MessageBox.Show(
                $"A new version ({update.TargetFullRelease.Version}) is available.\n\nWould you like to update now?",
                "Update Available",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Information);

            if (result == System.Windows.MessageBoxResult.Yes)
            {
                return await DownloadAndApplyAsync(update);
            }

            return false;
        }
    }
}
