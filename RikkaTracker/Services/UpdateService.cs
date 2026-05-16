using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace RikkaTracker.Services
{
    public interface IUpdateService
    {
        Task<UpdateCheckResult> CheckForUpdatesAsync();
        Task DownloadAndInstallAsync(UpdateCheckResult updateInfo, Action<double> progressCallback = null);
        string GetCurrentVersion();
    }

    public class UpdateCheckResult
    {
        public bool HasUpdate { get; set; }
        public string LatestVersion { get; set; } = string.Empty;
        public string ReleaseNotes { get; set; } = string.Empty;
        public string DownloadUrl { get; set; } = string.Empty;
    }

    public class UpdateService : IUpdateService
    {
        private const string Owner = "remnant-song";
        private const string Repo = "RikkaTrack";
        private readonly HttpClient _httpClient;

        public UpdateService()
        {
            _httpClient = new HttpClient();
            // GitHub API requires User-Agent
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "RikkaTracker-Updater");
        }

        public string GetCurrentVersion()
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            return version?.ToString(3) ?? "1.0.0";
        }

        public async Task<UpdateCheckResult> CheckForUpdatesAsync()
        {
            try
            {
                string url = $"https://api.github.com/repos/{Owner}/{Repo}/releases/latest";
                string json = await _httpClient.GetStringAsync(url);
                var release = JsonConvert.DeserializeObject<JObject>(json);

                if (release == null) return new UpdateCheckResult { HasUpdate = false };

                string tagName = release["tag_name"]?.ToString() ?? "v1.0.0";
                string latestVersionStr = tagName.TrimStart('v');
                
                Version currentVersion = new Version(GetCurrentVersion());
                Version latestVersion = new Version(latestVersionStr);

                if (latestVersion > currentVersion)
                {
                    var result = new UpdateCheckResult
                    {
                        HasUpdate = true,
                        LatestVersion = latestVersionStr,
                        ReleaseNotes = release["body"]?.ToString() ?? string.Empty
                    };

                    var assets = release["assets"] as JArray;
                    if (assets != null)
                    {
                        foreach (var asset in assets)
                        {
                            string name = asset["name"]?.ToString() ?? "";
                            string downloadUrl = asset["browser_download_url"]?.ToString() ?? "";

                            // 优先寻找 .exe 以进行直接替换更新
                            if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                            {
                                result.DownloadUrl = downloadUrl;
                                break;
                            }
                            else if (name.EndsWith(".msi", StringComparison.OrdinalIgnoreCase))
                            {
                                result.DownloadUrl = downloadUrl;
                            }
                        }
                    }

                    return result;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Update check failed: {ex.Message}");
            }

            return new UpdateCheckResult { HasUpdate = false };
        }

        public async Task DownloadAndInstallAsync(UpdateCheckResult updateInfo, Action<double> progressCallback = null)
        {
            if (string.IsNullOrEmpty(updateInfo.DownloadUrl)) return;

            string extension = Path.GetExtension(updateInfo.DownloadUrl).ToLower();
            string tempPath = Path.Combine(Path.GetTempPath(), $"RikkaTracker-Update-{updateInfo.LatestVersion}{extension}");

            using (var response = await _httpClient.GetAsync(updateInfo.DownloadUrl, HttpCompletionOption.ResponseHeadersRead))
            {
                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                var canReportProgress = totalBytes != -1 && progressCallback != null;

                using (var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                using (var downloadStream = await response.Content.ReadAsStreamAsync())
                {
                    var buffer = new byte[8192];
                    var totalRead = 0L;
                    int read;

                    while ((read = await downloadStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        await fileStream.WriteAsync(buffer, 0, read);
                        totalRead += read;

                        if (canReportProgress)
                        {
                            progressCallback!((double)totalRead / totalBytes);
                        }
                    }
                }
            }

            if (extension == ".msi")
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "msiexec.exe",
                    Arguments = $"/i \"{tempPath}\" /passive",
                    UseShellExecute = true
                });
            }
            else if (extension == ".exe")
            {
                string currentExePath = Process.GetCurrentProcess().MainModule.FileName;
                string scriptPath = Path.Combine(Path.GetTempPath(), "rikka_update.bat");

                // 创建一个简单的批处理脚本来替换正在运行的 EXE
                string script = $@"
@echo off
timeout /t 1 /nobreak > nul
del /f /q ""{currentExePath}""
move /y ""{tempPath}"" ""{currentExePath}""
start """" ""{currentExePath}""
del ""%~f0""
";
                File.WriteAllText(scriptPath, script);

                Process.Start(new ProcessStartInfo
                {
                    FileName = scriptPath,
                    CreateNoWindow = true,
                    UseShellExecute = true
                });
            }

            System.Windows.Application.Current.Shutdown();
        }
    }
}
