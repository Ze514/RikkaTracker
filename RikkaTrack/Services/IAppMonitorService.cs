using System;

namespace RikkaTrack.Services
{
    public interface IAppMonitorService
    {
        void Start();
        void Stop();
        event Action<Models.AppUsage>? AppChanged;
    }
}
