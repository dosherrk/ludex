using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Ludex.Services;

namespace Ludex.UI;

/// <summary>
/// Вкладка «Рекомендации»: сетевые витрины магазина (лидеры продаж, новинки, скидки,
/// «скоро»), поиск, фильтр по жанру, сортировка, загрузка обложек в кэш.
/// Если сети нет — показывает встроенный каталог и честно об этом пишет.
/// </summary>
public sealed class RecommendationsView : Grid
{
    private const double CardWidth = 296;
    private const double CoverHeight = 169;

    // состояние переживает перестройку интерфейса (тема/язык пересоздают окно)
    private static string _query = "";
    private static string _section = "today";
    private static string _genre = "all";
    private static string _sort = "steam";

    private readonly Func<string, bool> _inLibrary;
    private readonly Action<string>? _status;
    private readonly Action<int>? _count;

    private readonly WrapPanel _cards = new() { Orientation = Orientation.Horizontal, Margin = new Thickness(2) };
    private readonly WrapPanel _sectionChips = new() { Orientation = Orientation.Horizontal };
    private readonly WrapPanel _genreChips = new() { Orientation = Orientation.Horizontal };
    private readonly Dictionary<string, (Border Frame, TextBlock Label)> _sections = new();
    private readonly Dictionary<string, (Border Frame, TextBlock Label)> _genres = new();

    private readonly TextBlock _found = Ui.T("", 11.5, Skin.TextDim);
    private readonly TextBlock _note = Ui.T("", 11, Skin.TextDim);
    private readonly TextBlock _empty = Ui.T("", 15, Skin.Text, FontWeight.SemiBold);
    private readonly TextBlock _loadingText = Ui.T(Loc.T("recs.loading"), 13, Skin.TextMuted);
    private readonly Border _loading;
    private readonly Border _detail;

    private List<StoreGame> _games = new();
    private CancellationTokenSource? _cts;
    private bool _busy;

    public RecommendationsView(Func<string, bool> inLibrary, Action<string>? status = null, Action<int>? count = null)
    {
        _inLibrary = inLibrary;
        _status = status;
        _count = count;

        RowDefinitions = new RowDefinitions("Auto,*,Auto");

        // ---------------------------------------------------------------- панель фильтров
        var toolbar = new StackPanel { Spacing = 9, Margin = new Thickness(22, 12, 22, 8) };

        var searchBox = Ui.SearchBox(_query, Loc.T("recs.search"), text =>
        {
            _query = text ?? "";
            Refresh();
        });
        searchBox.Width = 286;
        searchBox.Height = 38;

        var sortDropdown = new Dropdown(new[]
        {
            Loc.T("recs.sortSteam"), Loc.T("recs.sortDiscount"), Loc.T("recs.sortPrice"),
            Loc.T("recs.sortAz"), Loc.T("recs.sortNew"),
        }, _sort switch { "discount" => 1, "price" => 2, "az" => 3, "new" => 4, _ => 0 }, 190, 38, 12.5);

        sortDropdown.SelectionChanged += index =>
        {
            _sort = index switch { 1 => "discount", 2 => "price", 3 => "az", 4 => "new", _ => "steam" };
            Refresh();
        };

        var refresh = Ui.GhostButton(Loc.T("recs.refresh"), () => _ = LoadAsync(true), Skin.TextMuted);
        refresh.Height = 38;
        refresh.Padding = new Thickness(14, 0);

        var firstRow = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto") };
        firstRow.Children.Add(searchBox);

        refresh.Margin = new Thickness(0, 0, 8, 0);
        Grid.SetColumn(refresh, 2);
        firstRow.Children.Add(refresh);

        Grid.SetColumn(sortDropdown, 3);
        firstRow.Children.Add(sortDropdown);
        toolbar.Children.Add(firstRow);

        // ---- разделы витрины
        _sectionChips.Margin = new Thickness(-2, 2, 0, 0);
        toolbar.Children.Add(_sectionChips);
        BuildSectionChips();

        // ---- жанры
        _genreChips.Margin = new Thickness(-2, 0, 0, 0);
        toolbar.Children.Add(_genreChips);
        BuildGenreChips();

        var meta = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Margin = new Thickness(0, 2, 0, 0) };
        meta.Children.Add(_found);
        _note.Opacity = 0.85;
        meta.Children.Add(_note);
        toolbar.Children.Add(meta);

        Children.Add(toolbar);

        // ---------------------------------------------------------------- карточки
        var scroll = new ScrollViewer
        {
            Content = _cards,
            Padding = new Thickness(20, 4, 14, 12),
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };
        Ui.SmoothScroll(scroll, 130);
        Grid.SetRow(scroll, 1);
        Children.Add(scroll);

        var emptyBox = new StackPanel
        {
            Spacing = 6,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false,
            IsVisible = false,
        };
        emptyBox.Children.Add(_empty);
        var emptyHint = Ui.T(Loc.T("recs.emptyHint"), 12, Skin.TextDim);
        emptyBox.Children.Add(emptyHint);
        Grid.SetRow(emptyBox, 1);
        Children.Add(emptyBox);
        _emptyBox = emptyBox;

        // ---------------------------------------------------------------- загрузка
        _loading = new Border
        {
            Child = new StackPanel
            {
                Spacing = 10,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Children = { _loadingText },
            },
            IsVisible = true,
        };
        Grid.SetRow(_loading, 1);
        Children.Add(_loading);

        // ---------------------------------------------------------------- карточка игры
        _detail = new Border { Background = Skin.Overlay, IsVisible = false };
        _detail.PointerPressed += (_, e) =>
        {
            if (ReferenceEquals(e.Source, _detail)) _detail.IsVisible = false;
        };
        Grid.SetRowSpan(_detail, 3);
        Children.Add(_detail);

        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape && _detail.IsVisible)
            {
                _detail.IsVisible = false;
                e.Handled = true;
            }
        };

        _ = LoadAsync(false);
    }

    private readonly StackPanel _emptyBox;

    // ------------------------------------------------------------------ загрузка

    private async Task LoadAsync(bool force)
    {
        if (_busy) return;
        _busy = true;

        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        _loading.IsVisible = true;
        _loadingText.Text = Loc.T("recs.loading");
        _note.Text = "";

        try
        {
            var result = await StoreFeed.LoadAsync(force, ct);
            if (ct.IsCancellationRequested) return;

            if (result.Ok && result.Games.Count > 0)
            {
                _games = result.Games;
                _loading.IsVisible = false;
                Refresh();

                var stamp = result.FetchedUtc.ToLocalTime().ToString("HH:mm");
                _note.Text = result.FromCache ? Loc.F("recs.cached", stamp) : Loc.F("recs.updated", stamp);
                _status?.Invoke(Loc.F("recs.loaded", _games.Count));

                // жанры/описания и обложки — в фоне, список обновляется по мере поступления
                _ = EnrichAsync(ct);
                _ = CoversAsync(ct);
            }
            else
            {
                // сети нет — встроенный каталог, чтобы вкладка не была пустой
                _games = OfflineGames();
                _loading.IsVisible = false;
                _note.Text = Loc.T("recs.offline");
                BuildGenreChips();
                Refresh();
                _status?.Invoke(Loc.T("recs.offline"));
            }
        }
        catch (Exception ex)
        {
            AppPaths.Log("RecommendationsView load: " + ex.Message);
            _games = OfflineGames();
            _loading.IsVisible = false;
            _note.Text = Loc.T("recs.offline");
            BuildGenreChips();
            Refresh();
        }
        finally
        {
            _busy = false;
        }
    }

    private async Task EnrichAsync(CancellationToken ct)
    {
        try
        {
            var last = 0;
            await StoreFeed.EnrichAsync(_games, () => Dispatcher.UIThread.Post(() =>
            {
                var now = DateTime.UtcNow.Second;
                if (now == last) return;
                last = now;
                BuildGenreChips();
                Refresh();
            }), ct);
        }
        catch (Exception ex)
        {
            AppPaths.Log("RecommendationsView enrich: " + ex.Message);
        }
        finally
        {
            Dispatcher.UIThread.Post(() =>
            {
                BuildGenreChips();
                Refresh();
            });
        }
    }

    private async Task CoversAsync(CancellationToken ct)
    {
        try
        {
            await StoreFeed.DownloadCoversAsync(_games, () => Dispatcher.UIThread.Post(Refresh), ct);
        }
        catch (Exception ex)
        {
            AppPaths.Log("RecommendationsView covers: " + ex.Message);
        }
    }

    /// <summary>Встроенный каталог на случай отсутствия сети.</summary>
    private static List<StoreGame> OfflineGames()
    {
        var list = new List<StoreGame>();
        foreach (var g in Recommendations.Games)
        {
            var game = new StoreGame
            {
                AppId = 0,
                Name = g.Title,
                ReleaseDate = g.Year.ToString(),
            };
            game.Genres = g.Genres.Select(x => Loc.T("genre." + x)).ToArray();
            game.Description = g.Description;
            game.Sections.Add("today");
            game.Sections.Add(g.Year >= Recommendations.NewSinceYear ? "new" : "top");
            list.Add(game);
        }
        return list;
    }

    // ------------------------------------------------------------------ фильтры

    private void BuildSectionChips()
    {
        _sectionChips.Children.Clear();
        _sections.Clear();

        foreach (var (key, title) in new[]
                 {
                     ("today", Loc.T("recs.sectionToday")),
                     ("new", Loc.T("recs.sectionNew")),
                     ("discount", Loc.T("recs.sectionDiscount")),
                     ("top", Loc.T("recs.sectionTop")),
                     ("soon", Loc.T("recs.sectionSoon")),
                 })
        {
            AddChip(_sections, _sectionChips, key, title, () =>
            {
                _section = key;
                ApplyChipStyles();
                Refresh();
            }, bold: true);
        }

        ApplyChipStyles();
    }

    private void BuildGenreChips()
    {
        _genreChips.Children.Clear();
        _genres.Clear();

        AddChip(_genres, _genreChips, "all", Loc.T("recs.allGenres"), () =>
        {
            _genre = "all";
            ApplyChipStyles();
            Refresh();
        });

        var popular = _games
            .SelectMany(g => g.Genres)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .GroupBy(s => s)
            .OrderByDescending(gr => gr.Count())
            .Take(8)
            .Select(gr => gr.Key)
            .ToList();

        foreach (var genre in popular)
        {
            if (_genre != "all" && genre != _genre) continue;   // держим выбранный жанр в списке
            AddChip(_genres, _genreChips, genre, genre, () =>
            {
                _genre = genre;
                ApplyChipStyles();
                Refresh();
            });
        }

        if (_genre != "all" && !_genres.ContainsKey(_genre))
        {
            AddChip(_genres, _genreChips, _genre, _genre, () =>
            {
                _genre = "all";
                ApplyChipStyles();
                Refresh();
            });
        }

        ApplyChipStyles();
    }

    private static void AddChip(Dictionary<string, (Border, TextBlock)> map, WrapPanel host,
        string key, string title, Action onClick, bool bold = false)
    {
        var label = Ui.T(title, 12, Skin.TextMuted, bold ? FontWeight.SemiBold : FontWeight.Medium);
        var frame = Ui.Clickable(label, onClick, Brushes.Transparent, Skin.PanelAlt,
            new CornerRadius(999), new Thickness(13, 7));
        frame.Margin = new Thickness(2, 0, 4, 0);

        map[key] = (frame, label);
        host.Children.Add(frame);
    }

    private void ApplyChipStyles()
    {
        foreach (var (key, (frame, label)) in _sections)
        {
            var selected = key == _section;
            Ui.SetColors(frame, selected ? Skin.PanelAlt : Brushes.Transparent,
                selected ? Skin.CardHover : Skin.PanelAlt);
            label.Foreground = selected ? Skin.Text : Skin.TextMuted;
        }

        foreach (var (key, (frame, label)) in _genres)
        {
            var selected = key == _genre;
            Ui.SetColors(frame, selected ? Skin.PanelAlt : Brushes.Transparent,
                selected ? Skin.CardHover : Skin.PanelAlt);
            label.Foreground = selected ? Skin.Text : Skin.TextMuted;
            label.FontWeight = selected ? FontWeight.SemiBold : FontWeight.Medium;
        }
    }

    // ------------------------------------------------------------------ список

    private List<StoreGame> Visible()
    {
        IEnumerable<StoreGame> q = _games.Where(g => g.Sections.Contains(_section));

        if (!string.IsNullOrWhiteSpace(_query))
        {
            var s = _query.Trim();
            q = q.Where(g => g.Name.Contains(s, StringComparison.CurrentCultureIgnoreCase)
                             || g.Genres.Any(x => x.Contains(s, StringComparison.CurrentCultureIgnoreCase)));
        }

        if (_genre != "all") q = q.Where(g => g.Genres.Contains(_genre));

        q = _sort switch
        {
            "discount" => q.OrderByDescending(g => g.DiscountPercent).ThenByDescending(g => g.FinalPrice),
            "price" => q.OrderBy(g => g.FinalPrice == 0 ? -1 : g.FinalPrice),
            "az" => q.OrderBy(g => g.Name, StringComparer.CurrentCultureIgnoreCase),
            "new" => q.OrderByDescending(g => g.AppId),
            _ => q,
        };

        return q.ToList();
    }

    private void Refresh()
    {
        var list = Visible();

        _cards.Children.Clear();
        foreach (var game in list) _cards.Children.Add(BuildCard(game));

        _found.Text = Loc.F("recs.found", list.Count);
        _empty.Text = Loc.T("recs.empty");
        _emptyBox.IsVisible = !_loading.IsVisible && list.Count == 0;
        _count?.Invoke(list.Count);

        if (list.Count > 0 && !_loading.IsVisible) Ui.FadeSlideIn(_cards, 0, 180, 8);
    }

    private Control BuildCard(StoreGame game)
    {
        var cover = new Grid { Width = CardWidth, Height = CoverHeight, ClipToBounds = true };
        cover.Children.Add(Placeholder(game));

        var bitmap = LoadCover(game.CoverPath);
        if (bitmap != null)
        {
            cover.Children.Add(new Image
            {
                Source = bitmap,
                Stretch = Stretch.UniformToFill,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
            });
        }

        if (game.DiscountPercent > 0)
        {
            var badge = new Border
            {
                Child = Ui.T($"-{game.DiscountPercent}%", 11.5, Skin.OnAccent, FontWeight.Bold),
                Background = Skin.Accent,
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(7, 3),
                Margin = new Thickness(8),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
            };
            cover.Children.Add(badge);
        }

        if (_inLibrary(game.Name))
        {
            var inLib = new Border
            {
                Child = Ui.T(Loc.T("recs.inLibrary"), 10.5, Skin.Text, FontWeight.SemiBold),
                Background = Skin.PanelAlt,
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(7, 3),
                Margin = new Thickness(8),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
            };
            cover.Children.Add(inLib);
        }

        var hover = new Border
        {
            Opacity = 0,
            IsHitTestVisible = false,
            Background = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                EndPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(Color.FromArgb(90, 255, 255, 255), 0),
                    new GradientStop(Color.FromArgb(0, 255, 255, 255), 0.6),
                },
            },
            Transitions = new Transitions
            {
                new DoubleTransition { Property = Visual.OpacityProperty, Duration = TimeSpan.FromMilliseconds(160) },
            },
        };
        cover.Children.Add(hover);

        // ---- подпись: название и цена
        const double pad = 12;
        var textInner = new StackPanel { Margin = new Thickness(pad, 9, pad, 11), Spacing = 4 };
        var title = Ui.T(game.Name, 12.5, Skin.Text, FontWeight.SemiBold);
        title.MaxWidth = CardWidth - pad * 2;
        textInner.Children.Add(title);

        var priceRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var price = Ui.T(PriceLine(game), 11.5, Skin.TextMuted, FontWeight.Medium);
        priceRow.Children.Add(price);

        var oldPrice = StoreFeed.OldPriceText(game);
        if (oldPrice.Length > 0)
        {
            var old = Ui.T(oldPrice, 11, Skin.TextDim);
            old.TextDecorations = TextDecorations.Strikethrough;
            priceRow.Children.Add(old);
        }

        if (game.ComingSoon && !string.IsNullOrEmpty(game.ReleaseDate))
            priceRow.Children.Add(Ui.T(Loc.F("recs.release", game.ReleaseDate), 11, Skin.TextDim));

        textInner.Children.Add(priceRow);

        var plate = new Border
        {
            Background = Skin.CardPlate,
            CornerRadius = new CornerRadius(0, 0, 6, 6),
            Child = textInner,
        };

        var stack = new StackPanel();
        stack.Children.Add(new Border { CornerRadius = new CornerRadius(6, 6, 0, 0), ClipToBounds = true, Child = cover });
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

    private static string PriceLine(StoreGame game)
    {
        if (game.ComingSoon && game.FinalPrice <= 0) return Loc.T("recs.soon");
        return StoreFeed.PriceText(game);
    }

    /// <summary>Заливка на месте обложки: пока картинка не скачана, карточка не «дырявая».</summary>
    private static Control Placeholder(StoreGame game)
    {
        var (a, b) = RecGame.PosterColors(game.Name);
        var border = new Border
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
        };
        return border;
    }

    private static Bitmap? LoadCover(string? path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        try
        {
            return new Bitmap(path);
        }
        catch
        {
            return null;
        }
    }

    // ------------------------------------------------------------------ карточка игры

    private void ShowDetail(StoreGame game)
    {
        var content = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };

        var shot = new Border
        {
            Width = 452,
            Height = 211,
            CornerRadius = new CornerRadius(6),
            ClipToBounds = true,
            VerticalAlignment = VerticalAlignment.Top,
            Child = Placeholder(game),
        };

        var header = LoadCover(game.HeaderPath ?? game.CoverPath);
        if (header != null)
        {
            ((Border)shot).Child = new Image { Source = header, Stretch = Stretch.UniformToFill };
        }
        content.Children.Add(shot);

        var info = new StackPanel { Spacing = 9, Margin = new Thickness(22, 0, 0, 0) };

        var title = Ui.T(game.Name, 19, Skin.Text, FontWeight.Bold, TextWrapping.Wrap);
        title.MaxWidth = 360;
        info.Children.Add(title);

        var priceRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        if (game.DiscountPercent > 0)
        {
            priceRow.Children.Add(new Border
            {
                Child = Ui.T($"-{game.DiscountPercent}%", 12, Skin.OnAccent, FontWeight.Bold),
                Background = Skin.Accent,
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8, 3),
                VerticalAlignment = VerticalAlignment.Center,
            });
        }

        priceRow.Children.Add(Ui.T(PriceLine(game), 15, Skin.Text, FontWeight.SemiBold));

        var oldPrice = StoreFeed.OldPriceText(game);
        if (oldPrice.Length > 0)
        {
            var old = Ui.T(oldPrice, 12.5, Skin.TextDim);
            old.TextDecorations = TextDecorations.Strikethrough;
            priceRow.Children.Add(old);
        }
        info.Children.Add(priceRow);

        if (!string.IsNullOrEmpty(game.ReleaseDate))
            info.Children.Add(Ui.T(Loc.F("recs.release", game.ReleaseDate), 11.5, Skin.TextDim));

        if (game.Genres.Length > 0)
        {
            var genres = new WrapPanel { Orientation = Orientation.Horizontal };
            foreach (var g in game.Genres)
            {
                genres.Children.Add(new Border
                {
                    Child = Ui.T(g, 11, Skin.TextMuted),
                    Background = Skin.PanelAlt,
                    CornerRadius = new CornerRadius(999),
                    Padding = new Thickness(10, 4),
                    Margin = new Thickness(0, 0, 6, 4),
                });
            }
            info.Children.Add(genres);
        }

        var description = game.Description;
        if (!string.IsNullOrWhiteSpace(description))
        {
            if (description.Length > 300) description = description[..300].TrimEnd() + "…";
            var text = Ui.T(description, 12.5, Skin.TextMuted, null, TextWrapping.Wrap);
            text.MaxWidth = 360;
            info.Children.Add(text);
        }

        if (_inLibrary(game.Name))
            info.Children.Add(Ui.T("★ " + Loc.T("recs.inLibrary"), 11.5, Skin.Text));

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Margin = new Thickness(0, 8, 0, 0) };
        buttons.Children.Add(Ui.PrimaryButton(Loc.T("recs.openSteam"), () => Open(game)));
        buttons.Children.Add(Ui.GhostButton(Loc.T("btn.close"), () => _detail.IsVisible = false));
        info.Children.Add(buttons);

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
        };

        _detail.Child = card;
        _detail.IsVisible = true;
        Ui.FadeSlideIn(card, 0, 180, 8);
    }

    private void Open(StoreGame game)
    {
        if (game.AppId <= 0)
        {
            var url = "https://duckduckgo.com/?q=" + Uri.EscapeDataString(game.Name + (Loc.IsRu ? " игра" : " game"));
            Shell.OpenUrl(url);
            _status?.Invoke(Loc.F("recs.opening", game.Name));
            return;
        }

        Shell.OpenUrl(game.SteamUrl);
        _status?.Invoke(Loc.F("recs.openingSteam", game.Name));
    }
}
