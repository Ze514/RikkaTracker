using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using RikkaTracker.Services;

namespace RikkaTracker.ViewModels
{
    public partial class ActivityListViewModel : ObservableObject
    {
        private readonly IDataService _dataService;

        [ObservableProperty]
        private DateTime _selectedDate = DateTime.Today;

        [ObservableProperty]
        private ObservableCollection<ProcessStatsModel> _processes = new();

        [ObservableProperty]
        private bool _isLoading;

        public ActivityListViewModel(IDataService dataService)
        {
            _dataService = dataService;
            LoadDataAsync();
        }

        partial void OnSelectedDateChanged(DateTime value)
        {
            LoadDataAsync();
        }

        public async void LoadDataAsync()
        {
            IsLoading = true;
            try
            {
                var start = SelectedDate.Date;
                var end = start.AddDays(1);

                var stats = await _dataService.GetTotalTimeByProcessAsync(start, end);
                var list = stats.ToList();
                var totalTicks = list.Sum(s => s.TotalTime.Ticks);

                Processes.Clear();
                foreach (var item in list)
                {
                    Processes.Add(new ProcessStatsModel
                    {
                        ProcessName = item.ProcessName,
                        TotalTime = item.TotalTime,
                        Percentage = totalTicks > 0 ? (double)item.TotalTime.Ticks / totalTicks : 0,
                        TimeDisplay = FormatTimeSpan(item.TotalTime)
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

    public class ProcessStatsModel
    {
        public string ProcessName { get; set; } = string.Empty;
        public TimeSpan TotalTime { get; set; }
        public double Percentage { get; set; }
        public string TimeDisplay { get; set; } = string.Empty;
        // 以后可以加入 Icon 路径
    }
}
