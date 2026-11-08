using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace Ludex.UI;

/// <summary>
/// Переключатель в стиле приложения: бегунок плавно едет, дорожка мягко
/// перекрашивается, подпись подсвечивается при наведении. Заменяет
/// стандартный CheckBox, у которого состояние меняется «рывком».
/// </summary>
public sealed class Toggle : Border
{
    private readonly Border _track;
    private readonly Border _knob;
    private readonly TextBlock _label;
    private bool _value;

    public Toggle(string text, bool value, Action<bool>? changed = null, bool interactive = true,
        double fontSize = 12.5)
    {
        _value = value;

        _knob = new Border
        {
            Width = 16,
            Height = 16,
            CornerRadius = new CornerRadius(8),
            Background = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(3, 0, 0, 0),
            RenderTransform = new TranslateTransform(value ? 18 : 0, 0),
            BoxShadow = new BoxShadows(new BoxShadow { OffsetY = 1, Blur = 4, Color = Color.Parse("#66000000") }),
            Transitions = new Avalonia.Animation.Transitions
            {
                new Avalonia.Animation.TransformOperationsTransition
                {
                    Property = Visual.RenderTransformProperty,
                    Duration = TimeSpan.FromMilliseconds(210),
                    Easing = new Avalonia.Animation.Easings.CubicEaseInOut(),
                },
            },
        };

        _track = new Border
        {
            Width = 40,
            Height = 22,
            CornerRadius = new CornerRadius(11),
            Background = value ? Skin.Accent : Skin.PanelAlt,
            BorderBrush = value ? Skin.Accent : Skin.Border,
            BorderThickness = new Thickness(1),
            VerticalAlignment = VerticalAlignment.Center,
            Child = _knob,
            Transitions = new Avalonia.Animation.Transitions
            {
                new Avalonia.Animation.BrushTransition
                {
                    Property = Border.BackgroundProperty,
                    Duration = TimeSpan.FromMilliseconds(190),
                },
                new Avalonia.Animation.BrushTransition
                {
                    Property = Border.BorderBrushProperty,
                    Duration = TimeSpan.FromMilliseconds(190),
                },
            },
        };

        _label = Ui.T(text, fontSize, interactive ? Skin.TextMuted : Skin.TextDim, FontWeight.Medium);
        _label.Transitions = new Avalonia.Animation.Transitions
        {
            new Avalonia.Animation.BrushTransition
            {
                Property = TextBlock.ForegroundProperty,
                Duration = TimeSpan.FromMilliseconds(150),
            },
        };

        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 11 };
        row.Children.Add(_track);
        row.Children.Add(_label);

        Child = row;
        Padding = new Thickness(0, 3);
        Background = Brushes.Transparent;
        HorizontalAlignment = HorizontalAlignment.Left;
        Opacity = interactive ? 1 : 0.55;
        Cursor = interactive ? new Cursor(StandardCursorType.Hand) : Cursor.Default;

        if (!interactive) return;

        PointerEntered += (_, _) => _label.Foreground = Skin.Text;
        PointerExited += (_, _) => _label.Foreground = Skin.TextMuted;

        PointerPressed += (_, e) =>
        {
            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
            e.Handled = true;
            Value = !_value;
            changed?.Invoke(Value);
        };
    }

    public bool Value
    {
        get => _value;
        set
        {
            if (_value == value) return;
            _value = value;
            Apply();
        }
    }

    /// <summary>Выставить состояние без вызова события (например, при сборке панели).</summary>
    public void SetSilently(bool value)
    {
        _value = value;
        Apply();
    }

    private void Apply()
    {
        _knob.RenderTransform = new TranslateTransform(_value ? 18 : 0, 0);
        _track.Background = _value ? Skin.Accent : Skin.PanelAlt;
        _track.BorderBrush = _value ? Skin.Accent : Skin.Border;
    }
}
