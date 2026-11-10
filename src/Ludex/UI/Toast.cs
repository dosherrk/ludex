using System;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace Ludex.UI;

/// <summary>Вид уведомления: ход работы (с полоской) или разовое событие (со значком).</summary>
public enum ToastKind
{
    Process,
    Event,
}

/// <summary>
/// Небольшая плашка-уведомление снизу окна вместо строки состояния: тёмное «стекло»,
/// мягкая тень, точка-индикатор и текст. Появляется и исчезает плавно, не занимая места
/// в раскладке (живёт в Canvas).
/// </summary>
public sealed class Toast : Border
{
    private readonly TextBlock _label;
    private readonly Border _dot;
    private readonly ProgressStrip? _strip;
    private bool _busy;

    public Toast(ToastKind kind)
    {
        MaxWidth = 520;
        MinHeight = 34;
        CornerRadius = new CornerRadius(10);
        Padding = new Thickness(12, 7, 14, 8);
        BorderThickness = new Thickness(1);
        Background = Skin.ToastBg;
        BorderBrush = Skin.ToastEdge;
        BoxShadow = new BoxShadows(new BoxShadow
        {
            OffsetX = 0,
            OffsetY = 8,
            Blur = 26,
            Color = Color.Parse(Skin.IsLight ? "#33000000" : "#7A000000"),
        });
        Opacity = 0;
        IsHitTestVisible = false;
        VerticalAlignment = VerticalAlignment.Top;

        Transitions = new Transitions
        {
            new DoubleTransition
            {
                Property = OpacityProperty,
                Duration = TimeSpan.FromMilliseconds(180),
                Easing = new CubicEaseOut(),
            },
        };

        _label = Ui.T("", 11.5, Skin.Text, null, TextWrapping.NoWrap);
        _label.TextTrimming = TextTrimming.CharacterEllipsis;
        _label.MaxWidth = 470;

        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 9,
            VerticalAlignment = VerticalAlignment.Center,
        };

        _dot = new Border
        {
            Width = 7,
            Height = 7,
            CornerRadius = new CornerRadius(4),
            Background = kind == ToastKind.Process ? Skin.Accent : Skin.TextMuted,
            VerticalAlignment = VerticalAlignment.Center,
        };
        row.Children.Add(_dot);
        row.Children.Add(_label);

        if (kind == ToastKind.Process)
        {
            _strip = new ProgressStrip(46, 3);
            _strip.SetActive(false);
            _strip.Margin = new Thickness(2, 0, 0, 0);
            _strip.VerticalAlignment = VerticalAlignment.Center;
            row.Children.Add(_strip);
        }

        Child = row;
    }

    /// <summary>Размер или содержимое изменились — вызывающему пора пересчитать положение.</summary>
    public event Action? Changed;

    public string Text => _label.Text ?? "";

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        Changed?.Invoke();
    }

    /// <summary>Показать текст; повторный вызов с тем же текстом ничего не делает.</summary>
    public void Show(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        _label.Text = text;
        if (_busy)
        {
            Changed?.Invoke();
            return;
        }

        _busy = true;
        IsVisible = true;
        _strip?.SetActive(true);

        // даём раскладке посчитать ширину — и только потом проявляем
        InvalidateMeasure();
        Dispatcher.UIThread.Post(() =>
        {
            (Parent as Control)?.InvalidateMeasure();
            Changed?.Invoke();
            Opacity = 1;
        }, DispatcherPriority.Loaded);
    }

    public void Hide()
    {
        if (!_busy) return;

        _busy = false;
        Opacity = 0;
        DispatcherTimer.RunOnce(() =>
        {
            if (_busy) return;
            IsVisible = false;
            _strip?.SetActive(false);
            Changed?.Invoke();
        }, TimeSpan.FromMilliseconds(220));
    }

    public void TogglePulse(bool on) => _strip?.SetActive(on);
}
