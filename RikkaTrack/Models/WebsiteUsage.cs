using System;

namespace RikkaTrack.Models
{
    public class WebsiteUsage
    {
        public string Url { get; set; } = string.Empty;
        public string Domain { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string BrowserName { get; set; } = string.Empty; // Chrome, Edge, etc.
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public TimeSpan Duration => EndTime - StartTime;
    }
}
