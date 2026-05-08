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

        [ObservableProperty]
        private bool _isAutoStart = true;

        [ObservableProperty]
        private string _storagePath = "Default";

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
