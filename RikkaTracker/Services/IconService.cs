using System;
using System.Collections.Concurrent;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RikkaTracker.Core.Monitor;

namespace RikkaTracker.Services
{
    public class IconService : IIconService
    {
        private readonly ConcurrentDictionary<string, ImageSource> _iconCache = new();
        private static readonly ImageSource _defaultIcon = CreateDefaultIcon();

        public ImageSource? GetIcon(string processName, string processPath)
        {
            if (string.IsNullOrEmpty(processName)) return _defaultIcon;

            // Try cache by process name
            if (_iconCache.TryGetValue(processName, out var cachedIcon))
                return cachedIcon;

            // Try extract from path
            if (!string.IsNullOrEmpty(processPath) && File.Exists(processPath))
            {
                var icon = ExtractIconFromPath(processPath);
                if (icon != null)
                {
                    _iconCache[processName] = icon;
                    return icon;
                }
            }

            // Fallback: try to find the process path if currently running
            // (Note: This is already done in AppActivityTracker for new events, 
            // but for historical data it might be missing)
            
            return _defaultIcon;
        }

        private static ImageSource? ExtractIconFromPath(string path)
        {
            try
            {
                var shfi = new Win32Api.SHFILEINFO();
                var res = Win32Api.SHGetFileInfo(
                    path, 
                    0, 
                    ref shfi, 
                    (uint)Marshal.SizeOf(shfi), 
                    Win32Api.SHGFI_ICON | Win32Api.SHGFI_SMALLICON);

                if (res != IntPtr.Zero && shfi.hIcon != IntPtr.Zero)
                {
                    try
                    {
                        var bitmapSource = Imaging.CreateBitmapSourceFromHIcon(
                            shfi.hIcon,
                            Int32Rect.Empty,
                            BitmapSizeOptions.FromEmptyOptions());
                        
                        bitmapSource.Freeze(); // Make it cross-thread accessible
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
                System.Diagnostics.Debug.WriteLine($"Icon extraction failed for {path}: {ex.Message}");
            }
            return null;
        }

        private static ImageSource CreateDefaultIcon()
        {
            // Create a simple generic app icon (a blue square with a letter 'A')
            var drawingVisual = new DrawingVisual();
            using (var dc = drawingVisual.RenderOpen())
            {
                dc.DrawRoundedRectangle(Brushes.DeepSkyBlue, null, new Rect(0, 0, 16, 16), 2, 2);
                dc.DrawText(
                    new FormattedText("A", 
                        System.Globalization.CultureInfo.InvariantCulture, 
                        FlowDirection.LeftToRight, 
                        new Typeface("Segoe UI"), 10, Brushes.White, 1.0),
                    new Point(4, 1));
            }
            var renderTargetBitmap = new RenderTargetBitmap(16, 16, 96, 96, PixelFormats.Pbgra32);
            renderTargetBitmap.Render(drawingVisual);
            renderTargetBitmap.Freeze();
            return renderTargetBitmap;
        }
    }
}
