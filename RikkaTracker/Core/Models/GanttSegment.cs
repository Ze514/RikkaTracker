using System;
using RikkaTracker.Core.Monitor;

namespace RikkaTracker.Core.Models
{
    public class GanttSegment
    {
        public string ProcessName { get; set; } = string.Empty;
        public string ProcessPath { get; set; } = string.Empty;
        public string WindowTitle { get; set; } = string.Empty;
        public string Alias { get; set; } = string.Empty;
        public ActivityStatus Status { get; set; }
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public int RowIndex { get; set; }
        
        [System.Text.Json.Serialization.JsonIgnore]
        public System.Windows.Media.ImageSource? Icon { get; set; }

        public TimeSpan Duration => End - Start;
        public string DisplayName => !string.IsNullOrEmpty(Alias) ? Alias : ProcessName;
    }
}
