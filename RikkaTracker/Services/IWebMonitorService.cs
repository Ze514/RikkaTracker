using System;

namespace RikkaTracker.Services
{
    public interface IWebMonitorService
    {
        void Start();
        void Stop();
        event Action<Models.WebsiteUsage>? WebUsageReceived;
    }
}
