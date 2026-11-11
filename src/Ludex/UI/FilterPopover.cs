using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace Ludex.UI;

/// <summary>Значения фильтров каталога. Пустое значение = «любое».</summary>
public sealed class CatalogFilter
{
    public HashSet<string> Genres { get; } = new(StringComparer.CurrentCultureIgnoreCase);
    public HashSet<string> Tags { get; } = new(StringComparer.CurrentCultureIgnoreCase);
    public string Decade { get; set; } = "";
    public string Size { get; set; } = "";
    public string Sort { get; set; } = "catalog";
    public bool OnlyDescribed { get; set; }
    public bool OnlyUnshelved { get; set; }

    public int Count => Genres.Count + Tags.Count
                        + (Decade.Length > 0 ? 1 : 0)
                        + (Size.Length > 0 ? 1 : 0)
                        + (OnlyDescribed ? 1 : 0)
                        + (OnlyUnshelved ? 1 : 0);

    public CatalogFilter Clone()
    {
        var copy = new CatalogFilter { Decade = Decade, Size = Size, Sort = Sort,
            OnlyDescribed = OnlyDescribed, OnlyUnshelved = OnlyUnshelved };

        foreach (var g in Genres) copy.Genres.Add(g);
        foreach (var t in Tags) copy.Tags.Add(t);
        return copy;
    }

    public void CopyFrom(CatalogFilter other)
    {
        Genres.Clear();
        Tags.Clear();
        foreach (var g in other.Genres) Genres.Add(g);
        foreach (var t in other.Tags) Tags.Add(t);
        Decade = other.Decade;
        Size = other.Size;
        Sort = other.Sort;
        OnlyDescribed = other.OnlyDescribed;
        OnlyUnshelved = other.OnlyUnshelved;
    }
}

/// <summary>
/// Одна кнопка «Фильтры» и большая панель за ней, как в Steam: жанры, темы, год, объём
/// описания и сортировка в одном месте. Панель открывается поверх окна и растёт по высоте
/// содержимого; выбор в черновике, пока не нажато «Показать».
/// </summary>
public sealed class FilterPopover
{
    private readonly OverlayPanel _overlay;
    private readonly Border _panel;

    public event Action<CatalogFilter>? Applied;

    private FilterPopover(OverlayPanel overlay, Border panel)
    {
        _overlay = overlay;
        _panel = panel;
    }

    /// <summary>
    /// Показать панель под якорем. <paramref name="current"/> — текущие фильтры,
    /// <paramref name="onApply"/> получает изменённую копию.
    /// </summary>
    public static FilterPopover? Show(Control anchor, CatalogFilter current,
        Action<CatalogFilter> onApply, double width)
    {
        var draft = current.Clone();
        var chips = new List<(Border Frame, TextBlock Label, Func<bool> IsOn)>();
        var host = new StackPanel { Spacing = 15 };

        // объявлены заранее: пресеты и «Сбросить» должны уметь выставлять переключатели
        Toggle? described = null;
        Toggle? unshelved = null;
        OverlayPanel? overlay = null;

        void Toggle(HashSet<string> set, string value)
        {
            if (!set.Remove(value)) set.Add(value);
        }

        // ---- пресеты
        var presetRow = new WrapPanel { Orientation = Orientation.Horizontal };
        foreach (var (title, preset) in CatalogFacets.Presets())
        {
            var label = Ui.T(title, 12, Skin.TextMuted, FontWeight.Medium);
            var chip = Ui.Clickable(label, () =>
            {
                draft.CopyFrom(preset);
                Apply();          // пресет заменяет всё — включая переключатели
                Sync();
            }, Brushes.Transparent, Skin.PanelAlt, new CornerRadius(999), new Thickness(12, 6));
            chip.Margin = new Thickness(0, 0, 6, 6);
            presetRow.Children.Add(chip);
        }

        // ---- жанры: плитки в две-три колонки
        var genreGrid = new WrapPanel { Orientation = Orientation.Horizontal };
        foreach (var genre in CatalogFacets.Genres)
        {
            var label = Ui.T(CatalogFacets.Label(genre), 12.5, Skin.TextMuted, FontWeight.Medium);
            var frame = Ui.Clickable(label, () => { Toggle(draft.Genres, genre); Sync(); },
                Brushes.Transparent, Skin.CardHover, new CornerRadius(8), new Thickness(14, 9));
            frame.Margin = new Thickness(0, 0, 6, 6);
            frame.MinWidth = 118;
            genreGrid.Children.Add(frame);
            chips.Add((frame, label, () => draft.Genres.Contains(genre)));
        }

        // ---- темы
        var tagWrap = new WrapPanel { Orientation = Orientation.Horizontal };
        foreach (var tag in CatalogFacets.Tags)
        {
            var label = Ui.T(CatalogFacets.Label(tag), 11.5, Skin.TextMuted, FontWeight.Medium);
            var frame = Ui.Clickable(label, () => { Toggle(draft.Tags, tag); Sync(); },
                Brushes.Transparent, Skin.PanelAlt, new CornerRadius(999), new Thickness(11, 5));
            frame.Margin = new Thickness(0, 0, 5, 5);
            tagWrap.Children.Add(frame);
            chips.Add((frame, label, () => draft.Tags.Contains(tag)));
        }

        // ---- год выхода
        var decadeRow = new WrapPanel { Orientation = Orientation.Horizontal };
        foreach (var (value, title) in new (string, string)[]
                 {
                     ("", Loc.T("facet.any")), ("1990", "1990-е"), ("2000", "2000-е"),
                     ("2010", "2010-е"), ("2020", "2020-е"), ("2024", "2024+"),
                 })
        {
            var v = value;
            var label = Ui.T(title, 11.5, Skin.TextMuted, FontWeight.Medium);
            var frame = Ui.Clickable(label, () => { draft.Decade = v; Sync(); },
                Brushes.Transparent, Skin.PanelAlt, new CornerRadius(999), new Thickness(11, 5));
            frame.Margin = new Thickness(0, 0, 5, 5);
            decadeRow.Children.Add(frame);
            chips.Add((frame, label, () => draft.Decade == v));
        }

        // ---- сколько у игры описания
        var sizeRow = new WrapPanel { Orientation = Orientation.Horizontal };
        foreach (var (value, title) in new (string, string)[]
                 {
                     ("", Loc.T("facet.any")), ("described", Loc.T("facet.size.described")),
                     ("short", Loc.T("facet.size.short")), ("long", Loc.T("facet.size.long")),
                 })
        {
            var v = value;
            var label = Ui.T(title, 11.5, Skin.TextMuted, FontWeight.Medium);
            var frame = Ui.Clickable(label, () => { draft.Size = v; Sync(); },
                Brushes.Transparent, Skin.PanelAlt, new CornerRadius(999), new Thickness(11, 5));
            frame.Margin = new Thickness(0, 0, 5, 5);
            sizeRow.Children.Add(frame);
            chips.Add((frame, label, () => draft.Size == v));
        }

        // ---- сортировка
        var sortRow = new WrapPanel { Orientation = Orientation.Horizontal };
        foreach (var (value, key) in CatalogFacets.Sorts)
        {
            var v = value;
            var label = Ui.T(Loc.T(key), 12, Skin.TextMuted, FontWeight.Medium);
            var frame = Ui.Clickable(label, () => { draft.Sort = v; Sync(); },
                Brushes.Transparent, Skin.PanelAlt, new CornerRadius(999), new Thickness(12, 6));
            frame.Margin = new Thickness(0, 0, 6, 6);
            sortRow.Children.Add(frame);
            chips.Add((frame, label, () => draft.Sort == v));
        }

        // ---- переключатели
        var toggles = new StackPanel { Spacing = 2 };
        described = new Toggle(Loc.T("facet.onlyDescribed"), draft.OnlyDescribed,
            on => draft.OnlyDescribed = on, fontSize: 12);
        unshelved = new Toggle(Loc.T("facet.onlyUnshelved"), draft.OnlyUnshelved,
            on => draft.OnlyUnshelved = on, fontSize: 12);
        toggles.Children.Add(described);
        toggles.Children.Add(unshelved);

        host.Children.Add(Section(Loc.T("facet.presets"), presetRow));
        host.Children.Add(Section(Loc.T("facet.genres"), genreGrid));
        host.Children.Add(Section(Loc.T("facet.tags"), tagWrap));
        host.Children.Add(Section(Loc.T("facet.decade"), decadeRow));
        host.Children.Add(Section(Loc.T("facet.size"), sizeRow));
        host.Children.Add(Section(Loc.T("facet.sortTitle"), sortRow));
        host.Children.Add(toggles);

        // ---- подвал
        var reset = Ui.GhostButton(Loc.T("facet.reset"), () =>
        {
            draft.CopyFrom(new CatalogFilter());
            Apply();
            Sync();
        }, Skin.TextMuted);
        reset.Padding = new Thickness(14, 8);
        reset.Margin = new Thickness(0, 0, 8, 0);

        var apply = Ui.Clickable(Ui.T(Loc.T("facet.show"), 12.5, Skin.OnAccent, FontWeight.SemiBold),
            () =>
            {
                overlay!.Close();
                onApply(draft);
            }, Skin.Accent, Skin.AccentHover, new CornerRadius(999), new Thickness(20, 9));

        host.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 2, 0, 0),
            Children = { reset, apply },
        });

        // ---- сама панель
        var body = new Border
        {
            Background = new SolidColorBrush(Skin.PanelColor),
            BorderBrush = Skin.GlassEdge,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(18, 16),
            Width = width,
            Child = host,
            Opacity = 0,
            RenderTransform = new TranslateTransform(0, -10),
            BoxShadow = new BoxShadows(new BoxShadow
            {
                OffsetX = 0, OffsetY = 14, Blur = 34, Color = Color.Parse("#88000000"),
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
                    Duration = TimeSpan.FromMilliseconds(180),
                    Easing = new Avalonia.Animation.Easings.CubicEaseOut(),
                },
            },
        };

        overlay = OverlayPanel.Show(anchor, body, new Point(0, anchor.Bounds.Height + 8));
        if (overlay == null) return null;

        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            body.Opacity = 1;
            body.RenderTransform = new TranslateTransform(0, 0);
        }, Avalonia.Threading.DispatcherPriority.Input);

        void Apply()
        {
            described?.SetSilently(draft.OnlyDescribed);
            unshelved?.SetSilently(draft.OnlyUnshelved);
        }

        void Sync()
        {
            foreach (var (frame, label, isOn) in chips)
            {
                var on = isOn();
                Ui.SetColors(frame, on ? Skin.CardHover : Brushes.Transparent,
                    on ? Skin.CardHover : Skin.PanelAlt);
                label.Foreground = on ? Skin.Text : Skin.TextMuted;
                label.FontWeight = on ? FontWeight.SemiBold : FontWeight.Medium;
            }
        }

        Sync();
        return new FilterPopover(overlay!, body);
    }

    private static Control Section(string title, Control content)
    {
        var stack = new StackPanel { Spacing = 8 };
        stack.Children.Add(Ui.T(title, 11, Skin.TextDim, FontWeight.SemiBold));
        stack.Children.Add(content);
        return stack;
    }

}
