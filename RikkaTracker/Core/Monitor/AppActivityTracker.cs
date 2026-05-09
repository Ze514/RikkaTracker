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
        public string ProcessPath { get; set; } = string.Empty;
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

        // 按进程ID跟踪状态
        private class ProcessState
        {
            public string ProcessName { get; set; } = string.Empty;
            public string LastTitle { get; set; } = string.Empty;
            public ActivityStatus LastStatus { get; set; } = ActivityStatus.Background;
            public IntPtr LastHwnd { get; set; } = IntPtr.Zero;
        }

        private readonly Dictionary<int, ProcessState> _processStates = new();
        private int _currentForegroundPid;
        private IntPtr _currentForegroundHwnd;
        // 标记当前焦点进程是否因空闲而被降级
        private bool _isIdleDemoted;

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
            // 仅处理我们关心的三种事件类型，忽略菜单、拖拽、对话框等
            if (eventType != Win32Api.EVENT_SYSTEM_FOREGROUND &&
                eventType != Win32Api.EVENT_SYSTEM_MINIMIZESTART &&
                eventType != Win32Api.EVENT_SYSTEM_MINIMIZEEND)
            {
                return;
            }

            // 在系统线程提取非托管信息
            uint pid;
            Win32Api.GetWindowThreadProcessId(hwnd, out pid);
            string title = Win32Api.GetWindowTitle(hwnd);
            bool isIconic = Win32Api.IsIconic(hwnd);
            bool isVisible = Win32Api.IsWindowVisible(hwnd);

            // 调度到UI线程执行状态机更新
            System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                switch (eventType)
                {
                    case Win32Api.EVENT_SYSTEM_FOREGROUND:
                        HandleForegroundChange(hwnd, (int)pid, title, isIconic, isVisible);
                        break;
                    case Win32Api.EVENT_SYSTEM_MINIMIZESTART:
                        HandleMinimize(hwnd, (int)pid);
                        break;
                    case Win32Api.EVENT_SYSTEM_MINIMIZEEND:
                        HandleRestore(hwnd, (int)pid, title);
                        break;
                }
            });
        }

        /// <summary>
        /// 初始化时主动检查当前前台窗口
        /// </summary>
        private void UpdateForegroundStatus()
        {
            IntPtr hwnd = Win32Api.GetForegroundWindow();
            if (hwnd == IntPtr.Zero) return;

            Win32Api.GetWindowThreadProcessId(hwnd, out uint pid);
            HandleForegroundChange(hwnd, (int)pid, Win32Api.GetWindowTitle(hwnd), Win32Api.IsIconic(hwnd), Win32Api.IsWindowVisible(hwnd));
        }

        /// <summary>
        /// 处理焦点切换事件：降级旧焦点进程，提升新焦点进程
        /// </summary>
        private void HandleForegroundChange(IntPtr hwnd, int newPid, string title, bool isIconic, bool isVisible)
        {
            string processName = GetProcessName(newPid);
            if (IsSystemUI(newPid, title) || _filterEngine.ShouldIgnore(processName))
            {
                return;
            }

            // 新焦点的目标状态
            ActivityStatus newStatus = (isIconic || !isVisible)
                ? ActivityStatus.Background
                : ActivityStatus.ForegroundActive;

            // 1. 降级旧焦点进程
            if (_currentForegroundPid != 0 && _currentForegroundPid != newPid)
            {
                // 回查旧窗口的真实可见性来决定降级目标
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

            // 2. 提升新焦点进程
            UpdateProcessStatus(newPid, title, newStatus);
            _currentForegroundPid = newPid;
            _currentForegroundHwnd = hwnd;

            // 更新进程状态中的窗口句柄
            if (_processStates.TryGetValue(newPid, out var state))
            {
                state.LastHwnd = hwnd;
            }

            // 3. 管理空闲计时器
            _isIdleDemoted = false;
            if (newStatus == ActivityStatus.ForegroundActive)
            {
                if (!_idleTimer.IsEnabled) _idleTimer.Start();
            }
            else
            {
                _idleTimer.Stop();
            }
        }

        /// <summary>
        /// 处理窗口最小化事件：仅更新被最小化窗口所属进程的状态，不变更焦点跟踪
        /// </summary>
        private void HandleMinimize(IntPtr hwnd, int pid)
        {
            string processName = GetProcessName(pid);
            if (IsSystemUI(pid, string.Empty) || _filterEngine.ShouldIgnore(processName))
            {
                return;
            }

            // 将该进程降为后台
            UpdateProcessStatus(pid, string.Empty, ActivityStatus.Background);

            // 如果被最小化的是当前焦点进程，清除焦点跟踪
            // （后续会有 FOREGROUND 事件为新的焦点窗口触发）
            if (pid == _currentForegroundPid)
            {
                _currentForegroundPid = 0;
                _currentForegroundHwnd = IntPtr.Zero;
                _idleTimer.Stop();
                _isIdleDemoted = false;
            }
        }

        /// <summary>
        /// 处理窗口恢复事件：将进程从后台提升为前台非活动（除非它同时获得焦点）
        /// </summary>
        private void HandleRestore(IntPtr hwnd, int pid, string title)
        {
            string processName = GetProcessName(pid);
            if (IsSystemUI(pid, title) || _filterEngine.ShouldIgnore(processName))
            {
                return;
            }

            // 检查恢复的窗口是否就是当前焦点窗口
            IntPtr fgHwnd = Win32Api.GetForegroundWindow();
            if (fgHwnd == hwnd)
            {
                // 恢复并同时获得焦点 → ForegroundActive
                HandleForegroundChange(hwnd, pid, title, false, true);
            }
            else
            {
                // 仅恢复显示，未获焦点 → ForegroundInactive
                UpdateProcessStatus(pid, title, ActivityStatus.ForegroundInactive);

                // 更新进程的窗口句柄
                if (_processStates.TryGetValue(pid, out var state))
                {
                    state.LastHwnd = hwnd;
                }
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

            // 严格去重：同状态不重复下发
            if (state.LastStatus != newStatus)
            {
                var oldStatus = state.LastStatus;
                state.LastStatus = newStatus;

                string processPath = Win32Api.GetProcessPath(pid);

                AppActivityChanged?.Invoke(this, new AppActivityChangedEventArgs
                {
                    ProcessId = pid,
                    ProcessName = state.ProcessName,
                    ProcessPath = processPath,
                    WindowTitle = state.LastTitle,
                    OldStatus = oldStatus,
                    NewStatus = newStatus,
                    Timestamp = DateTime.Now
                });
            }
        }

        /// <summary>
        /// 空闲检测Tick：检测空闲超时降级和输入恢复提升
        /// </summary>
        private void OnIdleTimerTick(object? sender, EventArgs e)
        {
            if (_currentForegroundPid == 0) return;

            var lii = new Win32Api.LASTINPUTINFO();
            lii.cbSize = (uint)Marshal.SizeOf(lii);
            if (!Win32Api.GetLastInputInfo(ref lii)) return;

            uint idleTimeMs = (uint)Environment.TickCount - lii.dwTime;
            double idleMinutes = idleTimeMs / 60000.0;

            string processName = GetProcessName(_currentForegroundPid);

            if (idleMinutes >= _configService.Config.IdleTimeoutMinutes)
            {
                // 超过空闲阈值 → 降级为 ForegroundInactive
                if (!_isIdleDemoted)
                {
                    // 检查是否免除空闲检测
                    if (_filterEngine.ShouldDisableIdleDetection(processName))
                    {
                        return; // 豁免，保持 Active
                    }

                    UpdateProcessStatus(_currentForegroundPid, string.Empty, ActivityStatus.ForegroundInactive);
                    _isIdleDemoted = true;
                    // 注意：Timer不停止，继续运行以检测输入恢复
                }
            }
            else
            {
                // 未超过空闲阈值 → 如果之前因空闲降级了，恢复为 ForegroundActive
                if (_isIdleDemoted)
                {
                    UpdateProcessStatus(_currentForegroundPid, string.Empty, ActivityStatus.ForegroundActive);
                    _isIdleDemoted = false;
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
