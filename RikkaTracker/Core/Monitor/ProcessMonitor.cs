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
        private HashSet<int> _lastPids = new();

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
            _lastPids = GetCurrentPids();
            _scanTimer.Start();
        }

        public void Stop()
        {
            _scanTimer.Stop();
        }

        private void OnScanTimerTick(object? sender, EventArgs e)
        {
            var currentPids = GetCurrentPids();

            // Detected started processes
            var started = currentPids.Except(_lastPids);
            foreach (var pid in started)
            {
                try
                {
                    using var proc = Process.GetProcessById(pid);
                    ProcessStarted?.Invoke(this, new ProcessEventArgs { ProcessId = pid, ProcessName = proc.ProcessName });
                }
                catch { }
            }

            // Detected exited processes
            var exited = _lastPids.Except(currentPids);
            foreach (var pid in exited)
            {
                ProcessExited?.Invoke(this, new ProcessEventArgs { ProcessId = pid, ProcessName = "Unknown" });
            }

            _lastPids = currentPids;
        }

        private HashSet<int> GetCurrentPids()
        {
            // Lightweight PID snapshot
            return Process.GetProcesses()
                .Where(p => p.SessionId != 0) // Exclude system/background sessions if possible
                .Select(p => p.Id)
                .ToHashSet();
        }
    }
}
