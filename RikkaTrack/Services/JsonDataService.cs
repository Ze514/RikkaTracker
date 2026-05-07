using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using RikkaTrack.Models;

namespace RikkaTrack.Services
{
    public class JsonDataService : IDataService
    {
        private readonly string _dataDirectory;
        private readonly string _appUsageFile;
        private readonly string _webUsageFile;

        public JsonDataService()
        {
            _dataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RikkaTrack", "Data");
            if (!Directory.Exists(_dataDirectory))
            {
                Directory.CreateDirectory(_dataDirectory);
            }

            _appUsageFile = Path.Combine(_dataDirectory, "app_usage.json");
            _webUsageFile = Path.Combine(_dataDirectory, "web_usage.json");
        }

        public async Task SaveAppUsageAsync(IEnumerable<AppUsage> usage)
        {
            var json = JsonConvert.SerializeObject(usage, Formatting.Indented);
            await File.WriteAllTextAsync(_appUsageFile, json);
        }

        public async Task<IEnumerable<AppUsage>> LoadAppUsageAsync()
        {
            if (!File.Exists(_appUsageFile)) return new List<AppUsage>();
            var json = await File.ReadAllTextAsync(_appUsageFile);
            return JsonConvert.DeserializeObject<List<AppUsage>>(json) ?? new List<AppUsage>();
        }

        public async Task SaveWebsiteUsageAsync(IEnumerable<WebsiteUsage> usage)
        {
            var json = JsonConvert.SerializeObject(usage, Formatting.Indented);
            await File.WriteAllTextAsync(_webUsageFile, json);
        }

        public async Task<IEnumerable<WebsiteUsage>> LoadWebsiteUsageAsync()
        {
            if (!File.Exists(_webUsageFile)) return new List<WebsiteUsage>();
            var json = await File.ReadAllTextAsync(_webUsageFile);
            return JsonConvert.DeserializeObject<List<WebsiteUsage>>(json) ?? new List<WebsiteUsage>();
        }
    }
}
