using System;
using System.Drawing;
using System.IO;
using System.Windows.Media.Imaging;

namespace digital_wellbeing_app.Services
{
    public static class AppIconService
    {
        public static BitmapImage? GetIconForExe(string exePath)
        {
            if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
                return null;

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
                return image;
            }
            catch
            {
                return null;
            }
        }
    }
}
