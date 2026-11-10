using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;

namespace Ludex.UI;

/// <summary>
/// Список игр, убранных из библиотеки навсегда. Каждую можно вернуть отдельно —
/// одной кнопкой, не возвращая весь список сразу.
/// </summary>
public sealed class HiddenPanel : Border
{
    private readonly List<string> _keys;
    private readonly Func<string, string> _titleOf;
    private readonly StackPanel _rows = new() { Spacing = 6 };
    private readonly TextBlock _summary;

    /// <summary>Пользователь вернул одну игру (ключ папки).</summary>
    public event Action<string>? Restored;

    /// <summary>Пользователь вернул все игры.</summary>
    public event Action? RestoredAll;

    public event Action? Closed;

    public HiddenPanel(IEnumerable<string> keys, Func<string, string> titleOf)
    {
        _keys = keys.ToList();
        _titleOf = titleOf;

        MaxWidth = 720;
        MaxHeight = 660;
        Background = Skin.Panel;
        CornerRadius = new CornerRadius(14);
        BoxShadow = new BoxShadows(new BoxShadow
        {
            OffsetX = 0,
            OffsetY = 14,
            Blur = 44,
            Color = Color.Parse("#AA000000"),
        });

        var layout = new DockPanel();

        // ---- заголовок
        var header = new DockPanel { Margin = new Thickness(26, 22, 22, 0) };
        var close = Ui.Clickable(Ui.Icon(Ui.CloseGeometry, Skin.TextMuted, 12, 12, true, 1.8),
            () => Closed?.Invoke(), Skin.PanelAlt, Skin.CardHover, new CornerRadius(6), new Thickness(10, 10));
        close.VerticalAlignment = VerticalAlignment.Top;
        DockPanel.SetDock(close, Dock.Right);
        header.Children.Add(close);
        header.Children.Add(Ui.T(Loc.T("hidden.title"), 21, Skin.Text, FontWeight.Bold));
        DockPanel.SetDock(header, Dock.Top);
        layout.Children.Add(header);

        // ---- нижняя часть с кнопками
        var footer = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 10,
            Margin = new Thickness(26, 12, 22, 22),
        };
        var restoreAll = Ui.GhostButton(Loc.T("hidden.restoreAll"), () =>
        {
            RestoredAll?.Invoke();
            _keys.Clear();
            RenderRows();
        });
        var done = Ui.PrimaryButton(Loc.T("btn.close"), () => Closed?.Invoke());
        footer.Children.Add(restoreAll);
        footer.Children.Add(done);
        DockPanel.SetDock(footer, Dock.Bottom);
        layout.Children.Add(footer);

        // ---- тело
        var body = new StackPanel { Margin = new Thickness(26, 14, 26, 0), Spacing = 12 };

        _summary = Ui.T(CountText(), 11.5, Skin.TextDim, wrap: TextWrapping.Wrap);
        body.Children.Add(Ui.T(Loc.T("hidden.hint"), 11.5, Skin.TextDim, wrap: TextWrapping.Wrap));
        body.Children.Add(_summary);

        var scroll = new ScrollViewer
        {
            Content = _rows,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            MaxHeight = 420,
            Background = Brushes.Transparent,
        };
        Ui.SmoothScroll(scroll, 70);
        body.Children.Add(scroll);

        layout.Children.Add(body);
        Child = layout;

        RenderRows();
    }

    private string CountText() => Loc.F("hidden.count", _keys.Count);

    private void RenderRows()
    {
        _rows.Children.Clear();

        if (_keys.Count == 0)
        {
            _rows.Children.Add(new Border
            {
                Background = Skin.PanelAlt,
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(14, 16),
                Child = Ui.T(Loc.T("hidden.empty"), 12, Skin.TextDim, wrap: TextWrapping.Wrap),
            });
            _summary.Text = CountText();
            return;
        }

        foreach (var key in _keys.OrderBy(k => _titleOf(k), StringComparer.CurrentCultureIgnoreCase))
            _rows.Children.Add(Row(key));

        _summary.Text = CountText();
    }

    private Control Row(string key)
    {
        var title = Ui.T(_titleOf(key), 12.5, Skin.Text, FontWeight.Medium);
        title.TextTrimming = TextTrimming.CharacterEllipsis;

        var path = Ui.T(key, 10.5, Skin.TextDim);
        path.TextTrimming = TextTrimming.CharacterEllipsis;

        var column = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        column.Children.Add(title);
        column.Children.Add(path);

        var restore = Ui.GhostButton(Loc.T("hidden.restore"), () =>
        {
            if (!_keys.Remove(key)) return;
            RenderRows();
            Restored?.Invoke(key);
        });
        restore.VerticalAlignment = VerticalAlignment.Center;

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        grid.Children.Add(column);
        Grid.SetColumn(restore, 1);
        grid.Children.Add(restore);

        return new Border
        {
            Background = Skin.PanelAlt,
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(14, 9, 10, 9),
            Child = grid,
        };
    }
}
