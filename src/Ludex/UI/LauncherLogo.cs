using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;

namespace Ludex.UI;

/// <summary>
/// Значки лаунчеров для панели «Интеграции»: вместо двух букв — узнаваемый знак
/// в фирменном цвете. Рисуется векторно, поэтому не зависит от темы и DPI.
/// </summary>
public static class LauncherLogo
{
    public static Control Create(string key, double size = 32)
    {
        var (from, to) = Palette(key);

        var tile = new Border
        {
            Width = size,
            Height = size,
            CornerRadius = new CornerRadius(size * 0.26),
            ClipToBounds = true,
            VerticalAlignment = VerticalAlignment.Center,
            Background = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(Color.Parse(from), 0),
                    new GradientStop(Color.Parse(to), 1),
                },
            },
        };

        // сетка знака задана в координатах 32×32 и масштабируется под нужный размер
        var s = size / 32.0;
        var canvas = new Canvas { Width = 32 * s, Height = 32 * s };
        tile.Child = canvas;
        var white = Brushes.White;

        switch (key)
        {
            case "steam":
                // кольцо, малое колесо и рычаг с клапаном — знак не читается как «запрещено»
                Add(canvas, Ring(16, 16, 7.6, 1.6, white, s));
                Add(canvas, Dot(13.4, 18.2, 2.4, white, s));
                Add(canvas, Line(15.0, 17.2, 21.0, 13.4, 1.8, white, s));
                Add(canvas, Bar(19.9, 10.5, 3.0, 3.0, white, s));
                break;

            case "epic":
                // три полосы, соединённые слева — фирменная «E»
                Add(canvas, Bar(10.0, 9.0, 2.3, 14.0, white, s));
                Add(canvas, Bar(10.0, 9.0, 12.0, 2.6, white, s));
                Add(canvas, Bar(10.0, 14.7, 8.6, 2.6, white, s));
                Add(canvas, Bar(10.0, 20.4, 5.8, 2.6, white, s));
                break;

            case "heroic":
                // ракета: корпус с окном, крылья и пламя
                Add(canvas, Polygon(new[]
                {
                    new Point(16, 4.4), new Point(19.6, 12.0), new Point(19.6, 18.0),
                    new Point(12.4, 18.0), new Point(12.4, 12.0),
                }, white, s));
                Add(canvas, Polygon(new[]
                {
                    new Point(12.4, 12.6), new Point(8.8, 18.6), new Point(12.4, 17.6),
                }, white, s));
                Add(canvas, Polygon(new[]
                {
                    new Point(19.6, 12.6), new Point(23.2, 18.6), new Point(19.6, 17.6),
                }, white, s));
                Add(canvas, Polygon(new[]
                {
                    new Point(16, 19.6), new Point(18.0, 24.4), new Point(14.0, 24.4),
                }, white, s));
                Add(canvas, Dot(16, 11.4, 1.5, new SolidColorBrush(Color.Parse(to)), s));
                break;

            case "lutris":
                // геймпад
                Add(canvas, RoundedRing(9.0, 12.4, 14.0, 8.6, 1.6, white, s));
                Add(canvas, Bar(12.0, 15.4, 1.7, 5.2, white, s));
                Add(canvas, Bar(10.3, 17.1, 5.2, 1.7, white, s));
                Add(canvas, Dot(20.0, 16.9, 1.2, white, s));
                Add(canvas, Dot(21.9, 19.2, 1.2, white, s));
                break;

            case "ubisoft":
                Add(canvas, Arc("M 24.2,11.6 A 8.2,8.2 0 1 0 12.6,24.2", 1.9, white, s));
                Add(canvas, Dot(23.6, 10.4, 1.9, white, s));
                break;

            case "battlenet":
                Add(canvas, Polygon(new[]
                {
                    new Point(16, 4.6), new Point(19.0, 13.0), new Point(27.4, 16),
                    new Point(19.0, 19.0), new Point(16, 27.4), new Point(13.0, 19.0),
                    new Point(4.6, 16), new Point(13.0, 13.0),
                }, white, s));
                break;

            case "ea":
                tile.Child = Wordmark("EA", 11.5, white);
                break;

            case "gog":
                tile.Child = Wordmark("GOG", 8.0, white);
                break;

            default:
                tile.Child = Wordmark(key.Length >= 2 ? key[..2].ToUpperInvariant() : key.ToUpperInvariant(),
                    10, white);
                break;
        }

        return tile;
    }

    private static (string From, string To) Palette(string key) => key switch
    {
        "steam" => ("#1B3A5C", "#0C1B2A"),
        "epic" => ("#232323", "#0D0D0D"),
        "gog" => ("#7A2BC9", "#3A0E72"),
        "heroic" => ("#5A43B8", "#2A1A55"),
        "lutris" => ("#FF9A2E", "#D8441A"),
        "ubisoft" => ("#2C2C3A", "#101018"),
        "ea" => ("#3A3A3A", "#151515"),
        "battlenet" => ("#1B4C86", "#0A1B33"),
        _ => ("#3A3A46", "#1A1A22"),
    };

    private static Control Wordmark(string text, double fontSize, IBrush brush)
    {
        var mark = Ui.T(text, fontSize, brush, FontWeight.Bold);
        mark.HorizontalAlignment = HorizontalAlignment.Center;
        mark.VerticalAlignment = VerticalAlignment.Center;
        mark.TextAlignment = TextAlignment.Center;
        // буквы в знаке должны идти вплотную
        mark.Margin = new Thickness(0, 0, 0, 0.5);
        return mark;
    }

    private static void Add(Canvas canvas, Control shape) => canvas.Children.Add(shape);

    private static Shape Ring(double cx, double cy, double r, double thickness, IBrush brush, double s) =>
        new Ellipse
        {
            Width = 2 * r * s,
            Height = 2 * r * s,
            Stroke = brush,
            StrokeThickness = thickness * s,
            Fill = Brushes.Transparent,
            Margin = new Thickness((cx - r) * s, (cy - r) * s, 0, 0),
        };

    private static Shape Dot(double cx, double cy, double r, IBrush brush, double s) =>
        new Ellipse
        {
            Width = 2 * r * s,
            Height = 2 * r * s,
            Fill = brush,
            Margin = new Thickness((cx - r) * s, (cy - r) * s, 0, 0),
        };

    private static Control RoundedRing(double x, double y, double w, double h, double thickness,
        IBrush brush, double s) =>
        new Border
        {
            Width = w * s,
            Height = h * s,
            CornerRadius = new CornerRadius(h * s / 2),
            BorderBrush = brush,
            BorderThickness = new Thickness(thickness * s),
            Background = Brushes.Transparent,
            Margin = new Thickness(x * s, y * s, 0, 0),
        };

    private static Shape Bar(double x, double y, double w, double h, IBrush brush, double s) =>
        new Rectangle
        {
            Width = w * s,
            Height = h * s,
            RadiusX = 1.2 * s,
            RadiusY = 1.2 * s,
            Fill = brush,
            Margin = new Thickness(x * s, y * s, 0, 0),
        };

    private static Shape Line(double x1, double y1, double x2, double y2, double thickness,
        IBrush brush, double s) =>
        new Avalonia.Controls.Shapes.Line
        {
            StartPoint = new Point(x1 * s, y1 * s),
            EndPoint = new Point(x2 * s, y2 * s),
            Stroke = brush,
            StrokeThickness = thickness * s,
            Margin = new Thickness(0),
        };

    private static Shape Polygon(Point[] points, IBrush brush, double s)
    {
        var scaled = new List<Point>(points.Length);
        foreach (var p in points) scaled.Add(new Point(p.X * s, p.Y * s));

        return new Polygon { Points = scaled, Fill = brush };
    }

    private static Shape Arc(string data, double thickness, IBrush brush, double s) =>
        new Path
        {
            Data = Geometry.Parse(data),
            Stroke = brush,
            StrokeThickness = thickness * s,
            StrokeLineCap = PenLineCap.Round,
            Fill = Brushes.Transparent,
            RenderTransform = new ScaleTransform(s, s),
            RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative),
        };
}
