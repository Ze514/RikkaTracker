using CommunityToolkit.Mvvm.ComponentModel;
using RikkaTracker.Services;

namespace RikkaTracker.ViewModels
{
    public partial class SettingsViewModel : ObservableObject
    {
        private readonly IThemeService _themeService;

        public SettingsViewModel(IThemeService themeService)
        {
            _themeService = themeService;
            _isDarkMode = _themeService.GetCurrentTheme() == "Dark";
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
