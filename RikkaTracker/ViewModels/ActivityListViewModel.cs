using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using RikkaTracker.Core.Librarys;
using RikkaTracker.Services;

namespace RikkaTracker.ViewModels
{
    public partial class ActivityListViewModel : ObservableObject
    {
        private readonly IDataService _dataService;
        private readonly IIconService _iconService;
        private readonly IConfigService _configService;

        [ObservableProperty]
        private DateTime _selectedDate = DateTime.Today;

        [ObservableProperty]
        private ObservableCollection<ProcessStatsModel> _processes = new();

        [ObservableProperty]
        private bool _isLoading;

        /*
         * @Author: trae + deepseek-V4-pro
         * @Date: 2026-06-23
         * @Desc: 构造函数。订阅 ConfigChanged 事件，当视图模式切换时即时刷新数据。
         */
        public ActivityListViewModel(IDataService dataService, IIconService iconService, IConfigService configService)
        {
            _dataService = dataService;
            _iconService = iconService;
            _configService = configService;
            // 订阅配置变更事件，实现视图模式切换时即时刷新
            _configService.ConfigChanged += OnConfigChanged;
            LoadDataAsync();
        }

        partial void OnSelectedDateChanged(DateTime value)
        {
            LoadDataAsync();
        }

        /// <summary>
        /// @Author: trae + deepseek-V4-pro
        /// @Date: 2026-06-23
        /// @Desc: 配置变更回调。当 DisplayMode 变化时，重新加载活跃应用列表。
        /// </summary>
        private void OnConfigChanged()
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
                string displayMode = _configService.Config.DisplayMode;

                Processes.Clear();

                if (displayMode == "WebOnly")
                {
                    await LoadWebOnlyAsync(start, end);
                }
                else if (displayMode == "AppOnly")
                {
                    await LoadAppOnlyAsync(start, end);
                }
                else // Combined
                {
                    await LoadCombinedAsync(start, end);
                }
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// @Author: trae + deepseek-V4-pro
        /// @Date: 2026-06-23
        /// @Desc: 仅网页模式：加载网页统计数据并显示网页图标。
        /// </summary>
        private async Task LoadWebOnlyAsync(DateTime start, DateTime end)
        {
            var stats = await _dataService.GetTopSitesByDomainAsync(start, end);
            var list = stats.ToList();
            var totalTicks = list.Sum(s => s.TotalTime.Ticks);
            // 获取所有站点的 favicon 路径
            var faviconMap = await GetFaviconMapAsync(start, end);

            foreach (var item in list)
            {
                string domain = item.Domain;
                Processes.Add(new ProcessStatsModel
                {
                    ProcessName = UrlHelper.GetName(domain),
                    Domain = domain,
                    TotalTime = item.TotalTime,
                    Percentage = totalTicks > 0 ? (double)item.TotalTime.Ticks / totalTicks : 0,
                    TimeDisplay = FormatTimeSpan(item.TotalTime),
                    IsWeb = true,
                    Icon = faviconMap.TryGetValue(domain, out var iconPath)
                        ? LoadFaviconSource(iconPath) ?? GlobeIcon.Source
                        : GlobeIcon.Source
                });
            }
        }

        /// <summary>
        /// @Author: trae + deepseek-V4-pro
        /// @Date: 2026-06-23
        /// @Desc: 仅应用模式：加载应用统计数据并显示应用图标。
        /// </summary>
        private async Task LoadAppOnlyAsync(DateTime start, DateTime end)
        {
            var stats = await _dataService.GetTotalTimeByProcessAsync(start, end);
            var list = stats.ToList();
            var totalTicks = list.Sum(s => s.TotalTime.Ticks);
            foreach (var item in list)
            {
                Processes.Add(new ProcessStatsModel
                {
                    ProcessName = item.ProcessName,
                    TotalTime = item.TotalTime,
                    Percentage = totalTicks > 0 ? (double)item.TotalTime.Ticks / totalTicks : 0,
                    TimeDisplay = FormatTimeSpan(item.TotalTime),
                    Icon = _iconService.GetIcon(item.ProcessName, item.ProcessPath)
                });
            }
        }

        /// <summary>
        /// @Author: trae + deepseek-V4-pro
        /// @Date: 2026-06-23
        /// @Desc: 混合视图模式：将应用和网页数据合并后统一按总时长降序排序，
        ///         不再将应用和网页分成上下两部分。
        ///         网页行头显示 favicon。
        /// </summary>
        private async Task LoadCombinedAsync(DateTime start, DateTime end)
        {
            var allItems = new List<ProcessStatsModel>();

            // 加载应用数据
            var appStats = await _dataService.GetTotalTimeByProcessAsync(start, end);
            var appList = appStats.ToList();
            foreach (var item in appList)
            {
                allItems.Add(new ProcessStatsModel
                {
                    ProcessName = item.ProcessName,
                    TotalTime = item.TotalTime,
                    TimeDisplay = FormatTimeSpan(item.TotalTime),
                    Icon = _iconService.GetIcon(item.ProcessName, item.ProcessPath)
                });
            }

            // 加载网页数据
            var webStats = await _dataService.GetTopSitesByDomainAsync(start, end);
            var webList = webStats.ToList();
            var faviconMap = await GetFaviconMapAsync(start, end);

            foreach (var item in webList)
            {
                string domain = item.Domain;
                allItems.Add(new ProcessStatsModel
                {
                    ProcessName = UrlHelper.GetName(domain),
                    Domain = domain,
                    TotalTime = item.TotalTime,
                    TimeDisplay = FormatTimeSpan(item.TotalTime),
                    IsWeb = true,
                    Icon = faviconMap.TryGetValue(domain, out var iconPath)
                        ? LoadFaviconSource(iconPath) ?? GlobeIcon.Source
                        : GlobeIcon.Source
                });
            }

            // 统一按总时长降序排序，应用和网页混合排列
            allItems = allItems.OrderByDescending(i => i.TotalTime).ToList();

            // 计算百分比（基于合并后的总时长）
            var combinedTotal = allItems.Sum(i => i.TotalTime.Ticks);
            foreach (var item in allItems)
            {
                item.Percentage = combinedTotal > 0 ? (double)item.TotalTime.Ticks / combinedTotal : 0;
            }

            foreach (var item in allItems)
            {
                Processes.Add(item);
            }
        }

        /// <summary>
        /// @Author: trae + deepseek-V4-pro
        /// @Date: 2026-06-23
        /// @Desc: 获取指定时间段内所有站点的 Domain → Icon 路径映射表。
        /// </summary>
        private async Task<Dictionary<string, string>> GetFaviconMapAsync(DateTime start, DateTime end)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                var sites = await _dataService.GetSitesInPeriodAsync(start, end);
                foreach (var (domain, _, icon) in sites)
                {
                    if (!string.IsNullOrWhiteSpace(icon) && !map.ContainsKey(domain))
                    {
                        map[domain] = icon;
                    }
                }
            }
            catch { /* 忽略 favicon 获取失败，使用默认地球图标 */ }
            return map;
        }

        /// <summary>
        /// @Author: trae + deepseek-V4-pro
        /// @Date: 2026-06-23
        /// @Desc: 从本地文件路径加载 favicon 图片。与 StatisticsViewModel 中同名方法
        ///         逻辑一致，用于活跃应用页显示网页图标。
        /// </summary>
        private static ImageSource? LoadFaviconSource(string iconPath)
        {
            if (string.IsNullOrWhiteSpace(iconPath))
                return null;

            try
            {
                if (iconPath.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                    return null;

                if (!File.Exists(iconPath))
                    return null;

                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(iconPath);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            }
            catch
            {
                return null;
            }
        }

        private static string FormatTimeSpan(TimeSpan ts)
        {
            if (ts.TotalHours >= 1)
                return $"{(int)ts.TotalHours}h {ts.Minutes}m";
            return $"{ts.Minutes}m {ts.Seconds}s";
        }
    }

    /*
     * @Author: trae + deepseek-V4-pro
     * @Date: 2026-06-23
     * @Desc: 活跃应用列表项模型。新增 Domain 属性用于标识网页来源域名。
     */
    public class ProcessStatsModel
    {
        public string ProcessName { get; set; } = string.Empty;
        public string Domain { get; set; } = string.Empty;
        public TimeSpan TotalTime { get; set; }
        public double Percentage { get; set; }
        public string TimeDisplay { get; set; } = string.Empty;
        public System.Windows.Media.ImageSource? Icon { get; set; }
        public bool IsWeb { get; set; }
    }
}
