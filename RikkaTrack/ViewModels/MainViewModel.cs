using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace RikkaTrack.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _statusText = "RikkaTrack 运行中...";

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
    }
}
