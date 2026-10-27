using SunnyBar.Core;
using Xunit;

namespace SunnyBar.Tests;

public sealed class OverlayLayoutTests
{
    [Theory]
    [InlineData(96, 32, 0)]
    [InlineData(144, 48, 0)]
    [InlineData(192, 64, 0)]
    public void Full_width_bar_sits_flush_above_taskbar_at_each_dpi(int dpi, int height, int gap)
    {
        var work = new PixelRect(0, 0, 1920, 1040);
        var actual = OverlayLayoutCalculator.Calculate(work, dpi);
        Assert.Equal(1920, actual.Width);
        Assert.Equal(work.Left, actual.Left);
        Assert.Equal(work.Right, actual.Right);
        Assert.Equal(height, actual.Height);
        Assert.Equal(gap, work.Bottom - actual.Bottom);
        Assert.True(actual.Top >= work.Top);
    }

    [Fact]
    public void Placement_preserves_negative_virtual_screen_origins()
    {
        var work = new PixelRect(-1920, -1080, 0, -40);
        Assert.Equal(new PixelRect(-1920, -72, 0, -40), OverlayLayoutCalculator.Calculate(work, 96));
    }

    [Fact]
    public void New_work_area_repositions_above_changed_taskbar_or_resolution()
    {
        var initial = OverlayLayoutCalculator.Calculate(new(0, 0, 1920, 1040), 96);
        var changed = OverlayLayoutCalculator.Calculate(new(0, 0, 2560, 1360), 144);
        Assert.NotEqual(initial, changed);
        Assert.Equal(2560, changed.Width);
        Assert.Equal(1360, changed.Bottom);
    }

    [Fact]
    public void Very_small_work_area_does_not_produce_negative_coordinates_or_size()
    {
        Assert.Equal(new PixelRect(0, 0, 1, 1), OverlayLayoutCalculator.Calculate(new(0, 0, 1, 1), 192));
    }

    [Fact]
    public void Invalid_work_area_or_dpi_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => OverlayLayoutCalculator.Calculate(new(0, 0, 0, 1080), 96));
        Assert.Throws<ArgumentException>(() => OverlayLayoutCalculator.Calculate(new(0, 10, 1920, 0), 96));
        Assert.Throws<ArgumentOutOfRangeException>(() => OverlayLayoutCalculator.Calculate(new(0, 0, 1920, 1080), 0));
    }
}
