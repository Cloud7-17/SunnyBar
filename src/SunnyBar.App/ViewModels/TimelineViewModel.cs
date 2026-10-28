using System.ComponentModel;
using System.Windows.Media.Imaging;
using SunnyBar.App.Services;
using SunnyBar.Core;

namespace SunnyBar.App.ViewModels;

public sealed record PreviewLocation(string Name, GeoLocation? Coordinates);

public sealed class TimelineViewModel(IClock clock) : INotifyPropertyChanged
{
    private readonly TimelineService timeline = new(clock);
    public AppSettings Settings { get; private set; } = new();
    public ScheduleDocument Schedule { get; private set; } = new();
    public BitmapSource? IndicatorImage { get; private set; }
    public string IndicatorMessage { get; private set; } = "";
    public event EventHandler? SettingsChanged;
    public IReadOnlyList<PreviewLocation> Locations { get; } = Array.AsReadOnly<PreviewLocation>(
    [
        new("未选择地点（中性色）", null),
        new("北京", new(39.9042, 116.4074)),
        new("上海", new(31.2304, 121.4737)),
        new("广州", new(23.1291, 113.2644))
    ]);
    private PreviewLocation? selectedLocation;
    private TimeZoneInfo lastZone = TimeZoneInfo.Local;
    public PreviewLocation SelectedLocation
    {
        get => selectedLocation ?? Locations[0];
        set
        {
            if (value == SelectedLocation) return;
            selectedLocation = value;
            Settings = Settings with { Location = value.Coordinates is { } location
                ? new LocationSettings { Name = value.Name, Latitude = location.Latitude, Longitude = location.Longitude } : null };
            Refresh(lastZone);
        }
    }
    public TimelineSnapshot? Snapshot { get; private set; }
    public string DisplayDayText => Snapshot is { } state
        ? $"{state.Window.Start:yyyy-MM-dd HH:mm} → {state.Window.End:yyyy-MM-dd HH:mm}"
        : string.Empty;
    public string CurrentTimeText => Snapshot is { } state
        ? $"当前 {state.Now:HH:mm:ss zzz}  ·  {state.Progress:P1}"
        : string.Empty;
    public string TimeZoneText => Snapshot is { } state
        ? $"{state.TimeZoneName}  ·  本展示日实际时长 {(state.Window.End - state.Window.Start).TotalHours:0.#} 小时"
        : string.Empty;
    public string SolarText => Snapshot is { } state
        ? state.SolarMessage + (state.Solar is { Markers.Count: > 0 } solar
            ? "\n" + string.Join("  ·  ", solar.Markers.Select(marker =>
                $"{(marker.Kind == SolarEventKind.Sunrise ? "日出" : "日落")} {marker.Instant:MM-dd HH:mm}"))
            : string.Empty)
        : string.Empty;
    public string DailyMarkersText => Snapshot?.DailyMarkers is { Count: > 0 } markers
        ? "每日标记：" + string.Join(" · ", markers.Take(4).Select(marker => $"{marker.Label} {marker.Instant:HH:mm}"))
            + (markers.Count > 4 ? $" 等共 {markers.Count} 个" : "") : "";

    public UpcomingDailyMarker? NextMarker { get; private set; }
    public string HoverTimeText => Snapshot is { } state ? $"当前 {state.Now:HH:mm}" : "";
    public string NextMarkerText => NextMarker is { } marker ? $"下一标记：{marker.Label} {marker.Instant:HH:mm}" : "下一标记：暂无";
    public string RemainingText => NextMarker is { } marker ? "还有 " + marker.RemainingText : "";
    private ScheduleHoverInfo HoverSchedule => Snapshot is { } state
        ? ScheduleHoverInfo.Capture(state.Schedule, state.Now, lastZone) : new("当前日程：暂无", "下一日程：暂无");
    public string HoverCurrentScheduleText => HoverSchedule.CurrentText;
    public string HoverNextScheduleText => HoverSchedule.NextText;
    public string CurrentScheduleText => Snapshot?.Schedule?.Current is { Count: > 0 } current
        ? "当前日程：" + string.Join(" · ", current.Take(3).Select(item => item.Title)) + (current.Count > 3 ? $" 等 {current.Count} 项" : "") : "当前日程：暂无";
    public string NextScheduleText => Snapshot?.Schedule?.Next is { } next
        ? $"下一日程：{next.Title} · {TimeZoneInfo.ConvertTime(next.StartAt, lastZone):MM-dd HH:mm}" : "下一日程：暂无";
    public void ApplySchedule(ScheduleDocument document) { Schedule = document; Refresh(lastZone); }

    public void ApplySettings(AppSettings settings, SettingsService store)
    {
        Settings = settings;
        selectedLocation = settings.Location is { } location
            ? new PreviewLocation(location.Name, new GeoLocation(location.Latitude, location.Longitude)) : Locations[0];
        IndicatorImage = settings.Appearance.Indicator.Kind == "png"
            ? PngImageLoader.Load(store.ResolveAsset(settings.Appearance.Indicator.Value)) : null;
        IndicatorMessage = settings.Appearance.Indicator.Kind == "png" && IndicatorImage is null
            ? "指针图片不可用，已回退到圆点；可在设置中重新选择 PNG。" : "";
        Refresh(lastZone);
        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void Refresh(TimeZoneInfo timeZone)
    {
        lastZone = timeZone;
        Snapshot = timeline.Capture(timeZone, SelectedLocation.Coordinates, Settings.DailyMarkers, Settings.Appearance, Schedule.Events);
        NextMarker = new NextDailyMarkerService().Find(Snapshot.Now, timeZone, Settings.DailyMarkers);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
    }
}
