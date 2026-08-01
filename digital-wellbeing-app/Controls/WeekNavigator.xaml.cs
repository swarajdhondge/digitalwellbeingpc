using System;
using System.Windows;
using System.Windows.Controls;
using digital_wellbeing_app.Helpers;

namespace digital_wellbeing_app.Controls
{
    public partial class WeekNavigator : System.Windows.Controls.UserControl
    {
        public WeekNavigator()
        {
            InitializeComponent();
        }

        private bool _preparingCalendar;

        public event EventHandler? PreviousRequested;
        public event EventHandler? NextRequested;
        public event EventHandler<DateTime>? WeekSelected;

        public static readonly DependencyProperty WeekLabelProperty = DependencyProperty.Register(
            nameof(WeekLabel), typeof(string), typeof(WeekNavigator), new PropertyMetadata(string.Empty));
        public static readonly DependencyProperty SelectedWeekStartProperty = DependencyProperty.Register(
            nameof(SelectedWeekStart), typeof(DateTime), typeof(WeekNavigator), new PropertyMetadata(DateTime.Today));
        public static readonly DependencyProperty EarliestWeekStartProperty = DependencyProperty.Register(
            nameof(EarliestWeekStart), typeof(DateTime?), typeof(WeekNavigator), new PropertyMetadata(null));
        public static readonly DependencyProperty CanGoPreviousProperty = DependencyProperty.Register(
            nameof(CanGoPrevious), typeof(bool), typeof(WeekNavigator), new PropertyMetadata(false));
        public static readonly DependencyProperty CanGoNextProperty = DependencyProperty.Register(
            nameof(CanGoNext), typeof(bool), typeof(WeekNavigator), new PropertyMetadata(false));

        public string WeekLabel { get => (string)GetValue(WeekLabelProperty); set => SetValue(WeekLabelProperty, value); }
        public DateTime SelectedWeekStart { get => (DateTime)GetValue(SelectedWeekStartProperty); set => SetValue(SelectedWeekStartProperty, value); }
        public DateTime? EarliestWeekStart { get => (DateTime?)GetValue(EarliestWeekStartProperty); set => SetValue(EarliestWeekStartProperty, value); }
        public bool CanGoPrevious { get => (bool)GetValue(CanGoPreviousProperty); set => SetValue(CanGoPreviousProperty, value); }
        public bool CanGoNext { get => (bool)GetValue(CanGoNextProperty); set => SetValue(CanGoNextProperty, value); }

        private void PrepareCalendar()
        {
            _preparingCalendar = true;
            var currentWeek = WeekNavigationHelper.StartOfWeek(DateTime.Today);
            WeekCalendar.DisplayDateEnd = currentWeek.AddDays(6);
            WeekCalendar.DisplayDateStart = EarliestWeekStart;
            OldestButton.IsEnabled = EarliestWeekStart.HasValue;
            WeekCalendar.SelectedDate = SelectedWeekStart;
            WeekCalendar.DisplayDate = SelectedWeekStart;
            _preparingCalendar = false;
        }

        private void JumpButton_Click(object sender, RoutedEventArgs e)
        {
            PrepareCalendar();
            WeekPopup.IsOpen = true;
        }

        private void Previous_Click(object sender, RoutedEventArgs e) => PreviousRequested?.Invoke(this, EventArgs.Empty);
        private void Next_Click(object sender, RoutedEventArgs e) => NextRequested?.Invoke(this, EventArgs.Empty);
        private void ThisWeek_Click(object sender, RoutedEventArgs e) => Select(DateTime.Today);
        private void FourWeeksBack_Click(object sender, RoutedEventArgs e) => Select(DateTime.Today.AddDays(-28));
        private void Oldest_Click(object sender, RoutedEventArgs e)
        {
            if (EarliestWeekStart.HasValue) Select(EarliestWeekStart.Value);
        }

        private void WeekCalendar_SelectedDatesChanged(object? sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (!_preparingCalendar && WeekCalendar.SelectedDate.HasValue && WeekPopup.IsOpen)
                Select(WeekCalendar.SelectedDate.Value);
        }

        private void Select(DateTime date)
        {
            var selected = WeekNavigationHelper.Clamp(date, EarliestWeekStart);
            WeekPopup.IsOpen = false;
            WeekSelected?.Invoke(this, selected);
        }

        private void WeekPopup_Closed(object? sender, EventArgs e) { }
    }
}
