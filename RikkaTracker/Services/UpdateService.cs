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

#if PORTABLE
        private const string TargetKeyword = "Portable";
#else
        private const string TargetKeyword = "Light";
#endif

        public UpdateService()
        {
            _httpClient = new HttpClient();
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

                            // 精准匹配当前版本类型（Portable 更新 Portable，Light 更新 Light）
                            if (name.Contains(TargetKeyword, StringComparison.OrdinalIgnoreCase) && 
                                name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                            {
                                result.DownloadUrl = downloadUrl;
                                break;
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

            string tempPath = Path.Combine(Path.GetTempPath(), $"RikkaTracker-Update-{updateInfo.LatestVersion}.exe");

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

            string currentExePath = Process.GetCurrentProcess().MainModule?.FileName;
            if (string.IsNullOrEmpty(currentExePath)) return;

            string scriptPath = Path.Combine(Path.GetTempPath(), "rikka_update.bat");

            // 创建增强版批处理脚本：
            // 1. 循环等待主进程彻底退出
            // 2. 覆盖替换
            // 3. 重新启动
            // 4. 自销毁
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

            Process.Start(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c \"{scriptPath}\"",
                CreateNoWindow = true,
                UseShellExecute = true
            });

            System.Windows.Application.Current.Shutdown();
        }
    }
}
