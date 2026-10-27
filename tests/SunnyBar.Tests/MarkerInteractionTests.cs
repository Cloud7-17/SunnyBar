using SunnyBar.Core;
using Xunit;

namespace SunnyBar.Tests;

public sealed class MarkerInteractionTests
{
    [Theory]
    [InlineData("9", "5", "09:05")]
    [InlineData("00", "00", "00:00")]
    [InlineData("23", "59", "23:59")]
    public void Separate_time_fields_normalize(string hour, string minute, string expected) =>
        Assert.Equal(expected, DailyMarkerTime.FromParts(hour, minute));

    [Theory]
    [InlineData("24", "00")]
    [InlineData("10", "60")]
    [InlineData("", "00")]
    [InlineData("1.5", "00")]
    [InlineData("-1", "00")]
    public void Invalid_fields_are_rejected(string hour, string minute) =>
        Assert.Throws<FormatException>(() => DailyMarkerTime.FromParts(hour, minute));

    [Fact]
    public void Presets_match_requested_labels_times_and_are_valid()
    {
        Assert.Equal(new[] { "上班", "午饭", "晚饭", "睡觉" }, DailyMarkerPresets.All.Select(marker => marker.Label));
        Assert.Equal(new[] { "10:00", "12:00", "18:00", "23:00" }, DailyMarkerPresets.All.Select(marker => marker.Time));
        Assert.Empty(SettingsValidator.Validate(new() { DailyMarkers = DailyMarkerPresets.All.ToList() }));
    }

    [Fact]
    public void Next_marker_ignores_disabled_and_switches_at_exact_time()
    {
        var now = new DateTimeOffset(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);
        var markers = DailyMarkerPresets.All.ToList();
        markers[2] = markers[2] with { Enabled = false };
        var result = new NextDailyMarkerService().Find(now, TimeZoneInfo.Utc, markers)!;
        Assert.Equal("睡觉", result.Label);
        Assert.Equal(TimeSpan.FromHours(11), result.Remaining);
    }

    [Theory]
    [InlineData(23, 50, "00:30", 40)]
    [InlineData(3, 30, "04:00", 30)]
    [InlineData(23, 30, "10:00", 630)]
    public void Next_marker_wraps_across_midnight_and_display_day(int hour, int minute, string time, int minutes)
    {
        var now = new DateTimeOffset(2026, 10, 27, hour, minute, 0, TimeSpan.Zero);
        var result = new NextDailyMarkerService().Find(now, TimeZoneInfo.Utc, [new() { Label = "目标", Time = time }])!;
        Assert.Equal(TimeSpan.FromMinutes(minutes), result.Remaining);
        Assert.Equal(now.AddMinutes(minutes), result.Instant);
    }

    [Fact]
    public void No_enabled_markers_has_no_next_marker() =>
        Assert.Null(new NextDailyMarkerService().Find(DateTimeOffset.UnixEpoch, TimeZoneInfo.Utc, [new() { Enabled = false }]));

    [Fact]
    public void Countdown_uses_elapsed_instants_across_DST()
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
        var now = new DateTimeOffset(2026, 3, 8, 1, 30, 0, TimeSpan.FromHours(-5));
        var result = new NextDailyMarkerService().Find(now, zone, [new() { Time = "03:30" }])!;
        Assert.Equal(TimeSpan.FromHours(1), result.Remaining);
    }

    [Theory]
    [InlineData(30, "不足 1 分钟")]
    [InlineData(61, "2 分钟")]
    [InlineData(3600, "1 小时")]
    [InlineData(3660, "1 小时 1 分钟")]
    public void Countdown_is_brief_and_does_not_round_to_zero(int seconds, string expected) =>
        Assert.Equal(expected, new UpcomingDailyMarker("测试", DateTimeOffset.UnixEpoch, TimeSpan.FromSeconds(seconds)).RemainingText);

    [Fact]
    public void Explicitly_saved_location_or_no_location_is_preserved()
    {
        var store = new SettingsService(Path.Combine(AppContext.BaseDirectory, "settings-tests", Guid.NewGuid().ToString("N")));
        store.Save(new() { Location = new() { Name = "我的地点", Latitude = 31, Longitude = 121 } });
        Assert.Equal("我的地点", store.Load().Settings.Location?.Name);
        store.Save(new() { Location = null });
        Assert.Null(store.Load().Settings.Location);
    }
}
