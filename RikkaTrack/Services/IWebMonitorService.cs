using System;

namespace RikkaTrack.Services
{
    public interface IWebMonitorService
    {
        void Start();
        void Stop();
        event Action<Models.WebsiteUsage>? WebUsageReceived;
    }
}
