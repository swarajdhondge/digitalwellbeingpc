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
            RenderRealTimeCanvas();
        }

        private void OnUnloaded(object sender, System.Windows.RoutedEventArgs e)
        {
            // Just stop the timer when navigating away, don't dispose the ViewModel
            // The ViewModel needs to stay alive to receive goal change events
            _realTimeTimer.Stop();
        }

        private void RenderRealTimeCanvas()
        {
            RealTimeCanvas.Children.Clear();

            double width = RealTimeCanvas.ActualWidth;
            double height = RealTimeCanvas.ActualHeight;
            if (width <= 0 || height <= 0) return;

            // Use the correct design system brush; fallback to green
            var brush = this.TryFindResource("Accent.Primary") as System.Windows.Media.Brush
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
    }
}
