using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using RikkaTracker.ViewModels;
using RikkaTracker.Services;
using Hardcodet.Wpf.TaskbarNotification;
using System.Windows.Controls;
using RikkaTracker.Core.Monitor;
using RikkaTracker.Core.Data;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;

namespace RikkaTracker
{
    public partial class App : Application
    {
        public IServiceProvider ServiceProvider { get; private set; }
        private TaskbarIcon? _notifyIcon;
        private ILoggerService? _logger;
        private static Mutex? _appMutex;

        public App()
        {
            // 确保单实例运行
            _appMutex = new Mutex(true, "Global\\RikkaTracker_Mutex_Unique_ID", out bool createdNew);
            if (!createdNew)
            {
                MessageBox.Show("RikkaTracker 已经在运行中。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                Application.Current.Shutdown();
                return;
            }

            ServiceProvider = ConfigureServices();
            _logger = ServiceProvider.GetRequiredService<ILoggerService>();
            
            // 最后的防线：全局异常捕获
            this.DispatcherUnhandledException += App_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        }

        private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            _logger?.Error("FATAL: Unhandled Dispatcher Exception", e.Exception);
            MessageBox.Show("应用遇到了严重的 UI 线程错误，即将记录并尝试关闭。详情请见日志。", "致命错误", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true; // 防止立即崩溃，尝试优雅退出
            ExitApplication();
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            _logger?.Error($"FATAL: Unhandled Domain Exception. IsTerminating: {e.IsTerminating}", e.ExceptionObject as Exception);
            if (!e.IsTerminating)
            {
                MessageBox.Show("应用遇到了严重的非 UI 线程错误，详情请见日志。", "致命错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            _logger?.Info("--- RikkaTracker Startup ---");

            LiveCharts.Configure(config => 
                config
                    .AddDefaultMappers()
                    .AddSkiaSharp()
                    .AddLightTheme());

            // Initialize Tray Icon
            _notifyIcon = new TaskbarIcon();
            var drawing = new System.Windows.Media.GeometryDrawing(
                System.Windows.Media.Brushes.HotPink,
                null,
                new System.Windows.Media.EllipseGeometry(new Point(16, 16), 12, 12)
            );
            _notifyIcon.IconSource = new System.Windows.Media.DrawingImage(drawing);
            _notifyIcon.ToolTipText = "RikkaTracker";
            _notifyIcon.DoubleClickCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(ShowMainWindow);

            bool startMinimized = true;
            foreach (var arg in e.Args)
            {
                if (arg.Equals("/show", StringComparison.OrdinalIgnoreCase)) startMinimized = false;
            }

            try 
            {
                InitializeCoreServices();
            }
            catch (Exception ex)
            {
                _logger?.Error("Failed to initialize core services during startup.", ex);
                MessageBox.Show("启动核心服务失败，应用可能无法正常工作。请检查日志。", "初始化失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            if (!startMinimized)
            {
                ShowMainWindow();
            }
        }

        private void InitializeCoreServices()
        {
            var activityTracker = ServiceProvider.GetRequiredService<IAppActivityTracker>();
            var logStore = ServiceProvider.GetRequiredService<IActivityLogStore>();

            activityTracker.AppActivityChanged += (s, args) =>
            {
                // 日志记录级别调整为 Info 以便追踪，但在生产环境下可以根据需要调低
                _logger?.Info($"[Activity] {args.ProcessName} -> {args.NewStatus} | {args.WindowTitle}");
            };
            activityTracker.Start();

            var processMonitor = ServiceProvider.GetRequiredService<IProcessMonitor>();
            processMonitor.ProcessStarted += (s, args) => _logger?.Info($"[Process] Started: {args.ProcessName}");
            processMonitor.ProcessExited += (s, args) =>
            {
                _logger?.Info($"[Process] Exited: {args.ProcessName}");
                logStore.CloseProcess(args.ProcessName, DateTime.Now);
            };
            processMonitor.Start();

            var themeService = ServiceProvider.GetRequiredService<IThemeService>();
            themeService.ApplyTheme(themeService.GetCurrentTheme());

            var localizationService = ServiceProvider.GetRequiredService<ILocalizationService>();
            var configService = ServiceProvider.GetRequiredService<IConfigService>();
            localizationService.Initialize(configService.Config.Language);

            UpdateTrayMenu();
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
            var showItem = new MenuItem { Header = GetResourceString("StrDashboard", "Show") };
            showItem.Click += (s, ex) => ShowMainWindow();
            var exitItem = new MenuItem { Header = CurrentLanguage == "zh-CN" ? "退出" : "Exit" };
            exitItem.Click += (s, ex) => ExitApplication();

            contextMenu.Items.Add(showItem);
            contextMenu.Items.Add(new Separator());
            contextMenu.Items.Add(exitItem);
            _notifyIcon.ContextMenu = contextMenu;
        }

        private string GetResourceString(string key, string fallback) => Application.Current.Resources[key] as string ?? fallback;
        private string CurrentLanguage => ServiceProvider?.GetService<ILocalizationService>()?.CurrentLanguage ?? "zh-CN";

        private void ExitApplication()
        {
            _logger?.Info("Shutting down application...");
            try 
            {
                if (ServiceProvider != null)
                {
                    var logStore = ServiceProvider.GetService<IActivityLogStore>() as IDisposable;
                    logStore?.Dispose();
                }
                _notifyIcon?.Dispose();
                _appMutex?.ReleaseMutex();
                _appMutex?.Dispose();
            }
            catch (Exception ex)
            {
                _logger?.Error("Error during shutdown cleanup.", ex);
            }
            Shutdown();
        }

        private IServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection();
            services.AddSingleton<ILoggerService, FileLoggerService>();
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
            services.AddSingleton<IUpdateService, UpdateService>();
            
            services.AddTransient<MainViewModel>();
            services.AddTransient<DashboardViewModel>();
            services.AddTransient<SettingsViewModel>();
            services.AddTransient<ActivityListViewModel>();
            services.AddTransient<StatisticsViewModel>();
            services.AddTransient<FilterSettingsViewModel>();
            services.AddTransient<UsageStatisticsViewModel>();

            return services.BuildServiceProvider();
        }

        public static new App Current => (App)Application.Current;
    }
}
