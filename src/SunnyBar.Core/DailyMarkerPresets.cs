namespace SunnyBar.Core;

public static class DailyMarkerPresets
{
    public static IReadOnlyList<DailyMarkerSettings> All { get; } = Array.AsReadOnly<DailyMarkerSettings>(
    [
        new() { Label = "上班", Time = "10:00", Color = "#5AA9E6" },
        new() { Label = "午饭", Time = "12:00", Color = "#44AA88" },
        new() { Label = "晚饭", Time = "18:00", Color = "#EE8844" },
        new() { Label = "睡觉", Time = "23:00", Color = "#AF8FDD" }
    ]);
}
