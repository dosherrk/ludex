using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Ludex.Services;

namespace Ludex.UI;

/// <summary>
/// Выбор обложки: варианты с CDN Steam, поиск по названию, файл с диска или ссылка.
/// Панель живёт внутри окна (как настройки): отдельные окна оставляли «залипшее»
/// наведение после закрытия и лишнюю системную рамку.
/// </summary>
public sealed class CoverPanel : Border
{
    private readonly string _gameId;
    private readonly string? _sgdbKey;
    private readonly WrapPanel _results = new() { Margin = new Thickness(0, 12, 0, 0) };
    private readonly TextBlock _status;
    private readonly TextBox _query;
    private TextBox _urlBox = null!;
    private TextBox _appIdBox = null!;
    private CancellationTokenSource? _cts;

    public string? ChosenCoverPath { get; private set; }
    public bool ChosenIsPortrait { get; private set; }
    public bool ResetRequested { get; private set; }

    /// <summary>Панель закрыта: применяем выбор или просто убираем её.</summary>
    public event Action? Closed;

    private int? _appId;

    public CoverPanel(string gameTitle, string? sgdbKey, string gameId, int? appId = null)
    {
        _gameId = gameId;
        _sgdbKey = sgdbKey;
        _appId = appId;

        MaxWidth = 980;
        MaxHeight = 760;
        Background = Skin.Panel;
        CornerRadius = new CornerRadius(14);
        BoxShadow = new BoxShadows(new BoxShadow
        {
            OffsetX = 0,
            OffsetY = 14,
            Blur = 44,
            Color = Color.Parse("#AA000000"),
        });

        _query = new TextBox
        {
            Text = gameTitle,
            FontSize = 14,
            Height = 38,
            Background = Skin.PanelAlt,
            Foreground = Skin.Text,
            BorderBrush = Skin.Border,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10, 0, 10, 0),
            VerticalContentAlignment = VerticalAlignment.Center,
        };
        Ui.AnimateTextBox(_query);
        _query.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter) _ = SearchAsync();
        };

        _status = Ui.T("", 12, Skin.TextMuted, wrap: TextWrapping.Wrap);

        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto") };
        header.Children.Add(_query);
        var searchButton = Ui.PrimaryButton(Loc.T("cover.searchBtn"), () => _ = SearchAsync());
        searchButton.Margin = new Thickness(10, 0, 0, 0);
        Grid.SetColumn(searchButton, 1);
        header.Children.Add(searchButton);

        var pickFile = Ui.GhostButton(Loc.T("btn.browseFile"), () => _ = PickFileAsync());
        pickFile.Margin = new Thickness(10, 0, 0, 0);
        Grid.SetColumn(pickFile, 2);
        header.Children.Add(pickFile);

        // ссылка на картинку: когда поиск ничего не нашёл, обложку можно вставить руками
        var urlRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            Margin = new Thickness(0, 10, 0, 0),
        };
        _urlBox = new TextBox
        {
            Watermark = Loc.T("cover.urlWatermark"),
            FontSize = 12.5,
            Height = 34,
            Background = Skin.PanelAlt,
            Foreground = Skin.Text,
            BorderBrush = Skin.Border,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10, 0, 10, 0),
            VerticalContentAlignment = VerticalAlignment.Center,
            Width = 420,
        };
        Ui.AnimateTextBox(_urlBox);
        urlRow.Children.Add(_urlBox);
        urlRow.Children.Add(Ui.GhostButton(Loc.T("cover.urlApply"), () => _ = UseUrlAsync()));

        // Steam AppId вручную: когда поиск по названию не находит игру (переименования, DLC)
        _appIdBox = new TextBox
        {
            Watermark = Loc.T("cover.appIdWatermark"),
            FontSize = 12.5,
            Height = 34,
            Width = 150,
            Background = Skin.PanelAlt,
            Foreground = Skin.Text,
            BorderBrush = Skin.Border,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10, 0, 10, 0),
            VerticalContentAlignment = VerticalAlignment.Center,
        };
        Ui.AnimateTextBox(_appIdBox);
        if (_appId.HasValue) _appIdBox.Text = _appId.Value.ToString();
        urlRow.Children.Add(_appIdBox);
        urlRow.Children.Add(Ui.GhostButton(Loc.T("cover.appIdApply"), () =>
        {
            if (!int.TryParse(_appIdBox.Text?.Trim(), out var id) || id <= 0) return;
            _appId = id;
            _ = SearchAsync();
        }));

        var scroll = new ScrollViewer
        {
            Content = _results,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Margin = new Thickness(0, 8, 0, 0),
        };
        Ui.SmoothScroll(scroll, 120);

        var bottom = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 12, 0, 0),
        };
        bottom.Children.Add(Ui.GhostButton(Loc.T("btn.resetCover"), () =>
        {
            ResetRequested = true;
            Close();
        }));
        bottom.Children.Add(Ui.GhostButton(Loc.T("btn.cancel"), Close));

        var root = new DockPanel { Margin = new Thickness(22) };

        var topStack = new StackPanel();
        topStack.Children.Add(header);
        topStack.Children.Add(urlRow);
        DockPanel.SetDock(topStack, Dock.Top);
        root.Children.Add(topStack);
        DockPanel.SetDock(_status, Dock.Top);
        _status.Margin = new Thickness(0, 12, 0, 0);
        root.Children.Add(_status);
        DockPanel.SetDock(bottom, Dock.Bottom);
        root.Children.Add(bottom);
        root.Children.Add(scroll);

        Child = root;

        AttachedToVisualTree += (_, _) =>
        {
            Ui.FadeSlideIn(root, 0, 220, 10);
            _query.Focus();
            _ = SearchAsync();
        };

        KeyDown += (_, e) =>
        {
            if (e.Key != Key.Escape) return;
            e.Handled = true;
            Close();
        };
    }

    /// <summary>Закрыть без применения выбора (клик по затемнению, Esc).</summary>
    public void Cancel() => Close();

    private void Close()
    {
        _cts?.Cancel();
        Closed?.Invoke();
    }

    private async Task PickFileAsync()
    {
        var top = TopLevel.GetTopLevel(this);
        if (top == null) return;

        var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = Loc.T("cover.title"),
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Images")
                {
                    Patterns = new[] { "*.jpg", "*.jpeg", "*.png", "*.webp", "*.bmp" },
                },
            },
        });

        var file = files.FirstOrDefault();
        if (file == null) return;

        var local = file.TryGetLocalPath();
        if (string.IsNullOrEmpty(local)) return;

        var (w, h) = ImageSize(local);
        var path = Artwork.SaveLocalCover(_gameId, local, h >= w);
        if (path != null)
        {
            ChosenCoverPath = path;
            ChosenIsPortrait = h >= w;
            Close();
            return;
        }

        _status.Text = Loc.T("cover.noresults");
    }

    private static (int w, int h) ImageSize(string path)
    {
        try
        {
            using var bmp = new Bitmap(path);
            return ((int)bmp.Size.Width, (int)bmp.Size.Height);
        }
        catch
        {
            return (600, 900);
        }
    }

    /// <summary>Обложка по ссылке: качаем байты и сохраняем как выбранную.</summary>
    private async Task UseUrlAsync()
    {
        var url = _urlBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(url)) return;

        if (!url.StartsWith("http", StringComparison.OrdinalIgnoreCase)) url = "https://" + url;

        _status.Text = Loc.T("cover.searching");
        var bytes = await Artwork.FetchBytesAsync(url);
        if (bytes == null)
        {
            _status.Text = Loc.T("cover.badUrl");
            return;
        }

        bool portrait;
        try
        {
            using var bmp = new Bitmap(new MemoryStream(bytes));
            portrait = bmp.Size.Height >= bmp.Size.Width;
        }
        catch
        {
            _status.Text = Loc.T("cover.badUrl");
            return;
        }

        var path = Artwork.SaveCover(_gameId, bytes, portrait);
        if (path == null)
        {
            _status.Text = Loc.T("cover.badUrl");
            return;
        }

        ChosenCoverPath = path;
        ChosenIsPortrait = portrait;
        Close();
    }

    private async Task SearchAsync()
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        _results.Children.Clear();

        var query = _query.Text?.Trim();
        if (string.IsNullOrWhiteSpace(query) && !_appId.HasValue) return;

        _status.Text = Loc.T("cover.searching");

        var candidates = new List<ArtCandidate>();

        // 1) appId известен — варианты с CDN Steam есть всегда, без поиска и ключей
        if (_appId.HasValue)
            candidates.AddRange(await Artwork.SteamCandidatesAsync(_appId.Value, query ?? "", ct));

        // 2) поиск по названию (Steam + SteamGridDB, если задан ключ)
        if (!string.IsNullOrWhiteSpace(query))
        {
            try
            {
                candidates.AddRange(await Artwork.SearchAsync(query, _sgdbKey, ct));
            }
            catch
            {
                // поиск не удался — остаются варианты по appId
            }
        }

        if (ct.IsCancellationRequested) return;

        candidates = candidates
            .Where(c => !string.IsNullOrWhiteSpace(c.Url))
            .GroupBy(c => c.Url)
            .Select(g => g.First())
            .OrderByDescending(c => c.Portrait)
            .ToList();

        if (candidates.Count == 0)
        {
            _status.Text = Loc.T("cover.noresults");
            return;
        }

        _status.Text = Loc.F("cover.tipPortrait") + " · " + Loc.T("cover.tipWide");
        var semaphore = new SemaphoreSlim(4);
        int shown = 0;

        var tasks = candidates.Take(14).Select(async candidate =>
        {
            await semaphore.WaitAsync(ct);
            try
            {
                var bytes = await Artwork.FetchBytesAsync(candidate.Url, ct);
                if (bytes == null || ct.IsCancellationRequested) return;

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (ct.IsCancellationRequested) return;
                    Bitmap bmp;
                    try
                    {
                        bmp = new Bitmap(new MemoryStream(bytes));
                    }
                    catch
                    {
                        return;
                    }

                    double boxW = 150;
                    double boxH = 225;
                    var image = new Image
                    {
                        Source = bmp,
                        Stretch = Stretch.UniformToFill,
                        Width = boxW,
                        Height = boxH,
                    };

                    var frame = Ui.Clickable(image, () =>
                    {
                        var path = Artwork.SaveCover(_gameId, bytes, candidate.Portrait);
                        if (path == null) return;
                        ChosenCoverPath = path;
                        ChosenIsPortrait = candidate.Portrait;
                        Close();
                    }, Skin.Card, Skin.Accent, new CornerRadius(6), new Thickness(0));

                    frame.Margin = new Thickness(0, 0, 12, 12);
                    frame.Width = boxW;
                    frame.Height = boxH;
                    frame.ClipToBounds = true;

                    var source = candidate.Source == "steamgriddb" ? "SteamGridDB" : "Steam";
                    var caption = Ui.T($"{source} · {candidate.Width}×{candidate.Height}", 10, Skin.TextDim);
                    caption.Margin = new Thickness(0, 4, 12, 12);

                    var cell = new StackPanel { Width = boxW };
                    cell.Children.Add(frame);
                    cell.Children.Add(caption);
                    cell.Margin = new Thickness(0, 0, 12, 12);

                    _results.Children.Add(cell);
                    shown++;
                    _status.Text = string.Format("{0} · {1}", Loc.T("cover.tipPortrait"), shown);
                }, DispatcherPriority.Background);
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);

        if (shown == 0 && !ct.IsCancellationRequested)
            _status.Text = Loc.T("cover.noresults");
    }
}
