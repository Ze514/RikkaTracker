using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using RikkaTracker.Services;

namespace RikkaTracker.Core.Monitor
{
    public enum ActivityStatus
    {
        Background = 0,
        ForegroundInactive = 1,
        ForegroundActive = 2
    }

    public class AppActivityChangedEventArgs : EventArgs
    {
        public string ProcessName { get; set; } = string.Empty;
        public string WindowTitle { get; set; } = string.Empty;
        public int ProcessId { get; set; }
        public ActivityStatus OldStatus { get; set; }
        public ActivityStatus NewStatus { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public interface IAppActivityTracker
    {
        event EventHandler<AppActivityChangedEventArgs> AppActivityChanged;
        void Start();
        void Stop();
    }

    public class AppActivityTracker : IAppActivityTracker
    {
        private readonly IConfigService _configService;
        private readonly Strategies.IFilterEngine _filterEngine;
        private readonly DispatcherTimer _idleTimer;
        private Win32Api.WinEventDelegate? _winEventDelegate;
        private IntPtr _hHook;

        // Task 2.2: Track state per process ID
        private class ProcessState
        {
            public string ProcessName { get; set; } = string.Empty;
            public string LastTitle { get; set; } = string.Empty;
            public ActivityStatus LastStatus { get; set; } = ActivityStatus.Background;
        }

        private readonly Dictionary<int, ProcessState> _processStates = new();
        private int _currentForegroundPid;
        private IntPtr _currentForegroundHwnd;

        public event EventHandler<AppActivityChangedEventArgs> AppActivityChanged;

        public AppActivityTracker(IConfigService configService, Strategies.IFilterEngine filterEngine)
        {
            _configService = configService;
            _filterEngine = filterEngine;
            _idleTimer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _idleTimer.Tick += OnIdleTimerTick;
        }

        public void Start()
        {
            _winEventDelegate = new Win32Api.WinEventDelegate(WinEventProc);
            _hHook = Win32Api.SetWinEventHook(
                Win32Api.EVENT_SYSTEM_FOREGROUND,
                Win32Api.EVENT_SYSTEM_MINIMIZEEND,
                IntPtr.Zero,
                _winEventDelegate,
                0, 0,
                Win32Api.WINEVENT_OUTOFCONTEXT | Win32Api.WINEVENT_SKIPOWNPROCESS);

            UpdateForegroundStatus();
        }

        public void Stop()
        {
            if (_hHook != IntPtr.Zero)
            {
                Win32Api.UnhookWinEvent(_hHook);
                _hHook = IntPtr.Zero;
            }
            _idleTimer.Stop();
        }

        private void WinEventProc(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            // 在独立系统线程中仅提取非托管信息
            uint pid;
            Win32Api.GetWindowThreadProcessId(hwnd, out pid);
            string title = Win32Api.GetWindowTitle(hwnd);
            bool isIconic = Win32Api.IsIconic(hwnd);
            bool isVisible = Win32Api.IsWindowVisible(hwnd);

            // 强制调度到 UI 线程执行状态机更新，消除竞态
            System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                UpdateForegroundStatusDirect(hwnd, (int)pid, title, isIconic, isVisible);
            });
        }

        private void UpdateForegroundStatus()
        {
            // 初始检查或主动刷新
            IntPtr hwnd = Win32Api.GetForegroundWindow();
            if (hwnd == IntPtr.Zero) return;

            Win32Api.GetWindowThreadProcessId(hwnd, out uint pid);
            UpdateForegroundStatusDirect(hwnd, (int)pid, Win32Api.GetWindowTitle(hwnd), Win32Api.IsIconic(hwnd), Win32Api.IsWindowVisible(hwnd));
        }

        private void UpdateForegroundStatusDirect(IntPtr hwnd, int newPid, string title, bool isIconic, bool isVisible)
        {
            // 1. 系统 UI 过滤器 (Explorer 相关)
            string processName = GetProcessName(newPid);
            if (IsSystemUI(newPid, title) || _filterEngine.ShouldIgnore(processName))
            {
                return; // 忽略系统 UI 或黑名单进程
            }

            ActivityStatus newStatus = (isIconic || !isVisible)
                ? ActivityStatus.Background
                : ActivityStatus.ForegroundActive;

            // 2. 处理旧焦点的精准降级
            if (_currentForegroundPid != 0 && _currentForegroundPid != newPid)
            {
                // 回查旧句柄的真实状态，避免误抬升
                ActivityStatus oldProcessNewStatus = ActivityStatus.ForegroundInactive;
                if (_currentForegroundHwnd != IntPtr.Zero)
                {
                    if (Win32Api.IsIconic(_currentForegroundHwnd) || !Win32Api.IsWindowVisible(_currentForegroundHwnd))
                    {
                        oldProcessNewStatus = ActivityStatus.Background;
                    }
                }
                UpdateProcessStatus(_currentForegroundPid, string.Empty, oldProcessNewStatus);
            }

            // 3. 处理当前焦点的状态切换
            UpdateProcessStatus(newPid, title, newStatus);
            _currentForegroundPid = newPid;
            _currentForegroundHwnd = hwnd;

            // 4. 管理空闲计时器
            if (newStatus == ActivityStatus.ForegroundActive)
            {
                if (!_idleTimer.IsEnabled) _idleTimer.Start();
            }
            else
            {
                _idleTimer.Stop();
            }
        }

        private bool IsSystemUI(int pid, string title)
        {
            string processName = GetProcessName(pid);
            if (processName.Equals("explorer", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrEmpty(title) || 
                    title.Contains("系统托盘") || 
                    title.Contains("开始") || 
                    title.Contains("任务栏") || 
                    title.Contains("OverflowWindow"))
                {
                    return true;
                }
            }
            return false;
        }

        private void UpdateProcessStatus(int pid, string title, ActivityStatus newStatus)
        {
            if (!_processStates.TryGetValue(pid, out var state))
            {
                state = new ProcessState { ProcessName = GetProcessName(pid) };
                _processStates[pid] = state;
            }

            if (!string.IsNullOrEmpty(title))
            {
                state.LastTitle = title;
            }

            // 严格去重逻辑
            if (state.LastStatus != newStatus)
            {
                var oldStatus = state.LastStatus;
                state.LastStatus = newStatus;

                AppActivityChanged?.Invoke(this, new AppActivityChangedEventArgs
                {
                    ProcessId = pid,
                    ProcessName = state.ProcessName,
                    WindowTitle = state.LastTitle,
                    OldStatus = oldStatus,
                    NewStatus = newStatus,
                    Timestamp = DateTime.Now
                });
            }
        }

        private void OnIdleTimerTick(object? sender, EventArgs e)
        {
            if (_currentForegroundPid == 0) return;

            var lii = new Win32Api.LASTINPUTINFO();
            lii.cbSize = (uint)Marshal.SizeOf(lii);
            if (Win32Api.GetLastInputInfo(ref lii))
            {
                uint idleTimeMs = (uint)Environment.TickCount - lii.dwTime;
                double idleMinutes = idleTimeMs / 60000.0;

                if (idleMinutes >= _configService.Config.IdleTimeoutMinutes)
                {
                    // 检查是否免除空闲检测
                    string processName = GetProcessName(_currentForegroundPid);
                    if (_filterEngine.ShouldDisableIdleDetection(processName))
                    {
                        return; // 豁免，保持 Active
                    }

                    UpdateProcessStatus(_currentForegroundPid, string.Empty, ActivityStatus.ForegroundInactive);
                    _idleTimer.Stop();
                }
            }
        }

        private string GetProcessName(int pid)
        {
            try
            {
                using var proc = Process.GetProcessById(pid);
                return proc.ProcessName;
            }
            catch { return "Unknown"; }
        }
    }
}
