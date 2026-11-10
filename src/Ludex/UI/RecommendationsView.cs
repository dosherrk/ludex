using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
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
/// Вкладка «Рекомендации»: большой каталог игр на любой вкус плюс свежие витрины магазина.
/// Обложки подгружаются по мере показа карточек, список идёт порциями по 60.
/// </summary>
public sealed class RecommendationsView : Grid
{
    private const double CardWidth = 296;
    private const double CoverHeight = 138;
    private const int PageSize = 60;

    // состояние переживает перестройку интерфейса (тема/язык пересоздают окно)
    private static string _query = "";
    private static string _genre = "all";
    private static string _sort = "catalog";

    private readonly Func<string, bool> _inLibrary;
    private readonly Func<string> _engine;
    private readonly Action<string>? _status;
    private readonly Action<int>? _count;

    private readonly WrapPanel _cards = new() { Orientation = Orientation.Horizontal, Margin = new Thickness(2) };
    private readonly WrapPanel _genreChips = new() { Orientation = Orientation.Horizontal };
    private readonly Dictionary<string, (Border Frame, TextBlock Label)> _genres = new();
    private readonly Dictionary<int, Image> _cardCovers = new();
    private readonly StackPanel _emptyBox;

    private readonly TextBlock _found = Ui.T("", 11.5, Skin.TextDim);
    private readonly TextBlock _note = Ui.T("", 11, Skin.TextDim);
    private readonly TextBlock _empty = Ui.T("", 15, Skin.Text, FontWeight.SemiBold);
    private readonly TextBlock _loadingText = Ui.T(Loc.T("catalog.loadingStart"), 13, Skin.TextMuted);
    private readonly Border _loading;
    private readonly Border _detail;
    private readonly Border _more;
    private readonly TextBlock _moreLabel;

    private List<StoreGame> _games = new();
    private CancellationTokenSource? _cts;
    private bool _busy;
    private int _shown = PageSize;

    public RecommendationsView(Func<string, bool> inLibrary, Action<string>? status = null,
        Action<int>? count = null, Func<string>? engine = null)
    {
        _inLibrary = inLibrary;
        _engine = engine ?? (() => "google");
        _status = status;
        _count = count;

        RowDefinitions = new RowDefinitions("Auto,*,Auto");

        // ---------------------------------------------------------------- панель фильтров
        var toolbar = new StackPanel { Spacing = 9, Margin = new Thickness(22, 12, 22, 8) };

        var searchBox = Ui.SearchBox(_query, Loc.T("recs.search"), text =>
        {
            _query = text ?? "";
            _shown = PageSize;
            Refresh();
        });
        searchBox.Width = 286;
        searchBox.Height = 38;

        var sortDropdown = new Dropdown(new[] { Loc.T("recs.sortStore"), Loc.T("recs.sortAz") },
            _sort == "az" ? 1 : 0, 190, 38, 12.5);

        sortDropdown.SelectionChanged += index =>
        {
            _sort = index == 1 ? "az" : "catalog";
            _shown = PageSize;
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

        _emptyBox = new StackPanel
        {
            Spacing = 6,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false,
            IsVisible = false,
        };
        _emptyBox.Children.Add(_empty);
        _emptyBox.Children.Add(Ui.T(Loc.T("recs.emptyHint"), 12, Skin.TextDim));
        Grid.SetRow(_emptyBox, 1);
        Children.Add(_emptyBox);

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

        // ---------------------------------------------------------------- «показать ещё»
        _moreLabel = Ui.T(Loc.T("recs.more"), 12.5, Skin.Text, FontWeight.SemiBold);
        _more = Ui.Clickable(_moreLabel, () =>
        {
            _shown += PageSize;
            Refresh();
        }, Skin.PanelAlt, Skin.CardHover, new CornerRadius(4), new Thickness(20, 10));
        _more.HorizontalAlignment = HorizontalAlignment.Left;
        _more.Margin = new Thickness(22, 0, 0, 10);
        _more.IsVisible = false;
        Grid.SetRow(_more, 2);
        Children.Add(_more);

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

    // ------------------------------------------------------------------ загрузка

    private async Task LoadAsync(bool force)
    {
        if (_busy) return;
        _busy = true;

        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        _loading.IsVisible = true;
        _loadingText.Text = Loc.T("catalog.loadingStart");
        _note.Text = "";
        _more.IsVisible = false;

        try
        {
            var catalog = await StoreCatalog.LoadAsync(force,
                text => Dispatcher.UIThread.Post(() => _loadingText.Text = text), ct);

            if (ct.IsCancellationRequested) return;

            _games = catalog;
            _loading.IsVisible = false;

            if (_games.Count == 0)
            {
                _games = OfflineGames();
                _note.Text = string.IsNullOrEmpty(StoreCatalog.LastError)
                    ? Loc.T("recs.offline")
                    : Loc.F("recs.offlineWhy", StoreCatalog.LastError!);
                _status?.Invoke(Loc.T("recs.offline"));
            }
            else
            {
                var stamp = StoreCatalog.FetchedUtc == default
                    ? DateTime.Now
                    : StoreCatalog.FetchedUtc.ToLocalTime();
                _note.Text = Loc.F("catalog.updated", stamp.ToString("HH:mm"));
                _status?.Invoke(Loc.F("recs.loaded", _games.Count));
            }

            BuildGenreChips();
            _shown = PageSize;
            Refresh();
        }
        catch (Exception ex)
        {
            AppPaths.Log("RecommendationsView load: " + ex.Message);
            _games = OfflineGames();
            _loading.IsVisible = false;
            _note.Text = Loc.F("recs.offlineWhy", ex.Message);
            BuildGenreChips();
            Refresh();
        }
        finally
        {
            _busy = false;
        }
    }

    /// <summary>Встроенный каталог на случай полного отсутствия сети.</summary>
    private static List<StoreGame> OfflineGames()
    {
        var list = new List<StoreGame>();
        foreach (var g in Recommendations.Games)
        {
            var game = new StoreGame
            {
                AppId = 0,
                Name = g.Title,
                Developer = g.Developer,
                ReleaseDate = g.Year.ToString(),
            };
            game.Genres = g.Genres.Select(x => Loc.T("genre." + x)).ToArray();
            game.Description = g.Description;
            game.Sections.Add("all");
            game.Sections.Add("today");
            list.Add(game);
        }
        return list;
    }

    // ------------------------------------------------------------------ фильтры

    private void BuildGenreChips()
    {
        _genreChips.Children.Clear();
        _genres.Clear();

        AddChip(_genres, _genreChips, "all", Loc.T("recs.allGenres"), () =>
        {
            _genre = "all";
            _shown = PageSize;
            ApplyChipStyles();
            Refresh();
        });

        var popular = _games
            .SelectMany(g => g.Genres)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .GroupBy(s => s)
            .OrderByDescending(gr => gr.Count())
            .Take(9)
            .Select(gr => gr.Key)
            .ToList();

        foreach (var genre in popular)
            AddChip(_genres, _genreChips, genre, genre, () =>
            {
                _genre = genre;
                _shown = PageSize;
                ApplyChipStyles();
                Refresh();
            });

        if (_genre != "all" && !_genres.ContainsKey(_genre))
        {
            AddChip(_genres, _genreChips, _genre, _genre, () =>
            {
                _genre = "all";
                _shown = PageSize;
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
        IEnumerable<StoreGame> q = _games;

        if (!string.IsNullOrWhiteSpace(_query))
        {
            var s = _query.Trim();
            q = q.Where(g => g.Name.Contains(s, StringComparison.CurrentCultureIgnoreCase)
                             || g.Developer.Contains(s, StringComparison.CurrentCultureIgnoreCase)
                             || g.Genres.Any(x => x.Contains(s, StringComparison.CurrentCultureIgnoreCase)));
        }

        if (_genre != "all") q = q.Where(g => g.Genres.Contains(_genre));

        if (_sort == "az")
            q = q.OrderBy(g => g.Name, StringComparer.CurrentCultureIgnoreCase);

        return q.ToList();
    }

    private void Refresh()
    {
        var list = Visible();
        var slice = list.Take(Math.Max(PageSize, _shown)).ToList();

        _cards.Children.Clear();
        _cardCovers.Clear();

        foreach (var game in slice)
            _cards.Children.Add(BuildCard(game));

        _found.Text = Loc.F("catalog.showing", slice.Count, list.Count);
        _empty.Text = Loc.T("recs.empty");
        _emptyBox.IsVisible = !_loading.IsVisible && list.Count == 0;
        _more.IsVisible = list.Count > slice.Count;
        _moreLabel.Text = Loc.F("recs.more", Math.Min(PageSize, list.Count - slice.Count));
        _count?.Invoke(list.Count);

        if (slice.Count > 0 && !_loading.IsVisible) Ui.FadeSlideIn(_cards, 0, 160, 6);

        // обложки — только для показанных карточек
        if (slice.Count > 0) _ = EnsureCoversAsync(slice);
    }

    /// <summary>Когда обложка не нашлась — не пытаемся снова раньше, чем через 5 минут.</summary>
    private static readonly Dictionary<int, DateTime> _coverFailed = new();

    private async Task EnsureCoversAsync(List<StoreGame> slice)
    {
        var now = DateTime.UtcNow;
        var need = slice
            .Where(g => g.CoverPath == null || !File.Exists(g.CoverPath))
            .Where(g => !_coverFailed.TryGetValue(g.AppId, out var at) || now - at > TimeSpan.FromMinutes(5))
            .ToList();
        if (need.Count == 0) return;

        var token = _cts?.Token ?? CancellationToken.None;

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(25) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Ludex/" + Updater.CurrentVersionText);

        using var gate = new SemaphoreSlim(6);

        await Task.WhenAll(need.Select(async game =>
        {
            try
            {
                await gate.WaitAsync(token);
            }
            catch
            {
                return;
            }

            try
            {
                var path = Path.Combine(StoreFeed.CoversDir, $"{game.AppId}.jpg");

                if (!File.Exists(path) || new FileInfo(path).Length < 1000)
                {
                    var bytes = await DownloadCoverAsync(http, game, token);
                    if (bytes == null)
                    {
                        _coverFailed[game.AppId] = DateTime.UtcNow;
                        return;
                    }

                    _coverFailed.Remove(game.AppId);
                    await File.WriteAllBytesAsync(path, bytes, token);
                }

                game.CoverPath = path;
                Dispatcher.UIThread.Post(() => ApplyCover(game));
            }
            catch (Exception ex)
            {
                AppPaths.Log($"Catalog cover {game.AppId}: {ex.Message}");
            }
            finally
            {
                gate.Release();
            }
        }));
    }

    private static async Task<byte[]?> DownloadCoverAsync(HttpClient http, StoreGame game, CancellationToken ct)
    {
        var urls = new List<string>
        {
            game.CoverUrl,
            $"https://cdn.cloudflare.steamstatic.com/steam/apps/{game.AppId}/capsule_231x87.jpg",
            $"https://cdn.cloudflare.steamstatic.com/steam/apps/{game.AppId}/capsule_616x353.jpg",
            $"https://cdn.akamai.steamstatic.com/steam/apps/{game.AppId}/header.jpg",
        };

        // у новых и снятых с продажи игр прямые адреса отдают 404 — тогда берём
        // ссылку из карточки магазина, там у обложки свой хеш в пути
        try
        {
            var fromStore = await StoreFeed.HeaderImageAsync(game.AppId, ct);
            if (!string.IsNullOrWhiteSpace(fromStore)) urls.Add(fromStore);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // карточка не открылась — обойдёмся прямыми адресами
        }

        foreach (var url in urls)
        {
            if (string.IsNullOrWhiteSpace(url)) continue;
            try
            {
                var bytes = await http.GetByteArrayAsync(url, ct);
                if (bytes.Length > 1000) return bytes;
            }
            catch
            {
                // пробуем следующий вариант
            }
        }

        return null;
    }

    private void ApplyCover(StoreGame game)
    {
        if (game.CoverPath == null) return;
        if (!_cardCovers.TryGetValue(game.AppId, out var image)) return;

        try
        {
            image.Source = new Bitmap(game.CoverPath);
        }
        catch
        {
            // битый файл — остаётся заглушка
        }
    }

    private Control BuildCard(StoreGame game)
    {
        var cover = new Grid { Width = CardWidth, Height = CoverHeight, ClipToBounds = true };
        cover.Children.Add(Placeholder(game));

        var image = new Image
        {
            Stretch = Stretch.UniformToFill,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        cover.Children.Add(image);

        var cached = LoadCover(game.CoverPath);
        if (cached != null) image.Source = cached;
        _cardCovers[game.AppId] = image;

        if (_inLibrary(game.Name))
        {
            cover.Children.Add(new Border
            {
                Child = Ui.T(Loc.T("recs.inLibrary"), 10.5, Skin.Text, FontWeight.SemiBold),
                Background = Skin.PanelAlt,
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(7, 3),
                Margin = new Thickness(8),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
            });
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

        const double pad = 12;
        var textInner = new StackPanel { Margin = new Thickness(pad, 9, pad, 11), Spacing = 4 };
        var title = Ui.T(game.Name, 12.5, Skin.Text, FontWeight.SemiBold);
        title.MaxWidth = CardWidth - pad * 2;
        textInner.Children.Add(title);

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

    private static Control Placeholder(StoreGame game)
    {
        var (a, b) = RecGame.PosterColors(game.Name);

        var grid = new Grid
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

        var initials = Initials(game.Name);
        if (initials.Length > 0)
        {
            grid.Children.Add(new TextBlock
            {
                Text = initials,
                FontSize = 30,
                FontWeight = FontWeight.Bold,
                Foreground = new SolidColorBrush(Color.FromArgb(46, 255, 255, 255)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            });
        }

        return grid;
    }

    /// <summary>Первые буквы названия — на случай, пока обложки нет.</summary>
    private static string Initials(string name)
    {
        var letters = new List<char>();
        foreach (var word in name.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var ch = word[0];
            if (!char.IsLetterOrDigit(ch)) continue;
            letters.Add(char.ToUpperInvariant(ch));
            if (letters.Count == 2) break;
        }

        return new string(letters.ToArray());
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
            Height = 254,
            CornerRadius = new CornerRadius(6),
            ClipToBounds = true,
            VerticalAlignment = VerticalAlignment.Top,
            Child = Placeholder(game),
        };

        var bitmap = LoadCover(game.CoverPath);
        if (bitmap != null)
            shot.Child = new Image { Source = bitmap, Stretch = Stretch.UniformToFill };

        content.Children.Add(shot);

        var info = new StackPanel { Spacing = 9, Margin = new Thickness(22, 0, 0, 0) };

        var title = Ui.T(game.Name, 19, Skin.Text, FontWeight.Bold, TextWrapping.Wrap);
        title.MaxWidth = 360;
        info.Children.Add(title);

        var devLine = Ui.T(string.IsNullOrEmpty(game.Developer)
            ? ""
            : Loc.F("recs.devLine", game.Developer, game.Publisher), 11.5, Skin.TextDim);
        info.Children.Add(devLine);

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

        if (!string.IsNullOrEmpty(game.ReleaseDate))
            info.Children.Add(Ui.T(Loc.F("recs.release", game.ReleaseDate), 11, Skin.TextDim));

        var description = game.Description;
        if (!string.IsNullOrWhiteSpace(description))
        {
            if (description.Length > 260) description = description[..260].TrimEnd() + "…";
            var text = Ui.T(description, 12.5, Skin.TextMuted, null, TextWrapping.Wrap);
            text.MaxWidth = 360;
            info.Children.Add(text);
        }
        else if (game.AppId > 0)
        {
            // у большого каталога описания нет — дотягиваем одно, когда карточку открыли
            var slot = Ui.T("", 12.5, Skin.TextMuted, null, TextWrapping.Wrap);
            slot.MaxWidth = 360;
            info.Children.Add(slot);
            _ = FillDescriptionAsync(game, slot, devLine);
        }

        if (_inLibrary(game.Name))
            info.Children.Add(Ui.T("★ " + Loc.T("recs.inLibrary"), 11.5, Skin.Text));

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Margin = new Thickness(0, 8, 0, 0) };
        buttons.Children.Add(Ui.PrimaryButton(Loc.T("recs.findWeb"), () => SearchWeb(game)));
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

    private static async Task FillDescriptionAsync(StoreGame game, TextBlock slot, TextBlock devLine)
    {
        try
        {
            await StoreFeed.EnrichAsync(new[] { game }, null, CancellationToken.None);
            var text = game.Description ?? "";
            if (text.Length > 260) text = text[..260].TrimEnd() + "…";

            Dispatcher.UIThread.Post(() =>
            {
                slot.Text = text;
                if (devLine.Text is "" or null && !string.IsNullOrEmpty(game.Developer))
                    devLine.Text = Loc.F("recs.devLine", game.Developer, game.Publisher);
            });
        }
        catch (Exception ex)
        {
            AppPaths.Log("RecommendationsView описание: " + ex.Message);
        }
    }

    private void SearchWeb(StoreGame game)
    {
        var query = Loc.IsRu ? $"\"{game.Name}\" скачать" : $"\"{game.Name}\" download";
        var url = _engine() switch
        {
            "yandex" => "https://yandex.ru/search/?text=" + Uri.EscapeDataString(query),
            "ddg" => "https://duckduckgo.com/?q=" + Uri.EscapeDataString(query),
            _ => "https://www.google.com/search?q=" + Uri.EscapeDataString(query),
        };

        Shell.OpenUrl(url);
        _status?.Invoke(Loc.F("recs.opening", game.Name));
    }
}
