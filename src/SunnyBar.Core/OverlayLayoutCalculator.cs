namespace SunnyBar.Core;

public readonly record struct PixelRect(int Left, int Top, int Right, int Bottom)
{
    public int Width => Right - Left;
    public int Height => Bottom - Top;
}

/// <summary>Physical pixel placement; DIP dimensions scale once using the target window DPI.</summary>
public static class OverlayLayoutCalculator
{
    public const double WidthRatio = 1;
    public const double HeightDip = 32;
    public const double BarHeightDip = 16;
    public const double GapDip = 0;

    public static PixelRect Calculate(PixelRect workArea, int dpi, AppearanceSettings? appearance = null)
    {
        if (workArea.Width <= 0 || workArea.Height <= 0)
            throw new ArgumentException("Monitor work area must have positive dimensions.", nameof(workArea));
        if (dpi <= 0) throw new ArgumentOutOfRangeException(nameof(dpi));
        appearance ??= new();
        var validation = SettingsValidator.Validate(new AppSettings { Appearance = appearance });
        if (validation.Count > 0) throw new SettingsValidationException(validation);
        var scale = dpi / 96.0;
        var width = Math.Max(1, (int)Math.Round(workArea.Width * appearance.WidthRatio));
        var totalHeight = 16 + appearance.BarHeightDip + (appearance.ShowHourTicks ? 16 : 0);
        var height = Math.Min(workArea.Height, Math.Max(1, (int)Math.Round(totalHeight * scale)));
        var gap = Math.Min(workArea.Height - height, (int)Math.Round(appearance.OffsetFromTaskbarDip * scale));
        var left = workArea.Left + (workArea.Width - width) / 2;
        var top = workArea.Bottom - gap - height;
        return new PixelRect(left, top, left + width, top + height);
    }
}
