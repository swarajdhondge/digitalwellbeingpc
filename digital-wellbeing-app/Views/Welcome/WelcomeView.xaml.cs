using System;
using System.Windows;

namespace digital_wellbeing_app.Views.Welcome
{
    public partial class WelcomeView : System.Windows.Controls.UserControl
    {
        /// <summary>
        /// Raised when the user clicks "Get Started" to dismiss the welcome screen.
        /// </summary>
        public event Action? Completed;

        public WelcomeView()
        {
            InitializeComponent();

            // Show the actual data path
            try
            {
                var folder = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Pulse");
                DataPathText.Text = folder;
            }
            catch { /* Keep placeholder text */ }
        }

        private void ContinueButton_Click(object sender, RoutedEventArgs e)
        {
            // Mark first run as completed
            var settings = new Services.SettingsService();
            settings.SaveFirstRunCompleted(true);

            Completed?.Invoke();
        }
    }
}
