using System;
using System.Collections.Generic;

namespace RikkaTracker.Models
{
    public class AppConfig
    {
        public int IdleTimeoutMinutes { get; set; } = 5;
        public bool StartWithWindows { get; set; } = false;
        public string DataStoragePath { get; set; } = string.Empty;
        public bool GitSyncEnabled { get; set; } = false;
        public string GitRepositoryPath { get; set; } = string.Empty;
        public string Theme { get; set; } = "Dark"; // "Light" or "Dark"
        public string TimelineZoomMode { get; set; } = "Center"; // "Center" or "Latest"
        public List<FilterRule> FilterRules { get; set; } = new List<FilterRule>();
    }

    public class FilterRule
    {
        public string ProcessPattern { get; set; } = string.Empty;
        public bool Ignore { get; set; } = false;
        public bool DisableIdleDetection { get; set; } = false;
    }
}
