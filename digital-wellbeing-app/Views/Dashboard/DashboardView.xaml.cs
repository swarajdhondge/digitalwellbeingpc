// File: Views/Dashboard/DashboardView.xaml.cs
using System.Windows.Controls;  // only WPF’s UserControl

namespace digital_wellbeing_app.Views.Dashboard
{
    public partial class DashboardView : System.Windows.Controls.UserControl
    {
        public DashboardView()
        {
            InitializeComponent();
            DataContext = new digital_wellbeing_app.ViewModels.DashboardViewModel();
        }
    }
}
