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
        private readonly ILocalizationService _localizationService;
        private readonly IUpdateService _updateService;

        public SettingsViewModel(IThemeService themeService, IConfigService configService, ILocalizationService localizationService, IUpdateService updateService)
        {
            _themeService = themeService;
            _configService = configService;
            _localizationService = localizationService;
            _updateService = updateService;
            
            _isDarkMode = _themeService.GetCurrentTheme() == "Dark";
            _idleTimeoutMinutes = _configService.Config.IdleTimeoutMinutes;
            _storagePath = _configService.Config.DataStoragePath;
            _timelineZoomMode = _configService.Config.TimelineZoomMode;
            _language = _configService.Config.Language;
            _currentVersion = _updateService.GetCurrentVersion();
        }

        [ObservableProperty]
        private string _currentVersion;

        [ObservableProperty]
        private string _updateStatus = string.Empty;

        [ObservableProperty]
        private bool _isUpdating;

        [RelayCommand]
        private async Task CheckForUpdate()
        {
            UpdateStatus = (string)System.Windows.Application.Current.Resources["StrCheckUpdate"] + "...";
            var result = await _updateService.CheckForUpdatesAsync();

            if (result.HasUpdate)
            {
                var msg = (string)System.Windows.Application.Current.Resources["StrUpdateAvailable"];
                var choice = System.Windows.MessageBox.Show(
                    $"{msg}\n\n{(_localizationService.CurrentLanguage == "zh-CN" ? "版本" : "Version")}: {result.LatestVersion}\n\n{result.ReleaseNotes}",
                    (string)System.Windows.Application.Current.Resources["StrUpdate"],
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Information);

                if (choice == System.Windows.MessageBoxResult.Yes)
                {
                    IsUpdating = true;
                    UpdateStatus = (string)System.Windows.Application.Current.Resources["StrUpdating"];
                    try
                    {
                        await _updateService.DownloadAndInstallAsync(result, p =>
                        {
                            UpdateStatus = $"{(string)System.Windows.Application.Current.Resources["StrUpdating"]} ({p:P0})";
                        });
                    }
                    catch (Exception ex)
                    {
                        System.Windows.MessageBox.Show($"Update failed: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                        IsUpdating = false;
                        UpdateStatus = string.Empty;
                    }
                }
                else
                {
                    UpdateStatus = string.Empty;
                }
            }
            else
            {
                System.Windows.MessageBox.Show((string)System.Windows.Application.Current.Resources["StrAlreadyLatest"], (string)System.Windows.Application.Current.Resources["StrUpdate"]);
                UpdateStatus = string.Empty;
            }
        }

        [ObservableProperty]
        private int _idleTimeoutMinutes;

        partial void OnIdleTimeoutMinutesChanged(int value)
        {
            _configService.Config.IdleTimeoutMinutes = value;
            _configService.Save();
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

        [ObservableProperty]
        private string _language;

        partial void OnLanguageChanged(string value)
        {
            _configService.Config.Language = value;
            _configService.Save();
            
            if (value == "Auto")
            {
                _localizationService.Initialize("Auto");
            }
            else
            {
                _localizationService.SetLanguage(value);
            }
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
