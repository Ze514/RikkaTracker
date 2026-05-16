using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RikkaTracker.Models;
using RikkaTracker.Services;
using RikkaTracker.Core.Strategies;

namespace RikkaTracker.ViewModels
{
    public partial class FilterSettingsViewModel : ObservableObject
    {
        private readonly IConfigService _configService;
        private readonly IFilterEngine _filterEngine;

        public FilterSettingsViewModel(IConfigService configService, IFilterEngine filterEngine)
        {
            _configService = configService;
            _filterEngine = filterEngine;
            
            FilterRules = new ObservableCollection<FilterRule>(_configService.Config.FilterRules);
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
        private void ResetToDefaults()
        {
            var result = System.Windows.MessageBox.Show(
                (string)System.Windows.Application.Current.Resources["StrResetConfirmDesc"],
                (string)System.Windows.Application.Current.Resources["StrResetConfirmTitle"],
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Question);

            if (result == System.Windows.MessageBoxResult.Yes)
            {
                var defaults = new AppConfig();
                FilterRules.Clear();
                _configService.Config.FilterRules.Clear();
                
                foreach (var rule in defaults.FilterRules)
                {
                    FilterRules.Add(rule);
                    _configService.Config.FilterRules.Add(rule);
                }
                
                _configService.Save();
                _filterEngine.Reload();
            }
        }
    }
}
