using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using RikkaTracker.ViewModels;
using RikkaTracker.Services;

namespace RikkaTracker
{
    public partial class App : Application
    {
        public IServiceProvider ServiceProvider { get; private set; }

        public App()
        {
            ServiceProvider = ConfigureServices();
        }

        private IServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection();

            // ViewModels
            services.AddSingleton<MainViewModel>();
            services.AddTransient<DashboardViewModel>();
            services.AddTransient<SettingsViewModel>();

            // Services
            services.AddSingleton<IDataService, JsonDataService>();
            // services.AddSingleton<IAppMonitorService, AppMonitorService>();
            // services.AddSingleton<IWebMonitorService, WebMonitorService>();

            return services.BuildServiceProvider();
        }

        public static new App Current => (App)Application.Current;
    }
}
