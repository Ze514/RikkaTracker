using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using RikkaTracker.Core.Models;
using RikkaTracker.Core.Models.WebSentry;
using RikkaTracker.Models;

namespace RikkaTracker.Services
{
    public class JsonDataService : IDataService
    {
        public Task<IEnumerable<AppUsage>> LoadAppUsageAsync() => Task.FromResult<IEnumerable<AppUsage>>(new List<AppUsage>());
        public Task SaveAppUsageAsync(IEnumerable<AppUsage> usage) => Task.CompletedTask;
        public Task<IEnumerable<WebsiteUsage>> LoadWebsiteUsageAsync() => Task.FromResult<IEnumerable<WebsiteUsage>>(new List<WebsiteUsage>());
        public Task SaveWebsiteUsageAsync(IEnumerable<WebsiteUsage> usage) => Task.CompletedTask;

        public Task<IEnumerable<RikkaTracker.Core.Models.ActivitySegment>> GetSegmentsAsync(DateTime start, DateTime end) 
            => Task.FromResult<IEnumerable<RikkaTracker.Core.Models.ActivitySegment>>(new List<RikkaTracker.Core.Models.ActivitySegment>());

        public Task<IEnumerable<(string ProcessName, string ProcessPath, TimeSpan TotalTime)>> GetTotalTimeByProcessAsync(DateTime start, DateTime end)
            => Task.FromResult<IEnumerable<(string ProcessName, string ProcessPath, TimeSpan TotalTime)>>(new List<(string, string, TimeSpan)>());

        public Task<IEnumerable<(int Hour, TimeSpan TotalTime)>> GetHourlyUsageAsync(DateTime date)
            => Task.FromResult<IEnumerable<(int Hour, TimeSpan TotalTime)>>(new List<(int, TimeSpan)>());

        public Task<IEnumerable<(DateTime Date, TimeSpan TotalTime)>> GetDailyTrendAsync(DateTime start, DateTime end)
            => Task.FromResult<IEnumerable<(DateTime, TimeSpan)>>(new List<(DateTime, TimeSpan)>());

        public Task<IEnumerable<(int Month, TimeSpan TotalTime)>> GetMonthlyTrendAsync(int year)
            => Task.FromResult<IEnumerable<(int, TimeSpan)>>(new List<(int, TimeSpan)>());

        public Task<(TimeSpan TotalTime, int AppCount, string TopAppName, TimeSpan TopAppTime, string TopAppPath)> GetStatsSummaryAsync(DateTime start, DateTime end)
            => Task.FromResult((TimeSpan.Zero, 0, "N/A", TimeSpan.Zero, string.Empty));

        public Task<IEnumerable<(string ProcessName, string ProcessPath, string Alias)>> GetAppsInPeriodAsync(DateTime start, DateTime end)
            => Task.FromResult<IEnumerable<(string ProcessName, string ProcessPath, string Alias)>>(new List<(string, string, string)>());

        public Task<IEnumerable<WebBrowseSegment>> GetWebSegmentsAsync(DateTime start, DateTime end)
            => Task.FromResult<IEnumerable<WebBrowseSegment>>(new List<WebBrowseSegment>());

        public Task<IEnumerable<(string Domain, TimeSpan TotalTime)>> GetTopSitesByDomainAsync(DateTime start, DateTime end)
            => Task.FromResult<IEnumerable<(string Domain, TimeSpan TotalTime)>>(new List<(string, TimeSpan)>());

        public Task<IEnumerable<(int Hour, TimeSpan TotalTime)>> GetWebHourlyUsageAsync(DateTime date)
            => Task.FromResult<IEnumerable<(int Hour, TimeSpan TotalTime)>>(new List<(int, TimeSpan)>());

        public Task<IEnumerable<(DateTime Date, TimeSpan TotalTime)>> GetWebDailyTrendAsync(DateTime start, DateTime end)
            => Task.FromResult<IEnumerable<(DateTime, TimeSpan)>>(new List<(DateTime, TimeSpan)>());

        public Task<IEnumerable<(int Month, TimeSpan TotalTime)>> GetWebMonthlyTrendAsync(int year)
            => Task.FromResult<IEnumerable<(int, TimeSpan)>>(new List<(int, TimeSpan)>());

        public Task<(TimeSpan TotalTime, int SiteCount, string TopDomain, TimeSpan TopDomainTime)> GetWebStatsSummaryAsync(DateTime start, DateTime end)
            => Task.FromResult((TimeSpan.Zero, 0, "N/A", TimeSpan.Zero));

        public Task<IEnumerable<(string Domain, string Title, string Icon)>> GetSitesInPeriodAsync(DateTime start, DateTime end)
            => Task.FromResult<IEnumerable<(string Domain, string Title, string Icon)>>(new List<(string, string, string)>());
    }
}
