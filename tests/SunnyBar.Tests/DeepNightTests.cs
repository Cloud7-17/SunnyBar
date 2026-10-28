using SunnyBar.Core;
using Xunit;

namespace SunnyBar.Tests;

public sealed class DeepNightTests
{
    [Fact]
    public void Midnight_to_sunrise_is_clipped_into_both_ends_of_operational_day()
    {
        var days = new DisplayDayService(TimeZoneInfo.Utc);
        var window = days.GetWindow(new DateTimeOffset(2026, 10, 28, 16, 0, 0, TimeSpan.Zero));
        var midnight = days.ResolveCalendarTime(window.Date, TimeOnly.MinValue);
        var rise = midnight.AddHours(6);
        var set = midnight.AddHours(18);
        var result = DeepNightService.Calculate(window, TimeZoneInfo.Utc, midnight,
            new(false, [new(rise, SolarEventKind.Sunrise), new(set, SolarEventKind.Sunset)]));
        Assert.Collection(result,
            left => { Assert.Equal(window.Start, left.Start); Assert.Equal(rise, left.End); Assert.Equal(0, left.StartProgress); Assert.Equal(2.0/24, left.EndProgress, 10); },
            right => { Assert.Equal(midnight.AddDays(1), right.Start); Assert.Equal(window.End, right.End); Assert.Equal(20.0/24, right.StartProgress, 10); Assert.Equal(1, right.EndProgress); });
    }

    [Fact]
    public void Sunrise_before_four_ends_deep_night_before_left_edge()
    {
        var days = new DisplayDayService(TimeZoneInfo.Utc);
        var window = days.GetWindow(new DateTimeOffset(2026, 6, 28, 16, 0, 0, TimeSpan.Zero));
        var midnight = days.ResolveCalendarTime(window.Date, TimeOnly.MinValue);
        var result = DeepNightService.Calculate(window, TimeZoneInfo.Utc, midnight, new(false,
            [new(midnight.AddHours(3), SolarEventKind.Sunrise), new(midnight.AddHours(21), SolarEventKind.Sunset), new(midnight.AddDays(1).AddHours(3), SolarEventKind.Sunrise)]));
        var only = Assert.Single(result);
        Assert.Equal(midnight.AddDays(1), only.Start);
        Assert.Equal(midnight.AddDays(1).AddHours(3), only.End);
    }

    [Theory]
    [InlineData("2026-03-07T12:00:00-05:00", 23)]
    [InlineData("2026-10-31T12:00:00-04:00", 25)]
    public void Midnight_and_deep_night_use_actual_DST_instants(string value, int hours)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
        var days = new DisplayDayService(zone);
        var now = DateTimeOffset.Parse(value);
        var window = days.GetWindow(now);
        var midnight = days.ResolveCalendarTime(window.Date, TimeOnly.MinValue);
        var nextMidnight = days.ResolveDailyTime(window, TimeOnly.MinValue);
        var rise = days.ResolveCalendarTime(window.Date, new(7, 0));
        var set = days.ResolveCalendarTime(window.Date, new(18, 0));
        var state = DeepNightService.Calculate(window, zone, midnight, new(false, [new(rise, SolarEventKind.Sunrise), new(set, SolarEventKind.Sunset)]));
        Assert.Equal(hours, (window.End-window.Start).TotalHours);
        Assert.Equal(nextMidnight, state[^1].Start);
        Assert.Equal((nextMidnight-window.Start).TotalSeconds/(window.End-window.Start).TotalSeconds, state[^1].StartProgress, 10);
        Assert.Equal(hours == 23 ? 3 : 5, (state[^1].End-state[^1].Start).TotalHours);
    }

    [Fact]
    public void Polar_day_has_no_stars_and_polar_night_does_not_fabricate_sunrise()
    {
        var zone = TimeZoneInfo.Utc;
        var days = new DisplayDayService(zone);
        var window = days.GetWindow(new DateTimeOffset(2026, 10, 28, 16, 0, 0, TimeSpan.Zero));
        var midnight = days.ResolveCalendarTime(window.Date, TimeOnly.MinValue);
        Assert.Empty(DeepNightService.Calculate(window, zone, midnight, new(true, [])));
        var night = Assert.Single(DeepNightService.Calculate(window, zone, midnight, new(false, [])));
        Assert.Equal(window.Start, night.Start); Assert.Equal(window.End, night.End);
    }

    [Fact]
    public void Unknown_location_still_has_midnight_marker_without_stars()
    {
        var before = new Clock(new(2026, 10, 28, 3, 59, 0, TimeSpan.Zero));
        var snapshot = new TimelineService(before).Capture(TimeZoneInfo.Utc);
        Assert.Equal(new DateTimeOffset(2026, 10, 28, 0, 0, 0, TimeSpan.Zero), snapshot.Midnight);
        Assert.Null(snapshot.Solar);
        before.UtcNow = before.UtcNow.AddMinutes(1);
        snapshot = new TimelineService(before).Capture(TimeZoneInfo.Utc);
        Assert.Equal(new DateTimeOffset(2026, 10, 29, 0, 0, 0, TimeSpan.Zero), snapshot.Midnight);
    }

    [Fact]
    public void Daylight_across_midnight_never_gets_stars_until_sunset()
    {
        var zone = TimeZoneInfo.Utc;
        var days = new DisplayDayService(zone);
        var window = days.GetWindow(new DateTimeOffset(2026, 10, 28, 16, 0, 0, TimeSpan.Zero));
        var midnight = days.ResolveCalendarTime(window.Date, TimeOnly.MinValue);
        var result = DeepNightService.Calculate(window, zone, midnight, new(true,
            [new(midnight.AddHours(5), SolarEventKind.Sunset), new(midnight.AddHours(20), SolarEventKind.Sunrise)]));
        var night = Assert.Single(result);
        Assert.Equal(midnight.AddHours(5), night.Start); Assert.Equal(midnight.AddHours(20), night.End);
    }

    private sealed class Clock(DateTimeOffset now) : IClock { public DateTimeOffset UtcNow { get; set; } = now; }
}
