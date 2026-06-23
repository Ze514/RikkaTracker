using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using RikkaTracker.Services;

namespace RikkaTracker.Core.Librarys
{
    public static class FaviconDownloader
    {
        private static readonly HttpClient _httpClient = new()
        {
            Timeout = TimeSpan.FromSeconds(10)
        };

        public static async Task<string> DownloadAsync(string faviconUrl, string domain, string dataStoragePath)
        {
            Debug.WriteLine($"[Favicon] DownloadAsync called: url='{faviconUrl}', domain='{domain}'");
            if (string.IsNullOrWhiteSpace(faviconUrl) || string.IsNullOrWhiteSpace(domain))
            {
                Debug.WriteLine("[Favicon] Skipped: empty url or domain");
                return string.Empty;
            }

            string dir = Path.Combine(dataStoragePath, "WebFavicons");
            string filePath = Path.Combine(dir, $"{domain}.ico");

            if (File.Exists(filePath))
            {
                Debug.WriteLine($"[Favicon] Already cached: {filePath}");
                return filePath;
            }

            string downloadUrl = faviconUrl
                .Replace(".svg", ".png")
                .Replace(".SVG", ".png");

            Debug.WriteLine($"[Favicon] Downloading: {downloadUrl} -> {filePath}");

            try
            {
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var response = await _httpClient.GetAsync(downloadUrl);
                response.EnsureSuccessStatusCode();

                await using var fs = new FileStream(filePath, FileMode.Create);
                await response.Content.CopyToAsync(fs);

                Debug.WriteLine($"[Favicon] Downloaded OK: {filePath} ({new FileInfo(filePath).Length} bytes)");
                return filePath;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Favicon] Download failed: {ex.Message}");
                return string.Empty;
            }
        }
    }
}
