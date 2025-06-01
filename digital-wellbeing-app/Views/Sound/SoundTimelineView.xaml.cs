using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using digital_wellbeing_app.ViewModels;

namespace digital_wellbeing_app.Views.Sound
{
    public partial class SoundTimelineView : UserControl
    {
        private SoundTimelineViewModel ViewModel => (SoundTimelineViewModel)DataContext!;

        public SoundTimelineView()
        {
            InitializeComponent();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            ViewModel.RefreshData();
            DrawTimeline();

            // At startup: hide the DataGrid, set button text
            DetailsDataGrid.Visibility = Visibility.Collapsed;
            ToggleDetailsButton.Content = "Show detailed tracking";
        }

        private void DrawTimeline()
        {
            FullTimelineCanvas.Children.Clear();

            double w = FullTimelineCanvas.ActualWidth;
            double h = FullTimelineCanvas.ActualHeight;

            // If the canvas isn’t measured yet, defer drawing
            if (w <= 0 || h <= 0)
            {
                FullTimelineCanvas.Loaded += (s, _) => DrawTimeline();
                return;
            }

            // 1) Light background covering full width
            var bgRect = new Rectangle
            {
                Width = w,
                Height = h,
                Fill = Brushes.WhiteSmoke
            };
            FullTimelineCanvas.Children.Add(bgRect);

            // 2) Draw each session as a gray bar + red overlay
            foreach (var bar in ViewModel.Bars)
            {
                double x1 = bar.StartFrac * w;
                double x2 = bar.EndFrac * w;
                double width = x2 - x1;
                if (width < 2) width = 2; // minimal width

                // Light gray safe segment
                var safeRect = new Rectangle
                {
                    Width = width,
                    Height = h * 0.6,
                    Fill = Brushes.LightGray,
                    ToolTip = $"{bar.SessionLabel} ({bar.DeviceName})"
                };
                Canvas.SetLeft(safeRect, x1);
                Canvas.SetTop(safeRect, h * 0.2);
                FullTimelineCanvas.Children.Add(safeRect);

                // Red overlay for harmful fraction
                if (bar.HarmfulFrac > 0)
                {
                    double harmWidth = width * bar.HarmfulFrac;
                    if (harmWidth > 1)
                    {
                        double harmStart = x2 - harmWidth;
                        var harmRect = new Rectangle
                        {
                            Width = harmWidth,
                            Height = h * 0.6,
                            Fill = Brushes.IndianRed,
                            ToolTip = $"Harmful: {bar.SessionLabel}"
                        };
                        Canvas.SetLeft(harmRect, harmStart);
                        Canvas.SetTop(harmRect, h * 0.2);
                        FullTimelineCanvas.Children.Add(harmRect);
                    }
                }
            }
        }

        private void ToggleDetailsButton_Click(object sender, RoutedEventArgs e)
        {
            if (DetailsDataGrid.Visibility == Visibility.Collapsed)
            {
                // Show the DataGrid
                DetailsDataGrid.Visibility = Visibility.Visible;
                ToggleDetailsButton.Content = "Hide detailed tracking";
            }
            else
            {
                // Hide the DataGrid
                DetailsDataGrid.Visibility = Visibility.Collapsed;
                ToggleDetailsButton.Content = "Show detailed tracking";
            }
        }
    }
}
