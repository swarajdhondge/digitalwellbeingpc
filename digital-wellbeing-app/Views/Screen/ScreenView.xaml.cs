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
    public partial class ScreenView : UserControl
    {
        private readonly ScreenViewModel _vm = new();
        private readonly ScreenTimeTracker _tracker = new();
        private DispatcherTimer? _timer;

        public ScreenView()
        {
            InitializeComponent();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            DataContext = _vm;
            _tracker.Start();
            _vm.LoadWeeklyUsage();

            TimeCanvas.SizeChanged += (_, __) => UpdateUI(null, null);

            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _timer.Tick += UpdateUI;
            _timer.Start();

            UpdateUI(null, null);
        }

        private void UpdateUI(object? sender, EventArgs? e)
        {
            var ts = _tracker.CurrentActiveTime;
            TodayTimeText.Text = $"{(int)ts.TotalHours} hr {ts.Minutes} min {ts.Seconds} sec";

            RenderTodayTimeline();
        }

        private void RenderTodayTimeline()
        {
            TimeCanvas.Children.Clear();

            var sessionStart = _tracker.SessionStartTime;
            var totalActiveTime = _tracker.CurrentActiveTime;
            double durationSeconds = totalActiveTime.TotalSeconds;
            if (durationSeconds <= 0) return;

            const double daySeconds = 24 * 60 * 60;
            double barWidth = TimeCanvas.ActualWidth > 0 ? TimeCanvas.ActualWidth : 500;

            double startSec = (sessionStart - DateTime.Today).TotalSeconds;
            double left = Math.Max(0, (startSec / daySeconds) * barWidth);
            double width = Math.Min(barWidth - left, (durationSeconds / daySeconds) * barWidth);

            var highlight = new Rectangle
            {
                Height = TimeCanvas.ActualHeight,
                Width = width,
                Fill = Brushes.Green
            };

            Canvas.SetLeft(highlight, left);
            Canvas.SetTop(highlight, 0);
            TimeCanvas.Children.Add(highlight);
        }
    }
}
