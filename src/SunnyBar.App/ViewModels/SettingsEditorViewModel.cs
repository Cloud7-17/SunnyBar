using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using SunnyBar.App.Services;
using SunnyBar.Core;

namespace SunnyBar.App.ViewModels;

public sealed class MarkerRow : INotifyPropertyChanged
{
    public DailyMarkerSettings Original { get; }
    public string Label { get; set; }
    public static string[] Hours { get; } = Enumerable.Range(0, 24).Select(value => value.ToString("00")).ToArray();
    public static string[] Minutes { get; } = Enumerable.Range(0, 60).Select(value => value.ToString("00")).ToArray();
    public string Hour { get; set; } = "09";
    public string Minute { get; set; } = "00";
    public string Time
    {
        get => $"{Hour}:{Minute}";
        set { var parts = value.Split(':'); Hour = parts[0]; Minute = parts.Length > 1 ? parts[1] : "00"; Notify(nameof(Hour)); Notify(nameof(Minute)); }
    }
    private string color = "#5AA9E6";
    private static readonly string[] Palette = ["#5AA9E6", "#44AA88", "#F2B544", "#EE8844", "#E86677", "#AF8FDD", "#5577CC", "#82909F", "#FFFFFF", "#28324B"];
    public string[] ColorChoices => Palette.Append(color).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    public string Color
    {
        get => color;
        set { if (string.IsNullOrWhiteSpace(value) || value == color) return; color = value; Notify(nameof(ColorChoices)); Notify(nameof(Color)); }
    }
    public bool Enabled { get; set; }
    public MarkerRow(DailyMarkerSettings marker) => (Original, Label, Time, Color, Enabled)
        = (marker, marker.Label, marker.Time, marker.Color, marker.Enabled);
    public DailyMarkerSettings Build() => Original with { Label = Label.Trim(), Time = DailyMarkerTime.FromParts(Hour, Minute), Color = Color.Trim(), Enabled = Enabled };
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Notify(string property) => PropertyChanged?.Invoke(this, new(property));
}

public sealed class SettingsEditorViewModel : INotifyPropertyChanged
{
    private readonly SettingsService store;
    private readonly TimelineViewModel timeline;
    private readonly IStartupRegistration startup;
    private AppSettings original;
    public bool CanSave { get; }
    public string[] Cities { get; } = ["未选择地点", "北京", "上海", "广州", "手动输入"];
    public string[] Indicators { get; } = ["圆点", "星形", "Emoji", "PNG 图片"];
    private int city;
    public int City
    {
        get => city;
        set
        {
            if (value < 0 || value >= Cities.Length) return;
            city = value;
            if (value is >= 1 and <= 3)
            {
                var preset = timeline.Locations[value];
                LocationName = Cities[value];
                Latitude = Format(preset.Coordinates!.Latitude);
                Longitude = Format(preset.Coordinates.Longitude);
            }
            Notify();
        }
    }
    public bool HasLocation => City != 0;
    public string LocationName { get; set; } = "";
    public string Latitude { get; set; } = "";
    public string Longitude { get; set; } = "";
    public string WidthPercent { get; set; }
    public string BarHeight { get; set; }
    public string Offset { get; set; }
    public string Opacity { get; set; }
    public string DayColor { get; set; }
    public string NightColor { get; set; }
    public string Transition { get; set; }
    public bool ShowHourTicks { get; set; }
    private int indicatorIndex;
    public int IndicatorIndex { get => indicatorIndex; set { if (value < 0 || value >= Indicators.Length) return; indicatorIndex = value; Notify(); } }
    public bool IsEmoji => IndicatorIndex == 2;
    public bool IsPng => IndicatorIndex == 3;
    public string Emoji { get; set; } = "🌻";
    public string ImagePath { get; set; } = "";
    public bool StartWithWindows { get; set; }
    public ObservableCollection<MarkerRow> Markers { get; }
    public MarkerRow? SelectedMarker { get; set; }
    public string Status { get; private set; }
    public string SettingsPath => store.FilePath;
    public string VersionText => "版本 " + (typeof(SettingsEditorViewModel).Assembly
        .GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
        .OfType<System.Reflection.AssemblyInformationalVersionAttribute>().FirstOrDefault()?.InformationalVersion.Split('+')[0] ?? "未知");
    public event PropertyChangedEventHandler? PropertyChanged;

    public SettingsEditorViewModel(TimelineViewModel timeline, SettingsService store, IStartupRegistration startup, string message, bool canSave)
    {
        this.timeline = timeline;
        this.store = store;
        this.startup = startup;
        original = timeline.Settings;
        CanSave = canSave;
        Status = message;
        var a = original.Appearance;
        WidthPercent = Format(a.WidthRatio * 100); BarHeight = Format(a.BarHeightDip);
        Offset = Format(a.OffsetFromTaskbarDip); Opacity = Format(a.Opacity);
        DayColor = a.DayColor; NightColor = a.NightColor; Transition = a.TransitionMinutes.ToString(CultureInfo.CurrentCulture);
        ShowHourTicks = a.ShowHourTicks;
        indicatorIndex = a.Indicator.Kind switch { "emoji" => 2, "png" => 3, _ => a.Indicator.Value == "star" ? 1 : 0 };
        if (a.Indicator.Kind == "emoji") Emoji = a.Indicator.Value;
        if (a.Indicator.Kind == "png") ImagePath = store.ResolveAsset(a.Indicator.Value);
        if (original.Location is { } location)
        {
            city = 4;
            for (var index = 1; index <= 3; index++)
                if (timeline.Locations[index].Coordinates is { } preset && preset.Latitude == location.Latitude && preset.Longitude == location.Longitude
                    && Cities[index] == location.Name) { city = index; break; }
            LocationName = location.Name; Latitude = Format(location.Latitude); Longitude = Format(location.Longitude);
        }
        StartWithWindows = original.StartWithWindows;
        Markers = new(original.DailyMarkers.Select(marker => new MarkerRow(marker)));
    }

    public void Notify() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
    public void AddMarker() { if (Markers.Count < 100) Markers.Add(new(new())); }
    public void AddPreset(int index)
    {
        if (Markers.Count < 100 && index >= 0 && index < DailyMarkerPresets.All.Count)
            Markers.Add(new(DailyMarkerPresets.All[index] with { Id = Guid.NewGuid().ToString("N") }));
    }
    public void RemoveMarker() { if (SelectedMarker is { } marker) Markers.Remove(marker); }

    public bool Apply()
    {
        try
        {
            if (!CanSave) throw new IOException("配置受保护，当前不能覆盖保存。");
            var indicator = original.Appearance.Indicator with
            {
                Kind = IndicatorIndex switch { 2 => "emoji", 3 => "png", _ => "builtin" },
                Value = IndicatorIndex switch { 1 => "star", 2 => Emoji.Trim(), 3 => ImagePath.Trim(), _ => "dot" }
            };
            var settings = original with
            {
                Location = HasLocation ? (original.Location ?? new LocationSettings()) with
                    { Name = LocationName.Trim(), Latitude = Number(Latitude, "纬度"), Longitude = Number(Longitude, "经度") } : null,
                Appearance = original.Appearance with
                {
                    WidthRatio = Number(WidthPercent, "宽度") / 100,
                    BarHeightDip = Number(BarHeight, "高度"), OffsetFromTaskbarDip = Number(Offset, "间距"),
                    Opacity = Number(Opacity, "不透明度"), DayColor = DayColor.Trim(), NightColor = NightColor.Trim(),
                    TransitionMinutes = int.TryParse(Transition, CultureInfo.CurrentCulture, out var minutes)
                        ? minutes : throw new FormatException("过渡分钟数须为整数。"), ShowHourTicks = ShowHourTicks, Indicator = indicator
                },
                DailyMarkers = Markers.Select(marker => marker.Build()).ToList(), StartWithWindows = StartWithWindows
            };
            var errors = SettingsValidator.Validate(settings);
            if (errors.Count > 0) throw new SettingsValidationException(errors);
            if (indicator.Kind == "png")
            {
                var previousPath = original.Appearance.Indicator.Kind == "png" ? store.ResolveAsset(original.Appearance.Indicator.Value) : "";
                if (!string.Equals(previousPath, ImagePath.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    if (PngImageLoader.Load(ImagePath.Trim()) is null) throw new IOException("PNG 无法读取，请选择有效图片（最多 5 MB）。");
                    settings = settings with { Appearance = settings.Appearance with { Indicator = indicator with { Value = store.ImportPng(ImagePath.Trim()) } } };
                }
                else settings = settings with { Appearance = settings.Appearance with { Indicator = indicator with { Value = original.Appearance.Indicator.Value } } };
            }
            startup.Apply(settings.StartWithWindows, () => store.Save(settings));
            timeline.ApplySettings(settings, store);
            original = settings;
            for (var index = 0; index < Markers.Count; index++) Markers[index].Time = settings.DailyMarkers[index].Time;
            if (indicator.Kind == "png") ImagePath = store.ResolveAsset(settings.Appearance.Indicator.Value);
            Status = "已保存并立即应用。" + timeline.IndicatorMessage;
            Notify();
            return true;
        }
        catch (Exception error) when (error is SettingsValidationException or IOException or UnauthorizedAccessException
            or FormatException or OverflowException or ArgumentException or System.Security.SecurityException)
        {
            Status = "未保存：" + error.Message;
            Notify();
            return false;
        }
    }

    private static string Format(double number) => number.ToString("G", CultureInfo.CurrentCulture);
    private static double Number(string text, string label) => double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out var number)
        ? number : throw new FormatException($"{label}须为数字。");
}
