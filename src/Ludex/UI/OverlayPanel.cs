using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace Ludex.UI;

/// <summary>
/// Панель поверх содержимого окна (выпадающий список, контекстное меню).
/// Рисуется внутри окна приложения, а не отдельным всплывающим окном ОС:
/// у таких окон бывает своя светлая подложка, из-за которой вокруг скруглённой
/// панели видна белая рамка. Здесь подложки нет — панель лежит прямо на интерфейсе.
/// </summary>
public sealed class OverlayPanel
{
    private readonly OverlayLayer _layer;
    private readonly Border _shield;
    private readonly Control _panel;
    private bool _closed;

    public event Action? Closed;

    private OverlayPanel(OverlayLayer layer, Control panel, Border shield)
    {
        _layer = layer;
        _panel = panel;
        _shield = shield;
    }

    /// <summary>Показать панель в точке <paramref name="position"/> (координаты якоря).</summary>
    public static OverlayPanel? Show(Control anchor, Control panel, Point position)
    {
        var layer = OverlayLayer.GetOverlayLayer(anchor);
        if (layer == null) return null;

        var origin = anchor.TranslatePoint(position, layer);
        if (origin == null) return null;

        panel.Measure(Size.Infinity);
        var size = panel.DesiredSize;

        var areaWidth = layer.Bounds.Width > 1 ? layer.Bounds.Width : 1280;
        var areaHeight = layer.Bounds.Height > 1 ? layer.Bounds.Height : 800;

        var x = Math.Max(6, Math.Min(origin.Value.X, areaWidth - size.Width - 6));
        var y = Math.Max(6, Math.Min(origin.Value.Y, areaHeight - size.Height - 6));

        var shield = new Border
        {
            Background = Brushes.Transparent,
            Width = areaWidth,
            Height = areaHeight,
            Focusable = true,
        };
        Canvas.SetLeft(shield, 0);
        Canvas.SetTop(shield, 0);

        Canvas.SetLeft(panel, x);
        Canvas.SetTop(panel, y);

        layer.Children.Add(shield);
        layer.Children.Add(panel);

        var handle = new OverlayPanel(layer, panel, shield);

        shield.PointerPressed += (_, e) =>
        {
            e.Handled = true;
            handle.Close();
        };
        shield.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Escape) return;
            e.Handled = true;
            handle.Close();
        };

        // подписываемся на изменение размера не сразу: при первом показе слой
        // может пересчитаться и «закрыть» панель в тот же кадр
        Avalonia.Threading.DispatcherTimer.RunOnce(
            () => layer.SizeChanged += handle.OnLayerResized,
            TimeSpan.FromMilliseconds(400));

        shield.Focus();
        return handle;
    }

    private void OnLayerResized(object? sender, SizeChangedEventArgs e) => Close();

    public void Close()
    {
        if (_closed) return;
        _closed = true;

        _layer.SizeChanged -= OnLayerResized;

        if (_layer.Children.Contains(_panel)) _layer.Children.Remove(_panel);
        if (_layer.Children.Contains(_shield)) _layer.Children.Remove(_shield);

        Closed?.Invoke();
    }
}
