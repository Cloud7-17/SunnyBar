using SunnyBar.Core;
using Xunit;

namespace SunnyBar.Tests;

public sealed class TimeAxisTests
{
    private static readonly TimeZoneInfo China = TimeZoneInfo.CreateCustomTimeZone(
        "Test/UTC+08", TimeSpan.FromHours(8), "UTC+08", "UTC+08");
    private readonly DisplayDayService days = new(China);
    private readonly TimeAxisMapper mapper = new();

    private static DateTimeOffset At(int day, int hour, int minute = 0) =>
        new(2026, 10, day, hour, minute, 0, TimeSpan.FromHours(8));

    [Theory]
    [InlineData(27, 0, 0, 26)]
    [InlineData(27, 3, 59, 26)]
    [InlineData(27, 4, 0, 27)]
    [InlineData(27, 16, 0, 27)]
    [InlineData(28, 0, 0, 27)]
    [InlineData(28, 4, 0, 28)]
    public void Window_uses_local_four_am_boundary(int day, int hour, int minute, int expectedDate)
    {
        var window = days.GetWindow(At(day, hour, minute));
        Assert.Equal(new DateOnly(2026, 10, expectedDate), window.Date);
        Assert.Equal(At(expectedDate, 4), window.Start);
        Assert.Equal(At(expectedDate + 1, 4), window.End);
    }

    [Theory]
    [InlineData(27, 3, 59, 0)]
    [InlineData(27, 4, 0, 0)]
    [InlineData(27, 6, 0, 2.0 / 24)]
    [InlineData(27, 16, 0, 0.5)]
    [InlineData(27, 18, 0, 14.0 / 24)]
    [InlineData(28, 0, 0, 20.0 / 24)]
    [InlineData(28, 3, 59, 1439.0 / 1440)]
    [InlineData(28, 4, 0, 1)]
    [InlineData(28, 5, 0, 1)]
    public void Mapping_is_clamped_against_a_fixed_window(int day, int hour, int minute, double expected)
    {
        var window = days.GetWindow(At(27, 12));
        var instant = At(day, hour, minute);
        Assert.Equal(expected, mapper.Map(instant, window), 10);
        Assert.Equal(30 + expected * 900, mapper.ToX(instant, window, 30, 900), 8);
    }

    [Fact]
    public void Four_am_finishes_old_window_and_resets_new_window()
    {
        var boundary = At(28, 4);
        Assert.Equal(1, mapper.Map(boundary, days.GetWindow(At(27, 16))));
        Assert.Equal(0, mapper.Map(boundary, days.GetWindow(boundary)));
    }

    [Theory]
    [InlineData(4, 0, 27, 0)]
    [InlineData(9, 0, 27, 5.0 / 24)]
    [InlineData(0, 30, 28, 20.5 / 24)]
    public void Daily_time_before_four_am_is_resolved_on_second_calendar_day(
        int hour, int minute, int expectedDay, double progress)
    {
        var window = days.GetWindow(At(27, 12));
        var instant = days.ResolveDailyTime(window, new TimeOnly(hour, minute));
        Assert.Equal(At(expectedDay, hour, minute), instant);
        Assert.Equal(progress, mapper.Map(instant, window), 10);
    }

    [Fact]
    public void Incoming_offset_does_not_override_requested_time_zone()
    {
        Assert.Equal(new DateOnly(2026, 10, 27), days.GetWindow(At(27, 4).ToUniversalTime()).Date);
    }

    [Fact]
    public void Invalid_window_and_geometry_are_rejected()
    {
        Assert.Throws<ArgumentException>(() => new DisplayDayWindow(new(2026, 10, 27), At(27, 4), At(27, 4)));
        Assert.Throws<ArgumentException>(() => new DisplayDayWindow(new(2026, 10, 27), At(28, 4), At(27, 4)));
        var window = days.GetWindow(At(27, 12));
        Assert.Throws<ArgumentOutOfRangeException>(() => mapper.ToX(At(27, 12), window, 0, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => mapper.ToX(At(27, 12), window, double.NaN, 10));
        Assert.Throws<ArgumentOutOfRangeException>(() => mapper.ToX(At(27, 12), window, 0, double.PositiveInfinity));
        Assert.Equal(15, mapper.ToX(At(27, 12), window, 15, 0));
    }

    [Fact]
    public void Snapshot_uses_injected_clock_and_maps_all_ticks_with_same_mapper()
    {
        var clock = new FixedClock(At(27, 16));
        var service = new TimelineService(clock);
        var snapshot = service.Capture(China);
        Assert.Equal(0.5, snapshot.Progress);
        Assert.Equal(clock.UtcNow, snapshot.Now);
        Assert.Equal(7, snapshot.Ticks.Count);
        Assert.Equal(0, snapshot.Ticks[0].Progress);
        Assert.Equal(1, snapshot.Ticks[^1].Progress);
        foreach (var tick in snapshot.Ticks)
            Assert.Equal(mapper.Map(tick.Instant, snapshot.Window), tick.Progress);
        clock.UtcNow = At(28, 4);
        var next = service.Capture(China);
        Assert.Equal(0, next.Progress);
        Assert.Equal(new DateOnly(2026, 10, 28), next.Window.Date);
        Assert.Equal(TimeSpan.Zero, service.Capture(TimeZoneInfo.Utc).Now.Offset);
    }

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = now.ToUniversalTime();
    }
}
