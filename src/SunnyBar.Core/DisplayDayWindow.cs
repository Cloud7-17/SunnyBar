namespace SunnyBar.Core;

/// <summary>A half-open interval [Start, End), named by its first local calendar date.</summary>
public sealed record DisplayDayWindow
{
    public DateOnly Date { get; }
    public DateTimeOffset Start { get; }
    public DateTimeOffset End { get; }

    public DisplayDayWindow(DateOnly date, DateTimeOffset start, DateTimeOffset end)
    {
        if (end <= start)
            throw new ArgumentException("Display-day end must be later than start.", nameof(end));
        Date = date;
        Start = start;
        End = end;
    }
}
