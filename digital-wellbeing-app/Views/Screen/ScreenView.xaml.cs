using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using digital_wellbeing_app.CoreLogic;
using digital_wellbeing_app.ViewModels;

namespace digital_wellbeing_app.Views.Screen
{
    public partial class ScreenView : System.Windows.Controls.UserControl
    {
        private readonly ScreenViewModel _vm = new();
        private readonly ScreenTimeTracker _tracker;
        private readonly DispatcherTimer _uiTimer = new() { Interval = TimeSpan.FromSeconds(1) };

        public ScreenView()
        {
            InitializeComponent();
            _tracker = (System.Windows.Application.Current as App)!.ScreenTracker;

            Loaded += ScreenView_Loaded;
            // Stop timer when the control is unloaded:
            Unloaded += (_, __) => _uiTimer.Stop();
        }

        private void ScreenView_Loaded(object sender, RoutedEventArgs e)
        {
            DataContext = _vm;
            _vm.LoadWeeklyUsage();

            TimeCanvas.SizeChanged += (_, __) => RenderTodayTimeline();

            _uiTimer.Tick += (_, __) => UpdateUI();
            _uiTimer.Start();

            UpdateUI();
        }

        private void UpdateUI()
        {
            var ts = _tracker.CurrentActiveTime;
            var totalSec = (int)ts.TotalSeconds;
            var hours = totalSec / 3600;
            var minutes = (totalSec % 3600) / 60;
            var seconds = totalSec % 60;

            TodayTimeText.Text = $"{hours} hr {minutes} min {seconds} sec";
            RenderTodayTimeline();
        }

        private void RenderTodayTimeline()
        {
            TimeCanvas.Children.Clear();

            var nowSecs = (DateTime.Now - DateTime.Today).TotalSeconds;
            var activeSeconds = _tracker.CurrentActiveTime.TotalSeconds;
            const double daySecs = 24 * 60 * 60.0;

            var width = TimeCanvas.ActualWidth > 0 ? TimeCanvas.ActualWidth : 500;
            var startSecs = Math.Max(0, nowSecs - activeSeconds);

            var offset = (startSecs / daySecs) * width;
            var barLen = Math.Min(width - offset, (activeSeconds / daySecs) * width);

            var rect = new System.Windows.Shapes.Rectangle
            {
                Height = TimeCanvas.ActualHeight,
                Width = barLen,
                Fill = System.Windows.Media.Brushes.Green
            };

            Canvas.SetLeft(rect, offset);
            Canvas.SetTop(rect, 0);
            TimeCanvas.Children.Add(rect);
        }
    }
}
