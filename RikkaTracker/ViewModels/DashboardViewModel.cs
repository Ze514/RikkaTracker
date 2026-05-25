using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.Defaults;
using SkiaSharp;
using RikkaTracker.Services;

namespace RikkaTracker.ViewModels
{
    public partial class DashboardViewModel : ObservableObject
    {
        private readonly IDataService _dataService;
        private readonly IIconService _iconService;

        [ObservableProperty]
        private string _totalTimeText = "0h 0m";

        [ObservableProperty]
        private int _appCount = 0;

        [ObservableProperty]
        private string _topAppName = "N/A";

        [ObservableProperty]
        private string _topAppTimeText = "0h 0m";

        [ObservableProperty]
        private ImageSource? _topAppIcon;

        [ObservableProperty]
        private string _timeComparedToYesterday = "-";

        [ObservableProperty]
        private string _mostActiveHour = "-";

        [ObservableProperty]
        private string _averageTimePerApp = "-";

        [ObservableProperty]
        private ISeries[] _series;

        [ObservableProperty]
        private Axis[] _xAxes;

        [ObservableProperty]
        private Axis[] _yAxes;

        public DashboardViewModel(IDataService dataService, IIconService iconService)
        {
            _dataService = dataService;
            _iconService = iconService;

            Series = new ISeries[]
            {
                new ColumnSeries<ObservablePoint>
                {
                    Values = new ObservablePoint[0],
                    Fill = new SolidColorPaint(SKColors.DodgerBlue),
                    MaxBarWidth = 40,
                    Rx = 4,
                    Ry = 4
                }
            };

            XAxes = new Axis[] { new Axis { Labels = new string[0] } };
            YAxes = new Axis[] { new Axis { Labeler = value => TimeSpan.FromSeconds(value).ToString(@"hh\:mm\:ss") } };

            InitializeAsync();
        }

        private async void InitializeAsync()
        {
            await RefreshDataAsync();
        }

        public async Task RefreshDataAsync()
        {
            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);
            var yesterday = today.AddDays(-1);

            // 1. 获取今日汇总数据
            var summary = await _dataService.GetStatsSummaryAsync(today, tomorrow);
            TotalTimeText = FormatTimeSpan(summary.TotalTime);
            AppCount = summary.AppCount;
            TopAppName = summary.TopAppName;
            TopAppTimeText = FormatTimeSpan(summary.TopAppTime);
            TopAppIcon = _iconService.GetIcon(summary.TopAppName, summary.TopAppPath);

            // 2. 平均单应用时长
            if (summary.AppCount > 0)
            {
                var avgSeconds = summary.TotalTime.TotalSeconds / summary.AppCount;
                AverageTimePerApp = FormatTimeSpan(TimeSpan.FromSeconds(avgSeconds));
            }
            else
            {
                AverageTimePerApp = "0m";
            }

            // 3. 与昨日对比
            var yesterdaySummary = await _dataService.GetStatsSummaryAsync(yesterday, today);
            var diff = summary.TotalTime - yesterdaySummary.TotalTime;
            if (yesterdaySummary.TotalTime.TotalSeconds == 0)
            {
                TimeComparedToYesterday = diff.TotalSeconds > 0 ? "↑ 100%" : "-";
            }
            else
            {
                var percent = (diff.TotalSeconds / yesterdaySummary.TotalTime.TotalSeconds) * 100;
                TimeComparedToYesterday = percent >= 0 ? $"↑ {Math.Abs(percent):F1}%" : $"↓ {Math.Abs(percent):F1}%";
            }

            // 4. 获取每小时活跃度与最活跃时段
            var hourly = await _dataService.GetHourlyUsageAsync(today);
            var hourlyList = hourly.ToList();
            
            var maxHour = hourlyList.OrderByDescending(h => h.TotalTime).FirstOrDefault();
            if (maxHour.TotalTime.TotalSeconds > 0)
            {
                MostActiveHour = $"{maxHour.Hour:D2}:00 - {maxHour.Hour + 1:D2}:00";
            }
            else
            {
                MostActiveHour = "-";
            }

            // 5. 更新图表
            var values = new List<ObservablePoint>();
            var labels = new List<string>();
            for (int i = 0; i < hourlyList.Count; i++)
            {
                values.Add(new ObservablePoint(i, hourlyList[i].TotalTime.TotalSeconds));
                labels.Add($"{hourlyList[i].Hour}:00");
            }

            var maxVal = values.Count > 0 ? values.Max(v => v.Y ?? 0) : 0;

            XAxes = new Axis[] { new Axis { Labels = labels } };
            YAxes = new Axis[] { new Axis { 
                MinLimit = 0,
                MaxLimit = maxVal == 0 ? 60 : null,
                Labeler = value => TimeSpan.FromSeconds(value).ToString(@"hh\:mm\:ss") 
            } };

            Series = new ISeries[]
            {
                new ColumnSeries<ObservablePoint>
                {
                    Values = values,
                    Fill = new SolidColorPaint(SKColors.DodgerBlue),
                    MaxBarWidth = 40,
                    Rx = 4,
                    Ry = 4
                }
            };
        }

        private string FormatTimeSpan(TimeSpan ts)
        {
            if (ts.TotalHours >= 1)
                return $"{(int)ts.TotalHours}h {ts.Minutes}m";
            return $"{ts.Minutes}m {ts.Seconds}s";
        }
    }
}
