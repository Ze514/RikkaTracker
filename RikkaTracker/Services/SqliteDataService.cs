using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using RikkaTracker.Core.Data;
using RikkaTracker.Core.Models;
using RikkaTracker.Core.Monitor;
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

        // Old methods (Legacy support)
        public Task<IEnumerable<AppUsage>> LoadAppUsageAsync() => Task.FromResult<IEnumerable<AppUsage>>(new List<AppUsage>());
        Task IDataService.SaveAppUsageAsync(IEnumerable<AppUsage> usage) => Task.CompletedTask;
        public Task<IEnumerable<WebsiteUsage>> LoadWebsiteUsageAsync() => Task.FromResult<IEnumerable<WebsiteUsage>>(new List<WebsiteUsage>());
        Task IDataService.SaveWebsiteUsageAsync(IEnumerable<WebsiteUsage> usage) => Task.CompletedTask;

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

        public async Task<(TimeSpan TotalTime, int AppCount, string TopAppName, TimeSpan TopAppTime)> GetStatsSummaryAsync(DateTime start, DateTime end)
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

                SELECT ProcessName, SUM(strftime('%s', EndTime) - strftime('%s', StartTime)) as TopSeconds, MAX(Alias) as TopAlias
                FROM ActivityLog 
                WHERE StartTime >= $from AND StartTime <= $to AND Status = $status
                GROUP BY ProcessName
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
                
                topAppName = !string.IsNullOrEmpty(alias) ? alias : procName;
                topAppTime = TimeSpan.FromSeconds(seconds);
            }

            return (totalTime, appCount, topAppName, topAppTime);
        }
    }
}
