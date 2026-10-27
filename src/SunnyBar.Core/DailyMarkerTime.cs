using System.Globalization;

namespace SunnyBar.Core;

public static class DailyMarkerTime
{
    public static string FromParts(string hour, string minute)
    {
        static int Part(string text, int maximum, string label) => text.Trim() is { Length: >= 1 and <= 2 } value
            && int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var number) && number <= maximum
            ? number : throw new FormatException($"标记的{label}须为 0–{maximum} 的整数。");
        return $"{Part(hour, 23, "小时"):00}:{Part(minute, 59, "分钟"):00}";
    }
}
