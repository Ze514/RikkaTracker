using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace RikkaTracker.Services
{
    public enum UpdateCheckStatus
    {
        NoUpdate,           // 已经是最新
        UpdateAvailable,    // 发现更新且资源就绪
        NetworkError,       // 网络错误
        AssetMissing,       // 发现新版本但没找到匹配的下载包
        InternalError       // 其他内部错误
    }

    public class UpdateCheckResult
    {
        public UpdateCheckStatus Status { get; set; } = UpdateCheckStatus.NoUpdate;
        public bool HasUpdate => Status == UpdateCheckStatus.UpdateAvailable;
        public string LatestVersion { get; set; } = string.Empty;
        public string ReleaseNotes { get; set; } = string.Empty;
        public string DownloadUrl { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
    }

    public interface IUpdateService
    {
        string GetCurrentVersion();
        Task<UpdateCheckResult> CheckForUpdatesAsync();
        Task DownloadAndInstallAsync(UpdateCheckResult updateInfo, Action<double, string> progressCallback = null);
    }

    public class UpdateService : IUpdateService
    {
        private const string Owner = "remnant-song";
        private const string Repo = "RikkaTrack";
        private readonly HttpClient _httpClient;
        private readonly ILoggerService _logger;

#if PORTABLE
        private const string TargetKeyword = "Portable";
#else
        private const string TargetKeyword = "Light";
#endif

        public UpdateService(ILoggerService logger)
        {
            _logger = logger;
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "RikkaTracker-Updater");
        }

        public string GetCurrentVersion()
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            return version?.ToString(3) ?? "1.0.0";
        }

        public async Task<UpdateCheckResult> CheckForUpdatesAsync()
        {
            _logger.Info($"Starting update check... Current version: {GetCurrentVersion()}");
            try
            {
                string url = $"https://api.github.com/repos/{Owner}/{Repo}/releases/latest";
                _logger.Info($"Requesting URL: {url}");
                
                string json = await _httpClient.GetStringAsync(url);
                _logger.Info("Received response from GitHub API.");
                
                var release = JsonConvert.DeserializeObject<JObject>(json);
                if (release == null) return new UpdateCheckResult { Status = UpdateCheckStatus.InternalError, ErrorMessage = "Invalid API response" };

                string tagName = release["tag_name"]?.ToString() ?? "v1.0.0";
                string latestVersionStr = tagName.TrimStart('v');
                _logger.Info($"Latest version on GitHub: {latestVersionStr}");
                
                Version currentVersion = new Version(GetCurrentVersion());
                Version latestVersion = new Version(latestVersionStr);

                if (latestVersion > currentVersion)
                {
                    _logger.Info("New version available! Checking assets...");
                    var result = new UpdateCheckResult
                    {
                        Status = UpdateCheckStatus.AssetMissing, // 默认先设为 AssetMissing，找到后再覆盖
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

                            if (name.Contains(TargetKeyword, StringComparison.OrdinalIgnoreCase) && 
                                name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                            {
                                _logger.Info($"Found matching asset: {name}");
                                result.DownloadUrl = downloadUrl;
                                result.Status = UpdateCheckStatus.UpdateAvailable;
                                break;
                            }
                        }
                    }

                    if (result.Status == UpdateCheckStatus.AssetMissing)
                    {
                        _logger.Warning($"Found version {latestVersionStr} but no {TargetKeyword} asset found.");
                    }

                    return result;
                }
                
                return new UpdateCheckResult { Status = UpdateCheckStatus.NoUpdate };
            }
            catch (HttpRequestException hex)
            {
                _logger.Error("Network error during update check.", hex);
                return new UpdateCheckResult { Status = UpdateCheckStatus.NetworkError, ErrorMessage = hex.Message };
            }
            catch (TaskCanceledException tex)
            {
                _logger.Error("Update check timed out.", tex);
                return new UpdateCheckResult { Status = UpdateCheckStatus.NetworkError, ErrorMessage = "Request timed out" };
            }
            catch (Exception ex)
            {
                _logger.Error("Unexpected error during update check.", ex);
                return new UpdateCheckResult { Status = UpdateCheckStatus.InternalError, ErrorMessage = ex.Message };
            }
        }

        public async Task DownloadAndInstallAsync(UpdateCheckResult updateInfo, Action<double, string> progressCallback = null)
        {
            if (string.IsNullOrEmpty(updateInfo.DownloadUrl)) 
            {
                _logger.Warning("DownloadAndInstallAsync called with empty URL.");
                throw new InvalidOperationException("Download URL is missing.");
            }

            // ... 其余逻辑保持不变 ...
            _logger.Info($"Starting download: {updateInfo.DownloadUrl}");
            string tempPath = Path.Combine(Path.GetTempPath(), $"RikkaTracker-Update-{updateInfo.LatestVersion}.exe");
            progressCallback?.Invoke(0,
                System.Windows.Application.Current.Resources["StrDownloadConnecting"] as string ?? "Connecting to server...");

            try
            {
                using (var response = await _httpClient.GetAsync(updateInfo.DownloadUrl, HttpCompletionOption.ResponseHeadersRead))
                {
                    response.EnsureSuccessStatusCode();
                    var totalBytes = response.Content.Headers.ContentLength ?? -1L;
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
                            if (totalBytes != -1)
                            {
                                double progress = (double)totalRead / totalBytes;
                                string? progressTemplate =
                                    System.Windows.Application.Current.Resources["StrDownloadProgress"] as string;
                                string template = string.IsNullOrEmpty(progressTemplate)
                                    ? "Downloading: {0:F2} MB / {1:F2} MB"
                                    : progressTemplate;
                                string status = string.Format(
                                    System.Globalization.CultureInfo.InvariantCulture,
                                    template,
                                    totalRead / 1024.0 / 1024.0,
                                    totalBytes / 1024.0 / 1024.0);
                                progressCallback?.Invoke(progress, status);
                            }
                        }
                    }
                }

                string currentExePath = Process.GetCurrentProcess().MainModule?.FileName;
                string scriptPath = Path.Combine(Path.GetTempPath(), "rikka_update.bat");
                string script = $@"
@echo off
setlocal
set ""target={currentExePath}""
set ""source={tempPath}""
:wait_loop
timeout /t 1 /nobreak > nul
del /f /q ""%target%"" > nul 2>&1
if exist ""%target%"" goto wait_loop
move /y ""%source%"" ""%target%""
start """" ""%target%""
del ""%~f0""
";
                File.WriteAllText(scriptPath, script);
                Process.Start(new ProcessStartInfo { FileName = "cmd.exe", Arguments = $"/c \"{scriptPath}\"", CreateNoWindow = true, UseShellExecute = true });
                System.Windows.Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                _logger.Error("Download failed.", ex);
                throw;
            }
        }
    }
}
