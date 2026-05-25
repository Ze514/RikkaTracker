using System;
using System.Windows;
using System.Windows.Media;
using Wpf.Ui.Controls;

namespace RikkaTracker.Controls
{
    public partial class RikkaMessageBoxWindow : FluentWindow
    {
        public System.Windows.MessageBoxResult Result { get; private set; } = System.Windows.MessageBoxResult.None;

        public RikkaMessageBoxWindow(string message, string title, System.Windows.MessageBoxButton button, MessageBoxImage image)
        {
            InitializeComponent();
            
            Title = title;
            MessageText.Text = message;

            SetupIcon(image);
            SetupButtons(button);
        }

        private void SetupIcon(MessageBoxImage image)
        {
            StatusIcon.Visibility = Visibility.Visible;
            switch (image)
            {
                case MessageBoxImage.Information:
                    StatusIcon.Symbol = SymbolRegular.Info24;
                    StatusIcon.Foreground = (Brush)Application.Current.Resources["SystemAccentColorBrush"] ?? Brushes.DeepSkyBlue;
                    break;
                case MessageBoxImage.Question:
                    StatusIcon.Symbol = SymbolRegular.QuestionCircle24;
                    StatusIcon.Foreground = (Brush)Application.Current.Resources["SystemAccentColorBrush"] ?? Brushes.DeepSkyBlue;
                    break;
                case MessageBoxImage.Warning:
                    StatusIcon.Symbol = SymbolRegular.Alert24;
                    StatusIcon.Foreground = new SolidColorBrush(Color.FromRgb(230, 162, 44)); // #E6A23C
                    break;
                case MessageBoxImage.Error:
                    StatusIcon.Symbol = SymbolRegular.DismissCircle24;
                    StatusIcon.Foreground = new SolidColorBrush(Color.FromRgb(245, 108, 108)); // #F56C6C
                    break;
                default:
                    StatusIcon.Visibility = Visibility.Collapsed;
                    break;
            }
        }

        private void SetupButtons(System.Windows.MessageBoxButton button)
        {
            ButtonsPanel.Children.Clear();

            switch (button)
            {
                case System.Windows.MessageBoxButton.OK:
                    AddButton("确定", System.Windows.MessageBoxResult.OK, isDefault: true);
                    break;
                case System.Windows.MessageBoxButton.OKCancel:
                    AddButton("确定", System.Windows.MessageBoxResult.OK, isDefault: true);
                    AddButton("取消", System.Windows.MessageBoxResult.Cancel, isCancel: true);
                    break;
                case System.Windows.MessageBoxButton.YesNo:
                    AddButton("是", System.Windows.MessageBoxResult.Yes, isDefault: true);
                    AddButton("否", System.Windows.MessageBoxResult.No);
                    break;
                case System.Windows.MessageBoxButton.YesNoCancel:
                    AddButton("是", System.Windows.MessageBoxResult.Yes, isDefault: true);
                    AddButton("否", System.Windows.MessageBoxResult.No);
                    AddButton("取消", System.Windows.MessageBoxResult.Cancel, isCancel: true);
                    break;
            }
        }

        private void AddButton(string text, System.Windows.MessageBoxResult result, bool isDefault = false, bool isCancel = false)
        {
            var btn = new Wpf.Ui.Controls.Button
            {
                Content = text,
                MinWidth = 85,
                Height = 32,
                IsDefault = isDefault,
                IsCancel = isCancel
            };

            if (ButtonsPanel.Children.Count > 0)
            {
                btn.Margin = new Thickness(10, 0, 0, 0);
            }

            if (isDefault)
            {
                btn.Appearance = ControlAppearance.Primary;
                btn.Focus();
            }
            else
            {
                btn.Appearance = ControlAppearance.Secondary;
            }

            btn.Click += (s, e) =>
            {
                Result = result;
                try
                {
                    DialogResult = true;
                }
                catch
                {
                    // 防止在非 ShowDialog 状态下设置 DialogResult 抛错
                }
                Close();
            };

            ButtonsPanel.Children.Add(btn);
        }
    }
}
