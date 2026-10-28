using System.Drawing;
using System.Windows;
using System.Windows.Media.Imaging;

namespace SunnyBar.App.Services;

public static class AppIcon
{
    private static readonly Uri Resource = new("pack://application:,,,/SunnyBar.App;component/Assets/SunnyBar.ico");
    public static BitmapFrame WindowImage { get; } = LoadImage();
    private static BitmapFrame LoadImage()
    {
        var decoder = BitmapDecoder.Create(Resource, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        var image = decoder.Frames.First(frame => frame.PixelWidth == 32);
        image.Freeze();
        return image;
    }
    public static Icon CreateTrayIcon()
    {
        using var stream = Application.GetResourceStream(Resource)!.Stream;
        using var icon = new Icon(stream, 32, 32);
        return (Icon)icon.Clone();
    }
}
