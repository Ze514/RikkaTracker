using System;
using Wpf.Ui.Appearance;
using RikkaTracker.Services;

namespace RikkaTracker.Services
{
    public interface IThemeService
    {
        void ApplyTheme(string themeName);
        string GetCurrentTheme();
    }

    public class ThemeService : IThemeService
    {
        private readonly IConfigService _configService;

        public ThemeService(IConfigService configService)
        {
            _configService = configService;
        }

        public void ApplyTheme(string themeName)
        {
            var theme = themeName.Equals("Light", StringComparison.OrdinalIgnoreCase) 
                ? ApplicationTheme.Light 
                : ApplicationTheme.Dark;

            ApplicationThemeManager.Apply(theme);
            _configService.Config.Theme = themeName;
            _configService.Save();
        }

        public string GetCurrentTheme()
        {
            return _configService.Config.Theme;
        }
    }
}
