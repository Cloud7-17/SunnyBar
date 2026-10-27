using System.Globalization;
using System.Windows;
using System.Windows.Media;
using SunnyBar.Core;

namespace SunnyBar.App.Controls;

/// <summary>Draws a preview from one immutable snapshot; contains no time-of-day arithmetic.</summary>
public sealed class TimelineControl : FrameworkElement
{
    public static readonly DependencyProperty SnapshotProperty = DependencyProperty.Register(
        nameof(Snapshot), typeof(TimelineSnapshot), typeof(TimelineControl),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty IsCompactProperty = DependencyProperty.Register(
        nameof(IsCompact), typeof(bool), typeof(TimelineControl),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty IndicatorImageProperty = DependencyProperty.Register(
        nameof(IndicatorImage), typeof(ImageSource), typeof(TimelineControl),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public ImageSource? IndicatorImage
    {
        get => (ImageSource?)GetValue(IndicatorImageProperty);
        set => SetValue(IndicatorImageProperty, value);
    }

    public bool IsCompact
    {
        get => (bool)GetValue(IsCompactProperty);
        set => SetValue(IsCompactProperty, value);
    }

    private static readonly Brush BarBrush = new SolidColorBrush(Color.FromRgb(74, 96, 119));
    private static readonly Brush TickBrush = new SolidColorBrush(Color.FromRgb(160, 177, 196));
    private static readonly Brush NowBrush = new SolidColorBrush(Color.FromRgb(255, 207, 112));
    private static readonly Geometry Moon = new CombinedGeometry(GeometryCombineMode.Exclude,
        new EllipseGeometry(new Point(0, 0), 7, 7), new EllipseGeometry(new Point(3, -3), 6.5, 6.5));
    private readonly TimeAxisMapper mapper = new();
    private readonly Typeface typeface = new("Segoe UI");

    public TimelineSnapshot? Snapshot
    {
        get => (TimelineSnapshot?)GetValue(SnapshotProperty);
        set => SetValue(SnapshotProperty, value);
    }

    public TimelineControl()
    {
        ClipToBounds = true;
        Focusable = false;
        SnapsToDevicePixels = true;
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        if (Snapshot is not { } state || ActualWidth < 80) return;
        var appearance = state.Appearance ?? new();
        var requiredHeight = IsCompact ? 16 + appearance.BarHeightDip + (appearance.ShowHourTicks ? 16 : 0) : 132;
        if (ActualHeight + 1 < requiredHeight)
            return;

        var left = IsCompact ? 0.0 : 32;
        var barTop = IsCompact ? 16 : 68;
        var barHeight = IsCompact ? appearance.BarHeightDip : 18;
        var width = ActualWidth - left * 2;
        Brush background = BarBrush;
        if (state.Solar is { } sunlight)
        {
            var night = (Color)ColorConverter.ConvertFromString(appearance.NightColor);
            var day = (Color)ColorConverter.ConvertFromString(appearance.DayColor);
            var gradient = new LinearGradientBrush { StartPoint = new Point(0, 0.5), EndPoint = new Point(1, 0.5) };
            foreach (var stop in sunlight.Stops)
                gradient.GradientStops.Add(new GradientStop(Mix(stop.Daylight, night, day), stop.Progress));
            gradient.Freeze();
            background = gradient;
        }
        if (IsCompact)
            drawingContext.DrawRectangle(background, null, new Rect(left, barTop, width, barHeight));
        else
            drawingContext.DrawRoundedRectangle(background, null, new Rect(left, barTop, width, barHeight), 9, 9);

        var labelBounds = new List<Rect>();
        if (state.Solar is { } solar)
            foreach (var marker in solar.Markers)
            {
                var x = mapper.ToX(marker.Instant, state.Window, left, width);
                if (IsCompact)
                {
                    drawingContext.PushTransform(new TranslateTransform(x, 7));
                    drawingContext.PushTransform(new ScaleTransform(0.65, 0.65));
                    DrawSolarIcon(drawingContext, marker.Kind, 0, 0);
                    drawingContext.Pop();
                    drawingContext.Pop();
                    drawingContext.DrawLine(new Pen(TickBrush, 1), new Point(x, 13), new Point(x, barTop));
                    continue;
                }
                DrawSolarIcon(drawingContext, marker.Kind, x, 22);
                drawingContext.DrawLine(new Pen(TickBrush, 1), new Point(x, 51), new Point(x, barTop - 3));
                var label = MakeText($"{(marker.Kind == SolarEventKind.Sunrise ? "日出" : "日落")} {marker.Instant:HH:mm}");
                var bounds = new Rect(Math.Clamp(x - label.Width / 2, 0, Math.Max(0, ActualWidth - label.Width)),
                    35, label.Width + 6, label.Height);
                if (!labelBounds.Any(prior => prior.IntersectsWith(bounds)))
                {
                    drawingContext.DrawText(label, bounds.TopLeft);
                    labelBounds.Add(bounds);
                }
            }

        if (state.DailyMarkers is { } markers)
            foreach (var marker in markers)
            {
                var x = mapper.ToX(marker.Instant, state.Window, left, width);
                var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(marker.Color));
                drawingContext.DrawLine(new Pen(brush, 1.5), new Point(x, barTop), new Point(x, barTop + barHeight));
                drawingContext.DrawRectangle(brush, null, new Rect(x - 2, barTop - 4, 4, 4));
                if (!IsCompact)
                {
                    var text = MakeText(marker.Label, 11, brush);
                    var bounds = new Rect(Math.Clamp(x - text.Width / 2, 0, Math.Max(0, ActualWidth - text.Width)), 51, text.Width + 5, text.Height);
                    if (!labelBounds.Any(prior => prior.IntersectsWith(bounds)))
                    {
                        drawingContext.DrawText(text, bounds.TopLeft);
                        labelBounds.Add(bounds);
                    }
                }
            }

        if (!IsCompact || appearance.ShowHourTicks)
        foreach (var tick in state.Ticks)
        {
            var x = mapper.ToX(tick.Instant, state.Window, left, width);
            drawingContext.DrawLine(new Pen(TickBrush, 1), new Point(x, barTop + barHeight + 1), new Point(x, IsCompact ? barTop + barHeight + 3 : 96));
            var text = MakeText(tick.Label, IsCompact ? 9 : 12);
            var labelLeft = Math.Clamp(x - text.Width / 2, 0, Math.Max(0, ActualWidth - text.Width));
            drawingContext.DrawText(text, new Point(labelLeft, IsCompact ? barTop + barHeight + 3 : 102));
        }

        var nowX = mapper.ToX(state.Now, state.Window, left, width);
        drawingContext.DrawLine(new Pen(NowBrush, 2), new Point(nowX, barTop - (IsCompact ? 3 : 9)),
            new Point(nowX, barTop + barHeight));
        var radius = IsCompact ? Math.Min(5, barHeight / 2 - 0.5) : 7;
        var center = new Point(Math.Clamp(nowX, radius + 1, Math.Max(radius + 1, ActualWidth - radius - 1)), barTop + barHeight / 2);
        var indicator = appearance.Indicator;
        if (indicator.Kind == "png" && IndicatorImage is { } image)
        {
            var size = IsCompact ? Math.Min(16, barHeight) : 22;
            drawingContext.DrawImage(image, new Rect(Math.Clamp(nowX - size / 2, 0, ActualWidth - size), center.Y - size / 2, size, size));
        }
        else if (indicator.Kind == "emoji")
        {
            drawingContext.DrawEllipse(Brushes.White, new Pen(Brushes.MidnightBlue, 1), center, radius + 1, radius + 1);
            var text = new FormattedText(indicator.Value, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                new Typeface("Segoe UI Emoji"), IsCompact ? Math.Max(4, barHeight * 0.7) : 18, Brushes.MidnightBlue, VisualTreeHelper.GetDpi(this).PixelsPerDip);
            drawingContext.DrawText(text, new Point(Math.Clamp(center.X - text.Width / 2, 0, Math.Max(0, ActualWidth - text.Width)), center.Y - text.Height / 2));
        }
        else if (indicator.Kind == "builtin" && indicator.Value == "star")
        {
            var shape = new StreamGeometry();
            using (var path = shape.Open())
                for (var point = 0; point < 10; point++)
                {
                    var angle = point * Math.PI / 5 - Math.PI / 2;
                    var r = point % 2 == 0 ? radius : radius * 0.45;
                    var position = new Point(center.X + Math.Cos(angle) * r, center.Y + Math.Sin(angle) * r);
                    if (point == 0) path.BeginFigure(position, true, true);
                    else path.LineTo(position, true, false);
                }
            drawingContext.DrawGeometry(NowBrush, new Pen(Brushes.White, 1), shape);
        }
        else drawingContext.DrawEllipse(NowBrush, new Pen(Brushes.White, 1.5), center, radius, radius);
    }

    private FormattedText MakeText(string label, double fontSize = 12, Brush? brush = null) => new(label, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
        typeface, fontSize, brush ?? TickBrush, VisualTreeHelper.GetDpi(this).PixelsPerDip);

    private static Color Mix(double daylight, Color night, Color day)
    {
        return Color.FromRgb((byte)Math.Round(night.R + (day.R - night.R) * daylight),
            (byte)Math.Round(night.G + (day.G - night.G) * daylight), (byte)Math.Round(night.B + (day.B - night.B) * daylight));
    }

    private static void DrawSolarIcon(DrawingContext context, SolarEventKind kind, double x, double y)
    {
        if (kind == SolarEventKind.Sunset)
        {
            context.PushTransform(new TranslateTransform(x, y));
            context.DrawGeometry(Brushes.LightSteelBlue, null, Moon);
            context.Pop();
            return;
        }
        context.DrawEllipse(NowBrush, null, new Point(x, y), 4, 4);
        for (var ray = 0; ray < 8; ray++)
        {
            var angle = ray * Math.PI / 4;
            context.DrawLine(new Pen(NowBrush, 1.5),
                new Point(x + Math.Cos(angle) * 6, y + Math.Sin(angle) * 6),
                new Point(x + Math.Cos(angle) * 9, y + Math.Sin(angle) * 9));
        }
    }
}
