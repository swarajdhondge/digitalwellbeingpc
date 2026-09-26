using System.Windows;
using digital_wellbeing_app.Helpers;
using System.Windows.Controls;
using System.Windows.Input;
using digital_wellbeing_app.Models;
using digital_wellbeing_app.Services;
using digital_wellbeing_app.ViewModels;

namespace digital_wellbeing_app.Views.Settings
{
    public partial class SettingsView : System.Windows.Controls.UserControl
    {
        private readonly ThemeService _themeService = new();
        private readonly GoalService _goalService = new();
        private readonly SettingsService _settingsService = new();
        private bool _isLoadingGoal;
        private bool _isLoadingBreakReminder;
        private bool _isLoadingWindDown;
        private bool _isLoadingRetention;
        private bool _isLoadingStartup;
        private bool _isLoadingCloseToTray;
        private bool _isLoadingWindowsFocusIntegration;
        private bool _isLoadingWebsiteTracking;
        private bool _isLoadingDeviceTypeOverride;

        public SettingsView()
        {
            InitializeComponent();
            digital_wellbeing_app.Helpers.PulseLayout.CapCenter(PageScroll, PageRoot);

            // Set version dynamically from assembly PE file version info
            try
            {
                var location = System.Reflection.Assembly.GetExecutingAssembly().Location;
                if (!string.IsNullOrEmpty(location))
                {
                    var fvi = System.Diagnostics.FileVersionInfo.GetVersionInfo(location);
                    AboutVersionText.Text = Loc.Format("Settings_VersionFormat", fvi.ProductVersion ?? "2.3.1");
                }
            }
            catch { /* Keep default text from XAML */ }

            // Load saved theme preference
            var savedTheme = _themeService.Load();
            switch (savedTheme)
            {
                case AppTheme.Light:
                    LightRadio.IsChecked = true;
                    UpdateThemeSelection("Light");
                    break;
                case AppTheme.Dark:
                    DarkRadio.IsChecked = true;
                    UpdateThemeSelection("Dark");
                    break;
                default: // Auto
                    AutoRadio.IsChecked = true;
                    UpdateThemeSelection("Auto");
                    break;
            }
            
            // Load startup preference (async: routes to Run key or MSIX StartupTask by build).
            _ = LoadStartupSettingAsync();

            // Load close-to-tray preference
            LoadCloseToTraySetting();

            // Load Windows Focus integration preference
            LoadWindowsFocusIntegrationSetting();

            // Load goal settings
            LoadGoalSettings();

            // Load break reminder settings
            LoadBreakReminderSettings();

            // Load Wind Down settings
            LoadWindDownSettings();

            // Load Hearing settings (device-type override)
            LoadHearingSettings();

            LoadLanguageSettings();

            // Note: Hearing Protection threshold is disabled (Coming Soon)
            // Default is 75 dB, set in SettingsService.LoadHarmfulThreshold()

            // Storage info and tracker health change while the app runs: reload them whenever
            // the page is opened, and keep the Diagnostics card current while it stays open.
            var healthTimer = new System.Windows.Threading.DispatcherTimer { Interval = System.TimeSpan.FromSeconds(30) };
            healthTimer.Tick += (_, _) => LoadTrackerHealth();
            Loaded += (_, _) =>
            {
                LoadDataPrivacyInfo();
                LoadTrackerHealth();
                healthTimer.Start();
            };
            Unloaded += (_, _) => healthTimer.Stop();
        }

        #region Data & Privacy

        private void LoadDataPrivacyInfo()
        {
            try
            {
                StoragePathText.Text = DatabaseService.GetDatabaseFilePath();
            }
            catch
            {
                StoragePathText.Text = Loc.Get("Settings_UnableToDetermine");
            }

            RefreshDbSize();
            LoadRetentionSetting();
            LoadWebsiteTrackingSetting();
        }

        private void LoadWebsiteTrackingSetting()
        {
            _isLoadingWebsiteTracking = true;
            try { WebsiteTrackingCheckBox.IsChecked = _settingsService.LoadWebsiteTrackingEnabled(); }
            finally { _isLoadingWebsiteTracking = false; }
        }

        private void WebsiteTrackingCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (_isLoadingWebsiteTracking) return;

            bool enabled = WebsiteTrackingCheckBox.IsChecked == true;
            _settingsService.SaveWebsiteTrackingEnabled(enabled);

            // Apply immediately to the live service, no restart needed.
            (System.Windows.Application.Current as App)?.WebsiteUsageSvc?.SetEnabled(enabled);
        }

        private void LoadRetentionSetting()
        {
            _isLoadingRetention = true;
            var months = _settingsService.LoadRetentionMonths();
            foreach (var item in RetentionCombo.Items)
            {
                if (item is System.Windows.Controls.ComboBoxItem cbi &&
                    cbi.Tag is string tag && int.TryParse(tag, out var m) && m == months)
                {
                    RetentionCombo.SelectedItem = cbi;
                    break;
                }
            }
            _isLoadingRetention = false;
        }

        private void RetentionCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (_isLoadingRetention) return;
            if (RetentionCombo.SelectedItem is System.Windows.Controls.ComboBoxItem cbi &&
                cbi.Tag is string tag && int.TryParse(tag, out var months))
            {
                _settingsService.SaveRetentionMonths(months);
            }
        }

        private void DeleteDateRange_Click(object sender, RoutedEventArgs e)
        {
            var from = RangeFromPicker.SelectedDate;
            var to = RangeToPicker.SelectedDate;
            if (from == null || to == null)
            {
                System.Windows.MessageBox.Show(Loc.Get("Settings_PickStartEndDate"), Loc.Get("Settings_DeleteDateRange"),
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (from > to)
            {
                (from, to) = (to, from);
            }

            var result = System.Windows.MessageBox.Show(
                Loc.Format("Settings_DeleteRangeConfirm", from, to),
                Loc.Get("Settings_DeleteDateRange"), MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes) return;

            try
            {
                DatabaseService.DeleteDataInRange(from.Value, to.Value);
                RefreshDbSize();
                System.Windows.MessageBox.Show(Loc.Get("Settings_RangeDeleted"), Loc.Get("Settings_DataDeletedTitle"),
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (System.Exception ex)
            {
                System.Windows.MessageBox.Show(Loc.Format("Settings_DeleteFailed", ex.Message), Loc.Get("Settings_ErrorTitle"),
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RefreshDbSize()
        {
            try
            {
                long bytes = DatabaseService.GetDatabaseFileSize();
                DbSizeText.Text = FormatFileSize(bytes);
            }
            catch
            {
                DbSizeText.Text = Loc.Get("Settings_Unknown");
            }
        }

        private static string FormatFileSize(long bytes)
        {
            if (bytes <= 0) return Loc.Format("Settings_SizeBytes", 0);
            if (bytes < 1024) return Loc.Format("Settings_SizeBytes", bytes);
            if (bytes < 1024 * 1024) return Loc.Format("Settings_SizeKB", bytes / 1024.0);
            return Loc.Format("Settings_SizeMB", bytes / (1024.0 * 1024.0));
        }

        private void OpenDataFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dbPath = DatabaseService.GetDatabaseFilePath();
                var folder = System.IO.Path.GetDirectoryName(dbPath);
                if (folder != null && System.IO.Directory.Exists(folder))
                {
                    System.Diagnostics.Process.Start("explorer.exe", folder);
                }
            }
            catch (System.Exception ex)
            {
                System.Windows.MessageBox.Show(
                    Loc.Format("Settings_OpenFolderFailed", ex.Message),
                    Loc.Get("Settings_ErrorTitle"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void BackupNow_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = Loc.Get("Backup_ChooseFolder"),
                ShowNewFolderButton = true
            };

            if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

            try
            {
                var backupPath = BackupService.CreateBackup(dialog.SelectedPath);

                System.Windows.MessageBox.Show(
                    Loc.Format("Backup_Saved", backupPath),
                    Loc.Get("Backup_CompleteTitle"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (System.Exception ex)
            {
                System.Windows.MessageBox.Show(
                    Loc.Format("Backup_Failed", ex.Message),
                    Loc.Get("Backup_ErrorTitle"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void RestoreBackup_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = Loc.Get("Backup_ChooseRestoreFile"),
                Filter = Loc.Get("Backup_FileFilter")
            };

            if (dialog.ShowDialog() != true) return;

            var manifest = BackupService.ReadManifest(dialog.FileName);

            var confirm = System.Windows.MessageBox.Show(
                manifest != null
                    ? Loc.Format("Backup_RestoreConfirmFrom", manifest.CreatedUtc.ToLocalTime())
                    : Loc.Get("Backup_RestoreConfirm"),
                Loc.Get("Backup_RestoreTitle"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes) return;

            try
            {
                BackupService.RestoreBackup(dialog.FileName);

                System.Windows.MessageBox.Show(
                    Loc.Get("Backup_RestoreDone"),
                    Loc.Get("Backup_RestoreCompleteTitle"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                System.Windows.Application.Current.Shutdown();
            }
            catch (System.Exception ex)
            {
                System.Windows.MessageBox.Show(
                    Loc.Format("Backup_RestoreFailed", ex.Message),
                    Loc.Get("Backup_RestoreErrorTitle"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void ExportData_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = Loc.Get("Export_ChooseFolder"),
                ShowNewFolderButton = true
            };

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                try
                {
                    var exportDir = System.IO.Path.Combine(dialog.SelectedPath, $"DigitalWellbeing_Export_{System.DateTime.Now:yyyyMMdd_HHmmss}");
                    int count = DataExportService.ExportAllToCsv(exportDir);

                    System.Windows.MessageBox.Show(
                        Loc.Format("Export_Success", count, exportDir),
                        Loc.Get("Export_CompleteTitle"),
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    // Open the export folder
                    System.Diagnostics.Process.Start("explorer.exe", exportDir);
                }
                catch (System.Exception ex)
                {
                    System.Windows.MessageBox.Show(
                        Loc.Format("Export_Failed", ex.Message),
                        Loc.Get("Export_ErrorTitle"),
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
        }

        private void DeleteAllData_Click(object sender, RoutedEventArgs e)
        {
            var result = System.Windows.MessageBox.Show(
                Loc.Get("Settings_DeleteAllConfirm"),
                Loc.Get("Settings_DeleteAllDataTitle"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    DatabaseService.DeleteAllData();
                    RefreshDbSize();

                    System.Windows.MessageBox.Show(
                        Loc.Get("Settings_AllDataDeleted"),
                        Loc.Get("Settings_DataDeletedTitle"),
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                catch (System.Exception ex)
                {
                    System.Windows.MessageBox.Show(
                        Loc.Format("Settings_DeleteFailed", ex.Message),
                        Loc.Get("Settings_ErrorTitle"),
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
        }

        private void ViewPrivacyInfo_Click(object sender, MouseButtonEventArgs e)
        {
            // Navigate to welcome/privacy screen
            var mainWindow = Window.GetWindow(this) as MainWindow.MainWindow;
            mainWindow?.ShowPrivacyInfo();
        }

        #endregion

        #region Goal Settings

        private void LoadGoalSettings()
        {
            _isLoadingGoal = true;
            
            var goal = _goalService.GetDailyScreenTimeGoal();
            GoalEnabledCheckBox.IsChecked = goal.HasValue;
            GoalInputPanel.Visibility = goal.HasValue ? Visibility.Visible : Visibility.Collapsed;

            if (goal.HasValue)
            {
                GoalHoursTextBox.Text = (goal.Value / 60).ToString();
                GoalMinutesTextBox.Text = (goal.Value % 60).ToString();
                UpdateCurrentGoalText(goal.Value);
            }
            else
            {
                GoalHoursTextBox.Text = "8";
                GoalMinutesTextBox.Text = "0";
                CurrentGoalText.Text = Loc.Get("Settings_NoGoalSet");
            }

            _isLoadingGoal = false;
        }

        private void GoalEnabledCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (_isLoadingGoal) return;

            bool enabled = GoalEnabledCheckBox.IsChecked == true;
            GoalInputPanel.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;

            if (enabled)
            {
                SaveGoal();
            }
            else
            {
                _goalService.SetDailyScreenTimeGoal(null);
                CurrentGoalText.Text = Loc.Get("Settings_NoGoalSet");
            }
        }

        private void GoalInput_Changed(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (_isLoadingGoal) return;
            if (GoalEnabledCheckBox.IsChecked != true) return;

            SaveGoal();
        }

        private void SaveGoal()
        {
            if (!int.TryParse(GoalHoursTextBox.Text, out int hours))
                hours = 0;
            if (!int.TryParse(GoalMinutesTextBox.Text, out int minutes))
                minutes = 0;

            // Clamp values
            hours = System.Math.Max(0, System.Math.Min(24, hours));
            minutes = System.Math.Max(0, System.Math.Min(59, minutes));

            int totalMinutes = (hours * 60) + minutes;

            if (totalMinutes > 0)
            {
                _goalService.SetDailyScreenTimeGoal(totalMinutes);
                UpdateCurrentGoalText(totalMinutes);
            }
            else
            {
                _goalService.SetDailyScreenTimeGoal(null);
                CurrentGoalText.Text = Loc.Get("Settings_GoalMustBePositive");
            }
        }

        private void UpdateCurrentGoalText(int totalMinutes)
        {
            var hours = totalMinutes / 60;
            var mins = totalMinutes % 60;
            
            if (hours > 0 && mins > 0)
                CurrentGoalText.Text = Loc.Format("Settings_GoalHoursMinutes", hours, mins);
            else if (hours > 0)
                CurrentGoalText.Text = Loc.Format("Settings_GoalHours", hours);
            else
                CurrentGoalText.Text = Loc.Format("Settings_GoalMinutes", mins);
        }

        #endregion

        #region Break Reminder Settings

        private void LoadBreakReminderSettings()
        {
            _isLoadingBreakReminder = true;

            bool enabled = _settingsService.LoadBreakReminderEnabled();
            int interval = _settingsService.LoadBreakReminderInterval();
            bool soundEnabled = _settingsService.LoadBreakReminderSound();

            BreakReminderToggle.IsChecked = enabled;
            BreakReminderOptionsPanel.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
            BreakReminderSoundPanel.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
            SoundToggle.IsChecked = soundEnabled;

            // Set interval selection
            foreach (System.Windows.Controls.ComboBoxItem item in IntervalComboBox.Items)
            {
                if (item.Tag?.ToString() == interval.ToString())
                {
                    IntervalComboBox.SelectedItem = item;
                    break;
                }
            }

            _isLoadingBreakReminder = false;
        }

        private void BreakReminderToggle_Changed(object sender, RoutedEventArgs e)
        {
            if (_isLoadingBreakReminder) return;

            bool enabled = BreakReminderToggle.IsChecked == true;
            BreakReminderOptionsPanel.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
            BreakReminderSoundPanel.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;

            _settingsService.SaveBreakReminderEnabled(enabled);

            // Notify MainWindow to restart the service
            NotifyBreakReminderServiceChanged();
        }

        private void IntervalComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (_isLoadingBreakReminder) return;

            var selected = IntervalComboBox.SelectedItem as System.Windows.Controls.ComboBoxItem;
            if (selected?.Tag != null && int.TryParse(selected.Tag.ToString(), out int interval))
            {
                _settingsService.SaveBreakReminderInterval(interval);
                NotifyBreakReminderServiceChanged();
            }
        }

        private void SoundToggle_Changed(object sender, RoutedEventArgs e)
        {
            if (_isLoadingBreakReminder) return;

            bool soundEnabled = SoundToggle.IsChecked == true;
            _settingsService.SaveBreakReminderSound(soundEnabled);
            NotifyBreakReminderServiceChanged();
        }

        private void NotifyBreakReminderServiceChanged()
        {
            // Find MainWindow and update break reminder service
            var mainWindow = Window.GetWindow(this) as MainWindow.MainWindow;
            mainWindow?.UpdateBreakReminderService();
        }

        #endregion

        #region Wind Down Settings

        private void LoadWindDownSettings()
        {
            _isLoadingWindDown = true;

            // Populate time combo boxes
            PopulateTimeComboBoxes();

            bool enabled = _settingsService.LoadWindDownEnabled();
            int startHour = _settingsService.LoadWindDownStartHour();
            int startMinute = _settingsService.LoadWindDownStartMinute();
            int endHour = _settingsService.LoadWindDownEndHour();
            int endMinute = _settingsService.LoadWindDownEndMinute();
            bool showNotification = _settingsService.LoadWindDownShowNotification();
            bool showVisualCue = _settingsService.LoadWindDownShowVisualCue();
            int visualStyle = _settingsService.LoadWindDownVisualStyle();

            WindDownToggle.IsChecked = enabled;
            WindDownOptionsPanel.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
            WindDownNotificationToggle.IsChecked = showNotification;
            WindDownVisualToggle.IsChecked = showVisualCue;
            VisualStylePanel.Visibility = showVisualCue ? Visibility.Visible : Visibility.Collapsed;

            // Select the saved times
            SelectTimeInComboBox(StartTimeComboBox, startHour, startMinute);
            SelectTimeInComboBox(EndTimeComboBox, endHour, endMinute);

            // Update visual style selection
            UpdateVisualStyleSelection((WindDownVisualStyle)visualStyle);

            // Update status text
            UpdateWindDownStatus(enabled, startHour, startMinute, endHour, endMinute);

            _isLoadingWindDown = false;
        }

        private void PopulateTimeComboBoxes()
        {
            StartTimeComboBox.Items.Clear();
            EndTimeComboBox.Items.Clear();

            // Generate time options in 30-minute increments
            for (int hour = 0; hour < 24; hour++)
            {
                for (int minute = 0; minute < 60; minute += 30)
                {
                    var timeStr = FormatTimeDisplay(hour, minute);
                    var startItem = new ComboBoxItem { Content = timeStr, Tag = $"{hour}:{minute}" };
                    var endItem = new ComboBoxItem { Content = timeStr, Tag = $"{hour}:{minute}" };
                    StartTimeComboBox.Items.Add(startItem);
                    EndTimeComboBox.Items.Add(endItem);
                }
            }
        }

        private string FormatTimeDisplay(int hour, int minute) => TimeFormatHelper.FormatClockTime(hour, minute);

        private void SelectTimeInComboBox(System.Windows.Controls.ComboBox comboBox, int hour, int minute)
        {
            // Round minute to nearest 30
            minute = minute >= 30 ? 30 : 0;
            var tagToFind = $"{hour}:{minute}";

            foreach (ComboBoxItem item in comboBox.Items)
            {
                if (item.Tag?.ToString() == tagToFind)
                {
                    comboBox.SelectedItem = item;
                    return;
                }
            }

            // Default to first item if not found
            if (comboBox.Items.Count > 0)
                comboBox.SelectedIndex = 0;
        }

        private (int hour, int minute) GetTimeFromComboBox(System.Windows.Controls.ComboBox comboBox)
        {
            if (comboBox.SelectedItem is ComboBoxItem item && item.Tag is string tag)
            {
                var parts = tag.Split(':');
                if (parts.Length == 2 && int.TryParse(parts[0], out int hour) && int.TryParse(parts[1], out int minute))
                {
                    return (hour, minute);
                }
            }
            return (0, 0);
        }

        private void WindDownToggle_Changed(object sender, RoutedEventArgs e)
        {
            // Guard against events firing during XAML initialization
            if (_isLoadingWindDown || !IsLoaded || WindDownOptionsPanel == null) return;

            bool enabled = WindDownToggle.IsChecked == true;
            WindDownOptionsPanel.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;

            _settingsService.SaveWindDownEnabled(enabled);

            // When enabling Wind Down, also save the current time selections
            // to ensure the service has the correct schedule
            var (startHour, startMinute) = GetTimeFromComboBox(StartTimeComboBox);
            var (endHour, endMinute) = GetTimeFromComboBox(EndTimeComboBox);
            
            if (enabled)
            {
                // Save times when enabling to ensure they're persisted
                _settingsService.SaveWindDownStartTime(startHour, startMinute);
                _settingsService.SaveWindDownEndTime(endHour, endMinute);
            }
            
            UpdateWindDownStatus(enabled, startHour, startMinute, endHour, endMinute);

            NotifyWindDownServiceChanged();
        }

        private void StartTimeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoadingWindDown) return;

            var (hour, minute) = GetTimeFromComboBox(StartTimeComboBox);
            _settingsService.SaveWindDownStartTime(hour, minute);

            var (endHour, endMinute) = GetTimeFromComboBox(EndTimeComboBox);
            UpdateWindDownStatus(WindDownToggle.IsChecked == true, hour, minute, endHour, endMinute);

            NotifyWindDownServiceChanged();
        }

        private void EndTimeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoadingWindDown) return;

            var (hour, minute) = GetTimeFromComboBox(EndTimeComboBox);
            _settingsService.SaveWindDownEndTime(hour, minute);

            var (startHour, startMinute) = GetTimeFromComboBox(StartTimeComboBox);
            UpdateWindDownStatus(WindDownToggle.IsChecked == true, startHour, startMinute, hour, minute);

            NotifyWindDownServiceChanged();
        }

        private void WindDownNotificationToggle_Changed(object sender, RoutedEventArgs e)
        {
            // Guard against events firing during XAML initialization
            if (_isLoadingWindDown || !IsLoaded) return;

            bool showNotification = WindDownNotificationToggle.IsChecked == true;
            _settingsService.SaveWindDownShowNotification(showNotification);
            NotifyWindDownServiceChanged();
        }

        private void WindDownVisualToggle_Changed(object sender, RoutedEventArgs e)
        {
            // Guard against events firing during XAML initialization
            if (_isLoadingWindDown || !IsLoaded || VisualStylePanel == null) return;

            bool showVisual = WindDownVisualToggle.IsChecked == true;
            VisualStylePanel.Visibility = showVisual ? Visibility.Visible : Visibility.Collapsed;
            _settingsService.SaveWindDownShowVisualCue(showVisual);
            NotifyWindDownServiceChanged();
        }

        private void AmberStyle_Click(object sender, MouseButtonEventArgs e)
        {
            UpdateVisualStyleSelection(WindDownVisualStyle.Amber);
            _settingsService.SaveWindDownVisualStyle((int)WindDownVisualStyle.Amber);
            NotifyWindDownServiceChanged();
        }

        private void PurpleStyle_Click(object sender, MouseButtonEventArgs e)
        {
            UpdateVisualStyleSelection(WindDownVisualStyle.Purple);
            _settingsService.SaveWindDownVisualStyle((int)WindDownVisualStyle.Purple);
            NotifyWindDownServiceChanged();
        }

        private void DimStyle_Click(object sender, MouseButtonEventArgs e)
        {
            UpdateVisualStyleSelection(WindDownVisualStyle.Dim);
            _settingsService.SaveWindDownVisualStyle((int)WindDownVisualStyle.Dim);
            NotifyWindDownServiceChanged();
        }

        private void UpdateVisualStyleSelection(WindDownVisualStyle selected)
        {
            var defaultBrush = (System.Windows.Media.Brush)FindResource("Card2");
            var accentBrush = (System.Windows.Media.Brush)FindResource("Accent");

            AmberStyleOption.Background = defaultBrush;
            PurpleStyleOption.Background = defaultBrush;
            DimStyleOption.Background = defaultBrush;

            switch (selected)
            {
                case WindDownVisualStyle.Amber:
                    AmberStyleOption.Background = accentBrush;
                    break;
                case WindDownVisualStyle.Purple:
                    PurpleStyleOption.Background = accentBrush;
                    break;
                case WindDownVisualStyle.Dim:
                    DimStyleOption.Background = accentBrush;
                    break;
            }
        }

        private void UpdateWindDownStatus(bool enabled, int startHour, int startMinute, int endHour, int endMinute)
        {
            if (!enabled)
            {
                WindDownStatusText.Text = Loc.Get("Settings_WindDownDisabled");
                WindDownStatusIcon.Kind = MaterialDesignThemes.Wpf.PackIconKind.MoonWaningCrescent;
                return;
            }

            var startTimeStr = FormatTimeDisplay(startHour, startMinute);
            var endTimeStr = FormatTimeDisplay(endHour, endMinute);
            WindDownStatusText.Text = Loc.Format("Settings_WindDownActiveRange", startTimeStr, endTimeStr);
            WindDownStatusIcon.Kind = MaterialDesignThemes.Wpf.PackIconKind.WeatherNight;
        }

        private void NotifyWindDownServiceChanged()
        {
            // Find MainWindow and update Wind Down service
            var mainWindow = Window.GetWindow(this) as MainWindow.MainWindow;
            mainWindow?.UpdateWindDownService();
        }

        #endregion

        #region Theme Settings

        private void UpdateThemeSelection(string selectedTheme)
        {
            // Reset all to default style
            var defaultBrush = (System.Windows.Media.Brush)FindResource("Card2");
            LightThemeOption.Background = defaultBrush;
            DarkThemeOption.Background = defaultBrush;
            AutoThemeOption.Background = defaultBrush;

            // Highlight selected
            var accentBrush = (System.Windows.Media.Brush)FindResource("Accent");
            switch (selectedTheme)
            {
                case "Light":
                    LightThemeOption.Background = accentBrush;
                    break;
                case "Dark":
                    DarkThemeOption.Background = accentBrush;
                    break;
                case "Auto":
                    AutoThemeOption.Background = accentBrush;
                    break;
            }
        }

        private void LightTheme_Click(object sender, MouseButtonEventArgs e)
        {
            LightRadio.IsChecked = true;
            UpdateThemeSelection("Light");
            ApplyTheme(AppTheme.Light);
        }

        private void DarkTheme_Click(object sender, MouseButtonEventArgs e)
        {
            DarkRadio.IsChecked = true;
            UpdateThemeSelection("Dark");
            ApplyTheme(AppTheme.Dark);
        }

        private void AutoTheme_Click(object sender, MouseButtonEventArgs e)
        {
            AutoRadio.IsChecked = true;
            UpdateThemeSelection("Auto");
            ApplyTheme(AppTheme.Auto);
        }

        private void ThemeRadio_Checked(object sender, RoutedEventArgs e)
        {
            // This is called during initialization, skip if not loaded yet
            if (!IsLoaded) return;
            
            if (LightRadio.IsChecked == true)
                ApplyTheme(AppTheme.Light);
            else if (DarkRadio.IsChecked == true)
                ApplyTheme(AppTheme.Dark);
            else
                ApplyTheme(AppTheme.Auto);
        }

        private void ApplyTheme(AppTheme theme)
        {
            // Save and apply using ThemeService
            _themeService.Save(theme);
            _themeService.ApplyTheme(theme);
        }

        #endregion

        #region Startup Settings

        private async System.Threading.Tasks.Task LoadStartupSettingAsync()
        {
            _isLoadingStartup = true;
            try { StartupCheckBox.IsChecked = await StartupService.IsEnabledAsync(); }
            finally { _isLoadingStartup = false; }
        }

        private async void StartupCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (_isLoadingStartup) return;
            bool enable = (StartupCheckBox.IsChecked == true);
            await StartupService.SetEnabledAsync(enable);
        }

        private void LoadCloseToTraySetting()
        {
            _isLoadingCloseToTray = true;
            try { CloseToTrayCheckBox.IsChecked = _settingsService.LoadCloseToTray(); }
            finally { _isLoadingCloseToTray = false; }
        }

        private void CloseToTrayCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (_isLoadingCloseToTray) return;
            _settingsService.SaveCloseToTray(CloseToTrayCheckBox.IsChecked == true);
        }

        private void LoadWindowsFocusIntegrationSetting()
        {
            _isLoadingWindowsFocusIntegration = true;
            try { WindowsFocusIntegrationCheckBox.IsChecked = _settingsService.LoadWindowsFocusIntegrationEnabled(); }
            finally { _isLoadingWindowsFocusIntegration = false; }
        }

        private void WindowsFocusIntegrationCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (_isLoadingWindowsFocusIntegration) return;
            _settingsService.SaveWindowsFocusIntegrationEnabled(WindowsFocusIntegrationCheckBox.IsChecked == true);
        }

        #endregion

        #region Hearing

        private void LoadHearingSettings()
        {
            _isLoadingDeviceTypeOverride = true;
            try
            {
                var current = _settingsService.LoadDeviceTypeOverride() ?? "";
                foreach (ComboBoxItem item in DeviceTypeOverrideCombo.Items)
                {
                    if (item.Tag as string == current)
                    {
                        DeviceTypeOverrideCombo.SelectedItem = item;
                        return;
                    }
                }
                DeviceTypeOverrideCombo.SelectedIndex = 0; // "Auto-detect"
            }
            finally { _isLoadingDeviceTypeOverride = false; }
        }

        private void DeviceTypeOverrideCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoadingDeviceTypeOverride) return;
            if (DeviceTypeOverrideCombo.SelectedItem is ComboBoxItem item)
                _settingsService.SaveDeviceTypeOverride(item.Tag as string);
        }

        #endregion

        #region About

        private async void CheckForUpdates_Click(object sender, RoutedEventArgs e)
        {
            UpdateStatusText.Text = Loc.Get("Settings_CheckingUpdates");
            UpdateStatusText.Visibility = Visibility.Visible;

            try
            {
                if (PackagedAppInfo.IsPackaged)
                {
                    UpdateStatusText.Text = Loc.Get("Settings_UpdatesViaStore");
                    OpenExternalLink(PackagedAppInfo.GetStoreDeepLink());
                    return;
                }

                var updateService = new UpdateService();
                if (!updateService.IsAvailable)
                {
                    UpdateStatusText.Text = updateService.UnavailableReason ?? Loc.Get("Settings_UpdatesUnavailable");
                    return;
                }
                var update = await updateService.CheckForUpdatesAsync();

                if (update != null)
                {
                    UpdateStatusText.Text = Loc.Format("Settings_UpdateAvailableStatus", update.TargetFullRelease.Version);

                    var result = System.Windows.MessageBox.Show(
                        Loc.Format("Settings_UpdateAvailablePrompt", update.TargetFullRelease.Version),
                        Loc.Get("Settings_UpdateAvailableTitle"),
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Information);

                    if (result == MessageBoxResult.Yes)
                    {
                        UpdateStatusText.Text = Loc.Get("Settings_DownloadingUpdate");
                        await updateService.DownloadAndApplyAsync(update);
                    }
                }
                else
                {
                    UpdateStatusText.Text = updateService.LastError == null
                        ? Loc.Get("Settings_UpToDate")
                        : Loc.Get("Settings_UpdateServiceUnreachable");
                }
            }
            catch (System.Exception ex)
            {
                UpdateStatusText.Text = Loc.Format("Settings_UpdateCheckFailed", ex.Message);
            }
        }

        private void OpenWebsite_Click(object sender, MouseButtonEventArgs e)
            => OpenExternalLink("https://www.digitalwellbeingpc.com");

        private static void OpenExternalLink(string url)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch { }
        }

        private void OpenGitHub_Click(object sender, MouseButtonEventArgs e)
        {
            OpenExternalLink("https://github.com/swarajdhondge/digitalwellbeingpc");
        }

        #endregion

        #region Language

        private bool _isLoadingLanguage;

        private void LoadLanguageSettings()
        {
            _isLoadingLanguage = true;
            try
            {
                var saved = new SettingsService().LoadLanguage();
                LanguageComboBox.Items.Add(new ComboBoxItem { Content = Loc.Get("Settings_LanguageSystem"), Tag = string.Empty });
                foreach (var culture in Loc.AvailableLanguages())
                    LanguageComboBox.Items.Add(new ComboBoxItem { Content = culture.NativeName, Tag = culture.Name });
                LanguageComboBox.SelectedItem = LanguageComboBox.Items.Cast<ComboBoxItem>()
                    .FirstOrDefault(i => string.Equals((string)i.Tag, saved, System.StringComparison.OrdinalIgnoreCase))
                    ?? LanguageComboBox.Items[0];
            }
            finally
            {
                _isLoadingLanguage = false;
            }
        }

        private void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoadingLanguage || LanguageComboBox.SelectedItem is not ComboBoxItem item) return;
            new SettingsService().SaveLanguage((string)item.Tag);
            LanguageRestartText.Visibility = Visibility.Visible;
        }

        #endregion

        #region Diagnostics

        private void LoadTrackerHealth()
        {
            var displays = TrackingHealthService.GetHealthStatuses()
                .Select(TrackerHealthDisplay.From)
                .ToList();
            TrackerHealthList.ItemsSource = displays;
        }

        private void OpenLogFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var folder = LogService.GetLogDirectory();
                if (System.IO.Directory.Exists(folder))
                {
                    System.Diagnostics.Process.Start("explorer.exe", folder);
                }
            }
            catch (System.Exception ex)
            {
                System.Windows.MessageBox.Show(Loc.Format("Settings_OpenLogFolderFailed", ex.Message), Loc.Get("Settings_ErrorTitle"),
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion
    }

    /// <summary>Display wrapper for TrackingHealthService.TrackerHealthInfo.</summary>
    internal class TrackerHealthDisplay
    {
        public string TrackerName { get; set; } = string.Empty;
        public string StatusText { get; set; } = string.Empty;
        public System.Windows.Media.Brush StatusColor { get; set; } = System.Windows.Media.Brushes.Gray;

        public static TrackerHealthDisplay From(TrackingHealthService.TrackerHealthInfo info)
        {
            var (statusWord, brush) = info.Status switch
            {
                TrackingHealthService.HealthStatus.Healthy => (Loc.Get("Settings_HealthHealthy"), System.Windows.Media.Brushes.LimeGreen),
                TrackingHealthService.HealthStatus.Stale => (Loc.Get("Settings_HealthStale"), System.Windows.Media.Brushes.Orange),
                TrackingHealthService.HealthStatus.Down => (Loc.Get("Settings_HealthDown"), System.Windows.Media.Brushes.Red),
                _ => (Loc.Get("Settings_HealthNotStarted"), System.Windows.Media.Brushes.Gray)
            };

            var lastSeenText = info.LastSeenUtc.HasValue
                ? Loc.Format("Settings_HealthLastSeen", statusWord, FormatAgo(DateTime.UtcNow - info.LastSeenUtc.Value))
                : statusWord;

            return new TrackerHealthDisplay
            {
                TrackerName = FriendlyTrackerName(info.TrackerName),
                StatusText = lastSeenText,
                StatusColor = brush
            };
        }

        private static string FriendlyTrackerName(string trackerName) => trackerName switch
        {
            "ScreenTimeTracker" => Loc.Get("Common_ScreenTime"),
            "AppUsageTracker" => Loc.Get("Common_AppUsage"),
            "SoundExposureManager" => Loc.Get("Common_Hearing2"),
            _ => trackerName
        };

        private static string FormatAgo(TimeSpan age)
        {
            if (age.TotalSeconds < 90) return Loc.Format("Settings_AgoSeconds", (int)age.TotalSeconds);
            if (age.TotalMinutes < 90) return Loc.Format("Settings_AgoMinutes", (int)age.TotalMinutes);
            return Loc.Format("Settings_AgoHours", (int)age.TotalHours);
        }
    }
}
