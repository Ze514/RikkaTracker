using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.Kernel.Sketches;
using LiveChartsCore.Defaults;
using SkiaSharp;
using RikkaTracker.Services;

namespace RikkaTracker.ViewModels
{
    public enum StatisticsMode
    {
        Day,
        Week,
        Month,
        Year
    }

    public partial class UsageStatisticsViewModel : ObservableObject
    {
        private readonly IDataService _dataService;
        private readonly IIconService _iconService;
        private readonly ILocalizationService _localizationService;
        private readonly IThemeService _themeService;

        private SolidColorPaint? _axisLabelPaint;
        private SolidColorPaint? _axisSeparatorPaint;
        private SolidColorPaint? _columnFillPaint;

        public UsageStatisticsViewModel(IDataService dataService, IIconService iconService, ILocalizationService localizationService, IThemeService themeService)
        {
            _dataService = dataService;
            _iconService = iconService;
            _localizationService = localizationService;
            _themeService = themeService;

            _themeService.ThemeChanged += OnThemeChanged;
            UpdateChartColors(_themeService.GetCurrentTheme());
            
            // Initialize chart first so that property setters triggering LoadDataAsync won't throw NRE
            Series = new ISeries[]
            {
                new ColumnSeries<ObservablePoint>
                {
                    Values = new ObservablePoint[0],
                    Fill = _columnFillPaint,
                    MaxBarWidth = 40,
                    Rx = 4,
                    Ry = 4
                }
            };

            XAxes = new Axis[] { new Axis { Labels = new string[0], LabelsPaint = _axisLabelPaint, SeparatorsPaint = _axisSeparatorPaint } };
            YAxes = new Axis[] { new Axis { Labeler = value => TimeSpan.FromSeconds(value).ToString(@"hh\:mm\:ss"), LabelsPaint = _axisLabelPaint, SeparatorsPaint = _axisSeparatorPaint } };

            // Initialize dates
            var now = DateTime.Today;
            SelectedDate = now;
            SelectedWeekStart = now.AddDays(-(int)now.DayOfWeek + (int)DayOfWeek.Monday); // Assuming Monday is start of week
            if (SelectedWeekStart > now) SelectedWeekStart = SelectedWeekStart.AddDays(-7);
            
            SelectedYear = now.Year;
            SelectedMonth = now.Month;
            SelectedYearOnly = now.Year;

            // Initialize available years and months
            AvailableYears = new ObservableCollection<int>(Enumerable.Range(now.Year - 5, 6).Reverse());
            AvailableMonths = new ObservableCollection<int>(Enumerable.Range(1, 12));

            LoadDataAsync();
        }

        [ObservableProperty]
        private StatisticsMode _currentMode = StatisticsMode.Day;
        partial void OnCurrentModeChanged(StatisticsMode value) => LoadDataAsync();

        [ObservableProperty]
        private DateTime _selectedDate;
        partial void OnSelectedDateChanged(DateTime value)
        {
            if (CurrentMode == StatisticsMode.Day) LoadDataAsync();
        }

        [ObservableProperty]
        private DateTime _selectedWeekStart;

        public string SelectedWeekDisplay => $"{SelectedWeekStart:yyyy-MM-dd} ~ {SelectedWeekStart.AddDays(6):yyyy-MM-dd}";

        [RelayCommand]
        private void PrevWeek()
        {
            SelectedWeekStart = SelectedWeekStart.AddDays(-7);
            OnPropertyChanged(nameof(SelectedWeekDisplay));
            if (CurrentMode == StatisticsMode.Week) LoadDataAsync();
        }

        [RelayCommand]
        private void NextWeek()
        {
            SelectedWeekStart = SelectedWeekStart.AddDays(7);
            OnPropertyChanged(nameof(SelectedWeekDisplay));
            if (CurrentMode == StatisticsMode.Week) LoadDataAsync();
        }

        [ObservableProperty]
        private int _selectedYear;
        partial void OnSelectedYearChanged(int value)
        {
            if (CurrentMode == StatisticsMode.Month) LoadDataAsync();
        }

        [ObservableProperty]
        private int _selectedMonth;
        partial void OnSelectedMonthChanged(int value)
        {
            if (CurrentMode == StatisticsMode.Month) LoadDataAsync();
        }

        [ObservableProperty]
        private int _selectedYearOnly;
        partial void OnSelectedYearOnlyChanged(int value)
        {
            if (CurrentMode == StatisticsMode.Year) LoadDataAsync();
        }

        public ObservableCollection<int> AvailableYears { get; }
        public ObservableCollection<int> AvailableMonths { get; }

        [ObservableProperty]
        private ISeries[] _series;

        [ObservableProperty]
        private Axis[] _xAxes;

        [ObservableProperty]
        private Axis[] _yAxes;

        [ObservableProperty]
        private ObservableCollection<ProcessStatsModel> _leaderboard = new();

        [ObservableProperty]
        private bool _isLoading;

        private async void LoadDataAsync()
        {
            IsLoading = true;
            try
            {
                var values = new List<ObservablePoint>();
                var labels = new List<string>();

                if (CurrentMode == StatisticsMode.Day)
                {
                    var data = await _dataService.GetHourlyUsageAsync(SelectedDate);
                    var list = data.ToList();
                    for(int i = 0; i < list.Count; i++) {
                        values.Add(new ObservablePoint(i, list[i].TotalTime.TotalSeconds));
                        labels.Add($"{list[i].Hour}:00");
                    }
                    
                    // Clear leaderboard until user clicks
                    Leaderboard.Clear();
                }
                else if (CurrentMode == StatisticsMode.Week)
                {
                    var start = SelectedWeekStart.Date;
                    var end = start.AddDays(7);
                    var data = await _dataService.GetDailyTrendAsync(start, end);
                    var dict = data.ToDictionary(d => d.Date, d => d.TotalTime.TotalSeconds);

                    for (int i = 0; i < 7; i++)
                    {
                        var d = start.AddDays(i);
                        values.Add(new ObservablePoint(i, dict.ContainsKey(d) ? dict[d] : 0));
                        labels.Add(d.ToString("MM-dd ddd"));
                    }
                    Leaderboard.Clear();
                }
                else if (CurrentMode == StatisticsMode.Month)
                {
                    var start = new DateTime(SelectedYear, SelectedMonth, 1);
                    var end = start.AddMonths(1);
                    var data = await _dataService.GetDailyTrendAsync(start, end);
                    var dict = data.ToDictionary(d => d.Date, d => d.TotalTime.TotalSeconds);

                    int days = DateTime.DaysInMonth(SelectedYear, SelectedMonth);
                    for (int i = 1; i <= days; i++)
                    {
                        var d = new DateTime(SelectedYear, SelectedMonth, i);
                        values.Add(new ObservablePoint(i - 1, dict.ContainsKey(d) ? dict[d] : 0));
                        labels.Add($"{i}");
                    }
                    Leaderboard.Clear();
                }
                else if (CurrentMode == StatisticsMode.Year)
                {
                    var data = await _dataService.GetMonthlyTrendAsync(SelectedYearOnly);
                    var dict = data.ToDictionary(d => d.Month, d => d.TotalTime.TotalSeconds);

                    for (int i = 1; i <= 12; i++)
                    {
                        values.Add(new ObservablePoint(i - 1, dict.ContainsKey(i) ? dict[i] : 0));
                        labels.Add($"{i}" + _localizationService.GetString("StrUnitMonth", "月"));
                    }
                    Leaderboard.Clear();
                }

                var maxVal = values.Count > 0 ? values.Max(v => v.Y ?? 0) : 0;
                
                XAxes = new Axis[] { new Axis { Labels = labels, LabelsPaint = _axisLabelPaint, SeparatorsPaint = _axisSeparatorPaint } };
                YAxes = new Axis[] { new Axis { 
                    MinLimit = 0,
                    MaxLimit = maxVal == 0 ? 60 : null, // Default max to 60s if no data
                    Labeler = value => TimeSpan.FromSeconds(value).ToString(@"hh\:mm\:ss"),
                    LabelsPaint = _axisLabelPaint,
                    SeparatorsPaint = _axisSeparatorPaint
                } };

                Series = new ISeries[]
                {
                    new ColumnSeries<ObservablePoint>
                    {
                        Values = values,
                        Fill = _columnFillPaint,
                        MaxBarWidth = 40,
                        Rx = 4,
                        Ry = 4
                    }
                };
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void UpdateChartColors(string theme)
        {
            var isDark = theme.Equals("Dark", StringComparison.OrdinalIgnoreCase);
            var labelColor = isDark ? new SKColor(220, 220, 220) : new SKColor(80, 80, 80);
            var separatorColor = isDark ? new SKColor(255, 255, 255, 30) : new SKColor(0, 0, 0, 15);
            var barColor = isDark ? new SKColor(244, 114, 182) : new SKColor(14, 165, 233);

            ( _axisLabelPaint as IDisposable)?.Dispose();
            ( _axisSeparatorPaint as IDisposable)?.Dispose();
            ( _columnFillPaint as IDisposable)?.Dispose();

            _axisLabelPaint = new SolidColorPaint(labelColor);
            _axisSeparatorPaint = new SolidColorPaint(separatorColor) { StrokeThickness = 1 };
            _columnFillPaint = new SolidColorPaint(barColor);
        }

        private void OnThemeChanged(string newTheme)
        {
            UpdateChartColors(newTheme);

            if (XAxes != null && XAxes.Length > 0)
            {
                XAxes[0].LabelsPaint = _axisLabelPaint;
                XAxes[0].SeparatorsPaint = _axisSeparatorPaint;
            }
            if (YAxes != null && YAxes.Length > 0)
            {
                YAxes[0].LabelsPaint = _axisLabelPaint;
                YAxes[0].SeparatorsPaint = _axisSeparatorPaint;
            }
            if (Series != null && Series.Length > 0 && Series[0] is ColumnSeries<ObservablePoint> colSeries)
            {
                colSeries.Fill = _columnFillPaint;
            }
        }

        [RelayCommand]
        private async Task ChartPointClicked(LiveChartsCore.Kernel.ChartPoint point)
        {
            if (point == null) return;
            // Get index from the ObservablePoint
            var ds = point.Context.DataSource as ObservablePoint;
            if (ds == null || ds.X == null) return;
            int index = (int)ds.X.Value;
            
            DateTime start = DateTime.MinValue;
            DateTime end = DateTime.MinValue;

            if (CurrentMode == StatisticsMode.Day)
            {
                start = SelectedDate.Date.AddHours(index);
                end = start.AddHours(1);
            }
            else if (CurrentMode == StatisticsMode.Week)
            {
                start = SelectedWeekStart.AddDays(index);
                end = start.AddDays(1);
            }
            else if (CurrentMode == StatisticsMode.Month)
            {
                start = new DateTime(SelectedYear, SelectedMonth, index + 1);
                end = start.AddDays(1);
            }
            else if (CurrentMode == StatisticsMode.Year)
            {
                start = new DateTime(SelectedYearOnly, index + 1, 1);
                end = start.AddMonths(1);
            }

            if (start != DateTime.MinValue)
            {
                await LoadLeaderboardAsync(start, end);
            }
        }

        private async Task LoadLeaderboardAsync(DateTime start, DateTime end)
        {
            IsLoading = true;
            try
            {
                var stats = await _dataService.GetTotalTimeByProcessAsync(start, end);
                var list = stats.ToList();
                var totalTicks = list.Sum(s => s.TotalTime.Ticks);

                Leaderboard.Clear();
                foreach (var item in list)
                {
                    Leaderboard.Add(new ProcessStatsModel
                    {
                        ProcessName = item.ProcessName,
                        TotalTime = item.TotalTime,
                        Percentage = totalTicks > 0 ? (double)item.TotalTime.Ticks / totalTicks : 0,
                        TimeDisplay = FormatTimeSpan(item.TotalTime),
                        Icon = _iconService.GetIcon(item.ProcessName, item.ProcessPath)
                    });
                }
            }
            finally
            {
                IsLoading = false;
            }
        }

        private string FormatTimeSpan(TimeSpan ts)
        {
            if (ts.TotalHours >= 1)
                return $"{(int)ts.TotalHours}h {ts.Minutes}m";
            return $"{ts.Minutes}m {ts.Seconds}s";
        }
    }
}
