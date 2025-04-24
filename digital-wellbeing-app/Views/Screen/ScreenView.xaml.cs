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
        private readonly ScreenTimeTracker _tracker;
        private readonly DispatcherTimer _uiTimer = new() { Interval = TimeSpan.FromSeconds(1) };

        public ScreenView()
        {
            InitializeComponent();

            _tracker = (Application.Current as App)!.ScreenTracker;

            Loaded += ScreenView_Loaded;
            Unloaded += (_, __) => _uiTimer.Stop();  // only stop the UI refresh
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

            // how many seconds we've tracked so far
            var activeSeconds = _tracker.CurrentActiveTime.TotalSeconds;

            const double daySecs = 24 * 60 * 60.0;
            
            var width = TimeCanvas.ActualWidth > 0 ? TimeCanvas.ActualWidth : 500;

            // how many seconds have elapsed since midnight
            var nowSecs = (DateTime.Now - DateTime.Today).TotalSeconds;

            
            var startSecs = Math.Max(0, nowSecs - activeSeconds);

            // translate into pixels
            var offset = (startSecs / daySecs) * width;
            var barLen = Math.Min(width - offset, (activeSeconds / daySecs) * width);

            var rect = new Rectangle
            {
                Height = TimeCanvas.ActualHeight,
                Width = barLen,
                Fill = Brushes.Green
            };

            Canvas.SetLeft(rect, offset);
            Canvas.SetTop(rect, 0);
            TimeCanvas.Children.Add(rect);
        }

    }
}
