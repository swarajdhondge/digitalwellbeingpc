namespace digital_wellbeing_app.Views.Screen
{
    public partial class ScreenView : System.Windows.Controls.UserControl
    {
        private readonly System.Windows.Threading.DispatcherTimer _realTimeTimer = new()
        {
            Interval = System.TimeSpan.FromSeconds(1)
        };


        public ScreenView()
        {
            InitializeComponent();

            var vm = new digital_wellbeing_app.ViewModels.ScreenViewModel();
            this.DataContext = vm;

            this.Loaded += OnLoaded;
            this.Unloaded += (s, e) => vm.Dispose();
        }

        private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
        {
            _realTimeTimer.Tick += (s, args) => RenderRealTimeCanvas();
            _realTimeTimer.Start();
            RenderRealTimeCanvas();
        }

        private void RenderRealTimeCanvas()
        {
            if (this.DataContext is not digital_wellbeing_app.ViewModels.ScreenViewModel vm)
                return;

            RealTimeCanvas.Children.Clear();

            double width = RealTimeCanvas.ActualWidth;
            double height = RealTimeCanvas.ActualHeight;
            if (width <= 0 || height <= 0) return;

            // Try to get the material brush; fallback to System.Windows.Media.Brushes.Green
            var brush = this.TryFindResource("PrimaryHueMidBrush") as System.Windows.Media.Brush
                        ?? System.Windows.Media.Brushes.Green;

            foreach (var seg in vm.TimelineSegments)
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
