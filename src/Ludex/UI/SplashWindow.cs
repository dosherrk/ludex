using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace Ludex.UI;

/// <summary>
/// Экран загрузки — отдельное маленькое окно, как у Steam: главное окно появляется
/// уже собранным, поэтому заставка не растягивается на весь интерфейс.
/// </summary>
public sealed class SplashWindow : Window
{
    private const double TrackWidth = 220;
    private const double Segment = 64;

    private readonly Border _fill;
    private readonly DispatcherTimer _timer;
    private readonly DateTime _started = DateTime.UtcNow;
    private bool _finished;

    public SplashWindow()
    {
        Title = "Ludex";
        SystemDecorations = SystemDecorations.None;
        CanResize = false;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Width = 340;
        Height = 190;
        // без прозрачности: на системах без композитора прозрачное окно рисуется чёрным
        Background = new SolidColorBrush(Skin.BgColor);

        // не лезем поверх чужих окон: если в фуллскрине запущена игра, заставка
        // просто ждёт своей очереди в фоне и не перехватывает фокус
        Topmost = false;
        ShowActivated = false;
        Focusable = false;

        _fill = new Border
        {
            Width = Segment,
            Height = 3,
            CornerRadius = new CornerRadius(2),
            Background = Skin.Accent,
            HorizontalAlignment = HorizontalAlignment.Left,
        };

        var track = new Border
        {
            Width = TrackWidth,
            Height = 3,
            CornerRadius = new CornerRadius(2),
            Background = Skin.PanelAlt,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ClipToBounds = true,
            Child = new Canvas { Width = TrackWidth, Height = 3, Children = { _fill } },
        };

        var logo = Ui.T("LUDEX", 21, Skin.Text, FontWeight.Bold);
        logo.TextAlignment = TextAlignment.Center;
        logo.HorizontalAlignment = HorizontalAlignment.Stretch;
        logo.LetterSpacing = 1.2;

        var stack = new StackPanel
        {
            Width = TrackWidth,
            Spacing = 14,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { logo, track },
        };

        // своя «рамка» вместо системной — окно без заголовка и кнопок
        Content = new Border
        {
            Background = new SolidColorBrush(Skin.BgColor),
            BorderBrush = Skin.Border,
            BorderThickness = new Thickness(1),
            Child = stack,
        };

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _timer.Tick += (_, _) => Animate();
        _timer.Start();
    }

    private void Animate()
    {
        if (_finished) return;

        const double period = 1.4;

        var phase = (DateTime.UtcNow - _started).TotalSeconds % period / period;
        var eased = phase < 0.5 ? phase * 2 : (1 - phase) * 2;

        Canvas.SetLeft(_fill, (TrackWidth - Segment) * eased);
        _fill.Opacity = 0.35 + 0.65 * Math.Sin(Math.PI * eased);
    }

    /// <summary>
    /// Заставка «раскрывается» в готовое приложение: полоска добегает до края,
    /// карточка чуть светлеет и растягивается — дальше её подхватывает окно приложения.
    /// </summary>
    public void OpenInto()
    {
        if (_finished) return;

        var stack = (Control)((Border)Content!).Child!;
        var plate = (Border)Content!;
        var easing = new Avalonia.Animation.Easings.CubicEaseOut();
        var clock = System.Diagnostics.Stopwatch.StartNew();
        var ms = int.TryParse(Environment.GetEnvironmentVariable("LUDEX_REVEAL_MS"), out var custom) && custom > 0
            ? custom : 320;

        // растягиваем карточку к размеру окна; окно закроется раньше, чем это станет заметно
        var scale = new ScaleTransform(1, 1);
        plate.RenderTransformOrigin = RelativePoint.Center;
        plate.RenderTransform = scale;

        _timer.Stop();

        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(12) };
        timer.Tick += (_, _) =>
        {
            var p = Math.Min(1, clock.Elapsed.TotalMilliseconds / ms);
            var e = easing.Ease(p);

            Canvas.SetLeft(_fill, (TrackWidth - Segment) * Math.Min(1, p * 1.6));
            _fill.Width = Segment + (TrackWidth - Segment) * Math.Min(1, p * 1.6);
            _fill.Opacity = 1;

            stack.Opacity = 1 - e * 0.55;
            scale.ScaleX = 1 + 0.06 * e;
            scale.ScaleY = 1 + 0.06 * e;

            if (p >= 1) timer.Stop();
        };
        timer.Start();
    }

    /// <summary>Загрузка закончилась: гасим окно.</summary>
    public void Finish()
    {
        if (_finished) return;
        _finished = true;
        _timer.Stop();
        try
        {
            Close();
        }
        catch
        {
            // окно уже закрыто — ничего страшного
        }
    }
}
