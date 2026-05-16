using System;
using System.IO;
using System.Diagnostics;

namespace RikkaTracker.Services
{
    public interface ILoggerService
    {
        void Info(string message);
        void Error(string message, Exception ex = null);
        void Warning(string message);
        string GetLogPath();
    }

    public class FileLoggerService : ILoggerService
    {
        private readonly string _logFilePath;
        private readonly object _lock = new object();

        public FileLoggerService()
        {
            string logDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "RikkaTracker",
                "logs");

            if (!Directory.Exists(logDir))
            {
                Directory.CreateDirectory(logDir);
            }

            _logFilePath = Path.Combine(logDir, $"app_{DateTime.Now:yyyyMMdd}.log");
            Info("=== Logger Initialized ===");
        }

        public string GetLogPath() => _logFilePath;

        public void Info(string message) => WriteLog("INFO", message);
        public void Warning(string message) => WriteLog("WARN", message);
        public void Error(string message, Exception ex = null)
        {
            string fullMessage = message;
            if (ex != null)
            {
                fullMessage += $"\nException: {ex.Message}\nStack: {ex.StackTrace}";
            }
            WriteLog("ERROR", fullMessage);
        }

        private void WriteLog(string level, string message)
        {
            string logLine = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {message}";
            
            // 输出到调试窗口
            Debug.WriteLine(logLine);

            // 异步写入文件，防止阻塞 UI
            Task.Run(() =>
            {
                lock (_lock)
                {
                    try
                    {
                        File.AppendAllText(_logFilePath, logLine + Environment.NewLine);
                    }
                    catch
                    {
                        // 忽略日志写入错误
                    }
                }
            });
        }
    }
}
