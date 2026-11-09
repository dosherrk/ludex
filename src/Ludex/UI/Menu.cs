using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace Ludex.UI;

/// <summary>
/// Контекстное меню приложения: своя панель с плавным появлением, строки
/// подсвечиваются при наведении и выезжают каскадом. Заменяет стандартное
/// ContextMenu, которое появляется мгновенно и без анимаций.
/// </summary>
public sealed class Menu
{
    private readonly StackPanel _list;
    private readonly Border _panel;
    private OverlayPanel? _overlay;

    public Menu()
    {
        _list = new StackPanel { Spacing = 1 };

        _panel = new Border
        {
            Background = new SolidColorBrush(Skin.PanelColor),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(6),
            MinWidth = 208,
            Child = _list,
            Opacity = 0,
            RenderTransformOrigin = new RelativePoint(0.5, 0, RelativeUnit.Relative),
            RenderTransform = new ScaleTransform(0.97, 0.97),
            BoxShadow = new BoxShadows(new BoxShadow
            {
                OffsetX = 0,
                OffsetY = 10,
                Blur = 28,
                Color = Color.Parse("#77000000"),
            }),
            Transitions = new Avalonia.Animation.Transitions
            {
                new Avalonia.Animation.DoubleTransition
                {
                    Property = Visual.OpacityProperty,
                    Duration = TimeSpan.FromMilliseconds(150),
                    Easing = new Avalonia.Animation.Easings.CubicEaseOut(),
                },
                new Avalonia.Animation.TransformOperationsTransition
                {
                    Property = Visual.RenderTransformProperty,
                    Duration = TimeSpan.FromMilliseconds(170),
                    Easing = new Avalonia.Animation.Easings.CubicEaseOut(),
                },
            },
        };
    }

    /// <summary>Добавить строку меню.</summary>
    public Menu Item(string text, Action action, bool danger = false)
    {
        var label = Ui.T(text, 13, danger ? Skin.Danger : Skin.Text, FontWeight.Normal);
        label.Transitions = new Avalonia.Animation.Transitions
        {
            new Avalonia.Animation.BrushTransition
            {
                Property = TextBlock.ForegroundProperty,
                Duration = TimeSpan.FromMilliseconds(130),
            },
        };

        var row = new Border
        {
            Child = label,
            Background = Brushes.Transparent,
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(11, 7),
            Cursor = new Cursor(StandardCursorType.Hand),
            Opacity = 0,
            Transitions = new Avalonia.Animation.Transitions
            {
                new Avalonia.Animation.BrushTransition
                {
                    Property = Border.BackgroundProperty,
                    Duration = TimeSpan.FromMilliseconds(130),
                },
                new Avalonia.Animation.DoubleTransition
                {
                    Property = Visual.OpacityProperty,
                    Duration = TimeSpan.FromMilliseconds(150),
                    Easing = new Avalonia.Animation.Easings.CubicEaseOut(),
                },
            },
        };

        row.PointerEntered += (_, _) => row.Background = danger ? Skin.DangerSoft : Skin.CardHover;
        row.PointerExited += (_, _) => row.Background = Brushes.Transparent;
        row.PointerReleased += (_, e) =>
        {
            // в PointerReleased левая кнопка уже отпущена — смотрим, какой кнопкой нажали
            if (e.InitialPressMouseButton != MouseButton.Left) return;
            e.Handled = true;
            Close();
            action();
        };

        _list.Children.Add(row);
        return this;
    }

    public Menu Separator()
    {
        var line = new Border
        {
            Height = 1,
            Margin = new Thickness(4, 4),
            Background = Skin.Border,
            Opacity = 0,
            Transitions = new Avalonia.Animation.Transitions
            {
                new Avalonia.Animation.DoubleTransition
                {
                    Property = Visual.OpacityProperty,
                    Duration = TimeSpan.FromMilliseconds(160),
                },
            },
        };

        _list.Children.Add(line);
        return this;
    }

    /// <summary>Показать меню в точке <paramref name="position"/> внутри target.</summary>
    public void Show(Control target, Point position)
    {
        _panel.Opacity = 0;
        _panel.RenderTransform = new ScaleTransform(0.97, 0.97);

        foreach (var child in _list.Children)
            if (child is Visual v) v.Opacity = 0;

        // меню рисуется внутри окна — вокруг панели нет светлой подложки ОС
        _overlay = OverlayPanel.Show(target, _panel, position);
        if (_overlay == null) return;

        _overlay.Closed += () => _overlay = null;

        Dispatcher.UIThread.Post(() =>
        {
            _panel.Opacity = 1;
            _panel.RenderTransform = new ScaleTransform(1, 1);
        }, DispatcherPriority.Input);

        var delay = 20;
        foreach (var child in _list.Children)
        {
            if (child is not Visual v) continue;
            var vis = v;
            DispatcherTimer.RunOnce(() => vis.Opacity = 1, TimeSpan.FromMilliseconds(delay));
            delay += 16;
        }
    }

    public void Close()
    {
        _panel.Opacity = 0;
        _panel.RenderTransform = new ScaleTransform(0.98, 0.98);

        var overlay = _overlay;
        _overlay = null;
        if (overlay == null) return;

        DispatcherTimer.RunOnce(() => overlay.Close(), TimeSpan.FromMilliseconds(130));
    }
}
