using System;
using System.Collections.Generic;
using System.Globalization;
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

        // Old methods (Legacy support or empty)
        public Task<IEnumerable<AppUsage>> LoadAppUsageAsync() => Task.FromResult<IEnumerable<AppUsage>>(new List<AppUsage>());
        Task IDataService.SaveAppUsageAsync(IEnumerable<AppUsage> usage) => Task.CompletedTask;
        public Task<IEnumerable<WebsiteUsage>> LoadWebsiteUsageAsync() => Task.FromResult<IEnumerable<WebsiteUsage>>(new List<WebsiteUsage>());
        Task IDataService.SaveWebsiteUsageAsync(IEnumerable<WebsiteUsage> usage) => Task.CompletedTask;

        // Phase 3 methods
        public async Task<IEnumerable<ActivitySegment>> GetSegmentsAsync(DateTime from, DateTime to)
        {
            var segments = new List<ActivitySegment>();
            using var connection = _dbContext.CreateConnection();
            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT Id, ProcessName, WindowTitle, Status, StartTime, EndTime 
                FROM ActivityLog 
                WHERE StartTime >= $from AND StartTime <= $to
                ORDER BY StartTime ASC
            ";
            command.Parameters.AddWithValue("$from", from.ToString("o"));
            command.Parameters.AddWithValue("$to", to.ToString("o"));

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
                    EndTime = DateTime.Parse(reader.GetString(5), null, DateTimeStyles.RoundtripKind)
                });
            }
            return segments;
        }

        public async Task<Dictionary<string, TimeSpan>> GetTotalTimeByProcessAsync(DateTime from, DateTime to, int? statusFilter = null)
        {
            var result = new Dictionary<string, TimeSpan>();
            using var connection = _dbContext.CreateConnection();
            using var command = connection.CreateCommand();
            
            string statusClause = statusFilter.HasValue ? "AND Status = $status" : "";
            command.CommandText = $@"
                SELECT ProcessName, StartTime, EndTime 
                FROM ActivityLog 
                WHERE StartTime >= $from AND StartTime <= $to {statusClause}
            ";
            command.Parameters.AddWithValue("$from", from.ToString("o"));
            command.Parameters.AddWithValue("$to", to.ToString("o"));
            if (statusFilter.HasValue) command.Parameters.AddWithValue("$status", statusFilter.Value);

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                string proc = reader.GetString(0);
                DateTime start = DateTime.Parse(reader.GetString(1), null, DateTimeStyles.RoundtripKind);
                DateTime end = DateTime.Parse(reader.GetString(2), null, DateTimeStyles.RoundtripKind);
                
                TimeSpan duration = end - start;
                if (result.ContainsKey(proc)) result[proc] += duration;
                else result[proc] = duration;
            }
            return result;
        }
    }
}
