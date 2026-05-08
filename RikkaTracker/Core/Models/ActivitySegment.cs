using System;
using RikkaTracker.Core.Monitor;

namespace RikkaTracker.Core.Models
{
    public class ActivitySegment
    {
        public int Id { get; set; }
        public string ProcessName { get; set; } = string.Empty;
        public string WindowTitle { get; set; } = string.Empty;
        public ActivityStatus Status { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }

        public TimeSpan Duration => EndTime - StartTime;
    }
}
