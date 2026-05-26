using System.Windows;

namespace RikkaTracker.Controls
{
    public static class RikkaMessageBox
    {
        public static MessageBoxResult Show(
            string message, 
            string title = "提示", 
            MessageBoxButton button = MessageBoxButton.OK, 
            MessageBoxImage image = MessageBoxImage.Information)
        {
            if (Application.Current.Dispatcher.CheckAccess())
            {
                return ShowInternal(message, title, button, image);
            }
            else
            {
                return Application.Current.Dispatcher.Invoke(() => ShowInternal(message, title, button, image));
            }
        }

        private static MessageBoxResult ShowInternal(string message, string title, MessageBoxButton button, MessageBoxImage image)
        {
            var win = new RikkaMessageBoxWindow(message, title, button, image);
            
            if (Application.Current.MainWindow != null && Application.Current.MainWindow.IsVisible)
            {
                win.Owner = Application.Current.MainWindow;
                win.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            }
            else
            {
                win.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }

            win.ShowDialog();
            return win.Result;
        }
    }
}
