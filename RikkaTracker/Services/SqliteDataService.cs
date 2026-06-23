using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using RikkaTracker.Core.Data;
using RikkaTracker.Core.Models;
using RikkaTracker.Core.Monitor;
using RikkaTracker.Core.Librarys;
using RikkaTracker.Core.Models.WebSentry;
using RikkaTracker.Models;

namespace RikkaTracker.Services
{
    public class SqliteDataService : IDataService
    {
        private readonly SqliteDbContext _dbContext;

        public SqliteDataService(SqliteDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public Task<IEnumerable<AppUsage>> LoadAppUsageAsync() => Task.FromResult<IEnumerable<AppUsage>>(new List<AppUsage>());
        Task IDataService.SaveAppUsageAsync(IEnumerable<AppUsage> usage) => Task.CompletedTask;

        public async Task<IEnumerable<WebsiteUsage>> LoadWebsiteUsageAsync()
        {
            var usages = new List<WebsiteUsage>();
            using var connection = _dbContext.CreateConnection();
            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT Url, Domain, Title, StartTime, EndTime
                FROM WebBrowseLog
                ORDER BY StartTime DESC
            ";
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                usages.Add(new WebsiteUsage
                {
                    Url = reader.GetString(0),
                    Domain = reader.GetString(1),
                    Title = reader.GetString(2),
                    StartTime = DateTime.Parse(reader.GetString(3), null, DateTimeStyles.RoundtripKind),
                    EndTime = DateTime.Parse(reader.GetString(4), null, DateTimeStyles.RoundtripKind)
                });
            }
            return usages;
        }

        async Task IDataService.SaveWebsiteUsageAsync(IEnumerable<WebsiteUsage> usage)
        {
            foreach (var u in usage)
            {
                using var connection = _dbContext.CreateConnection();
                using var command = connection.CreateCommand();
                command.CommandText = @"
                    INSERT INTO WebBrowseLog (Url, Domain, Title, Icon, StartTime, EndTime)
                    VALUES ($url, $domain, $title, $icon, $startTime, $endTime)
                ";
                command.Parameters.AddWithValue("$url", u.Url);
                command.Parameters.AddWithValue("$domain", string.IsNullOrWhiteSpace(u.Domain) ? UrlHelper.GetDomain(u.Url) : u.Domain);
                command.Parameters.AddWithValue("$title", u.Title);
                command.Parameters.AddWithValue("$icon", string.Empty);
                command.Parameters.AddWithValue("$startTime", u.StartTime.ToString("o"));
                command.Parameters.AddWithValue("$endTime", u.EndTime.ToString("o"));
                await command.ExecuteNonQueryAsync();
            }
        }

        // Phase 3 & 4 methods
        public async Task<IEnumerable<RikkaTracker.Core.Models.ActivitySegment>> GetSegmentsAsync(DateTime start, DateTime end)
        {
            var segments = new List<ActivitySegment>();
            using var connection = _dbContext.CreateConnection();
            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT Id, ProcessName, WindowTitle, Status, StartTime, EndTime, ProcessPath, Alias
                FROM ActivityLog 
                WHERE StartTime >= $from AND StartTime <= $to
                ORDER BY StartTime ASC
            ";
            command.Parameters.AddWithValue("$from", start.ToString("o"));
            command.Parameters.AddWithValue("$to", end.ToString("o"));

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                segments.Add(new ActivitySegment
                {
                    Id = reader.GetInt32(0),
                    ProcessName = reader.GetString(1),
                    WindowTitle = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                    Status = (ActivityStatus)reader.GetInt32(3),
                    StartTime = DateTime.Parse(reader.GetString(4), null, DateTimeStyles.RoundtripKind),
                    EndTime = DateTime.Parse(reader.GetString(5), null, DateTimeStyles.RoundtripKind),
                    ProcessPath = reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
                    Alias = reader.IsDBNull(7) ? string.Empty : reader.GetString(7)
                });
            }
            return segments;
        }

        public async Task<IEnumerable<(string ProcessName, string ProcessPath, TimeSpan TotalTime)>> GetTotalTimeByProcessAsync(DateTime start, DateTime end)
        {
            var result = new List<(string Name, string Path, double Seconds)>();
            using var connection = _dbContext.CreateConnection();
            using var command = connection.CreateCommand();
            
            // 使用 SQL 聚合计算秒数，同时通过 MAX(Alias) 获取最新的友好名称
            command.CommandText = @"
                SELECT ProcessName, ProcessPath, SUM(strftime('%s', EndTime) - strftime('%s', StartTime)) as TotalSeconds, MAX(Alias) as Alias
                FROM ActivityLog 
                WHERE StartTime >= $from AND StartTime <= $to AND Status = $status
                GROUP BY ProcessName, ProcessPath
                ORDER BY TotalSeconds DESC
            ";
            command.Parameters.AddWithValue("$from", start.ToString("o"));
            command.Parameters.AddWithValue("$to", end.ToString("o"));
            command.Parameters.AddWithValue("$status", (int)ActivityStatus.ForegroundActive);

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                string procName = reader.GetString(0);
                double seconds = reader.GetDouble(2);
                string alias = reader.IsDBNull(3) ? string.Empty : reader.GetString(3);
                
                string displayName = !string.IsNullOrEmpty(alias) ? alias : procName;
                result.Add((displayName, reader.IsDBNull(1) ? string.Empty : reader.GetString(1), seconds));
            }
            return result.Select(r => (r.Name, r.Path, TimeSpan.FromSeconds(r.Seconds)));
        }

        public async Task<IEnumerable<(int Hour, TimeSpan TotalTime)>> GetHourlyUsageAsync(DateTime date)
        {
            var hourlyData = new double[24];
            DateTime dayStart = date.Date;
            DateTime dayEnd = dayStart.AddDays(1);

            using var connection = _dbContext.CreateConnection();
            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT StartTime, EndTime 
                FROM ActivityLog 
                WHERE StartTime >= $from AND StartTime < $to AND Status = $status
            ";
            command.Parameters.AddWithValue("$from", dayStart.ToString("o"));
            command.Parameters.AddWithValue("$to", dayEnd.ToString("o"));
            command.Parameters.AddWithValue("$status", (int)ActivityStatus.ForegroundActive);

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                DateTime start = DateTime.Parse(reader.GetString(0), null, DateTimeStyles.RoundtripKind);
                DateTime end = DateTime.Parse(reader.GetString(1), null, DateTimeStyles.RoundtripKind);

                // 处理跨小时的片段
                for (int h = 0; h < 24; h++)
                {
                    DateTime hourStart = dayStart.AddHours(h);
                    DateTime hourEnd = hourStart.AddHours(1);

                    DateTime overlapStart = start > hourStart ? start : hourStart;
                    DateTime overlapEnd = end < hourEnd ? end : hourEnd;

                    if (overlapStart < overlapEnd)
                    {
                        hourlyData[h] += (overlapEnd - overlapStart).TotalSeconds;
                    }
                }
            }

            return Enumerable.Range(0, 24).Select(h => (h, TimeSpan.FromSeconds(hourlyData[h])));
        }

        public async Task<IEnumerable<(DateTime Date, TimeSpan TotalTime)>> GetDailyTrendAsync(DateTime start, DateTime end)
        {
            var result = new Dictionary<DateTime, double>();
            for (var d = start.Date; d < end.Date; d = d.AddDays(1))
            {
                result[d] = 0;
            }

            using var connection = _dbContext.CreateConnection();
            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT StartTime, EndTime 
                FROM ActivityLog 
                WHERE StartTime >= $from AND StartTime < $to AND Status = $status
            ";
            command.Parameters.AddWithValue("$from", start.ToString("o"));
            command.Parameters.AddWithValue("$to", end.ToString("o"));
            command.Parameters.AddWithValue("$status", (int)ActivityStatus.ForegroundActive);

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                DateTime segStart = DateTime.Parse(reader.GetString(0), null, DateTimeStyles.RoundtripKind);
                DateTime segEnd = DateTime.Parse(reader.GetString(1), null, DateTimeStyles.RoundtripKind);

                DateTime current = segStart.Date;
                while (current < segEnd.Date)
                {
                    DateTime nextDay = current.AddDays(1);
                    if (result.ContainsKey(current))
                    {
                        result[current] += (nextDay - (segStart > current ? segStart : current)).TotalSeconds;
                    }
                    current = nextDay;
                    segStart = current;
                }
                if (result.ContainsKey(current))
                {
                    result[current] += (segEnd - (segStart > current ? segStart : current)).TotalSeconds;
                }
            }

            return result.OrderBy(kvp => kvp.Key).Select(kvp => (kvp.Key, TimeSpan.FromSeconds(kvp.Value)));
        }

        public async Task<IEnumerable<(int Month, TimeSpan TotalTime)>> GetMonthlyTrendAsync(int year)
        {
            var result = new Dictionary<int, double>();
            for (int i = 1; i <= 12; i++) result[i] = 0;

            DateTime start = new DateTime(year, 1, 1);
            DateTime end = start.AddYears(1);

            using var connection = _dbContext.CreateConnection();
            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT StartTime, EndTime 
                FROM ActivityLog 
                WHERE StartTime >= $from AND StartTime < $to AND Status = $status
            ";
            command.Parameters.AddWithValue("$from", start.ToString("o"));
            command.Parameters.AddWithValue("$to", end.ToString("o"));
            command.Parameters.AddWithValue("$status", (int)ActivityStatus.ForegroundActive);

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                DateTime segStart = DateTime.Parse(reader.GetString(0), null, DateTimeStyles.RoundtripKind);
                DateTime segEnd = DateTime.Parse(reader.GetString(1), null, DateTimeStyles.RoundtripKind);

                DateTime currentMonthStart = new DateTime(segStart.Year, segStart.Month, 1);
                while (currentMonthStart < new DateTime(segEnd.Year, segEnd.Month, 1))
                {
                    DateTime nextMonth = currentMonthStart.AddMonths(1);
                    if (currentMonthStart.Year == year)
                    {
                        result[currentMonthStart.Month] += (nextMonth - (segStart > currentMonthStart ? segStart : currentMonthStart)).TotalSeconds;
                    }
                    currentMonthStart = nextMonth;
                    segStart = currentMonthStart;
                }
                if (currentMonthStart.Year == year)
                {
                    result[currentMonthStart.Month] += (segEnd - (segStart > currentMonthStart ? segStart : currentMonthStart)).TotalSeconds;
                }
            }

            return result.OrderBy(kvp => kvp.Key).Select(kvp => (kvp.Key, TimeSpan.FromSeconds(kvp.Value)));
        }

        public async Task<(TimeSpan TotalTime, int AppCount, string TopAppName, TimeSpan TopAppTime, string TopAppPath)> GetStatsSummaryAsync(DateTime start, DateTime end)
        {
            using var connection = _dbContext.CreateConnection();
            using var command = connection.CreateCommand();
            
            // 一次性查出总时长、应用数和 Top 1
            command.CommandText = @"
                SELECT 
                    SUM(strftime('%s', EndTime) - strftime('%s', StartTime)) as TotalSeconds,
                    COUNT(DISTINCT ProcessName) as AppCount
                FROM ActivityLog 
                WHERE StartTime >= $from AND StartTime <= $to AND Status = $status;

                SELECT ProcessName, SUM(strftime('%s', EndTime) - strftime('%s', StartTime)) as TopSeconds, MAX(Alias) as TopAlias, ProcessPath
                FROM ActivityLog 
                WHERE StartTime >= $from AND StartTime <= $to AND Status = $status
                GROUP BY ProcessName, ProcessPath
                ORDER BY TopSeconds DESC
                LIMIT 1;
            ";
            command.Parameters.AddWithValue("$from", start.ToString("o"));
            command.Parameters.AddWithValue("$to", end.ToString("o"));
            command.Parameters.AddWithValue("$status", (int)ActivityStatus.ForegroundActive);

            TimeSpan totalTime = TimeSpan.Zero;
            int appCount = 0;
            string topAppName = "N/A";
            TimeSpan topAppTime = TimeSpan.Zero;
            string topAppPath = string.Empty;

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                totalTime = TimeSpan.FromSeconds(reader.IsDBNull(0) ? 0 : reader.GetDouble(0));
                appCount = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
            }

            if (await reader.NextResultAsync() && await reader.ReadAsync())
            {
                string procName = reader.GetString(0);
                double seconds = reader.GetDouble(1);
                string alias = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);
                topAppPath = reader.IsDBNull(3) ? string.Empty : reader.GetString(3);
                
                topAppName = !string.IsNullOrEmpty(alias) ? alias : procName;
                topAppTime = TimeSpan.FromSeconds(seconds);
            }

            return (totalTime, appCount, topAppName, topAppTime, topAppPath);
        }

        public async Task<IEnumerable<(string ProcessName, string ProcessPath, string Alias)>> GetAppsInPeriodAsync(DateTime start, DateTime end)
        {
            var apps = new List<(string, string, string)>();
            using var connection = _dbContext.CreateConnection();
            using var command = connection.CreateCommand();
            
            // 使用 strftime('%s', EndTime) - strftime('%s', StartTime) 保证只列出有实际时长记录的应用
            // 另外，通过 MAX(Alias) 和 MAX(ProcessPath) 获取可能存在的名称与路径
            command.CommandText = @"
                SELECT ProcessName, MAX(ProcessPath) as ProcessPath, MAX(Alias) as Alias
                FROM ActivityLog
                WHERE StartTime >= $from AND StartTime <= $to
                GROUP BY ProcessName
                ORDER BY ProcessName ASC
            ";
            command.Parameters.AddWithValue("$from", start.ToString("o"));
            command.Parameters.AddWithValue("$to", end.ToString("o"));

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                apps.Add((
                    reader.GetString(0),
                    reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                    reader.IsDBNull(2) ? string.Empty : reader.GetString(2)
                ));
            }
            return apps;
        }

        public async Task<IEnumerable<WebBrowseSegment>> GetWebSegmentsAsync(DateTime start, DateTime end)
        {
            var segments = new List<WebBrowseSegment>();
            using var connection = _dbContext.CreateConnection();
            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT Id, Url, Domain, Title, Icon, StartTime, EndTime
                FROM WebBrowseLog
                WHERE StartTime >= $from AND StartTime <= $to
                ORDER BY StartTime ASC
            ";
            command.Parameters.AddWithValue("$from", start.ToString("o"));
            command.Parameters.AddWithValue("$to", end.ToString("o"));

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                segments.Add(new WebBrowseSegment
                {
                    Id = reader.GetInt32(0),
                    Url = reader.GetString(1),
                    Domain = reader.GetString(2),
                    Title = reader.GetString(3),
                    Icon = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                    StartTime = DateTime.Parse(reader.GetString(5), null, DateTimeStyles.RoundtripKind),
                    EndTime = DateTime.Parse(reader.GetString(6), null, DateTimeStyles.RoundtripKind)
                });
            }
            return segments;
        }

        public async Task<IEnumerable<(string Domain, TimeSpan TotalTime)>> GetTopSitesByDomainAsync(DateTime start, DateTime end)
        {
            var result = new List<(string Domain, TimeSpan TotalTime)>();
            using var connection = _dbContext.CreateConnection();
            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT Domain, SUM(strftime('%s', EndTime) - strftime('%s', StartTime)) as TotalSeconds
                FROM WebBrowseLog
                WHERE StartTime >= $from AND StartTime <= $to
                GROUP BY Domain
                ORDER BY TotalSeconds DESC
            ";
            command.Parameters.AddWithValue("$from", start.ToString("o"));
            command.Parameters.AddWithValue("$to", end.ToString("o"));

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                result.Add((reader.GetString(0), TimeSpan.FromSeconds(reader.GetDouble(1))));
            }
            return result;
        }

        public async Task<IEnumerable<(int Hour, TimeSpan TotalTime)>> GetWebHourlyUsageAsync(DateTime date)
        {
            var hourlyData = new double[24];
            DateTime dayStart = date.Date;
            DateTime dayEnd = dayStart.AddDays(1);

            using var connection = _dbContext.CreateConnection();
            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT StartTime, EndTime
                FROM WebBrowseLog
                WHERE StartTime >= $from AND StartTime < $to
            ";
            command.Parameters.AddWithValue("$from", dayStart.ToString("o"));
            command.Parameters.AddWithValue("$to", dayEnd.ToString("o"));

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                DateTime segStart = DateTime.Parse(reader.GetString(0), null, DateTimeStyles.RoundtripKind);
                DateTime segEnd = DateTime.Parse(reader.GetString(1), null, DateTimeStyles.RoundtripKind);

                for (int h = 0; h < 24; h++)
                {
                    DateTime hourStart = dayStart.AddHours(h);
                    DateTime hourEnd = hourStart.AddHours(1);
                    DateTime overlapStart = segStart > hourStart ? segStart : hourStart;
                    DateTime overlapEnd = segEnd < hourEnd ? segEnd : hourEnd;
                    if (overlapStart < overlapEnd)
                        hourlyData[h] += (overlapEnd - overlapStart).TotalSeconds;
                }
            }
            return Enumerable.Range(0, 24).Select(h => (h, TimeSpan.FromSeconds(hourlyData[h])));
        }

        public async Task<IEnumerable<(DateTime Date, TimeSpan TotalTime)>> GetWebDailyTrendAsync(DateTime start, DateTime end)
        {
            var result = new Dictionary<DateTime, double>();
            for (var d = start.Date; d < end.Date; d = d.AddDays(1))
                result[d] = 0;

            using var connection = _dbContext.CreateConnection();
            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT StartTime, EndTime
                FROM WebBrowseLog
                WHERE StartTime >= $from AND StartTime < $to
            ";
            command.Parameters.AddWithValue("$from", start.ToString("o"));
            command.Parameters.AddWithValue("$to", end.ToString("o"));

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                DateTime segStart = DateTime.Parse(reader.GetString(0), null, DateTimeStyles.RoundtripKind);
                DateTime segEnd = DateTime.Parse(reader.GetString(1), null, DateTimeStyles.RoundtripKind);

                DateTime current = segStart.Date;
                while (current < segEnd.Date)
                {
                    DateTime nextDay = current.AddDays(1);
                    if (result.ContainsKey(current))
                        result[current] += (nextDay - (segStart > current ? segStart : current)).TotalSeconds;
                    current = nextDay;
                    segStart = current;
                }
                if (result.ContainsKey(current))
                    result[current] += (segEnd - (segStart > current ? segStart : current)).TotalSeconds;
            }
            return result.OrderBy(kvp => kvp.Key).Select(kvp => (kvp.Key, TimeSpan.FromSeconds(kvp.Value)));
        }

        public async Task<IEnumerable<(int Month, TimeSpan TotalTime)>> GetWebMonthlyTrendAsync(int year)
        {
            var result = new Dictionary<int, double>();
            for (int i = 1; i <= 12; i++) result[i] = 0;

            DateTime start = new DateTime(year, 1, 1);
            DateTime end = start.AddYears(1);

            using var connection = _dbContext.CreateConnection();
            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT StartTime, EndTime
                FROM WebBrowseLog
                WHERE StartTime >= $from AND StartTime < $to
            ";
            command.Parameters.AddWithValue("$from", start.ToString("o"));
            command.Parameters.AddWithValue("$to", end.ToString("o"));

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                DateTime segStart = DateTime.Parse(reader.GetString(0), null, DateTimeStyles.RoundtripKind);
                DateTime segEnd = DateTime.Parse(reader.GetString(1), null, DateTimeStyles.RoundtripKind);

                DateTime currentMonthStart = new DateTime(segStart.Year, segStart.Month, 1);
                while (currentMonthStart < new DateTime(segEnd.Year, segEnd.Month, 1))
                {
                    DateTime nextMonth = currentMonthStart.AddMonths(1);
                    if (currentMonthStart.Year == year)
                        result[currentMonthStart.Month] += (nextMonth - (segStart > currentMonthStart ? segStart : currentMonthStart)).TotalSeconds;
                    currentMonthStart = nextMonth;
                    segStart = currentMonthStart;
                }
                if (currentMonthStart.Year == year)
                    result[currentMonthStart.Month] += (segEnd - (segStart > currentMonthStart ? segStart : currentMonthStart)).TotalSeconds;
            }
            return result.OrderBy(kvp => kvp.Key).Select(kvp => (kvp.Key, TimeSpan.FromSeconds(kvp.Value)));
        }

        public async Task<(TimeSpan TotalTime, int SiteCount, string TopDomain, TimeSpan TopDomainTime)> GetWebStatsSummaryAsync(DateTime start, DateTime end)
        {
            using var connection = _dbContext.CreateConnection();
            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT SUM(strftime('%s', EndTime) - strftime('%s', StartTime)) as TotalSeconds,
                       COUNT(DISTINCT Domain) as SiteCount
                FROM WebBrowseLog
                WHERE StartTime >= $from AND StartTime <= $to;

                SELECT Domain, SUM(strftime('%s', EndTime) - strftime('%s', StartTime)) as TopSeconds
                FROM WebBrowseLog
                WHERE StartTime >= $from AND StartTime <= $to
                GROUP BY Domain
                ORDER BY TopSeconds DESC
                LIMIT 1;
            ";
            command.Parameters.AddWithValue("$from", start.ToString("o"));
            command.Parameters.AddWithValue("$to", end.ToString("o"));

            TimeSpan totalTime = TimeSpan.Zero;
            int siteCount = 0;
            string topDomain = "N/A";
            TimeSpan topDomainTime = TimeSpan.Zero;

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                totalTime = TimeSpan.FromSeconds(reader.IsDBNull(0) ? 0 : reader.GetDouble(0));
                siteCount = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
            }
            if (await reader.NextResultAsync() && await reader.ReadAsync())
            {
                topDomain = reader.GetString(0);
                topDomainTime = TimeSpan.FromSeconds(reader.GetDouble(1));
            }
            return (totalTime, siteCount, topDomain, topDomainTime);
        }

        public async Task<IEnumerable<(string Domain, string Title, string Icon)>> GetSitesInPeriodAsync(DateTime start, DateTime end)
        {
            var sites = new List<(string, string, string)>();
            using var connection = _dbContext.CreateConnection();
            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT Domain, MAX(Title) as Title, MAX(Icon) as Icon
                FROM WebBrowseLog
                WHERE StartTime >= $from AND StartTime <= $to
                GROUP BY Domain
                ORDER BY Domain ASC
            ";
            command.Parameters.AddWithValue("$from", start.ToString("o"));
            command.Parameters.AddWithValue("$to", end.ToString("o"));

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                sites.Add((
                    reader.GetString(0),
                    reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                    reader.IsDBNull(2) ? string.Empty : reader.GetString(2)
                ));
            }
            return sites;
        }
    }
}
