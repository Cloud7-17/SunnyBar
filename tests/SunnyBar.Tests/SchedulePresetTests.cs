using SunnyBar.Core;
using Xunit;

namespace SunnyBar.Tests;

public sealed class SchedulePresetTests
{
    [Fact]
    public void Class_presets_match_requested_seven_alternatives()
    {
        Assert.Equal(new[] { "08:00–09:35", "09:50–11:25", "09:50–12:15", "13:30–15:05", "15:20–16:55", "19:20–20:55", "19:20–21:45" },
            SchedulePresets.Classes.Select(item => item.TimeText));
        Assert.All(SchedulePresets.Classes, preset => Assert.Equal("上课", preset.Title));
    }

    [Theory]
    [InlineData(0, 95)]
    [InlineData(1, 95)]
    [InlineData(2, 145)]
    [InlineData(3, 95)]
    [InlineData(4, 95)]
    [InlineData(5, 95)]
    [InlineData(6, 145)]
    public void Preset_creates_single_event_on_selected_date(int index, int minutes)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Shanghai");
        var date = new DateTime(2026, 11, 5);
        var item = SchedulePresets.Classes[index].Create(date, zone, "#44AA88");
        Assert.Equal(date, item.StartAt.Date);
        Assert.Equal(date, item.EndAt.Date);
        Assert.Equal(TimeSpan.FromHours(8), item.StartAt.Offset);
        Assert.Equal(minutes, (item.EndAt - item.StartAt).TotalMinutes);
        Assert.Equal("#44AA88", item.Color);
        Assert.Empty(ScheduleValidator.Validate(new() { Events = [item] }));
    }

    [Fact]
    public void New_colors_avoid_existing_values_including_custom_and_lowercase_colors()
    {
        var used = ScheduleColors.Palette.Select(color => color.ToLowerInvariant()).ToList();
        var firstGenerated = ScheduleColors.Choose(used);
        used.Add(firstGenerated.ToLowerInvariant());
        var next = ScheduleColors.Choose(used);
        Assert.DoesNotContain(next.ToLowerInvariant(), used);
        Assert.True(SettingsValidator.IsColor(firstGenerated));
        Assert.True(SettingsValidator.IsColor(next));
        Assert.Equal(ScheduleColors.Palette[0], ScheduleColors.Choose([]));
    }

    [Fact]
    public void Color_selection_stays_unique_through_maximum_supported_event_count()
    {
        var colors = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < 1000; i++)
        {
            var color = ScheduleColors.Choose(colors);
            Assert.True(SettingsValidator.IsColor(color));
            Assert.True(colors.Add(color));
        }
    }
}
