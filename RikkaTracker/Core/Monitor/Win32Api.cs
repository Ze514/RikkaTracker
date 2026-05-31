using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace RikkaTracker.Core.Monitor
{
    public static class Win32Api
    {
        public delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

        [DllImport("user32.dll")]
        public static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc, WinEventDelegate lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

        [DllImport("user32.dll")]
        public static extern bool UnhookWinEvent(IntPtr hWinEventHook);

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        public static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

        [DllImport("user32", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool EnumChildWindows(IntPtr hWndParent, EnumWindowProc lpEnumFunc, IntPtr lParam);

        public delegate bool EnumWindowProc(IntPtr hWnd, IntPtr parameter);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetGUIThreadInfo(uint idThread, ref GUITHREADINFO lpgui);

        [StructLayout(LayoutKind.Sequential)]
        public struct GUITHREADINFO
        {
            public uint cbSize;
            public uint flags;
            public IntPtr hwndActive;
            public IntPtr hwndFocus;
            public IntPtr hwndCapture;
            public IntPtr hwndMenuOwner;
            public IntPtr hwndMoveSize;
            public IntPtr hwndCaret;
            public RECT rcCaret;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct LASTINPUTINFO
        {
            public uint cbSize;
            public uint dwTime;
        }

        public const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
        public const uint EVENT_OBJECT_CREATE = 0x8000;
        public const uint EVENT_OBJECT_DESTROY = 0x8001;
        public const uint EVENT_OBJECT_SHOW = 0x8002;
        public const uint EVENT_OBJECT_HIDE = 0x8003;
        public const uint EVENT_SYSTEM_MINIMIZESTART = 0x0016;
        public const uint EVENT_SYSTEM_MINIMIZEEND = 0x0017;

        public const uint WINEVENT_OUTOFCONTEXT = 0;
        public const uint WINEVENT_SKIPOWNPROCESS = 2;

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbSizeFileInfo, uint uFlags);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        }

        public const uint SHGFI_ICON = 0x100;
        public const uint SHGFI_LARGEICON = 0x0;
        public const uint SHGFI_SMALLICON = 0x1;
        public const uint SHGFI_USEFILEATTRIBUTES = 0x10;

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool DestroyIcon(IntPtr hIcon);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool QueryFullProcessImageName([In] IntPtr hProcess, [In] int dwFlags, [Out] StringBuilder lpExeName, [In, Out] ref int lpdwSize);

        [DllImport("kernel32.dll")]
        public static extern IntPtr OpenProcess(uint processAccess, bool bInheritHandle, int processId);

        [DllImport("kernel32.dll")]
        public static extern bool CloseHandle(IntPtr hObject);

        public const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

        public static string GetWindowTitle(IntPtr hWnd)
        {
            const int nChars = 256;
            StringBuilder buff = new StringBuilder(nChars);
            if (GetWindowText(hWnd, buff, nChars) > 0)
            {
                return buff.ToString();
            }
            return string.Empty;
        }

        public static string GetProcessPath(int pid)
        {
            var hProcess = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
            if (hProcess != IntPtr.Zero)
            {
                try
                {
                    int size = 1024;
                    StringBuilder sb = new StringBuilder(size);
                    if (QueryFullProcessImageName(hProcess, 0, sb, ref size))
                    {
                        return sb.ToString();
                    }
                }
                finally
                {
                    CloseHandle(hProcess);
                }
            }
            return string.Empty;
        }

        public static string GetInternalProcessName(int pid)
        {
            try
            {
                using var proc = Process.GetProcessById(pid);
                return proc.ProcessName;
            }
            catch { return "Unknown"; }
        }

        public static string GetProcessAlias(int pid)
        {
            try
            {
                string path = GetProcessPath(pid);
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    var info = FileVersionInfo.GetVersionInfo(path);
                    
                    // 优先获取文件描述
                    if (!string.IsNullOrEmpty(info.FileDescription))
                    {
                        return info.FileDescription;
                    }
                    
                    // 其次获取产品名称
                    if (!string.IsNullOrEmpty(info.ProductName))
                    {
                        return info.ProductName;
                    }
                }
                return string.Empty; // 如果没有友好描述，返回空
            }
            catch
            {
                return string.Empty;
            }
        }

        public static int ResolveUwpProcessId(IntPtr hWnd, int currentPid)
        {
            WINDOWINFO windowinfo = new WINDOWINFO();
            windowinfo.ownerpid = (uint)currentPid;
            windowinfo.childpid = windowinfo.ownerpid;

            IntPtr pWindowinfo = Marshal.AllocHGlobal(Marshal.SizeOf(windowinfo));
            try
            {
                Marshal.StructureToPtr(windowinfo, pWindowinfo, false);

                EnumWindowProc lpEnumFunc = new EnumWindowProc(EnumChildWindowsCallback);
                EnumChildWindows(hWnd, lpEnumFunc, pWindowinfo);

                windowinfo = (WINDOWINFO)Marshal.PtrToStructure(pWindowinfo, typeof(WINDOWINFO));
                return (int)windowinfo.childpid;
            }
            finally
            {
                Marshal.FreeHGlobal(pWindowinfo);
            }
        }

        private struct WINDOWINFO
        {
            public uint ownerpid;
            public uint childpid;
        }

        private static bool EnumChildWindowsCallback(IntPtr hWnd, IntPtr lParam)
        {
            WINDOWINFO info = (WINDOWINFO)Marshal.PtrToStructure(lParam, typeof(WINDOWINFO));

            uint pID;
            GetWindowThreadProcessId(hWnd, out pID);

            if (pID != info.ownerpid)
            {
                info.childpid = pID;
            }

            Marshal.StructureToPtr(info, lParam, true);
            return true;
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool EnumWindows(EnumWindowProc lpEnumFunc, IntPtr lParam);

        [DllImport("dwmapi.dll")]
        public static extern int DwmGetWindowAttribute(IntPtr hwnd, uint dwAttribute, out int pvAttribute, int cbAttribute);

        public const uint DWMWA_CLOAKED = 14;

        [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
        private static extern IntPtr GetWindowLong32(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
        private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

        public static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex)
        {
            if (IntPtr.Size == 8)
                return GetWindowLongPtr64(hWnd, nIndex);
            else
                return GetWindowLong32(hWnd, nIndex);
        }

        public const int GWL_EXSTYLE = -20;
        public const int WS_EX_TOOLWINDOW = 0x00000080;
        public const int WS_EX_APPWINDOW = 0x00040000;

        [DllImport("user32.dll")]
        public static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

        public const uint GW_OWNER = 4;

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int GetWindowTextLength(IntPtr hWnd);

        // 检查窗口是否为正常应用窗口
        public static bool IsAppWindow(IntPtr hWnd)
        {
            // 必须是可见窗口
            if (!IsWindowVisible(hWnd)) return false;

            // 检查 DWM 遮蔽状态（排除虚拟桌面后台、挂起的 UWP 等）
            int cloaked = 0;
            if (DwmGetWindowAttribute(hWnd, DWMWA_CLOAKED, out cloaked, sizeof(int)) == 0)
            {
                if (cloaked != 0) return false;
            }

            // 检查窗口扩展样式，排除工具窗口（除非它有 APPWINDOW 样式）
            long exStyle = (long)GetWindowLongPtr(hWnd, GWL_EXSTYLE);
            bool isToolWindow = (exStyle & WS_EX_TOOLWINDOW) != 0;
            bool isAppWindowStyle = (exStyle & WS_EX_APPWINDOW) != 0;
            if (isToolWindow && !isAppWindowStyle) return false;

            // 检查所有者，如果是子/被拥有窗口则排除
            IntPtr owner = GetWindow(hWnd, GW_OWNER);
            if (owner != IntPtr.Zero) return false;

            // 检查标题长度，过滤掉无标题的系统窗口
            if (GetWindowTextLength(hWnd) == 0) return false;

            // 检查类名，排除已知的系统组件类名
            StringBuilder className = new StringBuilder(256);
            if (GetClassName(hWnd, className, 256) > 0)
            {
                string cls = className.ToString();
                string[] systemClasses = { 
                    "Shell_TrayWnd",                    // 任务栏
                    "Progman", "WorkerW",               // 桌面背景
                    "Button",                           // Win7 开始按钮
                    "Shell_InputSwitchTopLevelWindow",  // 输入法切换
                    "LockScreenControllerProxyWindow",  // 锁屏
                    "XamlExplorerHostIslandWindow",     // Win11 某些系统组件
                    "Windows.UI.Core.CoreWindow"        // UWP后台窗口或系统窗口
                };
                foreach (var systemClass in systemClasses)
                {
                    if (cls.Equals(systemClass, StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }
                }
            }

            // 检查进程名，排除已知的后台系统组件进程
            GetWindowThreadProcessId(hWnd, out uint pid);
            int processId = (int)pid;
            if (processId != 0)
            {
                string processName = GetInternalProcessName(processId);
                string[] systemProcesses = {
                    "SearchHost",
                    "StartMenuExperienceHost",
                    "ShellExperienceHost",
                    "TextInputHost",
                    "LockApp",
                    "RuntimeBroker",
                    "SettingSyncHost",
                    "backgroundTaskHost",
                    "sihost",
                    "svchost",
                    "dllhost",
                    "taskhostw"
                };
                foreach (var systemProcess in systemProcesses)
                {
                    if (processName.Equals(systemProcess, StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        // 获取所有属于正常应用的进程 ID
        public static HashSet<int> GetAppProcessIds()
        {
            var appPids = new HashSet<int>();
            EnumWindows((hWnd, lParam) =>
            {
                if (IsAppWindow(hWnd))
                {
                    GetWindowThreadProcessId(hWnd, out uint pid);
                    int processId = (int)pid;
                    if (processId != 0)
                    {
                        // 解析 UWP 窗口真正的子进程 PID
                        string processName = GetInternalProcessName(processId);
                        if (processName.Equals("ApplicationFrameHost", StringComparison.OrdinalIgnoreCase))
                        {
                            int realPid = ResolveUwpProcessId(hWnd, processId);
                            if (realPid != 0 && realPid != processId)
                            {
                                processId = realPid;
                            }
                        }

                        appPids.Add(processId);
                    }
                }
                return true;
            }, IntPtr.Zero);

            return appPids;
        }
    }
}
