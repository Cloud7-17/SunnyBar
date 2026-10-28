namespace SunnyBar.Core;

public sealed record SchedulePreset(string Title, TimeOnly Start, TimeOnly End)
{
    public string TimeText => $"{Start:HH:mm}–{End:HH:mm}";
    public ScheduleEvent Create(DateTime date, TimeZoneInfo zone, string color) => new()
    {
        Title = Title, Color = color,
        StartAt = ScheduleTimeResolver.Resolve(date, Start.Hour.ToString(), Start.Minute.ToString(), zone),
        EndAt = ScheduleTimeResolver.Resolve(date, End.Hour.ToString(), End.Minute.ToString(), zone)
    };
}

public static class SchedulePresets
{
    public static IReadOnlyList<SchedulePreset> Classes { get; } = Array.AsReadOnly<SchedulePreset>(
    [
        new("上课", new(8, 0), new(9, 35)),
        new("上课", new(9, 50), new(11, 25)),
        new("上课", new(9, 50), new(12, 15)),
        new("上课", new(13, 30), new(15, 5)),
        new("上课", new(15, 20), new(16, 55)),
        new("上课", new(19, 20), new(20, 55)),
        new("上课", new(19, 20), new(21, 45))
    ]);
}

public static class ScheduleColors
{
    public static IReadOnlyList<string> Palette { get; } = Array.AsReadOnly<string>(
        ["#5AA9E6", "#44AA88", "#EE8844", "#AF8FDD", "#E86677", "#F2B544", "#5577CC", "#36A5A0", "#C475A6", "#8BA64D", "#A57951", "#718096"]);

    public static string Choose(IEnumerable<string> existing)
    {
        var used = existing.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var color in Palette) if (!used.Contains(color)) return color;
        // An odd stride visits distinct 21-bit values. Mid-range RGB channels remain visible on the bar.
        for (var index = 0; index <= used.Count; index++)
        {
            var value = (0x154A37L + index * 104729L) & 0x1fffff;
            var color = $"#{64 + (value & 127):X2}{64 + ((value >> 7) & 127):X2}{64 + ((value >> 14) & 127):X2}";
            if (!used.Contains(color)) return color;
        }
        throw new InvalidOperationException("没有可用的日程颜色。");
    }
}
