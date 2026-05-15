using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RikkaTracker.Core.Models;
using RikkaTracker.Services;

namespace RikkaTracker.ViewModels
{
    public partial class StatisticsViewModel : ObservableObject
    {
        private readonly IDataService _dataService;
        private readonly IIconService _iconService;

        public StatisticsViewModel(IDataService dataService, IIconService iconService)
        {
            _dataService = dataService;
            _iconService = iconService;
            SelectedDate = DateTime.Today;
        }

        [ObservableProperty]
        private DateTime _selectedDate;

        partial void OnSelectedDateChanged(DateTime value)
        {
            _ = LoadGanttDataAsync();
        }

        [ObservableProperty]
        private string _searchText = string.Empty;
        partial void OnSearchTextChanged(string value) => ApplyFilters();

        [ObservableProperty]
        private bool _showInactive = true;
        partial void OnShowInactiveChanged(bool value) => ApplyFilters();

        [ObservableProperty]
        private bool _showBackground = false;
        partial void OnShowBackgroundChanged(bool value) => ApplyFilters();

        [ObservableProperty]
        private ObservableCollection<GanttSegment> _ganttSegments = new();

        [ObservableProperty]
        private bool _isLoading;

        private List<GanttSegment> _allSegments = new();

        public async Task LoadGanttDataAsync()
        {
            IsLoading = true;
            try
            {
                var start = SelectedDate.Date;
                var end = start.AddDays(1);
                var rawSegments = await _dataService.GetSegmentsAsync(start, end);

                var aggregated = new List<GanttSegment>();
                
                // 按进程名分组，然后在每个进程内部按时间排序并合并相邻同状态的片段
                var grouped = rawSegments.GroupBy(s => s.ProcessName);
                foreach (var group in grouped)
                {
                    var sorted = group.OrderBy(s => s.StartTime).ToList();
                    GanttSegment? current = null;

                    foreach (var s in sorted)
                    {
                        if (current == null)
                        {
                            current = new GanttSegment
                            {
                                ProcessName = s.ProcessName,
                                ProcessPath = s.ProcessPath,
                                WindowTitle = s.WindowTitle,
                                Status = s.Status,
                                Start = s.StartTime,
                                End = s.EndTime,
                                Alias = s.Alias,
                                Icon = _iconService.GetIcon(s.ProcessName, s.ProcessPath)
                            };
                        }
                        else
                        {
                            // 合并条件：状态相同，且时间间隔小于等于2秒 (处理细微的断层)
                            if (current.Status == s.Status && (s.StartTime - current.End).TotalSeconds <= 2)
                            {
                                current.End = s.EndTime > current.End ? s.EndTime : current.End;
                            }
                            else
                            {
                                aggregated.Add(current);
                                current = new GanttSegment
                                {
                                    ProcessName = s.ProcessName,
                                    ProcessPath = s.ProcessPath,
                                    WindowTitle = s.WindowTitle,
                                    Status = s.Status,
                                    Start = s.StartTime,
                                    End = s.EndTime,
                                    Alias = s.Alias,
                                    Icon = _iconService.GetIcon(s.ProcessName, s.ProcessPath)
                                };
                            }
                        }
                    }
                    if (current != null)
                    {
                        aggregated.Add(current);
                    }
                }

                _allSegments = aggregated;
                ApplyFilters();
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void ApplyFilters()
        {
            var filtered = _allSegments.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                filtered = filtered.Where(s => s.ProcessName.Contains(SearchText, StringComparison.OrdinalIgnoreCase) || 
                                              s.DisplayName.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
            }

            if (!ShowInactive)
            {
                filtered = filtered.Where(s => s.Status != Core.Monitor.ActivityStatus.ForegroundInactive);
            }

            if (!ShowBackground)
            {
                filtered = filtered.Where(s => s.Status != Core.Monitor.ActivityStatus.Background);
            }

            // 分组计算每行总时长以排序
            var grouped = filtered.GroupBy(s => s.ProcessName)
                .OrderByDescending(g => g.Sum(s => (s.End - s.Start).TotalSeconds))
                .ToList();

            var result = new List<GanttSegment>();
            for (int i = 0; i < grouped.Count; i++)
            {
                var group = grouped[i];
                foreach (var s in group)
                {
                    var copy = new GanttSegment
                    {
                        ProcessName = s.ProcessName,
                        ProcessPath = s.ProcessPath,
                        WindowTitle = s.WindowTitle,
                        Status = s.Status,
                        Start = s.Start,
                        End = s.End,
                        Alias = s.Alias,
                        Icon = s.Icon,
                        RowIndex = i
                    };
                    result.Add(copy);
                }
            }

            GanttSegments = new ObservableCollection<GanttSegment>(result);
        }

        [RelayCommand]
        private void SegmentClicked(GanttSegment segment)
        {
            if (segment == null) return;
            
            // TODO: 后续替换为导航到应用详情页的逻辑
            System.Diagnostics.Debug.WriteLine($"选中了应用: {segment.ProcessName}");
        }
    }
}
