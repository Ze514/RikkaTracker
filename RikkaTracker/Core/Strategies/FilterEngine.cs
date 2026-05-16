using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using RikkaTracker.Models;
using RikkaTracker.Services;

namespace RikkaTracker.Core.Strategies
{
    public interface IFilterEngine
    {
        bool ShouldIgnore(string processName);
        bool ShouldDisableIdleDetection(string processName);
        void Reload();
    }

    public class FilterEngine : IFilterEngine
    {
        private readonly IConfigService _configService;
        private List<(Regex Pattern, FilterRule Rule)> _compiledRules = new();

        public FilterEngine(IConfigService configService)
        {
            _configService = configService;
            Reload();
        }

        public void Reload()
        {
            var rules = _configService.Config.FilterRules;

            _compiledRules = rules.Select(r => (
                Pattern: new Regex("^" + Regex.Escape(r.ProcessPattern).Replace("\\*", ".*") + "$", RegexOptions.IgnoreCase),
                Rule: r
            )).ToList();
        }

        public bool ShouldIgnore(string processName)
        {
            if (string.IsNullOrEmpty(processName)) return false;
            return _compiledRules.Any(r => r.Rule.Ignore && r.Pattern.IsMatch(processName));
        }

        public bool ShouldDisableIdleDetection(string processName)
        {
            if (string.IsNullOrEmpty(processName)) return false;
            return _compiledRules.Any(r => r.Rule.DisableIdleDetection && r.Pattern.IsMatch(processName));
        }
    }
}
