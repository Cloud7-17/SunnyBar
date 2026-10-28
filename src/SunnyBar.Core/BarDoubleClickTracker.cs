namespace SunnyBar.Core;

/// <summary>Recognizes only clicks delivered to the bar, using the OS double-click thresholds.</summary>
public sealed class BarDoubleClickTracker
{
    private (long Time, int X, int Y)? previous;
    public bool Register(long milliseconds, int x, int y, int maximumMilliseconds, int width, int height)
    {
        var match = previous is { } prior && milliseconds >= prior.Time && milliseconds - prior.Time <= maximumMilliseconds
            && Math.Abs((long)x - prior.X) <= width / 2.0 && Math.Abs((long)y - prior.Y) <= height / 2.0;
        previous = match ? null : (milliseconds, x, y);
        return match;
    }
}
