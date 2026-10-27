using System.Text.Json;
using System.Text.Json.Serialization;

namespace SunnyBar.Core;

public sealed record AppSettings
{
    public int SchemaVersion { get; init; } = 1;
    public string DayStart { get; init; } = "04:00";
    public LocationSettings? Location { get; init; } = new() { Name = "北京", Latitude = 39.9042, Longitude = 116.4074 };
    public AppearanceSettings Appearance { get; init; } = new();
    public List<DailyMarkerSettings> DailyMarkers { get; init; } = [];
    public bool StartWithWindows { get; init; }
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; init; }
}

public sealed record LocationSettings
{
    public string Name { get; init; } = "";
    public double Latitude { get; init; }
    public double Longitude { get; init; }
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; init; }
}

public sealed record AppearanceSettings
{
    public double WidthRatio { get; init; } = 1;
    public double BarHeightDip { get; init; } = 16;
    public double OffsetFromTaskbarDip { get; init; }
    public double Opacity { get; init; } = 0.92;
    public string NightColor { get; init; } = "#28324B";
    public string DayColor { get; init; } = "#F6BD77";
    public int TransitionMinutes { get; init; } = 30;
    public bool ShowHourTicks { get; init; }
    public IndicatorSettings Indicator { get; init; } = new();
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; init; }
}

public sealed record IndicatorSettings
{
    public string Kind { get; init; } = "builtin";
    public string Value { get; init; } = "dot";
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; init; }
}

public sealed record DailyMarkerSettings
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Label { get; init; } = "新标记";
    public string Time { get; init; } = "09:00";
    public string Color { get; init; } = "#5AA9E6";
    public bool Enabled { get; init; } = true;
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; init; }
}
