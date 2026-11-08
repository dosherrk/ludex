using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace Ludex.UI;

/// <summary>
/// Индикатор работы вместо стандартного ProgressBar: по дорожке плавно
/// едет мягкое акцентное свечение. Стандартный «прыгающий» брус не используется.
/// </summary>
public sealed class ProgressStrip : Border
{
    private readonly Border _glow;
    private readonly DispatcherTimer _timer;
    private readonly double _width;
    private double _phase;

    public ProgressStrip(double width = 180, double height = 4)
    {
        _width = width;
        Width = width;
        Height = height;
        CornerRadius = new CornerRadius(height / 2);
        Background = Skin.PanelAlt;
        ClipToBounds = true;
        VerticalAlignment = VerticalAlignment.Center;

        var gradient = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0.5, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 0.5, RelativeUnit.Relative),
        };
        gradient.GradientStops.Add(new GradientStop(Color.Parse("#00FFFFFF"), 0));
        gradient.GradientStops.Add(new GradientStop(Skin.AccentColor, 0.5));
        gradient.GradientStops.Add(new GradientStop(Color.Parse("#00FFFFFF"), 1));

        _glow = new Border
        {
            Width = Math.Max(60, width * 0.45),
            Height = height,
            CornerRadius = new CornerRadius(height / 2),
            Background = gradient,
            HorizontalAlignment = HorizontalAlignment.Left,
        };

        Child = _glow;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _timer.Tick += (_, _) =>
        {
            _phase += 0.018;
            if (_phase > 1) _phase -= 1;

            var travel = _width + _glow.Width;
            _glow.RenderTransform = new TranslateTransform(_phase * travel - _glow.Width, 0);
        };
    }

    public void SetActive(bool active)
    {
        IsVisible = active;

        if (active)
        {
            _phase = 0;
            if (!_timer.IsEnabled) _timer.Start();
        }
        else
        {
            _timer.Stop();
        }
    }
}
