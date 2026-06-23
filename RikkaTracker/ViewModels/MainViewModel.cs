using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using RikkaTracker.Services;

namespace RikkaTracker.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly IConfigService _configService;

        /// <summary>
        /// @Author: trae + deepseek-3.1-pro
        /// @Date: 2026-06-22
        /// @Desc: 显示模式枚举列表，用于循环切换。顺序为：仅应用 → 仅网页 → 混合
        /// </summary>
        private readonly string[] _displayModes = { "AppOnly", "WebOnly", "Combined" };

        [ObservableProperty]
        private string _statusText = string.Empty;

        [ObservableProperty]
        private ObservableObject? _currentViewModel;

        [ObservableProperty]
        private string _displayMode = "Combined";

        public MainViewModel()
        {
            _configService = App.Current.ServiceProvider.GetRequiredService<IConfigService>();
            DisplayMode = _configService.Config.DisplayMode;
            NavigateToDashboard();
        }

        /// <summary>
        /// @Author: trae + deepseek-3.1-pro
        /// @Date: 2026-06-22
        /// @Desc: 获取当前显示模式对应的 Segoe Fluent Icons 图标字符。
        ///         AppOnly → \uECAA (AppIconDefault 应用图标)
        ///         WebOnly → \uE774 (Globe 地球图标)
        ///         Combined → \uF168 (GroupList 分组列表/混合图标)
        /// </summary>
        public string DisplayModeGlyph => DisplayMode switch
        {
            "AppOnly" => "\uECAA",
            "WebOnly" => "\uE774",
            "Combined" => "\uF168",
            _ => "\uF168"
        };

        partial void OnDisplayModeChanged(string value)
        {
            if (_configService.Config.DisplayMode != value)
            {
                _configService.Config.DisplayMode = value;
                _configService.Save();
            }
            // 通知图标属性更新，使紧凑模式下的图标按钮同步刷新
            OnPropertyChanged(nameof(DisplayModeGlyph));
        }

        /// <summary>
        /// @Author: trae + deepseek-3.1-pro
        /// @Date: 2026-06-22
        /// @Desc: 循环切换显示模式。在 AppOnly → WebOnly → Combined 间顺序轮转，
        ///         用于侧边导航栏收起时的图标点击切换。
        /// </summary>
        [RelayCommand]
        private void CycleDisplayMode()
        {
            var currentIndex = Array.IndexOf(_displayModes, DisplayMode);
            if (currentIndex < 0) currentIndex = 2; // 默认回退到 Combined
            var nextIndex = (currentIndex + 1) % _displayModes.Length;
            DisplayMode = _displayModes[nextIndex];
        }

        public IConfigService GetConfigService() => _configService;

        [RelayCommand]
        private void NavigateToDashboard()
        {
            CurrentViewModel = App.Current.ServiceProvider.GetRequiredService<DashboardViewModel>();
        }

         [RelayCommand]
        private void NavigateToSettings()
        {
            CurrentViewModel = App.Current.ServiceProvider.GetRequiredService<SettingsViewModel>();
        }

         [RelayCommand]
        private void NavigateToActivityList()
        {
            CurrentViewModel = App.Current.ServiceProvider.GetRequiredService<ActivityListViewModel>();
        }

        [RelayCommand]
        private void NavigateToStatistics()
        {
            CurrentViewModel = App.Current.ServiceProvider.GetRequiredService<StatisticsViewModel>();
        }

        [RelayCommand]
        private void NavigateToUsageStatistics()
        {
            CurrentViewModel = App.Current.ServiceProvider.GetRequiredService<UsageStatisticsViewModel>();
        }

        [RelayCommand]
        private void NavigateToFilterSettings()
        {
            CurrentViewModel = App.Current.ServiceProvider.GetRequiredService<FilterSettingsViewModel>();
        }

        [RelayCommand]
        public void NavigateToExport()
        {
            var exportVm = App.Current.ServiceProvider.GetRequiredService<ExportViewModel>();
            // 每次导航到导出页面，都重新加载一下当前时间区间的应用
            _ = exportVm.LoadAppsAsync();
            CurrentViewModel = exportVm;
        }
    }
}
