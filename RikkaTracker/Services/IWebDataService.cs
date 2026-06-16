using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using RikkaTracker.Core.Models.WebSentry;

namespace RikkaTracker.Services
{
    public interface IWebDataService
    {
        Task SaveSegmentAsync(WebBrowseSegment segment);
        Task<IEnumerable<WebBrowseSegment>> GetSegmentsAsync(DateTime start, DateTime end);
        Task<IEnumerable<(string Domain, string Title, string Icon, TimeSpan TotalTime)>> GetTopSitesAsync(DateTime start, DateTime end);
        Task<IEnumerable<(int Hour, TimeSpan TotalTime)>> GetHourlyUsageAsync(DateTime date);
        Task UpdateDomainFaviconAsync(string domain, string localPath);
    }
}
