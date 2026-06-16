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
        public string Language { get; set; } = "Auto"; // "Auto", "zh-CN", "en-US"
        public bool WebMonitorEnabled { get; set; } = true;
        public int WebSocketPort { get; set; } = 8910;
        public string DisplayMode { get; set; } = "Combined";
        public List<FilterRule> FilterRules { get; set; } = new List<FilterRule>
        {
            // 默认免空闲检测（多媒体应用）
            new FilterRule { ProcessPattern = "vlc*", DisableIdleDetection = true },
            new FilterRule { ProcessPattern = "spotify*", DisableIdleDetection = true },
            new FilterRule { ProcessPattern = "wmplayer*", DisableIdleDetection = true },
            // 默认忽略（系统组件）
            new FilterRule { ProcessPattern = "ShellExperienceHost", Ignore = true }
        };
    }

    public class FilterRule
    {
        public string ProcessPattern { get; set; } = string.Empty;
        public bool Ignore { get; set; } = false;
        public bool DisableIdleDetection { get; set; } = false;
    }
}
