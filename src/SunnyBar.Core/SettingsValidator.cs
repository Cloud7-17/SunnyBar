using System.Globalization;

namespace SunnyBar.Core;

public static class SettingsValidator
{
    public static IReadOnlyList<string> Validate(AppSettings settings)
    {
        var errors = new List<string>();
        if (settings.SchemaVersion != 1) errors.Add("不支持此设置版本。");
        if (settings.DayStart != "04:00") errors.Add("当前展示日必须从 04:00 开始。");
        if (settings.Location is { } location)
        {
            if (string.IsNullOrWhiteSpace(location.Name) || location.Name.Length > 80) errors.Add("地点名称须为 1–80 个字符。");
            if (!InRange(location.Latitude, -90, 90)) errors.Add("纬度须为 -90 到 90。");
            if (!InRange(location.Longitude, -180, 180)) errors.Add("经度须为 -180 到 180。");
        }
        if (settings.Appearance is not { } appearance) errors.Add("外观设置不能为空。");
        else
        {
            if (!InRange(appearance.WidthRatio, 0.2, 1)) errors.Add("宽度比例须为 0.2 到 1。");
            if (!InRange(appearance.BarHeightDip, 6, 24)) errors.Add("色条高度须为 6–24 DIP。");
            if (!InRange(appearance.ScheduleHeightRatio, 0.1, 1)) errors.Add("日程色带高度占比须为 10%–100%。");
            if (!InRange(appearance.OffsetFromTaskbarDip, 0, 80)) errors.Add("底部间距须为 0–80 DIP。");
            if (!InRange(appearance.Opacity, 0.2, 1)) errors.Add("不透明度须为 0.2 到 1。");
            if (!IsColor(appearance.DayColor) || !IsColor(appearance.NightColor)) errors.Add("日夜颜色须为 #RRGGBB。");
            if (appearance.TransitionMinutes is < 5 or > 120) errors.Add("过渡半宽须为 5–120 分钟。");
            if (appearance.Indicator is not { } indicator) errors.Add("指针设置不能为空。");
            else if (indicator.Kind == "builtin")
            {
                if (indicator.Value is not ("dot" or "star")) errors.Add("内置指针须为 dot 或 star。");
            }
            else if (indicator.Kind == "emoji")
            {
                if (string.IsNullOrWhiteSpace(indicator.Value) || indicator.Value.Length > 32) errors.Add("Emoji 须为 1–32 个字符。");
            }
            else if (indicator.Kind == "png")
            {
                if (string.IsNullOrWhiteSpace(indicator.Value) || indicator.Value.Length > 512) errors.Add("PNG 路径无效。");
            }
            else errors.Add("指针类型须为 builtin、emoji 或 png。");
        }
        if (settings.DailyMarkers is null) errors.Add("每日标记列表不能为空。");
        else
        {
            if (settings.DailyMarkers.Count > 100) errors.Add("每日标记最多 100 个。");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var marker in settings.DailyMarkers)
            {
                if (marker is null) { errors.Add("每日标记不能为空。"); continue; }
                if (string.IsNullOrWhiteSpace(marker.Id) || marker.Id.Length > 80 || !ids.Add(marker.Id)) errors.Add("标记 ID 须非空且不能重复。");
                if (string.IsNullOrWhiteSpace(marker.Label) || marker.Label.Length > 40) errors.Add("标记标签须为 1–40 个字符。");
                if (!TimeOnly.TryParseExact(marker.Time, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)) errors.Add($"标记“{marker.Label}”的时间须为 HH:mm。");
                if (!IsColor(marker.Color)) errors.Add($"标记“{marker.Label}”的颜色须为 #RRGGBB。");
            }
        }
        return errors;
    }

    public static bool IsColor(string? value) => value is { Length: 7 } && value[0] == '#'
        && value.AsSpan(1).ToArray().All(Uri.IsHexDigit);
    private static bool InRange(double value, double low, double high) => double.IsFinite(value) && value >= low && value <= high;
}

public sealed class SettingsValidationException(IReadOnlyList<string> errors) : Exception(string.Join(Environment.NewLine, errors));
