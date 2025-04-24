using System;
using System.ComponentModel;
using System.Windows;
using digital_wellbeing_app.CoreLogic;

namespace digital_wellbeing_app.MainWindow
{
    public partial class MainWindow : Window
    {
        private AppUsageTracker? _tracker;

        public MainWindow()
        {
            InitializeComponent();

            if (!DesignerProperties.GetIsInDesignMode(this))
            {
                Loaded += OnLoaded;
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            Loaded -= OnLoaded;
            _tracker = new AppUsageTracker();
            _tracker.Start();
        }
    }
}
