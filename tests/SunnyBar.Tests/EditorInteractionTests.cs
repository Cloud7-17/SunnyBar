using SunnyBar.Core;
using Xunit;

namespace SunnyBar.Tests;

public sealed class EditorInteractionTests
{
    [Fact]
    public void Double_click_uses_time_and_distance_and_consumes_pairs()
    {
        var clicks = new BarDoubleClickTracker();
        Assert.False(clicks.Register(1000, 20, 20, 500, 4, 4));
        Assert.True(clicks.Register(1500, 22, 22, 500, 4, 4));
        Assert.False(clicks.Register(1510, 22, 22, 500, 4, 4));
        Assert.True(clicks.Register(1520, 22, 22, 500, 4, 4));
    }

    [Theory]
    [InlineData(501, 0, 0)]
    [InlineData(100, 3, 0)]
    [InlineData(100, 0, 3)]
    [InlineData(-1, 0, 0)]
    public void Unrelated_clicks_do_not_open_editor(int elapsed, int x, int y)
    {
        var clicks = new BarDoubleClickTracker();
        clicks.Register(1000, 0, 0, 500, 4, 4);
        Assert.False(clicks.Register(1000 + elapsed, x, y, 500, 4, 4));
    }

    [Fact]
    public void Only_real_endpoints_are_marked_at_display_boundaries()
    {
        var now = new DateTimeOffset(2026, 10, 28, 16, 0, 0, TimeSpan.Zero);
        var window = new DisplayDayService(TimeZoneInfo.Utc).GetWindow(now);
        ScheduleEvent Event(DateTimeOffset start, DateTimeOffset end) => new() { Title = "测试", StartAt = start, EndAt = end };
        var crossing = Event(window.Start.AddHours(-1), window.End.AddHours(1));
        var starts = Event(window.Start, window.Start.AddHours(1));
        var ends = Event(window.End.AddHours(-1), window.End);
        var outside = Event(window.End, window.End.AddHours(1));
        var state = new ScheduleService().Capture([crossing, starts, ends, outside], window, now);
        var boundaries = ScheduleService.Boundaries(state, window).ToArray();
        Assert.Equal(4, boundaries.Length);
        Assert.DoesNotContain(boundaries, item => item.Event.Id == crossing.Id || item.Event.Id == outside.Id);
        Assert.Contains(boundaries, item => item.Instant == window.Start && item.IsStart);
        Assert.Contains(boundaries, item => item.Instant == window.End && !item.IsStart);
    }
}
