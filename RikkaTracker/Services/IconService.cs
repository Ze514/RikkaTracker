using System.Collections.Concurrent;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RikkaTracker.Core.Monitor;
using System.Diagnostics;

namespace RikkaTracker.Services
{
    public class IconService : IIconService
    {
        private readonly ConcurrentDictionary<string, ImageSource> _iconCache = new();
        private static readonly ImageSource _defaultIcon = CreateDefaultIcon();

        public ImageSource? GetIcon(string processName, string processPath)
        {
            if (string.IsNullOrEmpty(processName)) return _defaultIcon;

            // Use processPath as primary cache key if available, otherwise use processName
            string cacheKey = !string.IsNullOrEmpty(processPath) ? processPath.ToLowerInvariant() : processName.ToLowerInvariant();

            if (_iconCache.TryGetValue(cacheKey, out var cachedIcon))
                return cachedIcon;

            // Try extract from path
            if (!string.IsNullOrEmpty(processPath))
            {
                var icon = ExtractIconFromPath(processPath);
                if (icon != null)
                {
                    _iconCache[cacheKey] = icon;
                    return icon;
                }
            }
            else
            {
                // Fallback: If path is empty, try to find a running process with this name
                try
                {
                    var processes = Process.GetProcessesByName(processName);
                    if (processes.Length > 0)
                    {
                        var pid = processes[0].Id;
                        string resolvedPath = Win32Api.GetProcessPath(pid);
                        if (!string.IsNullOrEmpty(resolvedPath))
                        {
                            var icon = ExtractIconFromPath(resolvedPath);
                            if (icon != null)
                            {
                                _iconCache[cacheKey] = icon;
                                return icon;
                            }
                        }
                    }
                }
                catch { /* Ignore */ }
            }
            
            return _defaultIcon;
        }
    
        private static ImageSource? ExtractIconFromPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;

            try
            {
                // 1. Handle UWP apps (Check if in WindowsApps)
                if (path.Contains("WindowsApps", StringComparison.OrdinalIgnoreCase))
                {
                    var uwpIcon = ExtractUwpIcon(path);
                    if (uwpIcon != null) return uwpIcon;
                }

                // 2. Try ExtractAssociatedIcon (Tai's method, very reliable for standard Win32)
                if (File.Exists(path))
                {
                    try
                    {
                        using (var ico = Icon.ExtractAssociatedIcon(path))
                        {
                            if (ico != null)
                            {
                                var bitmapSource = Imaging.CreateBitmapSourceFromHIcon(
                                    ico.Handle,
                                    Int32Rect.Empty,
                                    BitmapSizeOptions.FromEmptyOptions());
                                
                                bitmapSource.Freeze();
                                return bitmapSource;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"ExtractAssociatedIcon failed for {path}: {ex.Message}");
                    }
                }

                // 3. Fallback to SHGetFileInfo
                var shfi = new Win32Api.SHFILEINFO();
                var res = Win32Api.SHGetFileInfo(
                    path, 
                    0, 
                    ref shfi, 
                    (uint)Marshal.SizeOf(shfi), 
                    Win32Api.SHGFI_ICON | Win32Api.SHGFI_LARGEICON);

                if (res != IntPtr.Zero && shfi.hIcon != IntPtr.Zero)
                {
                    try
                    {
                        var bitmapSource = Imaging.CreateBitmapSourceFromHIcon(
                            shfi.hIcon,
                            Int32Rect.Empty,
                            BitmapSizeOptions.FromEmptyOptions());
                        
                        bitmapSource.Freeze();
                        return bitmapSource;
                    }
                    finally
                    {
                        Win32Api.DestroyIcon(shfi.hIcon);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Icon extraction failed for {path}: {ex.Message}");
            }
            return null;
        }

        private static ImageSource? ExtractUwpIcon(string exePath)
        {
            try
            {
                // Similar to Tai's logic
                string appDir = exePath.Substring(0, exePath.Length - exePath.Split('\\').Last().Length);
                string manifestPath = Path.Combine(appDir, "AppxManifest.xml");
                
                if (!File.Exists(manifestPath))
                {
                    appDir = Path.GetDirectoryName(appDir) ?? "";
                    manifestPath = Path.Combine(appDir, "AppxManifest.xml");
                }

                if (File.Exists(manifestPath))
                {
                    string manifestText = File.ReadAllText(manifestPath);
                    var match = Regex.Match(manifestText, @"<Logo>(.*?)</Logo>");
                    string logoName = match.Success ? match.Groups[1].Value : "";

                    if (string.IsNullOrEmpty(logoName))
                    {
                        match = Regex.Match(manifestText, @"Square44x44Logo=""(.*?)""");
                        if (match.Success) logoName = match.Groups[1].Value;
                    }

                    if (!string.IsNullOrEmpty(logoName))
                    {
                        logoName = logoName.TrimStart('\\', '/');
                        
                        // Tai's ordered scale checking
                        string[] scales = { ".scale-200.png", ".scale-150.png", ".scale-125.png", ".scale-100.png", ".scale-400.png" };
                        string logoBase = logoName.EndsWith(".png", StringComparison.OrdinalIgnoreCase) 
                            ? logoName.Substring(0, logoName.Length - 4) 
                            : logoName;

                        string iconFile = string.Empty;
                        foreach (var scale in scales)
                        {
                            string testPath = Path.Combine(appDir, logoBase + scale);
                            if (File.Exists(testPath))
                            {
                                iconFile = testPath;
                                break;
                            }
                        }

                        // If no scaled version found, try the direct path
                        if (string.IsNullOrEmpty(iconFile))
                        {
                            string directPath = Path.Combine(appDir, logoName);
                            if (File.Exists(directPath)) iconFile = directPath;
                        }

                        if (!string.IsNullOrEmpty(iconFile) && File.Exists(iconFile))
                        {
                            var bitmap = new BitmapImage();
                            bitmap.BeginInit();
                            bitmap.UriSource = new Uri(iconFile);
                            bitmap.CacheOption = BitmapCacheOption.OnLoad;
                            bitmap.EndInit();
                            bitmap.Freeze();
                            return bitmap;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"UWP icon extraction failed: {ex.Message}");
            }
            return null;
        }

        private static ImageSource CreateDefaultIcon()
        {
            // Create a simple generic app icon (a blue square with a letter 'A')
            var drawingVisual = new DrawingVisual();
            using (var dc = drawingVisual.RenderOpen())
            {
                dc.DrawRoundedRectangle(System.Windows.Media.Brushes.DeepSkyBlue, null, new System.Windows.Rect(0, 0, 16, 16), 2, 2);
                dc.DrawText(
                    new FormattedText("A", 
                        System.Globalization.CultureInfo.InvariantCulture, 
                        FlowDirection.LeftToRight, 
                        new Typeface("Segoe UI"), 10, System.Windows.Media.Brushes.White, 1.0),
                    new System.Windows.Point(4, 1));
            }
            var renderTargetBitmap = new RenderTargetBitmap(16, 16, 96, 96, PixelFormats.Pbgra32);
            renderTargetBitmap.Render(drawingVisual);
            renderTargetBitmap.Freeze();
            return renderTargetBitmap;
        }
    }
}
