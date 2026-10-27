namespace SunnyBar.Core;

/// <summary>The single mapping path for every instant drawn on the timeline.</summary>
public sealed class TimeAxisMapper
{
    public double Map(DateTimeOffset instant, DisplayDayWindow window) =>
        Math.Clamp((instant - window.Start).Ticks / (double)(window.End - window.Start).Ticks, 0, 1);

    public double ToX(DateTimeOffset instant, DisplayDayWindow window, double barLeft, double barWidth)
    {
        if (!double.IsFinite(barLeft))
            throw new ArgumentOutOfRangeException(nameof(barLeft));
        if (!double.IsFinite(barWidth) || barWidth < 0)
            throw new ArgumentOutOfRangeException(nameof(barWidth));
        return barLeft + Map(instant, window) * barWidth;
    }
}
