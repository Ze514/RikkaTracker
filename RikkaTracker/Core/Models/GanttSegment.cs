using System;
using RikkaTracker.Core.Monitor;

namespace RikkaTracker.Core.Models
{
    public class GanttSegment
    {
        public string ProcessName { get; set; } = string.Empty;
        public string WindowTitle { get; set; } = string.Empty;
        public ActivityStatus Status { get; set; }
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public int RowIndex { get; set; }
        
        public TimeSpan Duration => End - Start;
    }
}
