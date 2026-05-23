using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace RikkaTracker.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _statusText = string.Empty;

        [ObservableProperty]
        private ObservableObject? _currentViewModel;

        public MainViewModel()
        {
            // 默认显示概览页
            NavigateToDashboard();
        }

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
    }
}
