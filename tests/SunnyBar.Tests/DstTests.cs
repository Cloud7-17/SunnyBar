using SunnyBar.Core;
using Xunit;

namespace SunnyBar.Tests;

public sealed class DstTests
{
    // Explicit rules keep these tests independent of OS time-zone database updates.
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.CreateCustomTimeZone(
        "Test/Eastern", TimeSpan.FromHours(-5), "Test Eastern", "Standard", "Daylight",
        [TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(
            new DateTime(2020, 1, 1), new DateTime(2035, 12, 31), TimeSpan.FromHours(1),
            TimeZoneInfo.TransitionTime.CreateFloatingDateRule(new DateTime(1, 1, 1, 2, 0, 0), 3, 2, DayOfWeek.Sunday),
            TimeZoneInfo.TransitionTime.CreateFloatingDateRule(new DateTime(1, 1, 1, 2, 0, 0), 11, 1, DayOfWeek.Sunday))]);

    private readonly DisplayDayService days = new(Zone);
    private readonly TimeAxisMapper mapper = new();

    [Theory]
    [InlineData("2026-03-07T12:00:00-05:00", 23, -5, -4)]
    [InlineData("2026-10-31T12:00:00-04:00", 25, -4, -5)]
    public void Window_ends_at_next_calendar_four_am_with_actual_duration(
        string now, int hours, int startOffset, int endOffset)
    {
        var window = days.GetWindow(DateTimeOffset.Parse(now));
        Assert.Equal(TimeSpan.FromHours(hours), window.End - window.Start);
        Assert.Equal(4, window.Start.Hour);
        Assert.Equal(4, window.End.Hour);
        Assert.Equal(TimeSpan.FromHours(startOffset), window.Start.Offset);
        Assert.Equal(TimeSpan.FromHours(endOffset), window.End.Offset);
        Assert.Equal(0.5, mapper.Map(window.Start.AddHours(hours / 2.0), window), 10);
    }

    [Fact]
    public void Repeated_hour_moves_forward_in_elapsed_time()
    {
        var first = DateTimeOffset.Parse("2026-11-01T01:30:00-04:00");
        var second = DateTimeOffset.Parse("2026-11-01T01:30:00-05:00");
        var window = days.GetWindow(first);
        Assert.Equal(window.Start, days.GetWindow(second).Start);
        Assert.Equal(21.5 / 25, mapper.Map(first, window), 10);
        Assert.Equal(22.5 / 25, mapper.Map(second, window), 10);
        Assert.True(mapper.Map(second, window) > mapper.Map(first, window));
        // A daily wall-clock time has no offset: choose its first occurrence explicitly.
        Assert.Equal(first, days.ResolveDailyTime(window, new TimeOnly(1, 30)));
    }

    [Fact]
    public void Spring_gap_advances_monotonically_and_invalid_daily_time_uses_next_valid_minute()
    {
        var before = DateTimeOffset.Parse("2026-03-08T01:59:00-05:00");
        var after = DateTimeOffset.Parse("2026-03-08T03:00:00-04:00");
        var window = days.GetWindow(before);
        Assert.Equal(1.0 / (23 * 60), mapper.Map(after, window) - mapper.Map(before, window), 10);
        Assert.Equal(after, days.ResolveDailyTime(window, new TimeOnly(2, 30)));
    }

    [Theory]
    [InlineData("2026-03-07T12:00:00-05:00", 4.0 / 23)]
    [InlineData("2026-10-31T12:00:00-04:00", 4.0 / 25)]
    public void Dst_ticks_are_mapped_by_instants_rather_than_even_spacing(string now, double secondTick)
    {
        var snapshot = new TimelineService(new FixedClock(DateTimeOffset.Parse(now))).Capture(Zone);
        Assert.Equal(secondTick, snapshot.Ticks[1].Progress, 10);
        Assert.Equal(1, snapshot.Ticks[^1].Progress);
    }

    [Fact]
    public void Every_elapsed_minute_is_monotonic_across_both_transitions()
    {
        foreach (var now in new[] { "2026-03-07T12:00:00-05:00", "2026-10-31T12:00:00-04:00" })
        {
            var window = days.GetWindow(DateTimeOffset.Parse(now));
            double previous = -1;
            for (var instant = window.Start; instant <= window.End; instant = instant.AddMinutes(1))
            {
                var current = mapper.Map(instant, window);
                Assert.True(current > previous);
                previous = current;
            }
            Assert.Equal(1, previous);
            Assert.Equal(0, mapper.Map(window.End, days.GetWindow(window.End)));
        }
    }

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow => now.ToUniversalTime();
    }
}
