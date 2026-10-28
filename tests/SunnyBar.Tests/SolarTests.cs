using SunnyBar.Core;
using Xunit;

namespace SunnyBar.Tests;

public sealed class SolarTests
{
    private readonly ApproximateSolarCalculator calculator = new();
    private static readonly GeoLocation Equator = new(0, 0);
    private static DateTimeOffset At(string value) => DateTimeOffset.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
    private static DisplayDayWindow Window(string start, string end) =>
        new(DateOnly.FromDateTime(At(start).DateTime), At(start), At(end));

    // Independent published SPA results, NREL/TP-560-34302, Table A2.1, page A-4.
    // The simplified USNO position model has a two-minute acceptance tolerance here.
    [Theory]
    [InlineData("1994-01-02", 35, "07:08:12.8", "16:59:55.9")]
    [InlineData("1996-07-05", -35, "07:08:15.4", "17:01:04.5")]
    [InlineData("2004-12-04", -35, "04:38:57.1", "19:02:02.5")]
    public void Offline_calculation_matches_published_nrel_reference(
        string date, double latitude, string sunrise, string sunset)
    {
        var start = At(date + "T00:00:00Z");
        var actual = calculator.Calculate(start, start.AddDays(1), new GeoLocation(latitude, 0));
        Assert.False(actual.InitiallyDaylight);
        Assert.Collection(actual.Events,
            rise => Check(rise, SolarEventKind.Sunrise, At(date + "T" + sunrise + "Z")),
            set => Check(set, SolarEventKind.Sunset, At(date + "T" + sunset + "Z")));

        static void Check(SolarEvent actual, SolarEventKind kind, DateTimeOffset expected)
        {
            Assert.Equal(kind, actual.Kind);
            Assert.InRange(Math.Abs((actual.Instant - expected).TotalMinutes), 0, 2);
        }
    }

    [Theory]
    [InlineData(69.6492, 18.9553, "2026-06-21", true)]
    [InlineData(69.6492, 18.9553, "2026-12-21", false)]
    [InlineData(-69.0, 18.0, "2026-06-21", false)]
    [InlineData(-69.0, 18.0, "2026-12-21", true)]
    [InlineData(90, 0, "2026-06-21", true)]
    [InlineData(-90, 0, "2026-06-21", false)]
    public void Polar_windows_have_no_fabricated_events(double latitude, double longitude, string date, bool day)
    {
        var start = At(date + "T04:00:00Z");
        var window = new DisplayDayWindow(DateOnly.FromDateTime(start.DateTime), start, start.AddDays(1));
        var result = new SolarEventService(calculator).Calculate(window, new(latitude, longitude), TimeZoneInfo.Utc);
        Assert.Empty(result.Markers);
        Assert.Equal(day ? SolarWindowState.DayOnly : SolarWindowState.NightOnly, result.State);
        Assert.All(result.Stops, stop => Assert.Equal(day ? 1 : 0, stop.Daylight));
    }

    [Fact]
    public void Simulated_events_use_same_mapping_as_now_and_smooth_sunrise_and_sunset()
    {
        var window = Window("2026-10-27T04:00:00+08:00", "2026-10-28T04:00:00+08:00");
        var rise = window.Start.AddHours(2);
        var set = window.Start.AddHours(14);
        var fake = new FixedCalculator(false, new(rise, SolarEventKind.Sunrise), new(set, SolarEventKind.Sunset));
        var result = new SolarEventService(fake).Calculate(window, Equator, TimeZoneInfo.CreateCustomTimeZone(
            "Test/UTC+08", TimeSpan.FromHours(8), "UTC+08", "UTC+08"));
        Assert.Equal(2.0 / 24, result.Markers[0].Progress, 10);
        Assert.Equal(14.0 / 24, result.Markers[1].Progress, 10);
        Assert.Equal(TimeSpan.FromHours(8), result.Markers[0].Instant.Offset);
        foreach (var marker in result.Markers)
            Assert.Equal(new TimeAxisMapper().Map(marker.Instant, window), marker.Progress);
        Assert.Equal(0, DayAt(rise.AddMinutes(-30)));
        Assert.Equal(0.5, DayAt(rise));
        Assert.Equal(1, DayAt(rise.AddMinutes(30)));
        Assert.Equal(1, DayAt(set.AddMinutes(-30)));
        Assert.Equal(0.5, DayAt(set));
        Assert.Equal(0, DayAt(set.AddMinutes(30)));
        Assert.All(result.Stops, s => Assert.InRange(s.Daylight, 0, 1));
        Assert.Equal(0, result.Stops[0].Progress);
        Assert.Equal(1, result.Stops[^1].Progress);
        Assert.True(result.Stops.Zip(result.Stops.Skip(1)).All(pair => pair.First.Progress < pair.Second.Progress));

        double DayAt(DateTimeOffset instant) => Assert.Single(result.Stops,
            s => Math.Abs(s.Progress - new TimeAxisMapper().Map(instant, window)) < 1e-9).Daylight;
    }

    [Fact]
    public void Window_edges_blend_using_events_outside_window_but_only_visible_events_get_markers()
    {
        var window = Window("2026-10-27T04:00:00Z", "2026-10-28T04:00:00Z");
        var before = window.Start.AddMinutes(-15);
        var after = window.End.AddMinutes(15);
        var fake = new FixedCalculator(false, new(before, SolarEventKind.Sunrise), new(after, SolarEventKind.Sunset));
        var result = new SolarEventService(fake).Calculate(window, Equator, TimeZoneInfo.Utc);
        Assert.Empty(result.Markers);
        Assert.Equal(0.84375, result.Stops[0].Daylight, 9);
        Assert.Equal(0.84375, result.Stops[^1].Daylight, 9);
        Assert.Equal(new DisplayDayService(TimeZoneInfo.Utc).ResolveCalendarTime(window.Date, TimeOnly.MinValue), fake.LastStart);
        Assert.Equal(window.End.AddMinutes(30), fake.LastEnd);
    }

    [Theory]
    [InlineData(23)]
    [InlineData(25)]
    public void Arbitrary_event_count_and_dst_duration_do_not_assume_one_pair(int hours)
    {
        var start = At("2026-10-27T04:00:00Z");
        var window = new DisplayDayWindow(new(2026, 10, 27), start, start.AddHours(hours));
        var fake = new FixedCalculator(false, new(start, SolarEventKind.Sunrise),
            new(start.AddHours(12), SolarEventKind.Sunset), new(start.AddHours(22), SolarEventKind.Sunrise),
            new(window.End, SolarEventKind.Sunset));
        var result = new SolarEventService(fake).Calculate(window, Equator, TimeZoneInfo.Utc);
        Assert.Equal(3, result.Markers.Count);
        Assert.Equal(0, result.Markers[0].Progress);
        Assert.Equal(12.0 / hours, result.Markers[1].Progress, 10);
        Assert.Equal(22.0 / hours, result.Markers[2].Progress, 10);
        Assert.DoesNotContain(result.Markers, m => m.Instant == window.End);
        Assert.Equal(0.5, result.Stops[^1].Daylight, 9);
    }

    [Fact]
    public void Single_event_is_supported_without_inventing_its_pair()
    {
        var window = Window("2026-10-27T04:00:00Z", "2026-10-28T04:00:00Z");
        var fake = new FixedCalculator(true, new SolarEvent(window.Start.AddHours(10), SolarEventKind.Sunset));
        var result = new SolarEventService(fake).Calculate(window, Equator, TimeZoneInfo.Utc);
        Assert.Single(result.Markers);
        Assert.Equal(SolarEventKind.Sunset, result.Markers[0].Kind);
        Assert.Equal(1, result.Stops[0].Daylight);
        Assert.Equal(0, result.Stops[^1].Daylight);
    }

    [Fact]
    public void Real_events_cover_both_calendar_dates_and_obey_dst_offsets()
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
        var window = new DisplayDayService(zone).GetWindow(At("2026-03-07T12:00:00-05:00"));
        var result = new SolarEventService(calculator).Calculate(window, new(40.7128, -74.006), zone);
        Assert.Equal(TimeSpan.FromHours(23), window.End - window.Start);
        Assert.Equal(2, result.Markers.Count);
        Assert.All(result.Markers, m =>
        {
            Assert.Equal(zone.GetUtcOffset(m.Instant), m.Instant.Offset);
            Assert.InRange(m.Progress, 0, 1);
        });
        // This longitude displayed in UTC puts the next day's sunrise after midnight.
        var utcWindow = Window("2026-10-27T04:00:00Z", "2026-10-28T04:00:00Z");
        var crossDate = new SolarEventService(calculator).Calculate(utcWindow, new(30, 80), TimeZoneInfo.Utc);
        Assert.Contains(crossDate.Markers, m => m.Instant.Day == 27);
        Assert.Contains(crossDate.Markers, m => m.Instant.Day == 28);
    }

    [Fact]
    public void Missing_location_never_invokes_calculator_and_failure_returns_neutral_snapshot()
    {
        var fake = new FixedCalculator(false) { Fail = true };
        var service = new TimelineService(new FixedClock(At("2026-10-27T16:00:00Z")), fake);
        Assert.Null(service.Capture(TimeZoneInfo.Utc).Solar);
        Assert.Equal(0, fake.Calls);
        var failed = service.Capture(TimeZoneInfo.Utc, Equator);
        Assert.Null(failed.Solar);
        Assert.Contains("计算失败", failed.SolarMessage);
        Assert.Equal(0.5, failed.Progress);
        fake.Fail = false;
        Assert.NotNull(service.Capture(TimeZoneInfo.Utc, Equator).Solar);
    }

    [Fact]
    public void Location_date_and_zone_changes_invalidate_cached_solar_data()
    {
        var clock = new FixedClock(At("2026-10-27T16:00:00Z"));
        var fake = new FixedCalculator(false);
        var service = new TimelineService(clock, fake);
        service.Capture(TimeZoneInfo.Utc, Equator);
        service.Capture(TimeZoneInfo.Utc, Equator);
        Assert.Equal(1, fake.Calls);
        service.Capture(TimeZoneInfo.Utc, new(30, 110));
        Assert.Equal(2, fake.Calls);
        clock.UtcNow = clock.UtcNow.AddDays(1);
        service.Capture(TimeZoneInfo.Utc, new(30, 110));
        Assert.Equal(3, fake.Calls);
        service.Capture(TimeZoneInfo.CreateCustomTimeZone("Test/Other", TimeSpan.Zero, "Other", "Other"), new(30, 110));
        Assert.Equal(4, fake.Calls);
    }

    [Theory]
    [InlineData(double.NaN, 0)]
    [InlineData(91, 0)]
    [InlineData(-91, 0)]
    [InlineData(0, 181)]
    [InlineData(0, -181)]
    [InlineData(0, double.PositiveInfinity)]
    public void Invalid_coordinates_are_rejected(double latitude, double longitude) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new GeoLocation(latitude, longitude));

    private sealed class FixedCalculator(bool initiallyDaylight, params SolarEvent[] events) : ISolarEventCalculator
    {
        public int Calls { get; private set; }
        public bool Fail { get; set; }
        public DateTimeOffset LastStart { get; private set; }
        public DateTimeOffset LastEnd { get; private set; }
        public SolarCalculation Calculate(DateTimeOffset start, DateTimeOffset end, GeoLocation location)
        {
            Calls++;
            LastStart = start;
            LastEnd = end;
            if (Fail) throw new InvalidOperationException("Simulated calculation failure");
            return new SolarCalculation(initiallyDaylight, events);
        }
    }

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = now.ToUniversalTime();
    }
}
