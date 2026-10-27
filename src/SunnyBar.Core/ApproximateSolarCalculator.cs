namespace SunnyBar.Core;

/// <summary>
/// Offline USNO approximate solar coordinates plus NOAA altitude / 90.833° zenith convention.
/// See docs/SOLAR_CALCULATION.md for sources, accuracy, and the one-minute search limitation.
/// </summary>
public sealed class ApproximateSolarCalculator : ISolarEventCalculator
{
    private static readonly DateTimeOffset Epoch = new(2000, 1, 1, 12, 0, 0, TimeSpan.Zero);

    public SolarCalculation Calculate(DateTimeOffset start, DateTimeOffset end, GeoLocation location)
    {
        ArgumentNullException.ThrowIfNull(location);
        if (end <= start || end - start > TimeSpan.FromDays(3))
            throw new ArgumentException("Solar search interval must be positive and at most three days.", nameof(end));
        if (start.UtcDateTime.Year < 1800 || end.UtcDateTime.Year > 2200)
            throw new ArgumentOutOfRangeException(nameof(start), "Solar estimates support years 1800 through 2200.");

        var events = new List<SolarEvent>();
        var previous = start.AddMinutes(-1);
        var wasDay = IsDaylight(previous, location);
        var initiallyDaylight = IsDaylight(start.AddTicks(-1), location);
        while (previous < end)
        {
            var next = previous.AddMinutes(1);
            if (next > end) next = end;
            var isDay = IsDaylight(next, location);
            if (isDay != wasDay)
            {
                var low = previous;
                var high = next;
                while ((high - low).TotalSeconds > 0.1)
                {
                    var middle = low.AddTicks((high - low).Ticks / 2);
                    if (IsDaylight(middle, location) == wasDay) low = middle;
                    else high = middle;
                }
                if (high >= start && high < end)
                    events.Add(new SolarEvent(high, isDay ? SolarEventKind.Sunrise : SolarEventKind.Sunset));
            }
            previous = next;
            wasDay = isDay;
        }
        return new SolarCalculation(initiallyDaylight, events.AsReadOnly());
    }

    private static bool IsDaylight(DateTimeOffset instant, GeoLocation location)
    {
        var utc = instant.ToUniversalTime();
        var days = (utc - Epoch).TotalDays;
        var anomaly = Radians(Normalize(357.529 + 0.98560028 * days));
        var meanLongitude = Normalize(280.459 + 0.98564736 * days);
        var longitude = Radians(meanLongitude + 1.915 * Math.Sin(anomaly) + 0.020 * Math.Sin(2 * anomaly));
        var obliquity = Radians(23.439 - 0.00000036 * days);
        var rightAscension = Math.Atan2(Math.Cos(obliquity) * Math.Sin(longitude), Math.Cos(longitude)) * 180 / Math.PI;
        var declination = Math.Asin(Math.Sin(obliquity) * Math.Sin(longitude));
        var equationMinutes = 4 * (Normalize(meanLongitude - rightAscension + 180) - 180);
        var hourAngle = Radians((utc.TimeOfDay.TotalMinutes + equationMinutes + 4 * location.Longitude) / 4 - 180);
        var latitude = Radians(location.Latitude);
        var sinElevation = Math.Sin(latitude) * Math.Sin(declination)
            + Math.Cos(latitude) * Math.Cos(declination) * Math.Cos(hourAngle);
        return sinElevation >= Math.Sin(Radians(-0.833));
    }

    private static double Radians(double degrees) => degrees * Math.PI / 180;
    private static double Normalize(double degrees) => (degrees % 360 + 360) % 360;
}
