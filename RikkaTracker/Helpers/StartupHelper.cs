using Microsoft.Win32;
using System;
using System.Diagnostics;

namespace RikkaTracker.Helpers
{
    /// <summary>
    /// 开机自启动辅助类 (仅适用于 Windows 平台)
    /// </summary>
    public static class StartupHelper
    {
        private const string AppName = "RikkaTracker";

        /// <summary>
        /// 设置开机自启动
        /// </summary>
        /// <param name="enable">是否启用自启动</param>
        public static void SetStartup(bool enable)
        {
            // 获取当前可执行文件路径
            string? appPath = Environment.ProcessPath;
            if (string.IsNullOrEmpty(appPath))
            {
                appPath = Process.GetCurrentProcess().MainModule?.FileName;
            }

            if (string.IsNullOrEmpty(appPath))
            {
                throw new InvalidOperationException("无法获取当前应用程序的可执行文件路径。");
            }

            // 打开当前用户的 Run 注册表项
            using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
            {
                if (key != null)
                {
                    if (enable)
                    {
                        // 带有 /silent 参数，以便在系统启动时静默最小化到系统托盘
                        key.SetValue(AppName, $"\"{appPath}\" /silent");
                    }
                    else
                    {
                        key.DeleteValue(AppName, false);
                    }
                }
                else
                {
                    throw new InvalidOperationException("无法打开注册表项 HKEY_CURRENT_USER\\Software\\Microsoft\\Windows\\CurrentVersion\\Run。");
                }
            }
        }

        /// <summary>
        /// 检查当前注册表中是否已经注册了自启动项
        /// </summary>
        public static bool IsStartupEnabled()
        {
            using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", false))
            {
                if (key != null)
                {
                    object? value = key.GetValue(AppName);
                    return value != null;
                }
            }
            return false;
        }
    }
}
