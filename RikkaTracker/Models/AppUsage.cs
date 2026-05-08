using System;

namespace RikkaTracker.Models
{
    public class AppUsage
    {
        public string ProcessName { get; set; } = string.Empty;
        public string AppTitle { get; set; } = string.Empty;
        public string? ExecutablePath { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public TimeSpan Duration => EndTime - StartTime;
        
        // 用于图标显示的辅助信息，或者存放在本地缓存
        public string? IconPath { get; set; }
    }
}
