using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Ludex.Services;

namespace Ludex.UI;

/// <summary>
/// Вкладка «Рекомендации»: встроенный каталог игр с поиском, фильтрами по жанру и годам,
/// сортировкой и карточкой игры. Никаких ссылок на сайты загрузок — кнопка открывает
/// обычный поиск по названию в браузере.
/// </summary>
public sealed class RecommendationsView : Grid
{
    private const double CardWidth = 188;
    private const double PosterHeight = 250;

    // состояние живёт между перестройками интерфейса (смена темы/языка пересоздаёт окно)
    private static string _query = "";
    private static string _genre = "all";
    private static string _period = "all";
    private static string _sort = "new";

    private readonly Func<string, bool> _inLibrary;
    private readonly Action<string>? _status;
    private readonly Action<int>? _count;

    private readonly WrapPanel _cards = new() { Orientation = Orientation.Horizontal, Margin = new Thickness(2) };
    private readonly WrapPanel _genreChips = new() { Orientation = Orientation.Horizontal };
    private readonly Dictionary<string, (Border Frame, TextBlock Label)> _chips = new();
    private readonly ScrollViewer _scroll;
    private readonly TextBlock _found = Ui.T("", 11.5, Skin.TextDim);
    private readonly Border _detail;
    private readonly Control _emptyState;

    public RecommendationsView(Func<string, bool> inLibrary, Action<string>? status = null, Action<int>? count = null)
    {
        _inLibrary = inLibrary;
        _status = status;
        _count = count;

        RowDefinitions = new RowDefinitions("Auto,*,Auto");

        // ---------------------------------------------------------------- панель фильтров
        var toolbar = new StackPanel { Spacing = 10, Margin = new Thickness(22, 12, 22, 8) };

        var searchBox = Ui.SearchBox(_query, Loc.T("recs.search"), text =>
        {
            _query = text ?? "";
            Refresh();
        });
        searchBox.Width = 300;
        searchBox.Height = 38;

        var periodDropdown = new Dropdown(new[]
        {
            Loc.T("recs.years"), Loc.T("recs.yearsNew"), Loc.T("recs.years2020"), Loc.T("recs.yearsOld"),
        }, _period switch { "new" => 1, "2020" => 2, "old" => 3, _ => 0 }, 140, 38, 12.5);

        periodDropdown.SelectionChanged += index =>
        {
            _period = index switch { 1 => "new", 2 => "2020", 3 => "old", _ => "all" };
            Refresh();
        };

        var sortDropdown = new Dropdown(new[]
        {
            Loc.T("recs.sortNew"), Loc.T("recs.sortOld"), Loc.T("recs.sortAz"), Loc.T("recs.sortDev"),
        }, _sort switch { "old" => 1, "az" => 2, "dev" => 3, _ => 0 }, 168, 38, 12.5);

        sortDropdown.SelectionChanged += index =>
        {
            _sort = index switch { 1 => "old", 2 => "az", 3 => "dev", _ => "new" };
            Refresh();
        };

        var firstRow = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto") };
        firstRow.Children.Add(searchBox);
        periodDropdown.Margin = new Thickness(10, 0, 8, 0);
        Grid.SetColumn(periodDropdown, 2);
        firstRow.Children.Add(periodDropdown);
        Grid.SetColumn(sortDropdown, 3);
        firstRow.Children.Add(sortDropdown);
        toolbar.Children.Add(firstRow);

        // ---- жанры
        _genreChips.Margin = new Thickness(-2, 0, 0, 0);
        toolbar.Children.Add(_genreChips);
        BuildGenreChips();

        // ---- счётчик и пояснение
        var meta = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        meta.Children.Add(_found);
        var hint = Ui.T(Loc.T("recs.hint"), 11, Skin.TextDim);
        hint.Opacity = 0.85;
        meta.Children.Add(hint);
        toolbar.Children.Add(meta);

        Children.Add(toolbar);

        // ---------------------------------------------------------------- сетка карточек
        _scroll = new ScrollViewer
        {
            Content = _cards,
            Padding = new Thickness(20, 4, 14, 12),
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };
        Ui.SmoothScroll(_scroll, 130);
        Grid.SetRow(_scroll, 1);
        Children.Add(_scroll);

        _emptyState = BuildEmptyState();
        Grid.SetRow(_emptyState, 1);
        Children.Add(_emptyState);

        // ---------------------------------------------------------------- карточка игры
        _detail = new Border
        {
            Background = Skin.Overlay,
            IsVisible = false,
        };
        _detail.PointerPressed += (_, e) =>
        {
            if (ReferenceEquals(e.Source, _detail)) HideDetail();
        };
        Grid.SetRowSpan(_detail, 3);
        Children.Add(_detail);

        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape && _detail.IsVisible)
            {
                HideDetail();
                e.Handled = true;
            }
        };

        Refresh();
    }

    // ------------------------------------------------------------------ фильтры

    private void BuildGenreChips()
    {
        _genreChips.Children.Clear();
        _chips.Clear();

        AddGenreChip("all", Loc.T("recs.allGenres"));
        foreach (var genre in Recommendations.Genres)
            AddGenreChip(genre, Loc.T("genre." + genre));
    }

    private void AddGenreChip(string key, string text)
    {
        var label = Ui.T(text, 12, Skin.TextMuted, FontWeight.Medium);
        var frame = Ui.Clickable(label, () =>
        {
            _genre = key;
            ApplyChipStyles();
            Refresh();
        }, Brushes.Transparent, Skin.PanelAlt, new CornerRadius(999), new Thickness(13, 7));

        frame.Margin = new Thickness(2, 0, 4, 0);
        _chips[key] = (frame, label);
        _genreChips.Children.Add(frame);
    }

    private void ApplyChipStyles()
    {
        foreach (var (key, (frame, label)) in _chips)
        {
            var selected = key == _genre;
            Ui.SetColors(frame, selected ? Skin.PanelAlt : Brushes.Transparent,
                selected ? Skin.CardHover : Skin.PanelAlt);
            label.Foreground = selected ? Skin.Text : Skin.TextMuted;
            label.FontWeight = selected ? FontWeight.SemiBold : FontWeight.Medium;
        }
    }

    // ------------------------------------------------------------------ содержимое

    private void Refresh()
    {
        ApplyChipStyles();

        var list = Recommendations.Filter(_query, _genre, _period, _sort);

        _cards.Children.Clear();
        for (int i = 0; i < list.Count; i++)
            _cards.Children.Add(BuildCard(list[i]));

        _found.Text = Loc.F("recs.found", list.Count);
        _emptyState.IsVisible = list.Count == 0;
        _count?.Invoke(list.Count);

        if (list.Count > 0) Ui.FadeSlideIn(_cards, 0, 200, 8);
    }

    private Control BuildCard(RecGame game)
    {
        var (a, b) = game.Poster();

        var poster = new Grid { Width = CardWidth, Height = PosterHeight, ClipToBounds = true };
        poster.Children.Add(new Border
        {
            Background = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(Color.Parse(a), 0),
                    new GradientStop(Color.Parse(b), 1),
                },
            },
        });

        poster.Children.Add(new TextBlock
        {
            Text = Initials(game.Title),
            FontSize = 74,
            FontWeight = FontWeight.Bold,
            Foreground = new SolidColorBrush(Colors.White, 0.18),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        });

        if (game.Year >= Recommendations.NewSinceYear) poster.Children.Add(Badge(Loc.T("recs.new"), true));

        if (_inLibrary(game.Title))
        {
            var inLib = Badge(Loc.T("recs.inLibrary"), false);
            inLib.HorizontalAlignment = HorizontalAlignment.Right;
            poster.Children.Add(inLib);
        }

        var hover = new Border
        {
            Background = new SolidColorBrush(Colors.White, 0.0),
            IsHitTestVisible = false,
            Transitions = new Transitions
            {
                new DoubleTransition { Property = Visual.OpacityProperty, Duration = TimeSpan.FromMilliseconds(160) },
            },
            Opacity = 0,
        };
        hover.Background = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(Color.FromArgb(90, 255, 255, 255), 0),
                new GradientStop(Color.FromArgb(0, 255, 255, 255), 0.55),
            },
        };
        poster.Children.Add(hover);

        // ---- подпись
        const double pad = 12;
        var textInner = new StackPanel { Margin = new Thickness(pad, 9, pad, 11), Spacing = 3 };
        var title = Ui.T(game.Title, 12.5, Skin.Text, FontWeight.SemiBold);
        title.MaxWidth = CardWidth - pad * 2;
        title.TextTrimming = TextTrimming.CharacterEllipsis;
        var subtitle = Ui.T(Loc.F("recs.devYear", game.Developer, game.Year), 11, Skin.TextDim);
        subtitle.MaxWidth = CardWidth - pad * 2;
        subtitle.TextTrimming = TextTrimming.CharacterEllipsis;
        textInner.Children.Add(title);
        textInner.Children.Add(subtitle);

        var plate = new Border
        {
            Background = Skin.CardPlate,
            CornerRadius = new CornerRadius(0, 0, 6, 6),
            Child = textInner,
        };

        var stack = new StackPanel();
        stack.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(6, 6, 0, 0),
            ClipToBounds = true,
            Child = poster,
        });
        stack.Children.Add(plate);

        var card = new Border
        {
            Child = stack,
            Background = Brushes.Transparent,
            CornerRadius = new CornerRadius(6),
            BoxShadow = Skin.CardShadow,
            Margin = new Thickness(0, 0, 16, 16),
            Width = CardWidth,
            Cursor = new Cursor(StandardCursorType.Hand),
            Transitions = new Transitions
            {
                new BoxShadowsTransition { Property = Border.BoxShadowProperty, Duration = TimeSpan.FromMilliseconds(160) },
            },
        };

        card.PointerEntered += (_, _) =>
        {
            card.BoxShadow = Ui.GlowShadow();
            hover.Opacity = 1;
        };
        card.PointerExited += (_, _) =>
        {
            card.BoxShadow = Skin.CardShadow;
            hover.Opacity = 0;
        };
        card.PointerPressed += (_, e) =>
        {
            if (!e.GetCurrentPoint(card).Properties.IsLeftButtonPressed) return;
            e.Handled = true;
            ShowDetail(game);
        };

        return card;
    }

    private static Border Badge(string text, bool accent)
    {
        var label = Ui.T(text, 10.5, accent ? Skin.OnAccent : Skin.Text, FontWeight.SemiBold);
        return new Border
        {
            Child = label,
            // «Новинка» — белая заливка акцента, «В библиотеке» — нейтральная плашка темы
            Background = accent ? Skin.Accent : Skin.PanelAlt,
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(7, 3),
            Margin = new Thickness(8),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
        };
    }

    private static string Initials(string title)
    {
        var words = title.Split(new[] { ' ', ':', '-', '\'' }, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0) return "?";
        if (words.Length == 1) return words[0][..Math.Min(2, words[0].Length)].ToUpperInvariant();
        return $"{char.ToUpperInvariant(words[0][0])}{char.ToUpperInvariant(words[1][0])}";
    }

    // ------------------------------------------------------------------ карточка игры

    private void ShowDetail(RecGame game)
    {
        var (a, b) = game.Poster();

        var poster = new Border
        {
            Width = 208,
            Height = 276,
            CornerRadius = new CornerRadius(6),
            ClipToBounds = true,
            VerticalAlignment = VerticalAlignment.Top,
            Background = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(Color.Parse(a), 0),
                    new GradientStop(Color.Parse(b), 1),
                },
            },
            Child = new TextBlock
            {
                Text = Initials(game.Title),
                FontSize = 86,
                FontWeight = FontWeight.Bold,
                Foreground = new SolidColorBrush(Colors.White, 0.18),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
        };

        var info = new StackPanel { Spacing = 10, Margin = new Thickness(22, 2, 0, 0) };

        var title = Ui.T(game.Title, 21, Skin.Text, FontWeight.Bold, TextWrapping.Wrap);
        title.MaxWidth = 420;
        info.Children.Add(title);

        info.Children.Add(Ui.T(Loc.F("recs.devYear", game.Developer, game.Year), 12, Skin.TextMuted));

        var genres = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };
        foreach (var g in game.Genres)
        {
            var chipLabel = Ui.T(Loc.T("genre." + g), 11, Skin.TextMuted);
            genres.Children.Add(new Border
            {
                Child = chipLabel,
                Background = Skin.PanelAlt,
                CornerRadius = new CornerRadius(999),
                Padding = new Thickness(10, 4),
                Margin = new Thickness(0, 0, 6, 4),
            });
        }
        info.Children.Add(genres);

        var about = Ui.T(game.Description, 13, Skin.TextMuted, null, TextWrapping.Wrap);
        about.MaxWidth = 430;
        about.Margin = new Thickness(0, 4, 0, 2);
        info.Children.Add(about);

        if (_inLibrary(game.Title))
        {
            var inLib = Ui.T("★ " + Loc.T("recs.inLibrary"), 11.5, Skin.Text);
            inLib.Opacity = 0.9;
            info.Children.Add(inLib);
        }

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            Margin = new Thickness(0, 8, 0, 0),
        };
        buttons.Children.Add(Ui.PrimaryButton(Loc.T("recs.findWeb"), () => SearchWeb(game)));
        buttons.Children.Add(Ui.GhostButton(Loc.T("btn.close"), HideDetail));
        info.Children.Add(buttons);

        var content = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
        content.Children.Add(poster);
        Grid.SetColumn(info, 1);
        content.Children.Add(info);

        var card = new Border
        {
            Background = Skin.Panel,
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(20),
            BoxShadow = Ui.GlowShadow(),
            Child = content,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MaxWidth = 760,
        };

        _detail.Child = card;
        _detail.IsVisible = true;
        Ui.FadeSlideIn(card, 0, 180, 8);
    }

    private void HideDetail() => _detail.IsVisible = false;

    private void SearchWeb(RecGame game)
    {
        var suffix = Loc.IsRu ? " игра" : " game";
        var url = "https://duckduckgo.com/?q=" + Uri.EscapeDataString(game.Title + suffix);
        Shell.OpenUrl(url);
        _status?.Invoke(Loc.F("recs.opening", game.Title));
    }

    private Control BuildEmptyState()
    {
        var stack = new StackPanel
        {
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 40, 0, 0),
        };

        stack.Children.Add(Ui.T(Loc.T("recs.empty"), 15, Skin.Text, FontWeight.SemiBold));
        var hint = Ui.T(Loc.T("recs.emptyHint"), 12, Skin.TextDim, null, TextWrapping.Wrap);
        hint.TextAlignment = TextAlignment.Center;
        stack.Children.Add(hint);

        return new Border { Child = stack, IsVisible = false, IsHitTestVisible = false };
    }
}
