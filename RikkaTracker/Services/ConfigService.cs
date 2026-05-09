using System;
using System.IO;
using System.Text.Json;
using RikkaTracker.Models;

namespace RikkaTracker.Services
{
    public interface IConfigService
    {
        AppConfig Config { get; }
        void Save();
        void Load();
    }

    public class ConfigService : IConfigService
    {
        private readonly string _configFilePath;
        private AppConfig _config = new();

        public AppConfig Config => _config;

        public ConfigService()
        {
            // 使用应用安装目录下的 Config 文件夹，而不是 AppData
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string configDir = Path.Combine(baseDir, "Config");
            
            if (!Directory.Exists(configDir))
            {
                Directory.CreateDirectory(configDir);
            }
            _configFilePath = Path.Combine(configDir, "config.json");
            Load();
        }

        public void Load()
        {
            if (File.Exists(_configFilePath))
            {
                try
                {
                    string json = File.ReadAllText(_configFilePath);
                    _config = JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
                }
                catch
                {
                    _config = new AppConfig();
                }
            }
            else
            {
                _config = new AppConfig();
                // 默认数据路径设为安装目录下的 Data 文件夹
                _config.DataStoragePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
                Save();
            }
        }

        public void Save()
        {
            try
            {
                string json = JsonSerializer.Serialize(_config, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_configFilePath, json);
            }
            catch (Exception ex)
            {
                // TODO: Log error
                Console.WriteLine($"Failed to save config: {ex.Message}");
            }
        }
    }
}
