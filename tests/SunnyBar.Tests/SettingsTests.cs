using System.Text.Json;
using SunnyBar.Core;
using Xunit;

namespace SunnyBar.Tests;

public sealed class SettingsTests
{
    private static SettingsService Store() => new(Path.Combine(AppContext.BaseDirectory, "settings-tests", Guid.NewGuid().ToString("N")));

    [Fact]
    public void First_launch_uses_requested_Beijing_preset_without_creating_file()
    {
        var store = Store();
        var result = store.Load();
        Assert.True(result.IsFirstLaunch);
        Assert.True(result.CanSave);
        Assert.Equal("北京", result.Settings.Location?.Name);
        Assert.Equal(39.9042, result.Settings.Location!.Latitude);
        Assert.Equal(116.4074, result.Settings.Location.Longitude);
        Assert.False(result.Settings.StartWithWindows);
        Assert.Empty(result.Settings.DailyMarkers);
        Assert.False(File.Exists(store.FilePath));
        Assert.Empty(SettingsValidator.Validate(result.Settings));
    }

    [Fact]
    public void Save_and_reload_preserve_user_values_and_unknown_fields_at_every_level()
    {
        var settings = JsonSerializer.Deserialize<AppSettings>("""
            {"SchemaVersion":1,"Location":{"Name":"Test","Latitude":30,"Longitude":110,"futureLocation":7},
             "Appearance":{"WidthRatio":0.75,"Opacity":0.65,"DayColor":"#ABCDEF","Indicator":{"Kind":"emoji","Value":"🌻","futureIcon":true},"futureTheme":42},
             "DailyMarkers":[{"Id":"sleep","Label":"睡觉","Time":"00:30","Color":"#112233","Enabled":true,"futureMarker":"keep"}],
             "StartWithWindows":true,"futureRoot":{"keep":1}}
            """)!;
        var store = Store();
        store.Save(settings);
        var restored = store.Load().Settings;
        Assert.Equal(settings.Location, restored.Location! with { Extra = settings.Location!.Extra });
        Assert.Equal(0.75, restored.Appearance.WidthRatio);
        Assert.Equal(0.65, restored.Appearance.Opacity);
        Assert.Equal("🌻", restored.Appearance.Indicator.Value);
        Assert.Equal("00:30", Assert.Single(restored.DailyMarkers).Time);
        Assert.True(restored.StartWithWindows);
        Assert.Equal(1, restored.Extra!["futureRoot"].GetProperty("keep").GetInt32());
        Assert.Equal(7, restored.Location!.Extra!["futureLocation"].GetInt32());
        Assert.Equal(42, restored.Appearance.Extra!["futureTheme"].GetInt32());
        Assert.True(restored.Appearance.Indicator.Extra!["futureIcon"].GetBoolean());
        Assert.Equal("keep", restored.DailyMarkers[0].Extra!["futureMarker"].GetString());
        Assert.Empty(Directory.GetFiles(store.DirectoryPath, "*.tmp-*"));
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("null")]
    [InlineData("{\"appearance\":null}")]
    [InlineData("{\"dailyMarkers\":[null]}")]
    [InlineData("{\"location\":{\"name\":\"Test\",\"latitude\":200,\"longitude\":0}}")]
    public void Corruption_is_backed_up_before_replacing_with_defaults(string original)
    {
        var store = Store();
        Directory.CreateDirectory(store.DirectoryPath);
        File.WriteAllText(store.FilePath, original);
        var result = store.Load();
        Assert.Contains("备份", result.Message);
        Assert.Equal(original, File.ReadAllText(Assert.Single(Directory.GetFiles(store.DirectoryPath, "settings.corrupt-*.json"))));
        Assert.Equal("北京", result.Settings.Location?.Name);
        Assert.True(result.CanSave);
        Assert.Empty(store.Load().Message);
    }

    [Fact]
    public void Future_version_is_not_overwritten_or_migrated_backwards()
    {
        var store = Store();
        Directory.CreateDirectory(store.DirectoryPath);
        const string original = "{\"schemaVersion\":99,\"future\":\"keep\"}";
        File.WriteAllText(store.FilePath, original);
        Assert.False(store.Load().CanSave);
        Assert.Throws<IOException>(() => store.Save(new()));
        Assert.Equal(original, File.ReadAllText(store.FilePath));
    }

    [Fact]
    public void Legacy_version_migration_keeps_values_and_original_backup()
    {
        var store = Store();
        Directory.CreateDirectory(store.DirectoryPath);
        const string original = "{\"schemaVersion\":0,\"appearance\":{\"opacity\":0.55},\"future\":3}";
        File.WriteAllText(store.FilePath, original);
        var result = store.Load();
        Assert.Equal(1, result.Settings.SchemaVersion);
        Assert.Equal(0.55, result.Settings.Appearance.Opacity);
        Assert.Equal(3, result.Settings.Extra!["future"].GetInt32());
        Assert.Equal(original, File.ReadAllText(Assert.Single(Directory.GetFiles(store.DirectoryPath, "settings.v0-*.json"))));
        Assert.Equal(1, store.Load().Settings.SchemaVersion);
    }

    [Fact]
    public void Invalid_save_and_locked_read_do_not_destroy_existing_configuration()
    {
        var store = Store();
        store.Save(new());
        var original = File.ReadAllText(store.FilePath);
        Assert.Throws<SettingsValidationException>(() => store.Save(new() { Appearance = new() { WidthRatio = 9 } }));
        Assert.Equal(original, File.ReadAllText(store.FilePath));
        using (File.Open(store.FilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            Assert.False(store.Load().CanSave);
        Assert.Throws<IOException>(() => store.Save(new()));
        Assert.Equal(original, File.ReadAllText(store.FilePath));
        Assert.True(store.Load().CanSave);
        store.Save(new());
    }

    [Theory]
    [InlineData("24:00", "#112233")]
    [InlineData("9:00", "#112233")]
    [InlineData("00:30", "red")]
    [InlineData("00:30", "#GG0000")]
    public void Marker_time_and_color_are_validated(string time, string color)
    {
        Assert.NotEmpty(SettingsValidator.Validate(new() { DailyMarkers = [new() { Time = time, Color = color }] }));
    }

    [Fact]
    public void Duplicate_ids_nonfinite_coordinates_and_invalid_indicator_are_rejected()
    {
        Assert.NotEmpty(SettingsValidator.Validate(new() { Location = new() { Name = "Test", Latitude = double.NaN } }));
        Assert.NotEmpty(SettingsValidator.Validate(new() { DailyMarkers = [new() { Id = "same" }, new() { Id = "same" }] }));
        Assert.NotEmpty(SettingsValidator.Validate(new() { Appearance = new() { Indicator = new() { Kind = "unknown" } } }));
    }

    [Fact]
    public void Daily_markers_use_shared_cross_midnight_mapping_and_skip_disabled_markers()
    {
        var clock = new FixedClock(new(2026, 10, 27, 16, 0, 0, TimeSpan.Zero));
        var snapshot = new TimelineService(clock).Capture(TimeZoneInfo.Utc, dailyMarkers:
        [new() { Id = "work", Time = "09:00" }, new() { Id = "sleep", Time = "00:30" }, new() { Enabled = false }]);
        Assert.Equal(2, snapshot.DailyMarkers!.Count);
        Assert.Equal(5.0 / 24, snapshot.DailyMarkers[0].Progress, 10);
        Assert.Equal(20.5 / 24, snapshot.DailyMarkers[1].Progress, 10);
        Assert.Equal(28, snapshot.DailyMarkers[1].Instant.Day);
        Assert.All(snapshot.DailyMarkers, marker => Assert.Equal(new TimeAxisMapper().Map(marker.Instant, snapshot.Window), marker.Progress));
    }

    [Fact]
    public void Appearance_changes_geometry_without_changing_default_full_width_placement()
    {
        var actual = OverlayLayoutCalculator.Calculate(new(0, 0, 1920, 1040), 144,
            new() { WidthRatio = 0.5, BarHeightDip = 20, OffsetFromTaskbarDip = 10, ShowHourTicks = true });
        Assert.Equal(960, actual.Width);
        Assert.Equal(78, actual.Height);
        Assert.Equal(1025, actual.Bottom);
    }

    [Fact]
    public void Imported_png_is_copied_into_owned_directory_and_source_can_be_removed()
    {
        var store = Store();
        Directory.CreateDirectory(store.DirectoryPath);
        var source = Path.Combine(store.DirectoryPath, "source.png");
        var bytes = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAusB9Wl6V1sAAAAASUVORK5CYII=");
        File.WriteAllBytes(source, bytes);
        var relative = store.ImportPng(source);
        File.Delete(source);
        Assert.StartsWith("Indicators", relative);
        Assert.Equal(bytes, File.ReadAllBytes(store.ResolveAsset(relative)));
        var invalid = Path.Combine(store.DirectoryPath, "bad.png");
        File.WriteAllText(invalid, "not an image");
        Assert.Throws<IOException>(() => store.ImportPng(invalid));
    }

    private sealed class FixedClock(DateTimeOffset now) : IClock { public DateTimeOffset UtcNow => now; }
}
