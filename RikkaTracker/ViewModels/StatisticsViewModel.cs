using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using RikkaTracker.Core.Models;
using RikkaTracker.Services;

namespace RikkaTracker.ViewModels
{
    public partial class StatisticsViewModel : ObservableObject
    {
        private readonly IDataService _dataService;

        public StatisticsViewModel(IDataService dataService)
        {
            _dataService = dataService;
            SelectedDate = DateTime.Today;
        }

        [ObservableProperty]
        private DateTime _selectedDate;

        partial void OnSelectedDateChanged(DateTime value)
        {
            _ = LoadGanttDataAsync();
        }

        [ObservableProperty]
        private ObservableCollection<GanttSegment> _ganttSegments = new();

        [ObservableProperty]
        private bool _isLoading;

        public async Task LoadGanttDataAsync()
        {
            IsLoading = true;
            try
            {
                var start = SelectedDate.Date;
                var end = start.AddDays(1);
                var segments = await _dataService.GetSegmentsAsync(start, end);

                // 按进程名分组并计算行索引
                var grouped = segments.GroupBy(s => s.ProcessName)
                    .OrderByDescending(g => g.Sum(s => (s.EndTime - s.StartTime).TotalSeconds))
                    .ToList();

                var result = new List<GanttSegment>();
                for (int i = 0; i < grouped.Count; i++)
                {
                    var group = grouped[i];
                    foreach (var s in group)
                    {
                        result.Add(new GanttSegment
                        {
                            ProcessName = s.ProcessName,
                            WindowTitle = s.WindowTitle,
                            Status = s.Status,
                            Start = s.StartTime,
                            End = s.EndTime,
                            RowIndex = i
                        });
                    }
                }

                GanttSegments = new ObservableCollection<GanttSegment>(result);
            }
            finally
            {
                IsLoading = false;
            }
        }
    }
}
