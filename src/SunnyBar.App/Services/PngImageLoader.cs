using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Media.Imaging;

namespace SunnyBar.App.Services;

public static class PngImageLoader
{
    public static BitmapSource? Load(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            if (stream.Length > 5 * 1024 * 1024) return null;
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.DecodePixelWidth = 64;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException
            or NotSupportedException or COMException or FormatException) { return null; }
    }
}
