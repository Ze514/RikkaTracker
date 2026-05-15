using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RikkaTracker.Models;
using RikkaTracker.Services;
using RikkaTracker.Core.Strategies;

namespace RikkaTracker.ViewModels
{
    public partial class SettingsViewModel : ObservableObject
    {
        private readonly IThemeService _themeService;
        private readonly IConfigService _configService;
        private readonly IFilterEngine _filterEngine;

        public SettingsViewModel(IThemeService themeService, IConfigService configService, IFilterEngine filterEngine)
        {
            _themeService = themeService;
            _configService = configService;
            _filterEngine = filterEngine;
            
            _isDarkMode = _themeService.GetCurrentTheme() == "Dark";
            _idleTimeoutMinutes = _configService.Config.IdleTimeoutMinutes;
            _storagePath = _configService.Config.DataStoragePath;
            _timelineZoomMode = _configService.Config.TimelineZoomMode;
            
            FilterRules = new ObservableCollection<FilterRule>(_configService.Config.FilterRules);
        }

        [ObservableProperty]
        private int _idleTimeoutMinutes;

        partial void OnIdleTimeoutMinutesChanged(int value)
        {
            _configService.Config.IdleTimeoutMinutes = value;
            _configService.Save();
        }

        public ObservableCollection<FilterRule> FilterRules { get; }

        [RelayCommand]
        private void AddRule()
        {
            var rule = new FilterRule { ProcessPattern = "new_process" };
            FilterRules.Add(rule);
            _configService.Config.FilterRules.Add(rule);
            _configService.Save();
            _filterEngine.Reload();
        }

        [RelayCommand]
        private void RemoveRule(FilterRule rule)
        {
            if (rule != null)
            {
                FilterRules.Remove(rule);
                _configService.Config.FilterRules.Remove(rule);
                _configService.Save();
                _filterEngine.Reload();
            }
        }

        [RelayCommand]
        private void SaveRules()
        {
            _configService.Save();
            _filterEngine.Reload();
        }

        [RelayCommand]
        private void ChangeStoragePath()
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "选择数据库存储位置",
                Filter = "SQLite Database (*.db)|*.db",
                FileName = "tracker.db" // 统一文件名
            };

            if (dialog.ShowDialog() == true)
            {
                string oldPath = _configService.Config.DataStoragePath;
                string newPath = System.IO.Path.GetDirectoryName(dialog.FileName);
                
                if (oldPath == newPath) return;

                try
                {
                    // 1. 确保新目录存在
                    if (!System.IO.Directory.Exists(newPath))
                    {
                        System.IO.Directory.CreateDirectory(newPath);
                    }

                    // 2. 迁移文件 (tracker.db)
                    string oldFile = System.IO.Path.Combine(oldPath, "tracker.db");
                    string newFile = System.IO.Path.Combine(newPath, "tracker.db");

                    if (System.IO.File.Exists(oldFile))
                    {
                        // 如果新位置已存在同名文件，先备份或覆盖（这里选择覆盖以完成迁移）
                        System.IO.File.Copy(oldFile, newFile, true);
                    }

                    // 3. 更新配置
                    StoragePath = newPath;
                    _configService.Config.DataStoragePath = StoragePath;
                    _configService.Save();

                    // 提示用户重启生效（因为数据库连接通常是单例且已打开）
                    System.Windows.MessageBox.Show(
                        "数据已迁移。为了确保所有服务都使用新路径，请重启应用程序。",
                        "更改成功",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Information);
                }
                catch (System.Exception ex)
                {
                    System.Windows.MessageBox.Show(
                        $"迁移数据失败: {ex.Message}",
                        "错误",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Error);
                }
            }
        }

        [ObservableProperty]
        private bool _isAutoStart = true;

        [ObservableProperty]
        private string _storagePath;

        [ObservableProperty]
        private string _timelineZoomMode;

        partial void OnTimelineZoomModeChanged(string value)
        {
            _configService.Config.TimelineZoomMode = value;
            _configService.Save();
        }

        private bool _isDarkMode;
        public bool IsDarkMode
        {
            get => _isDarkMode;
            set
            {
                if (SetProperty(ref _isDarkMode, value))
                {
                    _themeService.ApplyTheme(value ? "Dark" : "Light");
                }
            }
        }
    }
}
