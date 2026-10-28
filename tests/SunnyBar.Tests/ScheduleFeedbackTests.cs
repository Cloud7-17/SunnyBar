using System.Text.Json;
using SunnyBar.Core;
using Xunit;

namespace SunnyBar.Tests;

public sealed class ScheduleFeedbackTests
{
    [Fact]
    public void Older_settings_default_to_half_height_and_custom_ratio_round_trips()
    {
        var settings = JsonSerializer.Deserialize<AppSettings>("{\"Appearance\":{\"Opacity\":0.7}}")!;
        Assert.Equal(0.4, settings.Appearance.ScheduleHeightRatio);
        var store = new SettingsService(Path.Combine(AppContext.BaseDirectory, "feedback", Guid.NewGuid().ToString("N")));
        store.Save(settings with { Appearance = settings.Appearance with { ScheduleHeightRatio = 0.25 } });
        Assert.Equal(0.25, store.Load().Settings.Appearance.ScheduleHeightRatio);
        Assert.Equal(0.7, store.Load().Settings.Appearance.Opacity);
    }

    [Theory]
    [InlineData(0.1, true)]
    [InlineData(0.5, true)]
    [InlineData(1, true)]
    [InlineData(0, false)]
    [InlineData(1.01, false)]
    [InlineData(double.NaN, false)]
    public void Ratio_is_validated(double ratio, bool valid) => Assert.Equal(valid,
        SettingsValidator.Validate(new() { Appearance = new() { ScheduleHeightRatio = ratio } }).Count == 0);

    [Fact]
    public void Hover_shows_overlapping_current_ranges_and_next_start_countdown()
    {
        var now = new DateTimeOffset(2026, 10, 27, 16, 0, 0, TimeSpan.Zero);
        var first = new ScheduleEvent { Title = "写作", StartAt = now.AddHours(-2), EndAt = now.AddHours(1) };
        var second = first with { Title = "会议", EndAt = now.AddSeconds(30) };
        var next = first with { Title = "明日安排", StartAt = now.AddDays(1), EndAt = now.AddDays(1).AddHours(1) };
        var info = ScheduleHoverInfo.Capture(new([], [second, first], next), now, TimeZoneInfo.Utc);
        Assert.Contains("当前日程：会议", info.CurrentText);
        Assert.Contains("14:00 +00:00 → 16:00 +00:00", info.CurrentText);
        Assert.Contains("距结束 不足 1 分钟", info.CurrentText);
        Assert.Contains("距结束 1 小时", info.CurrentText);
        Assert.Contains("10-28 16:00", info.NextText);
        Assert.Contains("距开始 24 小时", info.NextText);
    }

    [Fact]
    public void Hover_updates_at_event_boundary_and_handles_empty_state()
    {
        var now = new DateTimeOffset(2026, 10, 27, 16, 0, 0, TimeSpan.Zero);
        var item = new ScheduleEvent { Title = "安排", StartAt = now, EndAt = now.AddHours(1) };
        var window = new DisplayDayService(TimeZoneInfo.Utc).GetWindow(now);
        var service = new ScheduleService();
        Assert.Contains("距开始 不足 1 分钟", ScheduleHoverInfo.Capture(service.Capture([item], window, now.AddSeconds(-1)), now.AddSeconds(-1), TimeZoneInfo.Utc).NextText);
        Assert.Contains("距结束 1 小时", ScheduleHoverInfo.Capture(service.Capture([item], window, now), now, TimeZoneInfo.Utc).CurrentText);
        var ended = ScheduleHoverInfo.Capture(service.Capture([item], window, item.EndAt), item.EndAt, TimeZoneInfo.Utc);
        Assert.Equal("当前日程：暂无", ended.CurrentText);
        Assert.Equal("下一日程：暂无", ended.NextText);
    }

    [Fact]
    public void Hover_countdown_uses_elapsed_time_across_repeated_DST_hour()
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
        var now = new DateTimeOffset(2026, 11, 1, 1, 30, 0, TimeSpan.FromHours(-4));
        var item = new ScheduleEvent { Title = "跨夏令时", StartAt = now, EndAt = now.ToOffset(TimeSpan.FromHours(-5)).AddHours(1) };
        var text = ScheduleHoverInfo.Capture(new([], [item], null), now, zone).CurrentText;
        Assert.Contains("01:30 -04:00 → 01:30 -05:00", text);
        Assert.Contains("距结束 1 小时", text);
    }
}
