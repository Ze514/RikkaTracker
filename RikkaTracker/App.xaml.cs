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
            
            // Create Context Menu
            var contextMenu = new ContextMenu();
            var showItem = new MenuItem { Header = "显示" };
            showItem.Click += (s, ex) => ShowMainWindow();
            var exitItem = new MenuItem { Header = "退出" };
            exitItem.Click += (s, ex) => ExitApplication();
            
            contextMenu.Items.Add(showItem);
            contextMenu.Items.Add(new Separator());
            contextMenu.Items.Add(exitItem);
            _notifyIcon.ContextMenu = contextMenu;
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
                System.Diagnostics.Debug.WriteLine($"[Activity] {args.ProcessName} ({args.ProcessId}) -> {args.NewStatus} | {args.WindowTitle}");
                logStore.RecordTransition(args.ProcessName, args.WindowTitle, args.NewStatus, args.Timestamp);
            };
            activityTracker.Start();

            var processMonitor = ServiceProvider.GetRequiredService<IProcessMonitor>();
            processMonitor.ProcessStarted += (s, args) => System.Diagnostics.Debug.WriteLine($"[Process] Started: {args.ProcessName} ({args.ProcessId})");
            processMonitor.ProcessExited += (s, args) => System.Diagnostics.Debug.WriteLine($"[Process] Exited: PID {args.ProcessId}");
            processMonitor.Start();

            // Apply saved theme
            var themeService = ServiceProvider.GetRequiredService<IThemeService>();
            themeService.ApplyTheme(themeService.GetCurrentTheme());

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
            services.AddSingleton<IAppActivityTracker, AppActivityTracker>();
            services.AddSingleton<IProcessMonitor, ProcessMonitor>();
            
            // ViewModels
            services.AddTransient<MainViewModel>(); // Transient so it's recreated
            services.AddTransient<DashboardViewModel>();
            services.AddTransient<SettingsViewModel>();
            services.AddTransient<ActivityListViewModel>();
            services.AddTransient<StatisticsViewModel>();

            return services.BuildServiceProvider();
        }

        public static new App Current => (App)Application.Current;
    }
}
