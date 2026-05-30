using System.Windows;
using System.Windows.Input;

namespace digital_wellbeing_app.Views.Help
{
    public partial class HelpView : System.Windows.Controls.UserControl
    {
        public HelpView()
        {
            InitializeComponent();
            digital_wellbeing_app.Helpers.PulseLayout.CapCenter(PageScroll, PageRoot);
        }

        private void ReportBug_Click(object sender, MouseButtonEventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "https://github.com/swarajdhondge/digitalwellbeingpc/issues",
                    UseShellExecute = true
                });
            }
            catch { }
        }

        private void GitHub_Click(object sender, MouseButtonEventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "https://github.com/swarajdhondge/digitalwellbeingpc",
                    UseShellExecute = true
                });
            }
            catch { }
        }
    }
}
