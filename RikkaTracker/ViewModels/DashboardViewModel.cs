using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using RikkaTracker.Services;

namespace RikkaTracker.ViewModels
{
    public partial class DashboardViewModel : ObservableObject
    {
        private readonly IDataService _dataService;

        [ObservableProperty]
        private string _totalTimeText = "0h 0m";

        [ObservableProperty]
        private int _appCount = 0;

        [ObservableProperty]
        private string _topAppName = "N/A";

        [ObservableProperty]
        private string _topAppTimeText = "0h 0m";

        [ObservableProperty]
        private ObservableCollection<HourlyActivityModel> _hourlyActivity = new();

        public DashboardViewModel(IDataService dataService)
        {
            _dataService = dataService;
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

            // 1. 获取汇总数据
            var summary = await _dataService.GetStatsSummaryAsync(today, tomorrow);
            TotalTimeText = FormatTimeSpan(summary.TotalTime);
            AppCount = summary.AppCount;
            TopAppName = summary.TopAppName;
            TopAppTimeText = FormatTimeSpan(summary.TopAppTime);

            // 2. 获取每小时活跃度
            var hourly = await _dataService.GetHourlyUsageAsync(today);
            HourlyActivity.Clear();
            foreach (var item in hourly)
            {
                HourlyActivity.Add(new HourlyActivityModel 
                { 
                    Hour = item.Hour, 
                    Time = item.TotalTime,
                    Percentage = summary.TotalTime.Ticks > 0 ? (double)item.TotalTime.Ticks / summary.TotalTime.Ticks : 0
                });
            }
        }

        private string FormatTimeSpan(TimeSpan ts)
        {
            if (ts.TotalHours >= 1)
                return $"{(int)ts.TotalHours}h {ts.Minutes}m";
            return $"{ts.Minutes}m {ts.Seconds}s";
        }
    }

    public class HourlyActivityModel
    {
        public int Hour { get; set; }
        public TimeSpan Time { get; set; }
        public double Percentage { get; set; } // 相对于今日总时长的百分比
        public string Label => $"{Hour}:00";
    }
}
