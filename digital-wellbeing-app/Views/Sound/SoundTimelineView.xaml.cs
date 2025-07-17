using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using digital_wellbeing_app.ViewModels;

namespace digital_wellbeing_app.Views.Sound
{
    public partial class SoundTimelineView : System.Windows.Controls.UserControl
    {
        private readonly DispatcherTimer _timer;
        private SoundTimelineViewModel ViewModel => (SoundTimelineViewModel)this.DataContext!;

        public SoundTimelineView()
        {
            InitializeComponent();

            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += (_, __) => {
                ViewModel.RefreshData();
                TotalListeningTextBlock.Text = ViewModel.TotalListeningText;
                TotalHarmfulTextBlock.Text = ViewModel.TotalHarmfulText;
                RenderCanvas();
            };

            this.Loaded += (s, e) => _timer.Start();
            this.Unloaded += (s, e) => _timer.Stop();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // First draw
            ViewModel.RefreshData();
            TotalListeningTextBlock.Text = ViewModel.TotalListeningText;
            TotalHarmfulTextBlock.Text = ViewModel.TotalHarmfulText;
            RenderCanvas();

            DetailsDataGrid.Visibility = Visibility.Collapsed;
            ToggleDetailsButton.Content = "Show detailed tracking";
        }

        private void RenderCanvas()
        {
            var canvas = RealTimeCanvas;
            canvas.Children.Clear();

            double w = canvas.ActualWidth;
            double h = canvas.ActualHeight;
            if (w <= 0 || h <= 0) return;

            var safeBrush = TryFindResource("MaterialDesignGreen") as System.Windows.Media.Brush
                            ?? System.Windows.Media.Brushes.LightGreen;
            var harmBrush = TryFindResource("MaterialDesignRed") as System.Windows.Media.Brush
                            ?? System.Windows.Media.Brushes.IndianRed;

            foreach (var seg in ViewModel.Bars)
            {
                double x1 = seg.StartFrac * w;
                double width = Math.Max((seg.EndFrac - seg.StartFrac) * w, 2);

                var safeRect = new System.Windows.Shapes.Rectangle
                {
                    Width = width,
                    Height = h,
                    Fill = safeBrush,
                    ToolTip = $"{seg.SessionLabel} ({seg.DeviceName})"
                };
                System.Windows.Controls.Canvas.SetLeft(safeRect, x1);
                canvas.Children.Add(safeRect);

                if (seg.HarmfulFrac > 0)
                {
                    double hw = width * seg.HarmfulFrac;
                    if (hw > 1)
                    {
                        var harmRect = new System.Windows.Shapes.Rectangle
                        {
                            Width = hw,
                            Height = h,
                            Fill = harmBrush,
                            ToolTip = $"Harmful: {seg.SessionLabel}"
                        };
                        System.Windows.Controls.Canvas.SetLeft(harmRect, x1 + width - hw);
                        canvas.Children.Add(harmRect);
                    }
                }
            }
        }

        private void ToggleDetailsButton_Click(object sender, RoutedEventArgs e)
        {
            if (DetailsDataGrid.Visibility == Visibility.Collapsed)
            {
                DetailsDataGrid.Visibility = Visibility.Visible;
                ToggleDetailsButton.Content = "Hide detailed tracking";
            }
            else
            {
                DetailsDataGrid.Visibility = Visibility.Collapsed;
                ToggleDetailsButton.Content = "Show detailed tracking";
            }
        }
    }
}
