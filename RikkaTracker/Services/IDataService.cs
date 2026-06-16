using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using RikkaTracker.Models;
using RikkaTracker.Core.Models;
using RikkaTracker.Core.Models.WebSentry;

namespace RikkaTracker.Services
{
    public interface IDataService
    {
        Task SaveAppUsageAsync(IEnumerable<AppUsage> usage);
        Task<IEnumerable<AppUsage>> LoadAppUsageAsync();
        
        Task SaveWebsiteUsageAsync(IEnumerable<WebsiteUsage> usage);
        Task<IEnumerable<WebsiteUsage>> LoadWebsiteUsageAsync();

        Task<IEnumerable<RikkaTracker.Core.Models.ActivitySegment>> GetSegmentsAsync(DateTime start, DateTime end);
        Task<IEnumerable<(string ProcessName, string ProcessPath, TimeSpan TotalTime)>> GetTotalTimeByProcessAsync(DateTime start, DateTime end);
        Task<IEnumerable<(int Hour, TimeSpan TotalTime)>> GetHourlyUsageAsync(DateTime date);
        Task<IEnumerable<(DateTime Date, TimeSpan TotalTime)>> GetDailyTrendAsync(DateTime start, DateTime end);
        Task<IEnumerable<(int Month, TimeSpan TotalTime)>> GetMonthlyTrendAsync(int year);
        Task<(TimeSpan TotalTime, int AppCount, string TopAppName, TimeSpan TopAppTime, string TopAppPath)> GetStatsSummaryAsync(DateTime start, DateTime end);
        Task<IEnumerable<(string ProcessName, string ProcessPath, string Alias)>> GetAppsInPeriodAsync(DateTime start, DateTime end);

        // Web browsing data queries
        Task<IEnumerable<WebBrowseSegment>> GetWebSegmentsAsync(DateTime start, DateTime end);
        Task<IEnumerable<(string Domain, TimeSpan TotalTime)>> GetTopSitesByDomainAsync(DateTime start, DateTime end);
        Task<IEnumerable<(int Hour, TimeSpan TotalTime)>> GetWebHourlyUsageAsync(DateTime date);
        Task<IEnumerable<(DateTime Date, TimeSpan TotalTime)>> GetWebDailyTrendAsync(DateTime start, DateTime end);
        Task<IEnumerable<(int Month, TimeSpan TotalTime)>> GetWebMonthlyTrendAsync(int year);
        Task<(TimeSpan TotalTime, int SiteCount, string TopDomain, TimeSpan TopDomainTime)> GetWebStatsSummaryAsync(DateTime start, DateTime end);
        Task<IEnumerable<(string Domain, string Title, string Icon)>> GetSitesInPeriodAsync(DateTime start, DateTime end);
    }
}
