using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Media.Imaging;

namespace digital_wellbeing_app.Services
{
    public class AppIconService
    {
        // Static icon cache: avoids re-extracting icons from disk on every UI refresh
        private static readonly Dictionary<string, BitmapImage?> _iconCache = new(StringComparer.OrdinalIgnoreCase);
        private static readonly object _cacheLock = new();

        public static BitmapImage? GetIconForExe(string exePath)
        {
            if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
                return null;

            lock (_cacheLock)
            {
                if (_iconCache.TryGetValue(exePath, out var cached))
                    return cached;
            }

            BitmapImage? result = null;
            try
            {
                using Icon? icon = Icon.ExtractAssociatedIcon(exePath);
                if (icon == null) return null;

                using var bmp = icon.ToBitmap();
                using var ms = new MemoryStream();
                bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                ms.Position = 0;

                var image = new BitmapImage();
                image.BeginInit();
                image.StreamSource = ms;
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.EndInit();
                image.Freeze(); // Make cross-thread safe and improve performance
                result = image;
            }
            catch
            {
                // Icon extraction failed - cache null so we don't retry every refresh
            }

            lock (_cacheLock)
            {
                _iconCache[exePath] = result;
            }

            return result;
        }

        /// <summary>
        /// Instance method for getting app icon (for compatibility)
        /// </summary>
        public BitmapImage? GetIconForApp(string exePath)
        {
            return GetIconForExe(exePath);
        }
    }
}
