using System;

namespace RikkaTracker.Services
{
    public interface IAppMonitorService
    {
        void Start();
        void Stop();
        event Action<Models.AppUsage>? AppChanged;
    }
}
