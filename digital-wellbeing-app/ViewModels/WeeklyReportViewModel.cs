using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Media;
using digital_wellbeing_app.Models;
using digital_wellbeing_app.Services;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace digital_wellbeing_app.ViewModels
{
    public class WeeklyReportViewModel : INotifyPropertyChanged
    {
        private readonly ReportService _reportService = new();
        private DateTime _currentWeekStart;
        private WeeklyReportData _reportData = new();

        // Chart color palette (matches theme tokens)
        private static readonly SKColor ChartPrimary = SKColor.Parse("#3B82F6");    // Blue 500 - Main chart bars (Samsung style)
        private static readonly SKColor AccentPrimary = SKColor.Parse("#2DD4BF");   // Teal 400
        private static readonly SKColor StatusSuccess = SKColor.Parse("#22C55E");   // Green 500
        private static readonly SKColor StatusWarning = SKColor.Parse("#F59E0B");   // Amber 500
        private static readonly SKColor StatusDanger = SKColor.Parse("#EF4444");    // Red 500
        private static readonly SKColor StatusInfo = SKColor.Parse("#3B82F6");      // Blue 500
        private static readonly SKColor TextSecondary = SKColor.Parse("#94A3B8");   // Slate 400

        public WeeklyReportViewModel()
        {
            _currentWeekStart = ReportService.GetWeekStart(DateTime.Now);
            
            // Initialize commands
            PreviousWeekCommand = new RelayCommand(_ => NavigateWeek(-1));
            NextWeekCommand = new RelayCommand(_ => NavigateWeek(1), _ => CanNavigateNext);
            
            LoadReportData();
        }

        #region Properties

        public string WeekLabel => _reportData.WeekLabel;
        public string TotalScreenTime => _reportData.TotalFormatted;
        public string AverageDailyTime => _reportData.AverageFormatted;
        public int FocusSessionCount => _reportData.FocusSessionCount;

        // Week-over-week comparison
        public string ChangePercent => _reportData.WeekOverWeek.ChangePercentFormatted;
        public bool ScreenTimeImproved => _reportData.WeekOverWeek.ScreenTimeImproved;
        public string ChangeDirection => ScreenTimeImproved ? "↓" : "↑";

        // Focus vs Leisure
        public string FocusTime => _reportData.FocusVsLeisure.FocusFormatted;
        public string LeisureTime => _reportData.FocusVsLeisure.LeisureFormatted;
        public string OtherTime => _reportData.FocusVsLeisure.OtherFormatted;
        public double FocusPercentage => _reportData.FocusVsLeisure.FocusPercentage;
        public double LeisurePercentage => _reportData.FocusVsLeisure.LeisurePercentage;
        
        // Percentage properties for stacked bar (0-100 scale)
        public double FocusPercent => _reportData.FocusVsLeisure.FocusPercentage;
        public double LeisurePercent => _reportData.FocusVsLeisure.LeisurePercentage;
        public double OtherPercent => 100.0 - FocusPercent - LeisurePercent;

        // Navigation
        public bool CanNavigateNext => _currentWeekStart.AddDays(7) <= ReportService.GetWeekStart(DateTime.Now);

        // Top Apps for display
        public ObservableCollection<Models.AppUsageSummary> TopApps { get; } = new();

        #endregion

        #region Chart Series

        private ISeries[] _dailyTrendSeries = Array.Empty<ISeries>();
        public ISeries[] DailyTrendSeries
        {
            get => _dailyTrendSeries;
            set { _dailyTrendSeries = value; OnPropertyChanged(); }
        }

        private Axis[] _dailyTrendXAxes = Array.Empty<Axis>();
        public Axis[] DailyTrendXAxes
        {
            get => _dailyTrendXAxes;
            set { _dailyTrendXAxes = value; OnPropertyChanged(); }
        }

        private Axis[] _dailyTrendYAxes = Array.Empty<Axis>();
        public Axis[] DailyTrendYAxes
        {
            get => _dailyTrendYAxes;
            set { _dailyTrendYAxes = value; OnPropertyChanged(); }
        }

        private ISeries[] _topAppsSeries = Array.Empty<ISeries>();
        public ISeries[] TopAppsSeries
        {
            get => _topAppsSeries;
            set { _topAppsSeries = value; OnPropertyChanged(); }
        }

        private Axis[] _topAppsXAxes = Array.Empty<Axis>();
        public Axis[] TopAppsXAxes
        {
            get => _topAppsXAxes;
            set { _topAppsXAxes = value; OnPropertyChanged(); }
        }

        private Axis[] _topAppsYAxes = Array.Empty<Axis>();
        public Axis[] TopAppsYAxes
        {
            get => _topAppsYAxes;
            set { _topAppsYAxes = value; OnPropertyChanged(); }
        }

        private ISeries[] _focusLeisureSeries = Array.Empty<ISeries>();
        public ISeries[] FocusLeisureSeries
        {
            get => _focusLeisureSeries;
            set { _focusLeisureSeries = value; OnPropertyChanged(); }
        }

        #endregion

        #region Commands

        public ICommand PreviousWeekCommand { get; }
        public ICommand NextWeekCommand { get; }

        private void NavigateWeek(int direction)
        {
            _currentWeekStart = _currentWeekStart.AddDays(direction * 7);
            LoadReportData();
            OnPropertyChanged(nameof(CanNavigateNext));
        }

        #endregion

        #region Data Loading

        private void LoadReportData()
        {
            _reportData = _reportService.GetWeeklyReport(_currentWeekStart);
            
            BuildDailyTrendChart();
            BuildTopAppsChart();
            BuildFocusLeisureChart();
            UpdateTopAppsList();

            // Notify all properties changed
            OnPropertyChanged(string.Empty);
        }

        private void BuildDailyTrendChart()
        {
            var values = _reportData.DailyTrend.Select(d => d.Hours).ToArray();
            var labels = _reportData.DailyTrend.Select(d => d.DayLabel).ToArray();

            DailyTrendSeries = new ISeries[]
            {
                new ColumnSeries<double>
                {
                    Values = values,
                    Fill = new SolidColorPaint(ChartPrimary),  // Samsung Blue for chart bars
                    Stroke = null,
                    MaxBarWidth = 40,
                    Rx = 4,
                    Ry = 4,
                    Padding = 8
                }
            };

            DailyTrendXAxes = new Axis[]
            {
                new Axis
                {
                    Labels = labels,
                    LabelsPaint = new SolidColorPaint(TextSecondary),
                    SeparatorsPaint = null,
                    TicksPaint = null
                }
            };

            DailyTrendYAxes = new Axis[]
            {
                new Axis
                {
                    LabelsPaint = new SolidColorPaint(TextSecondary),
                    SeparatorsPaint = new SolidColorPaint(SKColor.Parse("#334155")) { StrokeThickness = 1 },
                    Labeler = value => $"{value:F0}h",
                    MinLimit = 0
                }
            };
        }

        private void BuildTopAppsChart()
        {
            var topApps = _reportData.TopApps.Take(5).Reverse().ToList();
            
            if (topApps.Count == 0)
            {
                TopAppsSeries = Array.Empty<ISeries>();
                return;
            }

            var values = topApps.Select(a => a.Hours).ToArray();
            var labels = topApps.Select(a => TruncateName(a.AppName, 15)).ToArray();

            // Color based on category
            var colors = topApps.Select(a => a.Category switch
            {
                AppCategoryType.Work => StatusSuccess,
                AppCategoryType.Entertainment => StatusWarning,
                _ => AccentPrimary
            }).ToArray();

            TopAppsSeries = new ISeries[]
            {
                new RowSeries<double>
                {
                    Values = values,
                    Fill = new SolidColorPaint(ChartPrimary),  // Samsung Blue for chart bars
                    Stroke = null,
                    MaxBarWidth = 24,
                    Rx = 4,
                    Ry = 4,
                    Padding = 4,
                    DataLabelsPaint = new SolidColorPaint(TextSecondary),
                    DataLabelsSize = 11,
                    DataLabelsPosition = LiveChartsCore.Measure.DataLabelsPosition.End,
                    DataLabelsFormatter = point => 
                    {
                        var index = (int)point.Index;
                        if (index >= 0 && index < topApps.Count)
                            return topApps[index].FormattedTime;
                        return "";
                    }
                }
            };

            TopAppsXAxes = new Axis[]
            {
                new Axis
                {
                    LabelsPaint = new SolidColorPaint(TextSecondary),
                    SeparatorsPaint = new SolidColorPaint(SKColor.Parse("#334155")) { StrokeThickness = 1 },
                    Labeler = value => $"{value:F0}h",
                    MinLimit = 0
                }
            };

            TopAppsYAxes = new Axis[]
            {
                new Axis
                {
                    Labels = labels,
                    LabelsPaint = new SolidColorPaint(TextSecondary),
                    SeparatorsPaint = null,
                    TicksPaint = null
                }
            };
        }

        private void BuildFocusLeisureChart()
        {
            var focus = _reportData.FocusVsLeisure.FocusTime.TotalHours;
            var leisure = _reportData.FocusVsLeisure.LeisureTime.TotalHours;
            var other = _reportData.FocusVsLeisure.OtherTime.TotalHours;

            if (focus + leisure + other == 0)
            {
                FocusLeisureSeries = Array.Empty<ISeries>();
                return;
            }

            FocusLeisureSeries = new ISeries[]
            {
                new PieSeries<double>
                {
                    Values = new[] { focus },
                    Name = "Focus",
                    Fill = new SolidColorPaint(StatusSuccess),
                    Pushout = 0,
                    InnerRadius = 60
                },
                new PieSeries<double>
                {
                    Values = new[] { leisure },
                    Name = "Leisure",
                    Fill = new SolidColorPaint(StatusWarning),
                    Pushout = 0,
                    InnerRadius = 60
                },
                new PieSeries<double>
                {
                    Values = new[] { other },
                    Name = "Other",
                    Fill = new SolidColorPaint(SKColor.Parse("#64748B")),
                    Pushout = 0,
                    InnerRadius = 60
                }
            };
        }

        private void UpdateTopAppsList()
        {
            TopApps.Clear();
            foreach (var app in _reportData.TopApps)
            {
                TopApps.Add(app);
            }
        }

        private static string TruncateName(string name, int maxLength)
        {
            if (string.IsNullOrEmpty(name)) return "Unknown";
            if (name.Length <= maxLength) return name;
            return name.Substring(0, maxLength - 1) + "…";
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }

    /// <summary>
    /// Simple relay command implementation
    /// </summary>
    public class RelayCommand : ICommand
    {
        private readonly Action<object?> _execute;
        private readonly Func<object?, bool>? _canExecute;

        public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;
        public void Execute(object? parameter) => _execute(parameter);

        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }
    }
}

