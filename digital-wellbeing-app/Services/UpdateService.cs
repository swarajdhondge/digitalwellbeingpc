using System.Threading.Tasks;
using digital_wellbeing_app.Helpers;
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
                UnavailableReason = Loc.Get("Update_StoreManaged");
                return;
            }

            if (!VelopackLocator.IsCurrentSet)
            {
                UnavailableReason = Loc.Get("Update_DevBuild");
                return;
            }

            try
            {
                _updateManager = new UpdateManager(new GithubSource(GitHubRepoUrl, null, false));
                if (!_updateManager.IsInstalled)
                {
                    _updateManager = null;
                    UnavailableReason = Loc.Get("Update_PortableBuild");
                }
            }
            catch (System.Exception ex)
            {
                UnavailableReason = Loc.Get("Update_InitFailed");
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
                Loc.Format("Update_AvailablePrompt", update.TargetFullRelease.Version),
                Loc.Get("Update_AvailableTitle"),
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
