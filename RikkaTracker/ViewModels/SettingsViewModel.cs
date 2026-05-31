using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RikkaTracker.Models;
using RikkaTracker.Services;
using RikkaTracker.Core.Strategies;
using RikkaTracker.Controls;

namespace RikkaTracker.ViewModels
{
    public partial class SettingsViewModel : ObservableObject
    {
        private readonly IThemeService _themeService;
        private readonly IConfigService _configService;
        private readonly ILocalizationService _localizationService;
        private readonly IUpdateService _updateService;
        private readonly ILoggerService _logger;

        public SettingsViewModel(
            IThemeService themeService, 
            IConfigService configService, 
            ILocalizationService localizationService, 
            IUpdateService updateService,
            ILoggerService logger)
        {
            _themeService = themeService;
            _configService = configService;
            _localizationService = localizationService;
            _updateService = updateService;
            _logger = logger;
            
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

        [ObservableProperty]
        private double _updateProgress;

        [RelayCommand]
        private async Task CheckForUpdate()
        {
            _logger.Info("User clicked 'Check for Updates'.");
            UpdateStatus = (string)System.Windows.Application.Current.Resources["StrCheckUpdate"] + "...";
            
            try
            {
                var result = await _updateService.CheckForUpdatesAsync();

                switch (result.Status)
                {
                    case UpdateCheckStatus.UpdateAvailable:
                        _logger.Info($"Update found: {result.LatestVersion}");
                        HandleUpdateFound(result);
                        break;

                    case UpdateCheckStatus.NoUpdate:
                        _logger.Info("No update available.");
                        RikkaMessageBox.Show(
                            (string)System.Windows.Application.Current.Resources["StrAlreadyLatest"], 
                            (string)System.Windows.Application.Current.Resources["StrUpdate"]);
                        UpdateStatus = string.Empty;
                        break;

                    case UpdateCheckStatus.NetworkError:
                        _logger.Warning($"Update check failed due to network error: {result.ErrorMessage}");
                        RikkaMessageBox.Show(
                            $"Network Error: {result.ErrorMessage}\n\nPlease check your internet connection or proxy settings.",
                            "Update Check Failed",
                            System.Windows.MessageBoxButton.OK,
                            System.Windows.MessageBoxImage.Warning);
                        UpdateStatus = string.Empty;
                        break;

                    case UpdateCheckStatus.AssetMissing:
                        _logger.Warning($"New version {result.LatestVersion} found, but no matching asset for this installation type.");
                        RikkaMessageBox.Show(
                            $"New version {result.LatestVersion} is available, but the download package for your installation type was not found on the server.\n\nPlease visit GitHub releases manually.",
                            "Asset Missing",
                            System.Windows.MessageBoxButton.OK,
                            System.Windows.MessageBoxImage.Warning);
                        UpdateStatus = string.Empty;
                        break;

                    case UpdateCheckStatus.InternalError:
                    default:
                        _logger.Error($"Internal error during update check: {result.ErrorMessage}");
                        RikkaMessageBox.Show(
                            $"An internal error occurred: {result.ErrorMessage}",
                            "Error",
                            System.Windows.MessageBoxButton.OK,
                            System.Windows.MessageBoxImage.Error);
                        UpdateStatus = string.Empty;
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.Error("Unhandled exception in CheckForUpdate command.", ex);
                UpdateStatus = string.Empty;
            }
        }

        private void HandleUpdateFound(UpdateCheckResult result)
        {
            var msg = (string)System.Windows.Application.Current.Resources["StrUpdateAvailable"];
            var choice = RikkaMessageBox.Show(
                $"{msg}\n\n{(_localizationService.CurrentLanguage == "zh-CN" ? "版本" : "Version")}: {result.LatestVersion}\n\n{result.ReleaseNotes}",
                (string)System.Windows.Application.Current.Resources["StrUpdate"],
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Information);

            if (choice == System.Windows.MessageBoxResult.Yes)
            {
                _logger.Info("User accepted update. Starting download...");
                IsUpdating = true;
                
                // 执行异步下载安装
                _ = RunDownloadAndInstall(result);
            }
            else
            {
                _logger.Info("User declined update.");
                UpdateStatus = string.Empty;
            }
        }

        private async Task RunDownloadAndInstall(UpdateCheckResult result)
        {
            try
            {
                await _updateService.DownloadAndInstallAsync(result, (progress, status) =>
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        UpdateStatus = status;
                        UpdateProgress = progress * 100;
                    });
                });
            }
            catch (Exception ex)
            {
                _logger.Error("Download and Install failed.", ex);
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    RikkaMessageBox.Show(
                        $"Update download failed: {ex.Message}\n\nLogs: {_logger.GetLogPath()}", 
                        "Error", 
                        System.Windows.MessageBoxButton.OK, 
                        System.Windows.MessageBoxImage.Error);
                    IsUpdating = false;
                    UpdateStatus = string.Empty;
                });
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
                FileName = "tracker.db"
            };

            if (dialog.ShowDialog() == true)
            {
                string oldPath = _configService.Config.DataStoragePath;
                string newPath = System.IO.Path.GetDirectoryName(dialog.FileName);
                
                if (oldPath == newPath) return;

                try
                {
                    if (!System.IO.Directory.Exists(newPath))
                    {
                        System.IO.Directory.CreateDirectory(newPath);
                    }

                    string oldFile = System.IO.Path.Combine(oldPath, "tracker.db");
                    string newFile = System.IO.Path.Combine(newPath, "tracker.db");

                    if (System.IO.File.Exists(oldFile))
                    {
                        System.IO.File.Copy(oldFile, newFile, true);
                    }

                    StoragePath = newPath;
                    _configService.Config.DataStoragePath = StoragePath;
                    _configService.Save();

                    RikkaMessageBox.Show(
                        "数据已迁移。为了确保所有服务都使用新路径，请重启应用程序。",
                        "更改成功",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    _logger.Error("Failed to migrate data path.", ex);
                    RikkaMessageBox.Show(
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

        [RelayCommand]
        private void OpenLogFolder()
        {
            try
            {
                string logPath = _logger.GetLogPath();
                string? logDir = System.IO.Path.GetDirectoryName(logPath);

                if (!string.IsNullOrEmpty(logDir) && System.IO.Directory.Exists(logDir))
                {
                    _logger.Info($"User opening log folder: {logDir}");
                    System.Diagnostics.Process.Start("explorer.exe", logDir);
                }
                else
                {
                    RikkaMessageBox.Show("日志目录尚未创建或不存在。", "提示", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                _logger.Error("Failed to open log folder.", ex);
            }
        }

        [RelayCommand]
        private void OpenDiagnosticConsole()
        {
            App.Current.ShowDiagnosticWindow();
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
