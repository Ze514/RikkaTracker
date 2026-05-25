using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using RikkaTracker.Models;
using RikkaTracker.Core.Models;

namespace RikkaTracker.Services
{
    public interface IDataService
    {
        Task SaveAppUsageAsync(IEnumerable<AppUsage> usage);
        Task<IEnumerable<AppUsage>> LoadAppUsageAsync();
        
        Task SaveWebsiteUsageAsync(IEnumerable<WebsiteUsage> usage);
        Task<IEnumerable<WebsiteUsage>> LoadWebsiteUsageAsync();

        // Phase 3: SQLite Statistics
        Task<IEnumerable<RikkaTracker.Core.Models.ActivitySegment>> GetSegmentsAsync(DateTime start, DateTime end);
        
        /// <summary>
        /// 获取按应用统计的总时长排行
        /// </summary>
        Task<IEnumerable<(string ProcessName, string ProcessPath, TimeSpan TotalTime)>> GetTotalTimeByProcessAsync(DateTime start, DateTime end);

        /// <summary>
        /// 获取指定日期 24 小时的每小时用时汇总
        /// </summary>
        Task<IEnumerable<(int Hour, TimeSpan TotalTime)>> GetHourlyUsageAsync(DateTime date);

        /// <summary>
        /// 获取指定时间段内（按天）的汇总数据
        /// </summary>
        Task<IEnumerable<(DateTime Date, TimeSpan TotalTime)>> GetDailyTrendAsync(DateTime start, DateTime end);

        /// <summary>
        /// 获取指定年份（按月）的汇总数据
        /// </summary>
        Task<IEnumerable<(int Month, TimeSpan TotalTime)>> GetMonthlyTrendAsync(int year);

        /// <summary>
        /// 获取指定时段内的汇总数据（总时长、应用数、最长使用应用）
        /// </summary>
        Task<(TimeSpan TotalTime, int AppCount, string TopAppName, TimeSpan TopAppTime, string TopAppPath)> GetStatsSummaryAsync(DateTime start, DateTime end);
    }
}
