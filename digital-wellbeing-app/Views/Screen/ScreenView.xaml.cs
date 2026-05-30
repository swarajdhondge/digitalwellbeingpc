namespace digital_wellbeing_app.Views.Screen
{
    public partial class ScreenView : System.Windows.Controls.UserControl
    {
        private readonly System.Windows.Threading.DispatcherTimer _realTimeTimer = new()
        {
            Interval = System.TimeSpan.FromSeconds(1)
        };

        private readonly digital_wellbeing_app.ViewModels.ScreenViewModel _vm;
        private bool _timerEventAttached;

        public ScreenView()
        {
            InitializeComponent();

            _vm = new digital_wellbeing_app.ViewModels.ScreenViewModel();
            this.DataContext = _vm;

            this.Loaded += OnLoaded;
            this.Unloaded += OnUnloaded;
            this.IsVisibleChanged += OnIsVisibleChanged;
        }

        private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
        {
            // Only attach the tick event once
            if (!_timerEventAttached)
            {
                _realTimeTimer.Tick += (s, args) => RenderRealTimeCanvas();
                _timerEventAttached = true;
            }
            _realTimeTimer.Start();
            _vm.StartRefreshing();
            RenderRealTimeCanvas();
        }

        private void OnUnloaded(object sender, System.Windows.RoutedEventArgs e)
        {
            _realTimeTimer.Stop();
            _vm.StopRefreshing();
        }

        private void OnIsVisibleChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
        {
            if ((bool)e.NewValue)
            {
                _realTimeTimer.Start();
                _vm.StartRefreshing();
                RenderRealTimeCanvas();
            }
            else
            {
                _realTimeTimer.Stop();
                _vm.StopRefreshing();
            }
        }

        private void RenderRealTimeCanvas()
        {
            RealTimeCanvas.Children.Clear();

            double width = RealTimeCanvas.ActualWidth;
            double height = RealTimeCanvas.ActualHeight;
            if (width <= 0 || height <= 0) return;

            // Use the live Pulse section accent; fallback to the legacy teal then green
            var brush = this.TryFindResource("Accent") as System.Windows.Media.Brush
                        ?? this.TryFindResource("Accent.Primary") as System.Windows.Media.Brush
                        ?? System.Windows.Media.Brushes.Green;

            foreach (var seg in _vm.TimelineSegments)
            {
                // Create a Rectangle segment
                var rect = new System.Windows.Shapes.Rectangle
                {
                    Width = seg.WidthPercent * width,
                    Height = height,
                    RadiusX = 2,
                    RadiusY = 2,
                    Fill = brush
                };

                // Position it on the canvas
                System.Windows.Controls.Canvas.SetLeft(rect, seg.StartPercent * width);
                System.Windows.Controls.Canvas.SetTop(rect, 0);

                // Add to the canvas
                RealTimeCanvas.Children.Add(rect);
            }
        }

        private void ScreenScroll_SizeChanged(object sender, System.Windows.SizeChangedEventArgs e)
        {
            var inner = e.NewSize.Width - ScreenScroll.Padding.Left - ScreenScroll.Padding.Right;
            RootPanel.Width = System.Math.Min(System.Math.Max(inner, 0), 1080);
        }

        private void TodayToggle_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            _vm.IsWeeklyView = false;
        }

        private void WeeklyToggle_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            _vm.IsWeeklyView = true;
        }

        private void PrevWeek_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            _vm.GoToPreviousWeek();
        }

        private void NextWeek_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            _vm.GoToNextWeek();
        }
    }
}
