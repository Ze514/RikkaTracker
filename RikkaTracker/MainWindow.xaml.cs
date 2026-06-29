using System;
using System.Windows;
using System.Windows.Media;
using Microsoft.Extensions.DependencyInjection;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace RikkaTracker
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : FluentWindow
    {
        public MainWindow()
        {
            App.EnsureLiveChartsConfigured();
            InitializeComponent();

            if (!IsWindows11OrNewer)
            {
                // Windows 10 does not support Mica. Acrylic's legacy fallback is
                // unreliable across builds. Use no backdrop with a solid themed
                // background so the window never goes transparent/white.
                WindowBackdropType = WindowBackdropType.None;
                SetResourceReference(BackgroundProperty, "ApplicationBackgroundBrush");
            }

            // Seed WPF-UI control dictionaries into this window.
            ApplicationThemeManager.Apply(this);

            // When the theme changes globally, re-sync this window's local
            // dictionaries so controls follow the new theme immediately.
            ApplicationThemeManager.Changed += OnThemeChanged;

            DataContext = App.Current.ServiceProvider.GetService<ViewModels.MainViewModel>();
        }

        private void OnThemeChanged(ApplicationTheme currentTheme, Color systemAccent)
        {
            ApplicationThemeManager.Apply(this);
        }

        protected override void OnClosed(EventArgs e)
        {
            ApplicationThemeManager.Changed -= OnThemeChanged;
            base.OnClosed(e);
        }

        private static bool IsWindows11OrNewer => Environment.OSVersion.Version.Build >= 22000;
    }
}