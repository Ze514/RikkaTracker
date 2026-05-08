using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using RikkaTracker.Core.Models;
using RikkaTracker.Core.Monitor;

namespace RikkaTracker.Core.Data
{
    public interface IActivityLogStore
    {
        void RecordTransition(string processName, string windowTitle, ActivityStatus newStatus, DateTime timestamp);
    }

    public class SqliteLogStore : IActivityLogStore, IDisposable
    {
        private readonly SqliteDbContext _dbContext;
        private readonly ConcurrentQueue<ActivitySegment> _writeQueue = new();
        private readonly System.Threading.Timer _flushTimer;
        private ActivitySegment? _currentSegment;
        private readonly object _syncLock = new();

        public SqliteLogStore(SqliteDbContext dbContext)
        {
            _dbContext = dbContext;
            _flushTimer = new System.Threading.Timer(async _ => await FlushQueueAsync(), null, 1000, 1000);
        }

        public void RecordTransition(string processName, string windowTitle, ActivityStatus newStatus, DateTime timestamp)
        {
            lock (_syncLock)
            {
                // 1. Close current segment if exists
                if (_currentSegment != null)
                {
                    _currentSegment.EndTime = timestamp;
                    QueueSegments(_currentSegment);
                }

                // 2. Start new segment
                _currentSegment = new ActivitySegment
                {
                    ProcessName = processName,
                    WindowTitle = windowTitle,
                    Status = newStatus,
                    StartTime = timestamp
                };
            }
        }

        private void QueueSegments(ActivitySegment segment)
        {
            // Task 3.2: Cross-day split logic
            if (segment.StartTime.Date != segment.EndTime.Date)
            {
                DateTime endOfDay = segment.StartTime.Date.AddDays(1).AddTicks(-1);
                
                // First part: Start to end of first day
                var firstPart = new ActivitySegment
                {
                    ProcessName = segment.ProcessName,
                    WindowTitle = segment.WindowTitle,
                    Status = segment.Status,
                    StartTime = segment.StartTime,
                    EndTime = endOfDay
                };
                _writeQueue.Enqueue(firstPart);

                // Second part: Start of second day to EndTime
                var secondPart = new ActivitySegment
                {
                    ProcessName = segment.ProcessName,
                    WindowTitle = segment.WindowTitle,
                    Status = segment.Status,
                    StartTime = segment.StartTime.Date.AddDays(1),
                    EndTime = segment.EndTime
                };
                // Recursively handle if it spans more than 2 days
                QueueSegments(secondPart);
            }
            else
            {
                if (segment.Duration.TotalMilliseconds > 100) // Ignore very short transitions
                {
                    _writeQueue.Enqueue(segment);
                }
            }
        }

        private async Task FlushQueueAsync()
        {
            if (_writeQueue.IsEmpty) return;

            var segmentsToWhite = new List<ActivitySegment>();
            while (_writeQueue.TryDequeue(out var segment))
            {
                segmentsToWhite.Add(segment);
            }

            if (segmentsToWhite.Count == 0) return;

            try
            {
                using var connection = _dbContext.CreateConnection();
                using var transaction = connection.BeginTransaction();

                foreach (var seg in segmentsToWhite)
                {
                    using var command = connection.CreateCommand();
                    command.Transaction = transaction;
                    command.CommandText = @"
                        INSERT INTO ActivityLog (ProcessName, WindowTitle, Status, StartTime, EndTime)
                        VALUES ($proc, $title, $status, $start, $end)
                    ";
                    command.Parameters.AddWithValue("$proc", seg.ProcessName);
                    command.Parameters.AddWithValue("$title", seg.WindowTitle ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue("$status", (int)seg.Status);
                    command.Parameters.AddWithValue("$start", seg.StartTime.ToString("o")); // ISO 8601
                    command.Parameters.AddWithValue("$end", seg.EndTime.ToString("o"));
                    await command.ExecuteNonQueryAsync();
                }

                await transaction.CommitAsync();
            }
            catch (Exception ex)
            {
                // TODO: Proper logging
                System.Diagnostics.Debug.WriteLine($"Failed to flush activity log: {ex.Message}");
            }
        }

        public void Dispose()
        {
            // Final flush
            lock (_syncLock)
            {
                if (_currentSegment != null)
                {
                    _currentSegment.EndTime = DateTime.Now;
                    QueueSegments(_currentSegment);
                }
            }
            _flushTimer.Dispose();
            FlushQueueAsync().GetAwaiter().GetResult();
        }
    }
}
