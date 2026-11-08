using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace Ludex.UI;

/// <summary>
/// Выбор цвета «как в графических редакторах»:
/// квадрат насыщенность/яркость, полоса оттенков, кружок-предпросмотр и поля R/G/B.
/// Любой оттенок выбирается мышью — и в квадрате, и на полосе, и цифрами.
/// </summary>
public sealed class ColorPicker : StackPanel
{
    public const double SquareWidth = 252;
    public const double SquareHeight = 152;
    private const double SliderWidth = 148;
    private const double SliderHeight = 16;
    private const double MarkerSize = 14;

    private double _hue = 210;
    private double _saturation = 0.85;
    private double _value = 0.90;

    private readonly Canvas _square = new()
    {
        Width = SquareWidth,
        Height = SquareHeight,
        Background = Brushes.Transparent,
    };
    private readonly Border _squareHue = new() { Width = SquareWidth, Height = SquareHeight };
    private readonly Ellipse _squareMarker = new();
    private readonly Canvas _slider = new()
    {
        Width = SliderWidth,
        Height = SliderHeight,
        Background = Brushes.Transparent,
    };
    private readonly Ellipse _sliderMarker = new();
    private readonly Ellipse _preview = new();
    private readonly TextBox _rBox = null!;
    private readonly TextBox _gBox = null!;
    private readonly TextBox _bBox = null!;
    private TextBlock _hexLabel = null!;

    private bool _squareDragging;
    private bool _sliderDragging;
    private bool _updating;

    /// <summary>Цвет меняется на каждом движении мыши (живой предпросмотр).</summary>
    public event Action<Color>? ColorChanged;

    /// <summary>Пользователь закончил выбор (отпустил мышь / ввёл число) — можно сохранять.</summary>
    public event Action<Color>? ColorCommitted;

    public Color CurrentColor => HsvToColor(_hue, _saturation, _value);

    public ColorPicker()
    {
        Spacing = 10;

        BuildSquare();
        Children.Add(_square);

        Children.Add(BuildControls());

        _rBox = NewBox();
        _gBox = NewBox();
        _bBox = NewBox();

        _rBox.TextChanged += (_, _) => ApplyRgb();
        _gBox.TextChanged += (_, _) => ApplyRgb();
        _bBox.TextChanged += (_, _) => ApplyRgb();

        Children.Add(BuildRgbRow());

        Refresh();
    }

    // ---------------------------------------------------------------- сборка

    private void BuildSquare()
    {
        _squareHue.Background = new SolidColorBrush(HsvToColor(_hue, 1, 1));

        var saturationLayer = new Border
        {
            Width = SquareWidth,
            Height = SquareHeight,
            Background = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF), 0),
                    new GradientStop(Color.FromArgb(0x00, 0xFF, 0xFF, 0xFF), 1),
                },
            },
            IsHitTestVisible = false,
        };

        var valueLayer = new Border
        {
            Width = SquareWidth,
            Height = SquareHeight,
            Background = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(Color.FromArgb(0x00, 0x00, 0x00, 0x00), 0),
                    new GradientStop(Color.FromArgb(0xFF, 0x00, 0x00, 0x00), 1),
                },
            },
            IsHitTestVisible = false,
        };

        _squareMarker.Width = MarkerSize;
        _squareMarker.Height = MarkerSize;
        _squareMarker.Stroke = Brushes.White;
        _squareMarker.StrokeThickness = 2;
        _squareMarker.IsHitTestVisible = false;

        var frame = new Border
        {
            Width = SquareWidth,
            Height = SquareHeight,
            BorderBrush = Skin.GlassEdge,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            IsHitTestVisible = false,
        };

        _square.ClipToBounds = false;
        HorizontalAlignment = HorizontalAlignment.Left;
        _square.Children.Add(_squareHue);
        _square.Children.Add(saturationLayer);
        _square.Children.Add(valueLayer);
        _square.Children.Add(_squareMarker);
        _square.Children.Add(frame);

        _square.Cursor = new Cursor(StandardCursorType.Cross);

        _square.PointerPressed += (_, e) =>
        {
            if (!e.GetCurrentPoint(_square).Properties.IsLeftButtonPressed) return;
            e.Pointer.Capture(_square);
            _squareDragging = true;
            PickSquare(e.GetPosition(_square));
            e.Handled = true;
        };
        _square.PointerMoved += (_, e) =>
        {
            if (!_squareDragging) return;
            PickSquare(e.GetPosition(_square));
            e.Handled = true;
        };
        _square.PointerReleased += (_, e) =>
        {
            if (!_squareDragging) return;
            _squareDragging = false;
            e.Pointer.Capture(null);
            ColorCommitted?.Invoke(CurrentColor);
            e.Handled = true;
        };
    }

    private Control BuildControls()
    {
        _preview.Width = 34;
        _preview.Height = 34;
        _preview.Stroke = Skin.GlassEdge;
        _preview.StrokeThickness = 2;
        _preview.VerticalAlignment = VerticalAlignment.Center;

        var hueTrack = new Border
        {
            Width = SliderWidth,
            Height = SliderHeight,
            CornerRadius = new CornerRadius(8),
            Background = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
                GradientStops = HueStops(),
            },
            IsHitTestVisible = false,
        };

        _sliderMarker.Width = MarkerSize + 2;
        _sliderMarker.Height = MarkerSize + 2;
        _sliderMarker.Stroke = Brushes.White;
        _sliderMarker.StrokeThickness = 2.5;
        _sliderMarker.IsHitTestVisible = false;

        _slider.Children.Add(hueTrack);
        _slider.Children.Add(_sliderMarker);
        _slider.Cursor = new Cursor(StandardCursorType.Hand);
        _slider.VerticalAlignment = VerticalAlignment.Center;

        _slider.PointerPressed += (_, e) =>
        {
            if (!e.GetCurrentPoint(_slider).Properties.IsLeftButtonPressed) return;
            e.Pointer.Capture(_slider);
            _sliderDragging = true;
            PickHue(e.GetPosition(_slider));
            e.Handled = true;
        };
        _slider.PointerMoved += (_, e) =>
        {
            if (!_sliderDragging) return;
            PickHue(e.GetPosition(_slider));
            e.Handled = true;
        };
        _slider.PointerReleased += (_, e) =>
        {
            if (!_sliderDragging) return;
            _sliderDragging = false;
            e.Pointer.Capture(null);
            ColorCommitted?.Invoke(CurrentColor);
            e.Handled = true;
        };

        _hexLabel = Ui.T("", 11.5, Skin.TextDim, FontWeight.Medium);

        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            VerticalAlignment = VerticalAlignment.Center,
        };
        row.Children.Add(_preview);
        row.Children.Add(_slider);
        row.Children.Add(_hexLabel);

        return row;
    }

    private static TextBox NewBox()
    {
        var box = new TextBox
        {
            Width = 62,
            Height = 32,
            FontSize = 12.5,
            Text = "0",
            TextAlignment = TextAlignment.Center,
            Background = Skin.PanelAlt,
            Foreground = Skin.Text,
            BorderBrush = Skin.Border,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(4, 0, 4, 0),
            VerticalContentAlignment = VerticalAlignment.Center,
        };
        Ui.AnimateTextBox(box);
        return box;
    }

    private Control BuildRgbRow()
    {
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
        };

        row.Children.Add(RgbColumn(_rBox, "R"));
        row.Children.Add(RgbColumn(_gBox, "G"));
        row.Children.Add(RgbColumn(_bBox, "B"));

        return row;
    }

    private static Control RgbColumn(TextBox box, string label)
    {
        var stack = new StackPanel { Spacing = 3, Width = 62 };
        stack.Children.Add(box);

        var caption = Ui.T(label, 10.5, Skin.TextDim, FontWeight.Medium);
        caption.HorizontalAlignment = HorizontalAlignment.Center;
        stack.Children.Add(caption);

        return stack;
    }

    private static Avalonia.Media.GradientStops HueStops()
    {
        var stops = new Avalonia.Media.GradientStops();
        for (int i = 0; i <= 6; i++)
        {
            stops.Add(new GradientStop(HsvToColor(i * 60, 1, 1), i / 6.0));
        }

        return stops;
    }

    // ------------------------------------------------------------ управление

    /// <summary>Показать готовый цвет, не вызывая событий.</summary>
    public void SetColor(Color color)
    {
        ColorToHsv(color, out _hue, out _saturation, out _value);
        Refresh();
    }

    private void PickSquare(Point point)
    {
        var width = Math.Max(1, _square.Bounds.Width > 1 ? _square.Bounds.Width : SquareWidth);
        var height = Math.Max(1, _square.Bounds.Height > 1 ? _square.Bounds.Height : SquareHeight);

        _saturation = Math.Clamp(point.X / width, 0, 1);
        _value = Math.Clamp(1 - point.Y / height, 0, 1);

        Refresh();
        ColorChanged?.Invoke(CurrentColor);
    }

    private void PickHue(Point point)
    {
        var width = Math.Max(1, _slider.Bounds.Width > 1 ? _slider.Bounds.Width : SliderWidth);
        _hue = Math.Clamp(point.X / width, 0, 1) * 360;

        Refresh();
        ColorChanged?.Invoke(CurrentColor);
    }

    /// <summary>Цифры в полях R/G/B.</summary>
    private void ApplyRgb()
    {
        if (_updating) return;

        if (!TryByte(_rBox.Text, out var r) || !TryByte(_gBox.Text, out var g) || !TryByte(_bBox.Text, out var b))
            return;

        ColorToHsv(Color.FromRgb(r, g, b), out _hue, out _saturation, out _value);
        Refresh();
        ColorChanged?.Invoke(CurrentColor);
        ColorCommitted?.Invoke(CurrentColor);
    }

    private static bool TryByte(string? text, out byte value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;
        if (!int.TryParse(text.Trim(), out var parsed)) return false;
        if (parsed < 0 || parsed > 255) return false;

        value = (byte)parsed;
        return true;
    }

    /// <summary>Перерисовать все маркеры и поля под текущий цвет.</summary>
    private void Refresh()
    {
        var color = CurrentColor;

        _squareHue.Background = new SolidColorBrush(HsvToColor(_hue, 1, 1));

        var squareWidth = _square.Bounds.Width > 1 ? _square.Bounds.Width : SquareWidth;
        var squareHeight = _square.Bounds.Height > 1 ? _square.Bounds.Height : SquareHeight;
        Canvas.SetLeft(_squareMarker, _saturation * squareWidth - MarkerSize / 2);
        Canvas.SetTop(_squareMarker, (1 - _value) * squareHeight - MarkerSize / 2);
        _squareMarker.Fill = new SolidColorBrush(color);

        var sliderWidth = _slider.Bounds.Width > 1 ? _slider.Bounds.Width : SliderWidth;
        var sliderHeight = _slider.Bounds.Height > 1 ? _slider.Bounds.Height : SliderHeight;
        Canvas.SetLeft(_sliderMarker, _hue / 360.0 * sliderWidth - (MarkerSize + 2) / 2);
        Canvas.SetTop(_sliderMarker, sliderHeight / 2 - (MarkerSize + 2) / 2);
        _sliderMarker.Fill = new SolidColorBrush(color);

        _preview.Fill = new SolidColorBrush(color);
        _hexLabel.Text = Skin.ColorHex(color).ToUpperInvariant();

        _updating = true;
        _rBox.Text = color.R.ToString();
        _gBox.Text = color.G.ToString();
        _bBox.Text = color.B.ToString();
        _updating = false;
    }

    // ------------------------------------------------------------ HSV <-> RGB

    public static Color HsvToColor(double hue, double saturation, double value)
    {
        hue = ((hue % 360) + 360) % 360;
        saturation = Math.Clamp(saturation, 0, 1);
        value = Math.Clamp(value, 0, 1);

        var c = value * saturation;
        var x = c * (1 - Math.Abs(hue / 60.0 % 2 - 1));
        var m = value - c;

        double r = 0, g = 0, b = 0;
        if (hue < 60) { r = c; g = x; }
        else if (hue < 120) { r = x; g = c; }
        else if (hue < 180) { g = c; b = x; }
        else if (hue < 240) { g = x; b = c; }
        else if (hue < 300) { r = x; b = c; }
        else { r = c; b = x; }

        return Color.FromRgb(
            (byte)Math.Round((r + m) * 255),
            (byte)Math.Round((g + m) * 255),
            (byte)Math.Round((b + m) * 255));
    }

    public static void ColorToHsv(Color color, out double hue, out double saturation, out double value)
    {
        double r = color.R / 255.0, g = color.G / 255.0, b = color.B / 255.0;
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var delta = max - min;

        value = max;
        saturation = max <= 0 ? 0 : delta / max;

        if (delta <= 0) { hue = 0; return; }

        if (max == r) hue = 60 * ((g - b) / delta % 6);
        else if (max == g) hue = 60 * ((b - r) / delta + 2);
        else hue = 60 * ((r - g) / delta + 4);

        if (hue < 0) hue += 360;
    }
}
