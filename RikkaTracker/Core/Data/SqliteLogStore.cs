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
        void RecordTransition(string processName, string processPath, string windowTitle, ActivityStatus newStatus, DateTime timestamp, string alias = "");
        /// <summary>
        /// 进程退出时调用，闭合该进程的开放segment并停止记录
        /// </summary>
        void CloseProcess(string processName, DateTime timestamp);
    }

    public class SqliteLogStore : IActivityLogStore, IDisposable
    {
        private readonly SqliteDbContext _dbContext;
        private readonly ConcurrentQueue<ActivitySegment> _writeQueue = new();
        private readonly System.Threading.Timer _flushTimer;
        // 按进程名维护各自的开放segment，互不干扰
        private readonly Dictionary<string, ActivitySegment> _openSegments = new();
        private readonly object _syncLock = new();

        public SqliteLogStore(SqliteDbContext dbContext)
        {
            _dbContext = dbContext;
            _flushTimer = new System.Threading.Timer(async _ => await FlushQueueAsync(), null, 1000, 1000);
        }

        public void RecordTransition(string processName, string processPath, string windowTitle, ActivityStatus newStatus, DateTime timestamp, string alias = "")
        {
            lock (_syncLock)
            {
                // 1. 仅关闭该进程的旧segment（其他进程不受影响）
                if (_openSegments.TryGetValue(processName, out var oldSeg))
                {
                    // 如果新状态与旧状态相同，无需操作（去重）
                    if (oldSeg.Status == newStatus)
                    {
                        // 仅更新窗口标题、路径和别名
                        if (!string.IsNullOrEmpty(windowTitle))
                        {
                            oldSeg.WindowTitle = windowTitle;
                        }
                        if (!string.IsNullOrEmpty(processPath))
                        {
                            oldSeg.ProcessPath = processPath;
                        }
                        if (!string.IsNullOrEmpty(alias))
                        {
                            oldSeg.Alias = alias;
                        }
                        return;
                    }

                    oldSeg.EndTime = timestamp;
                    QueueSegments(oldSeg);
                    _openSegments.Remove(processName);
                }

                // 2. 为该进程创建新的开放segment
                _openSegments[processName] = new ActivitySegment
                {
                    ProcessName = processName,
                    ProcessPath = !string.IsNullOrEmpty(processPath) ? processPath : oldSeg?.ProcessPath ?? string.Empty,
                    WindowTitle = !string.IsNullOrEmpty(windowTitle) ? windowTitle : oldSeg?.WindowTitle ?? string.Empty,
                    Alias = !string.IsNullOrEmpty(alias) ? alias : oldSeg?.Alias ?? string.Empty,
                    Status = newStatus,
                    StartTime = timestamp
                };
            }
        }

        public void CloseProcess(string processName, DateTime timestamp)
        {
            lock (_syncLock)
            {
                if (_openSegments.TryGetValue(processName, out var seg))
                {
                    seg.EndTime = timestamp;
                    QueueSegments(seg);
                    _openSegments.Remove(processName);
                }
            }
        }

        private void QueueSegments(ActivitySegment segment)
        {
            // 跨天拆分逻辑
            if (segment.StartTime.Date != segment.EndTime.Date)
            {
                DateTime endOfDay = segment.StartTime.Date.AddDays(1).AddTicks(-1);
                
                var firstPart = new ActivitySegment
                {
                    ProcessName = segment.ProcessName,
                    ProcessPath = segment.ProcessPath,
                    WindowTitle = segment.WindowTitle,
                    Alias = segment.Alias,
                    Status = segment.Status,
                    StartTime = segment.StartTime,
                    EndTime = endOfDay
                };
                _writeQueue.Enqueue(firstPart);

                var secondPart = new ActivitySegment
                {
                    ProcessName = segment.ProcessName,
                    ProcessPath = segment.ProcessPath,
                    WindowTitle = segment.WindowTitle,
                    Alias = segment.Alias,
                    Status = segment.Status,
                    StartTime = segment.StartTime.Date.AddDays(1),
                    EndTime = segment.EndTime
                };
                // 递归处理跨多天的情况
                QueueSegments(secondPart);
            }
            else
            {
                if (segment.Duration.TotalMilliseconds > 100) // 过滤极短暂的过渡
                {
                    _writeQueue.Enqueue(segment);
                }
            }
        }

        private async Task FlushQueueAsync()
        {
            if (_writeQueue.IsEmpty) return;

            var segmentsToWrite = new List<ActivitySegment>();
            while (_writeQueue.TryDequeue(out var segment))
            {
                segmentsToWrite.Add(segment);
            }

            if (segmentsToWrite.Count == 0) return;

            try
            {
                using var connection = _dbContext.CreateConnection();
                using var transaction = connection.BeginTransaction();

                foreach (var seg in segmentsToWrite)
                {
                    using var command = connection.CreateCommand();
                    command.Transaction = transaction;
                    command.CommandText = @"
                        INSERT INTO ActivityLog (ProcessName, ProcessPath, WindowTitle, Alias, Status, StartTime, EndTime)
                        VALUES ($proc, $path, $title, $alias, $status, $start, $end)
                    ";
                    command.Parameters.AddWithValue("$proc", seg.ProcessName);
                    command.Parameters.AddWithValue("$path", seg.ProcessPath ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue("$title", seg.WindowTitle ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue("$alias", seg.Alias ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue("$status", (int)seg.Status);
                    command.Parameters.AddWithValue("$start", seg.StartTime.ToString("o"));
                    command.Parameters.AddWithValue("$end", seg.EndTime.ToString("o"));
                    await command.ExecuteNonQueryAsync();
                }

                await transaction.CommitAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to flush activity log: {ex.Message}");
            }
        }

        public void Dispose()
        {
            // 闭合所有进程的开放segment
            lock (_syncLock)
            {
                var now = DateTime.Now;
                foreach (var seg in _openSegments.Values)
                {
                    seg.EndTime = now;
                    QueueSegments(seg);
                }
                _openSegments.Clear();
            }
            _flushTimer.Dispose();
            FlushQueueAsync().GetAwaiter().GetResult();
        }
    }
}
