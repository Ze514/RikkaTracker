using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using RikkaTracker.ViewModels;
using RikkaTracker.Services;
using Hardcodet.Wpf.TaskbarNotification;
using System.Windows.Controls;
using RikkaTracker.Core.Monitor;
using RikkaTracker.Core.Data;

namespace RikkaTracker
{
    public partial class App : Application
    {
        public IServiceProvider ServiceProvider { get; private set; }
        private TaskbarIcon? _notifyIcon;

        public App()
        {
            ServiceProvider = ConfigureServices();
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Initialize Tray Icon
            _notifyIcon = new TaskbarIcon();
            // Placeholder Icon (Pink Circle) to ensure visibility
            var drawing = new System.Windows.Media.GeometryDrawing(
                System.Windows.Media.Brushes.HotPink,
                null,
                new System.Windows.Media.EllipseGeometry(new Point(16, 16), 12, 12)
            );
            _notifyIcon.IconSource = new System.Windows.Media.DrawingImage(drawing);
            _notifyIcon.ToolTipText = "RikkaTracker";
            
            _notifyIcon.DoubleClickCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(ShowMainWindow);

            // Handle startup parameters
            bool startMinimized = true; // Default to tray
            foreach (var arg in e.Args)
            {
                if (arg.Equals("/show", StringComparison.OrdinalIgnoreCase)) startMinimized = false;
            }

            // Start Monitoring Services
            var activityTracker = ServiceProvider.GetRequiredService<IAppActivityTracker>();
            var logStore = ServiceProvider.GetRequiredService<IActivityLogStore>();

            activityTracker.AppActivityChanged += (s, args) =>
            {
                System.Diagnostics.Debug.WriteLine($"[Activity] {args.ProcessName} ({args.Alias}) ({args.ProcessId}) -> {args.NewStatus} | {args.WindowTitle}");
            };
            activityTracker.Start();

            var processMonitor = ServiceProvider.GetRequiredService<IProcessMonitor>();
            processMonitor.ProcessStarted += (s, args) => System.Diagnostics.Debug.WriteLine($"[Process] Started: {args.ProcessName} ({args.ProcessId})");
            processMonitor.ProcessExited += (s, args) =>
            {
                System.Diagnostics.Debug.WriteLine($"[Process] Exited: {args.ProcessName} ({args.ProcessId})");
                // 进程退出时闭合其开放segment
                logStore.CloseProcess(args.ProcessName, DateTime.Now);
            };
            processMonitor.Start();

            // Apply saved theme
            var themeService = ServiceProvider.GetRequiredService<IThemeService>();
            themeService.ApplyTheme(themeService.GetCurrentTheme());

            // Initialize Localization
            var localizationService = ServiceProvider.GetRequiredService<ILocalizationService>();
            var configService = ServiceProvider.GetRequiredService<IConfigService>();
            localizationService.Initialize(configService.Config.Language);

            // Update Tray Menu with localized headers
            UpdateTrayMenu();

            if (!startMinimized)
            {
                ShowMainWindow();
            }
        }

        public void ShowMainWindow()
        {
            if (MainWindow != null)
            {
                MainWindow.Activate();
                if (MainWindow.WindowState == WindowState.Minimized)
                    MainWindow.WindowState = WindowState.Normal;
                return;
            }

            var mainWindow = new MainWindow
            {
                DataContext = ServiceProvider.GetRequiredService<MainViewModel>()
            };

            mainWindow.Closed += (s, e) =>
            {
                // Task 1.3: Destroy View/ViewModel and collect GC
                MainWindow = null;
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            };

            MainWindow = mainWindow;
            mainWindow.Show();
        }

        private void UpdateTrayMenu()
        {
            if (_notifyIcon == null) return;
            
            var contextMenu = new ContextMenu();
            var showItem = new MenuItem { Header = Application.Current.Resources["StrScale"] != null ? (string)Application.Current.Resources["StrDashboard"] : "Show" }; 
            // Wait, I should use specific keys for tray
            
            // Re-using keys for now or adding new ones
            showItem.Header = GetResourceString("StrDashboard", "Show");
            showItem.Click += (s, ex) => ShowMainWindow();
            
            var exitItem = new MenuItem { Header = GetResourceString("StrExportData", "Exit") }; // Just placeholder
            // Let's add specific tray keys to xaml later, but for now:
            exitItem.Header = CurrentLanguage == "zh-CN" ? "退出" : "Exit";
            exitItem.Click += (s, ex) => ExitApplication();

            contextMenu.Items.Add(showItem);
            contextMenu.Items.Add(new Separator());
            contextMenu.Items.Add(exitItem);
            _notifyIcon.ContextMenu = contextMenu;
        }

        private string GetResourceString(string key, string fallback)
        {
            return Application.Current.Resources[key] as string ?? fallback;
        }

        private string CurrentLanguage => ServiceProvider?.GetService<ILocalizationService>()?.CurrentLanguage ?? "zh-CN";

        private void ExitApplication()
        {
            if (ServiceProvider != null)
            {
                var logStore = ServiceProvider.GetService<IActivityLogStore>() as IDisposable;
                logStore?.Dispose();
            }
            _notifyIcon?.Dispose();
            Shutdown();
        }

        private IServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection();

            // Services
            services.AddSingleton<IConfigService, ConfigService>();
            services.AddSingleton<IThemeService, ThemeService>();
            services.AddSingleton<SqliteDbContext>();
            services.AddSingleton<IActivityLogStore, SqliteLogStore>();
            services.AddSingleton<IDataService, SqliteDataService>();
            services.AddSingleton<RikkaTracker.Core.Strategies.IFilterEngine, RikkaTracker.Core.Strategies.FilterEngine>();
            services.AddSingleton<IAppActivityTracker, AppActivityTracker>();
            services.AddSingleton<IProcessMonitor, ProcessMonitor>();
            services.AddSingleton<IIconService, IconService>();
            services.AddSingleton<ILocalizationService, LocalizationService>();
            
            // ViewModels
            services.AddTransient<MainViewModel>(); // Transient so it's recreated
            services.AddTransient<DashboardViewModel>();
            services.AddTransient<SettingsViewModel>();
            services.AddTransient<ActivityListViewModel>();
            services.AddTransient<StatisticsViewModel>();
            services.AddTransient<FilterSettingsViewModel>();

            return services.BuildServiceProvider();
        }

        public static new App Current => (App)Application.Current;
    }
}
