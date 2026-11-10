using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.Animation;

namespace Ludex.UI;

public static class Ui
{
    public static TextBlock T(string text, double size = 13, IBrush? color = null, FontWeight? weight = null,
        TextWrapping wrap = TextWrapping.NoWrap)
    {
        return new TextBlock
        {
            Text = text,
            FontSize = size,
            Foreground = color ?? Skin.Text,
            FontWeight = weight ?? FontWeight.Normal,
            TextWrapping = wrap,
            TextTrimming = wrap == TextWrapping.NoWrap ? TextTrimming.CharacterEllipsis : TextTrimming.None,
            VerticalAlignment = VerticalAlignment.Center,
        };
    }

    /// <summary>A button-like border: full control over colours and hover behaviour.</summary>
    public static Border Clickable(Control content, Action? onClick, IBrush? bg = null, IBrush? hover = null,
        CornerRadius? radius = null, Thickness? padding = null, IBrush? borderBrush = null, Thickness? borderThickness = null)
    {
        var frame = new Border
        {
            Child = content,
            Background = bg ?? Brushes.Transparent,
            CornerRadius = radius ?? new CornerRadius(6),
            Padding = padding ?? new Thickness(14, 8),
            BorderBrush = borderBrush,
            BorderThickness = borderThickness ?? new Thickness(0),
            Cursor = onClick != null ? new Cursor(StandardCursorType.Hand) : Cursor.Default,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
        };

        if (onClick != null)
        {
            frame.Tag = "clickable";

            // подложка наведения: та же краска, но прозрачная. Так переход идёт
            // только по альфе и не даёт яркой «вспышки» в начале наведения
            var hoverBrush = hover ?? Skin.PanelAlt;
            var baseBrush = bg ?? Brushes.Transparent;
            if (hoverBrush is ISolidColorBrush hs && baseBrush is ISolidColorBrush { Color.A: 0 })
                baseBrush = new SolidColorBrush(Color.FromArgb(0, hs.Color.R, hs.Color.G, hs.Color.B));

            frame.Resources["gvBaseBg"] = baseBrush;
            frame.Resources["gvHoverBg"] = hoverBrush;
            frame.Background = baseBrush;

            frame.RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative);
            frame.Transitions = new Transitions
            {
                new BrushTransition { Property = Border.BackgroundProperty, Duration = TimeSpan.FromMilliseconds(130) },
                new TransformOperationsTransition
                {
                    Property = Visual.RenderTransformProperty,
                    Duration = TimeSpan.FromMilliseconds(110),
                    Easing = new Avalonia.Animation.Easings.CubicEaseOut(),
                },
            };

            frame.PointerEntered += (_, _) => frame.Background = HoverOf(frame);
            frame.PointerExited += (_, _) => frame.Background = BaseOf(frame);
            frame.PointerReleased += (_, _) => frame.RenderTransform = new ScaleTransform(1, 1);
            frame.PointerCaptureLost += (_, _) => frame.RenderTransform = new ScaleTransform(1, 1);
            frame.PointerPressed += (_, e) =>
            {
                if (!e.GetCurrentPoint(frame).Properties.IsLeftButtonPressed) return;

                e.Handled = true;
                frame.RenderTransform = new ScaleTransform(0.955, 0.955);
                onClick();
            };
        }

        return frame;
    }

    private static IBrush BaseOf(Border frame) =>
        frame.Resources.TryGetValue("gvBaseBg", out var value) && value is IBrush brush ? brush : Brushes.Transparent;

    private static IBrush HoverOf(Border frame) =>
        frame.Resources.TryGetValue("gvHoverBg", out var value) && value is IBrush brush ? brush : Skin.PanelAlt;

    /// <summary>Поменять цвета «кликабельной» плашки без пересборки интерфейса — плавно.</summary>
    public static void SetColors(Border frame, IBrush baseBg, IBrush hoverBg)
    {
        if (hoverBg is ISolidColorBrush hs && baseBg is ISolidColorBrush { Color.A: 0 })
            baseBg = new SolidColorBrush(Color.FromArgb(0, hs.Color.R, hs.Color.G, hs.Color.B));

        frame.Resources["gvBaseBg"] = baseBg;
        frame.Resources["gvHoverBg"] = hoverBg;
        frame.Background = baseBg;
    }

    /// <summary>Мягкая рамка у текстового поля: подсветка акцентом при фокусе.</summary>
    public static void AnimateTextBox(TextBox box)
    {
        box.Transitions = new Transitions
        {
            new BrushTransition { Property = Border.BorderBrushProperty, Duration = TimeSpan.FromMilliseconds(160) },
            new BrushTransition { Property = Border.BackgroundProperty, Duration = TimeSpan.FromMilliseconds(160) },
        };

        box.GotFocus += (_, _) => box.BorderBrush = Skin.Accent;
        box.LostFocus += (_, _) => box.BorderBrush = Skin.Border;
    }

    public static Border PrimaryButton(string text, Action onClick, double extraWidth = 0)
    {
        var label = T(text, 13, Skin.OnAccent, FontWeight.SemiBold);
        label.HorizontalAlignment = HorizontalAlignment.Center;
        var b = Clickable(label, onClick, Skin.Accent, Skin.AccentHover, new CornerRadius(4), new Thickness(20, 10));
        b.BoxShadow = GlowShadow();
        b.Transitions = new Transitions
        {
            new BoxShadowsTransition
            {
                Property = Border.BoxShadowProperty,
                Duration = TimeSpan.FromMilliseconds(200),
            },
        };
        b.PointerEntered += (_, _) => b.BoxShadow = GlowShadow(strong: true);
        b.PointerExited += (_, _) => b.BoxShadow = GlowShadow();
        if (extraWidth > 0) b.MinWidth = extraWidth;
        return b;
    }

    /// <summary>Мягкое свечение вокруг кнопки.</summary>
    public static BoxShadows GlowShadow(bool strong = false)
    {
        var a = (byte)(strong ? 0x4C : 0x2A);
        return new BoxShadows(new BoxShadow
        {
            OffsetX = 0,
            OffsetY = 0,
            Blur = strong ? 26 : 18,
            Spread = strong ? 2 : 0,
            Color = Color.FromArgb(a, 0xFF, 0xFF, 0xFF),
        });
    }

    public static Border GhostButton(string text, Action onClick, IBrush? fg = null)
    {
        var label = T(text, 13, fg ?? Skin.Text, FontWeight.Medium);
        var b = Clickable(label, onClick, Brushes.Transparent, Skin.PanelAlt, new CornerRadius(4),
            new Thickness(18, 9), Skin.Border, new Thickness(1));
        return b;
    }

    public static TextBox SearchBox(string text, string watermark, Action<string> onChanged)
    {
        var tb = new TextBox
        {
            Text = text,
            Watermark = watermark,
            FontSize = 13,
            Height = 36,
            VerticalContentAlignment = VerticalAlignment.Center,
            Background = Skin.PanelAlt,
            Foreground = Skin.Text,
            BorderBrush = Brushes.Transparent,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(12, 0, 12, 0),
        };
        tb.TextChanged += (_, _) => onChanged(tb.Text ?? "");
        AnimateTextBox(tb);
        return tb;
    }

    public const string PlayGeometry = "M0,0 L15,8.5 L0,17 Z";
    public const string CloseGeometry = "M1,1 L11,11 M11,1 L1,11";
    public const string MenuGeometry = "M0,2.5 H20 M0,10 H20 M0,17.5 H20";
    /// <summary>Шестерёнка (Material "settings").</summary>
    public const string GearGeometry =
        "M19.14,12.94c0.04-0.3,0.06-0.61,0.06-0.94c0-0.32-0.02-0.64-0.07-0.94l2.03-1.58c0.18-0.14,0.23-0.41,0.12-0.61" +
        "l-1.92-3.32c-0.12-0.22-0.37-0.29-0.59-0.22l-2.39,0.96c-0.5-0.38-1.03-0.7-1.62-0.94L14.4,2.81" +
        "c-0.04-0.24-0.24-0.41-0.48-0.41h-3.84c-0.24,0-0.43,0.17-0.47,0.41L9.25,5.35C8.66,5.59,8.12,5.92,7.63,6.29" +
        "L5.24,5.33c-0.22-0.08-0.47,0-0.59,0.22L2.74,8.87C2.62,9.08,2.66,9.34,2.86,9.48l2.03,1.58" +
        "C4.84,11.36,4.8,11.69,4.8,12s0.02,0.64,0.07,0.94l-2.03,1.58c-0.18,0.14-0.23,0.41-0.12,0.61l1.92,3.32" +
        "c0.12,0.22,0.37,0.29,0.59,0.22l2.39-0.96c0.5,0.38,1.03,0.7,1.62,0.94l0.36,2.54c0.05,0.24,0.24,0.41,0.48,0.41h3.84" +
        "c0.24,0,0.44-0.17,0.47-0.41l0.36-2.54c0.59-0.24,1.13-0.56,1.62-0.94l2.39,0.96c0.22,0.08,0.47,0,0.59-0.22l1.92-3.32" +
        "c0.12-0.22,0.07-0.47-0.12-0.61L19.14,12.94z M12,15.6c-1.98,0-3.6-1.62-3.6-3.6s1.62-3.6,3.6-3.6s3.6,1.62,3.6,3.6" +
        "S13.98,15.6,12,15.6z";

    /// <summary>Свернуть — обычная горизонтальная полоска (заливка, не штрих).</summary>
    public const string MinimizeGeometry = "M0,0 H13 V2.4 H0 Z";
    public const string MaximizeGeometry = "M0.6,0.6 H11.4 V11.4 H0.6 Z";
    public const string RestoreGeometry = "M2.6,0.6 H11.4 V9.4 M0.6,2.6 H9.4 V11.4 H0.6 Z";

    /// <summary>Палитра (Material "palette") — вход в «Кастомизацию».</summary>
    public const string PaletteGeometry =
        "M12,22c5.52,0,10-4.48,10-10S17.52,2,12,2S2,6.48,2,12c0,3.31,2.69,6,6,6h1.5c0.83,0,1.5,0.67,1.5,1.5" +
        "c0,0.28-0.08,0.54-0.21,0.76C10.4,20.88,11.13,22,12,22z M6.5,11C5.67,11,5,10.33,5,9.5S5.67,8,6.5,8" +
        "S8,8.67,8,9.5S7.33,11,6.5,11z M9.5,7C8.67,7,8,6.33,8,5.5S8.67,4,9.5,4S11,4.67,11,5.5S10.33,7,9.5,7z " +
        "M14.5,7C13.67,7,13,6.33,13,5.5S13.67,4,14.5,4S16,4.67,16,5.5S15.33,7,14.5,7z M17.5,11" +
        "c-0.83,0-1.5-0.67-1.5-1.5S16.67,8,17.5,8S19,8.67,19,9.5S18.33,11,17.5,11z";

    /// <summary>Сетка (Material «grid_view») — пункт «Все игры».</summary>
    /// <summary>Звено цепи — интеграции с лаунчерами.</summary>
    public const string LinkGeometry =
        "M7.5,12.5 L12.5,7.5 M8.6,4.6 L10.4,2.8 A3.9,3.9 0 0 1 15.9,8.3 L14.1,10.1 M5.9,9.9 L4.1,11.7 A3.9,3.9 0 0 0 9.6,17.2 L11.4,15.4";

    public const string GridGeometry = "M0,0 H8.4 V8.4 H0 Z M11.6,0 H20 V8.4 H11.6 Z M0,11.6 H8.4 V20 H0 Z M11.6,11.6 H20 V20 H11.6 Z";

    /// <summary>Искра (4-луч) — пункт «Рекомендации».</summary>
    public const string SparkleGeometry =
        "M10,0 L12.1,7.9 L20,10 L12.1,12.1 L10,20 L7.9,12.1 L0,10 L7.9,7.9 Z";

    public const string StarGeometry = "M10,0 L12.6,6.6 L19.6,7.2 L14.3,11.9 L15.9,18.9 L10,15.3 L4.1,18.9 L5.7,11.9 L0.4,7.2 L7.4,6.6 Z";

    /// <summary>Scalable vector icon (no font glyphs -> renders identically everywhere).</summary>
    public static Avalonia.Controls.Shapes.Path Icon(string data, IBrush brush, double width, double height,
        bool stroke = false, double thickness = 1.8)
    {
        return new Avalonia.Controls.Shapes.Path
        {
            Data = Geometry.Parse(data),
            Fill = stroke ? null : brush,
            Stroke = stroke ? brush : null,
            StrokeThickness = thickness,
            StrokeLineCap = PenLineCap.Round,
            StrokeJoin = PenLineJoin.Round,
            Stretch = Stretch.Uniform,
            Width = width,
            Height = height,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
    }

    /// <summary>Плавное появление элемента.</summary>
    public static void FadeIn(Visual visual, int delayMs = 0, int durationMs = 240)
    {
        visual.Opacity = 0;
        visual.Transitions = new Transitions
        {
            new DoubleTransition { Property = Visual.OpacityProperty, Duration = TimeSpan.FromMilliseconds(durationMs) },
        };

        if (delayMs <= 0) visual.Opacity = 1;
        else DispatcherTimer.RunOnce(() => visual.Opacity = 1, TimeSpan.FromMilliseconds(delayMs));
    }

    /// <summary>Короткий «пружинный» отклик на нажатие.</summary>
    public static void Bounce(Visual visual, double to = 0.9, int holdMs = 110)
    {
        visual.RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative);
        visual.Transitions = new Transitions
        {
            new TransformOperationsTransition
            {
                Property = Visual.RenderTransformProperty,
                Duration = TimeSpan.FromMilliseconds(140),
                Easing = new Avalonia.Animation.Easings.CubicEaseOut(),
            },
        };

        visual.RenderTransform = new ScaleTransform(to, to);
        DispatcherTimer.RunOnce(() => visual.RenderTransform = new ScaleTransform(1, 1),
            TimeSpan.FromMilliseconds(holdMs));
    }

    /// <summary>Мягкая прокрутка колесом мыши (с инерцией), вместо «рывков».</summary>
    public static void SmoothScroll(ScrollViewer scroll, double step = 110)
    {
        double target = 0;
        double current = 0;
        DispatcherTimer? timer = null;

        void Tick()
        {
            var diff = target - current;
            if (Math.Abs(diff) < 0.5)
            {
                current = target;
                scroll.Offset = new Vector(scroll.Offset.X, current);
                timer?.Stop();
                return;
            }

            current += diff * 0.14;
            scroll.Offset = new Vector(scroll.Offset.X, current);
        }

        scroll.AddHandler(InputElement.PointerWheelChangedEvent, (_, e) =>
        {
            var delta = e.Delta.Y;
            if (Math.Abs(delta) < 0.01) return;

            var max = Math.Max(0, scroll.Extent.Height - scroll.Viewport.Height);
            if (max <= 0.5) return;

            var running = timer?.IsEnabled == true;
            if (!running)
            {
                current = scroll.Offset.Y;
                target = current;
            }

            target = Math.Clamp(target - delta * step, 0, max);
            e.Handled = true;

            if (timer == null)
            {
                timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(12) };
                timer.Tick += (_, _) => Tick();
            }

            if (!timer.IsEnabled) timer.Start();
        }, RoutingStrategies.Tunnel);
    }

    /// <summary>Мягкое появление с небольшим подъёмом снизу.</summary>
    public static void FadeSlideIn(Visual visual, int delayMs = 0, int durationMs = 220, double offset = 10)
    {
        visual.Opacity = 0;
        visual.RenderTransform = new TranslateTransform(0, offset);
        visual.Transitions = new Transitions
        {
            new DoubleTransition
            {
                Property = Visual.OpacityProperty,
                Duration = TimeSpan.FromMilliseconds(durationMs),
                Easing = new Avalonia.Animation.Easings.CubicEaseOut(),
            },
            new TransformOperationsTransition
            {
                Property = Visual.RenderTransformProperty,
                Duration = TimeSpan.FromMilliseconds(durationMs),
                Easing = new Avalonia.Animation.Easings.CubicEaseOut(),
            },
        };

        void Show()
        {
            visual.Opacity = 1;
            visual.RenderTransform = new TranslateTransform(0, 0);
        }

        if (delayMs <= 0) Dispatcher.UIThread.Post(Show);
        else DispatcherTimer.RunOnce(Show, TimeSpan.FromMilliseconds(delayMs));
    }

    /// <summary>Короткая «вспышка» — отклик на выбор в выпадающем списке и т. п.</summary>
    public static void Pulse(Visual visual, double from = 0.45, int durationMs = 180)
    {
        visual.Transitions = new Transitions
        {
            new DoubleTransition
            {
                Property = Visual.OpacityProperty,
                Duration = TimeSpan.FromMilliseconds(durationMs),
                Easing = new Avalonia.Animation.Easings.CubicEaseOut(),
            },
        };

        visual.Opacity = from;
        DispatcherTimer.RunOnce(() => visual.Opacity = 1, TimeSpan.FromMilliseconds(30));
    }

    public static Bitmap? LoadBitmap(string? path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path) || !System.IO.File.Exists(path)) return null;
            using var fs = System.IO.File.OpenRead(path);
            return new Bitmap(fs);
        }
        catch (Exception ex)
        {
            AppPaths.Log("LoadBitmap failed: " + ex.Message);
            return null;
        }
    }

    public static void Tip(Control control, string text)
    {
        ToolTip.SetTip(control, text);
        ToolTip.SetShowDelay(control, 400);
    }

    public static Border Divider(double height = 1) => new()
    {
        Height = height,
        Background = Skin.Border,
        Margin = new Thickness(0, 6),
        HorizontalAlignment = HorizontalAlignment.Stretch,
    };
}
