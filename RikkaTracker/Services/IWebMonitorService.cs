using System;

namespace RikkaTracker.Services
{
    // @Author: trae + deepseek-V4-pro
    // @Date: 2026-06-23
    // @Desc: 新增 ConnectionStatusChanged 事件和 IsConnected 属性，
    //        用于在设置页显示浏览器扩展连接状态
    public interface IWebMonitorService
    {
        void Start();
        void Stop();
        bool IsConnected { get; }
        event Action<Models.WebsiteUsage>? WebUsageReceived;
        event Action<bool>? ConnectionStatusChanged;
    }
}
