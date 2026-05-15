using System;
using System.IO;
using Microsoft.Data.Sqlite;
using RikkaTracker.Services;

namespace RikkaTracker.Core.Data
{
    public class SqliteDbContext
    {
        private readonly string _connectionString;
        private readonly string _dbPath;

        public SqliteDbContext(IConfigService configService)
        {
            _dbPath = Path.Combine(configService.Config.DataStoragePath, "tracker.db");
            var directory = Path.GetDirectoryName(_dbPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            _connectionString = $"Data Source={_dbPath};";
            InitializeDatabase();
        }

        private void InitializeDatabase()
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            // Performance optimizations
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
                    PRAGMA journal_mode=WAL;
                    PRAGMA synchronous=NORMAL;
                    PRAGMA temp_store=MEMORY;
                ";
                command.ExecuteNonQuery();
            }

            // Create table
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
                    CREATE TABLE IF NOT EXISTS ActivityLog (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        ProcessName TEXT NOT NULL,
                        ProcessPath TEXT,
                        WindowTitle TEXT,
                        Alias TEXT,
                        Status INTEGER NOT NULL,
                        StartTime TEXT NOT NULL,
                        EndTime TEXT NOT NULL
                    );
                    CREATE INDEX IF NOT EXISTS IX_Log_Start ON ActivityLog(StartTime);
                    CREATE INDEX IF NOT EXISTS IX_Log_Process_Start ON ActivityLog(ProcessName, StartTime);
                    CREATE INDEX IF NOT EXISTS IX_Log_Process_Path_Start ON ActivityLog(ProcessPath, StartTime);
                ";
                command.ExecuteNonQuery();
            }
        }

        public SqliteConnection CreateConnection()
        {
            var connection = new SqliteConnection(_connectionString);
            connection.Open();
            return connection;
        }
    }
}
