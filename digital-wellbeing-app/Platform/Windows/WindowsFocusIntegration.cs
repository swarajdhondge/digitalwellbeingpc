using System;

namespace digital_wellbeing_app.Platform.Windows
{
    /// <summary>
    /// Thin, exception-safe wrapper around <c>Windows.UI.Shell.FocusSessionManager</c> - the OS-level
    /// Focus feature (Settings > Focus, Windows 11's Do-Not-Disturb-adjacent mode). Read/react only:
    /// there is no public API to register as a Focus *partner* the way Spotify/Microsoft To Do are,
    /// so this never starts/stops the OS session, only observes it.
    ///
    /// Guarded the same defensive way <c>PackagedAppInfo.DetectPackaged()</c> guards an unfamiliar
    /// WinRT surface: every call is wrapped in try/catch, because merely JIT-ing a method that
    /// references a WinRT type can throw on a Windows channel/version that doesn't project it, not
    /// just the call itself. Verified against Microsoft Learn's "Detect and react to focus session
    /// state" doc (2026): <c>IsSupported</c> is a static property, <c>GetDefault()</c> a static
    /// method, <c>IsFocusActive</c> an instance property, <c>IsFocusActiveChanged</c> an instance
    /// event with signature <c>(FocusSessionManager sender, object args)</c>. That doc doesn't call
    /// out an MSIX-only restriction, but earlier research flagged that some <c>Windows.UI.Shell</c>
    /// surfaces have needed package identity to *function* even when they compile - unconfirmed for
    /// this specific API either way (see open spike in docs/roadmap-handoff.md).
    /// </summary>
    public sealed class WindowsFocusIntegration : IWindowsFocusSource, IDisposable
    {
        private global::Windows.UI.Shell.FocusSessionManager? _manager;
        private bool _subscribed;
        private bool _isDisposed;

        public event Action<bool>? FocusActiveChanged;

        public bool IsSupported
        {
            get
            {
                try
                {
                    return global::Windows.UI.Shell.FocusSessionManager.IsSupported;
                }
                catch (Exception ex)
                {
                    Services.LogService.Warning($"FocusSessionManager.IsSupported check failed: {ex.Message}");
                    return false;
                }
            }
        }

        public bool IsFocusActive
        {
            get
            {
                try
                {
                    return GetManager()?.IsFocusActive ?? false;
                }
                catch (Exception ex)
                {
                    Services.LogService.Warning($"FocusSessionManager.IsFocusActive read failed: {ex.Message}");
                    return false;
                }
            }
        }

        /// <summary>Start listening for OS Focus session changes. Safe to call even if unsupported.</summary>
        public void Start()
        {
            if (_subscribed) return;

            try
            {
                var manager = GetManager();
                if (manager == null) return;

                manager.IsFocusActiveChanged += OnIsFocusActiveChanged;
                _subscribed = true;
            }
            catch (Exception ex)
            {
                Services.LogService.Warning($"WindowsFocusIntegration start skipped: {ex.Message}");
            }
        }

        public void Stop()
        {
            try
            {
                if (_subscribed && _manager != null)
                    _manager.IsFocusActiveChanged -= OnIsFocusActiveChanged;
            }
            catch (Exception ex)
            {
                Services.LogService.Warning($"WindowsFocusIntegration stop failed: {ex.Message}");
            }
            finally
            {
                _subscribed = false;
            }
        }

        private global::Windows.UI.Shell.FocusSessionManager? GetManager()
        {
            if (_manager != null) return _manager;

            try
            {
                if (!global::Windows.UI.Shell.FocusSessionManager.IsSupported) return null;
                _manager = global::Windows.UI.Shell.FocusSessionManager.GetDefault();
                return _manager;
            }
            catch (Exception ex)
            {
                Services.LogService.Warning($"FocusSessionManager.GetDefault() failed: {ex.Message}");
                return null;
            }
        }

        private void OnIsFocusActiveChanged(global::Windows.UI.Shell.FocusSessionManager sender, object args)
        {
            try
            {
                FocusActiveChanged?.Invoke(sender.IsFocusActive);
            }
            catch (Exception ex)
            {
                Services.LogService.Warning($"WindowsFocusIntegration change handler failed: {ex.Message}");
            }
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            Stop();
            _isDisposed = true;
        }
    }
}
