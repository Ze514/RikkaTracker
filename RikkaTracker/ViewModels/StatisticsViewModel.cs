using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RikkaTracker.Core.Models;
using RikkaTracker.Core.Monitor;
using RikkaTracker.Services;

namespace RikkaTracker.ViewModels
{
    public partial class StatisticsViewModel : ObservableObject
    {
        private readonly IDataService _dataService;
        private readonly IIconService _iconService;
        private readonly IConfigService _configService;

        public StatisticsViewModel(IDataService dataService, IIconService iconService, IConfigService configService)
        {
            _dataService = dataService;
            _iconService = iconService;
            _configService = configService;
            SelectedDate = DateTime.Today;
        }

        public string ZoomMode => _configService.Config.TimelineZoomMode;
        public string DisplayMode => _configService.Config.DisplayMode;

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

        [ObservableProperty]
        private int _refreshTrigger;

        private List<GanttSegment> _allSegments = new();

        public async Task LoadGanttDataAsync()
        {
            IsLoading = true;
            try
            {
                var start = SelectedDate.Date;
                var end = start.AddDays(1);
                var aggregated = new List<GanttSegment>();

                var mode = DisplayMode;
                bool loadApp = mode != "WebOnly";
                bool loadWeb = mode != "AppOnly";

                if (loadApp)
                {
                    var rawSegments = await _dataService.GetSegmentsAsync(start, end);

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
                                    SegmentType = "App",
                                    Icon = _iconService.GetIcon(s.ProcessName, s.ProcessPath)
                                };
                            }
                            else
                            {
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
                                        SegmentType = "App",
                                        Icon = _iconService.GetIcon(s.ProcessName, s.ProcessPath)
                                    };
                                }
                            }
                        }
                        if (current != null) aggregated.Add(current);
                    }
                }

                if (loadWeb)
                {
                    var webSegments = await _dataService.GetWebSegmentsAsync(start, end);

                    var webGrouped = webSegments.GroupBy(s => s.Domain);
                    foreach (var group in webGrouped)
                    {
                        var sorted = group.OrderBy(s => s.StartTime).ToList();
                        GanttSegment? current = null;

                        foreach (var s in sorted)
                        {
                            if (current == null)
                            {
                                current = new GanttSegment
                                {
                                    ProcessName = s.Domain,
                                    WindowTitle = s.Title,
                                    ProcessPath = s.Url,
                                    Status = ActivityStatus.ForegroundActive,
                                    Start = s.StartTime,
                                    End = s.EndTime,
                                    SegmentType = "Web",
                                    Domain = s.Domain,
                                    Url = s.Url,
                                    Icon = LoadFaviconSource(s.Icon)
                                };
                            }
                            else
                            {
                                if ((s.StartTime - current.End).TotalSeconds <= 2)
                                {
                                    current.End = s.EndTime > current.End ? s.EndTime : current.End;
                                }
                                else
                                {
                                    aggregated.Add(current);
                                    current = new GanttSegment
                                    {
                                        ProcessName = s.Domain,
                                        WindowTitle = s.Title,
                                        ProcessPath = s.Url,
                                        Status = ActivityStatus.ForegroundActive,
                                        Start = s.StartTime,
                                        End = s.EndTime,
                                        SegmentType = "Web",
                                        Domain = s.Domain,
                                        Url = s.Url,
                                        Icon = LoadFaviconSource(s.Icon)
                                    };
                                }
                            }
                        }
                        if (current != null) aggregated.Add(current);
                    }
                }

                _allSegments = aggregated;
                ApplyFilters();
            }
            finally
            {
                IsLoading = false;
                RefreshTrigger++;
            }
        }

        private void ApplyFilters()
        {
            var filtered = _allSegments.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                filtered = filtered.Where(s =>
                    s.ProcessName.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                    s.DisplayName.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                    s.WindowTitle.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
            }

            if (!ShowInactive)
                filtered = filtered.Where(s => s.SegmentType == "Web" || s.Status != ActivityStatus.ForegroundInactive);

            if (!ShowBackground)
                filtered = filtered.Where(s => s.SegmentType == "Web" || s.Status != ActivityStatus.Background);

            var sortMode = _configService.Config.TimelineSortMode;

            var groups = filtered
                .GroupBy(s => s.SegmentType == "Web" ? "_W_" + s.Domain : "_A_" + s.ProcessName);

            if (sortMode == "Recency")
                groups = groups.OrderByDescending(g => g.Max(s => s.End));
            else
                groups = groups.OrderByDescending(g => g.Sum(s => (s.End - s.Start).TotalSeconds));

            var orderedGroups = groups.ToList();
            var result = new List<GanttSegment>();
            int rowIndex = 0;

            foreach (var group in orderedGroups)
            {
                foreach (var s in group)
                {
                    result.Add(new GanttSegment
                    {
                        ProcessName = s.ProcessName,
                        ProcessPath = s.ProcessPath,
                        WindowTitle = s.WindowTitle,
                        Status = s.Status,
                        Start = s.Start,
                        End = s.End,
                        Alias = s.Alias,
                        Icon = s.Icon,
                        SegmentType = s.SegmentType,
                        Domain = s.Domain,
                        Url = s.Url,
                        RowIndex = rowIndex
                    });
                }
                rowIndex++;
            }

            GanttSegments = new ObservableCollection<GanttSegment>(result);
        }

        [RelayCommand]
        private void SegmentClicked(GanttSegment segment)
        {
            if (segment == null) return;
            System.Diagnostics.Debug.WriteLine($"Selected: {segment.ProcessName}");
        }

        [RelayCommand]
        private async Task Refresh()
        {
            await LoadGanttDataAsync();
        }

        [ObservableProperty]
        private bool _requestFocusLatest;

        [RelayCommand]
        private void FocusLatest()
        {
            RequestFocusLatest = true;
            RequestFocusLatest = false;
            RefreshTrigger++;
        }

        private static System.Windows.Media.ImageSource? LoadFaviconSource(string iconPath)
        {
            System.Diagnostics.Debug.WriteLine($"[Favicon] LoadFaviconSource called, path='{iconPath}'");
            if (string.IsNullOrWhiteSpace(iconPath))
            {
                System.Diagnostics.Debug.WriteLine("[Favicon] Path is empty or whitespace");
                return null;
            }

            try
            {
                if (iconPath.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                {
                    System.Diagnostics.Debug.WriteLine($"[Favicon] Path is HTTP URL, skipping: {iconPath}");
                    return null;
                }

                if (!File.Exists(iconPath))
                {
                    System.Diagnostics.Debug.WriteLine($"[Favicon] File not found: {iconPath}");
                    return null;
                }

                System.Diagnostics.Debug.WriteLine($"[Favicon] Loading file: {iconPath}");
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(iconPath);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
                bitmap.EndInit();
                bitmap.Freeze();
                System.Diagnostics.Debug.WriteLine($"[Favicon] Loaded successfully: {iconPath}");
                return bitmap;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Favicon] Error loading '{iconPath}': {ex.Message}");
                return null;
            }
        }
    }
}
