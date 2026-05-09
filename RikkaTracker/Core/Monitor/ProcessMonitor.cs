using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows.Threading;

namespace RikkaTracker.Core.Monitor
{
    public class ProcessEventArgs : EventArgs
    {
        public int ProcessId { get; set; }
        public string ProcessName { get; set; } = string.Empty;
    }

    public interface IProcessMonitor
    {
        event EventHandler<ProcessEventArgs> ProcessStarted;
        event EventHandler<ProcessEventArgs> ProcessExited;
        void Start();
        void Stop();
    }

    public class ProcessMonitor : IProcessMonitor
    {
        private readonly DispatcherTimer _scanTimer;
        // 维护 PID→进程名 映射，确保进程退出时能提供正确名称
        private Dictionary<int, string> _pidNames = new();

        public event EventHandler<ProcessEventArgs> ProcessStarted;
        public event EventHandler<ProcessEventArgs> ProcessExited;

        public ProcessMonitor()
        {
            _scanTimer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromSeconds(5)
            };
            _scanTimer.Tick += OnScanTimerTick;
        }

        public void Start()
        {
            _pidNames = GetCurrentPidNames();
            _scanTimer.Start();
        }

        public void Stop()
        {
            _scanTimer.Stop();
        }

        private void OnScanTimerTick(object? sender, EventArgs e)
        {
            var currentPidNames = GetCurrentPidNames();

            // 检测新启动的进程
            foreach (var kvp in currentPidNames)
            {
                if (!_pidNames.ContainsKey(kvp.Key))
                {
                    ProcessStarted?.Invoke(this, new ProcessEventArgs
                    {
                        ProcessId = kvp.Key,
                        ProcessName = kvp.Value
                    });
                }
            }

            // 检测已退出的进程（使用之前缓存的名称）
            foreach (var kvp in _pidNames)
            {
                if (!currentPidNames.ContainsKey(kvp.Key))
                {
                    ProcessExited?.Invoke(this, new ProcessEventArgs
                    {
                        ProcessId = kvp.Key,
                        ProcessName = kvp.Value
                    });
                }
            }

            _pidNames = currentPidNames;
        }

        private Dictionary<int, string> GetCurrentPidNames()
        {
            var result = new Dictionary<int, string>();
            try
            {
                foreach (var p in Process.GetProcesses())
                {
                    try
                    {
                        // 排除系统会话进程
                        if (p.SessionId != 0)
                        {
                            result[p.Id] = p.ProcessName;
                        }
                    }
                    catch { }
                    finally
                    {
                        p.Dispose();
                    }
                }
            }
            catch { }
            return result;
        }
    }
}
