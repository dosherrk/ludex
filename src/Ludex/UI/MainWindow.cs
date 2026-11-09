using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Animation;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaPath = Avalonia.Controls.Shapes.Path;
using Ludex.Services;

namespace Ludex.UI;

public sealed class MainWindow : Window
{
    private const double CardMinWidth = 164;
    private const double CardMaxWidth = 216;
    private const double CardGap = 18;      // отступ между плашками (и снизу)
    private double _cardWidth = 176;
    private double _cardHeight = 264;

    private readonly AppSettings _settings;
    private readonly List<GameEntry> _games;

    private readonly Dictionary<string, Image> _covers = new();
    private readonly Dictionary<string, Control> _placeholders = new();
    private readonly Dictionary<string, TextBlock> _titles = new();
    private readonly Dictionary<string, TextBlock> _subtitles = new();

    private string _search = "";
    private bool _favoritesOnly;
    private bool _scanning;
    private CancellationTokenSource? _workCts;

    private WrapPanel _grid = new();
    private ScrollViewer _libraryScroll = null!;
    private DispatcherTimer? _resizeTimer;
    private Control _emptyState = null!;
    private Border _detailLayer = null!;
    private TextBlock _statusLabel = null!;
    private TextBlock _countLabel = null!;
    private ProgressStrip _progress = null!;
    private Border _scanButton = null!;
    private TextBox? _searchBox;
    private Border? _favChip;
    private TextBlock? _favChipLabel;
    private TextBlock? _emptyTitle;
    private TextBlock? _emptyHint;
    private DispatcherTimer? _searchTimer;
    private Border _settingsLayer = null!;
    private Border _launchLayer = null!;
    private SettingsPanel? _settingsPanel;
    private AvaloniaPath? _maximizePath;

    private Grid _wallpaperLayer = null!;
    private DispatcherTimer? _launchTimer;
    private Stream? _wallpaperStream;
    private bool _appearanceDirty;

    private readonly Dictionary<string, Border> _starButtons = new();
    private readonly Dictionary<string, AvaloniaPath> _starIcons = new();

    public MainWindow()
    {
        _settings = Storage.LoadSettings();
        Loc.Lang = _settings.Language;
        Skin.Apply(_settings.Theme, _settings.Accent, _settings.Transparent);
        _games = Storage.LoadGames();
        _search = _settings.LastSearch ?? "";

        Title = "Ludex";
        Width = Math.Max(900, _settings.WindowWidth);
        Height = Math.Max(600, _settings.WindowHeight);
        MinWidth = 880;
        MinHeight = 560;
        Background = Skin.Bg;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        // своё обрамление: убираем стандартную полосу Windows
        ExtendClientAreaToDecorationsHint = true;
        ExtendClientAreaChromeHints = ExtendClientAreaChromeHints.NoChrome;
        ExtendClientAreaTitleBarHeightHint = -1;
        ApplyTransparency();

        if (_settings.WindowMaximized) WindowState = WindowState.Maximized;

        TrySetIcon();
        BuildUi();
        RenderGrid();

        Opened += OnOpened;
        Closing += OnClosing;
        SizeChanged += OnWindowResized;
        PropertyChanged += (_, e) =>
        {
            if (e.Property == WindowStateProperty) UpdateMaximizeIcon();
        };
    }

    /// <summary>Картинка логотипа из ресурсов приложения (для шапки).</summary>
    private static Image? LoadBrandImage(double size)
    {
        try
        {
            using var stream = AssetLoader.Open(new Uri("avares://Ludex/Assets/icon.png"));
            var bitmap = new Bitmap(stream);
            return new Image
            {
                Source = bitmap,
                Width = size,
                Height = size,
                Stretch = Stretch.Uniform,
            };
        }
        catch (Exception ex)
        {
            AppPaths.Log("Logo failed: " + ex.Message);
            return null;
        }
    }

    private void TrySetIcon()
    {
        try
        {
            using var stream = AssetLoader.Open(new Uri("avares://Ludex/Assets/icon.png"));
            Icon = new WindowIcon(stream);
        }
        catch (Exception ex)
        {
            AppPaths.Log("Icon failed: " + ex.Message);
        }
    }

    // ------------------------------------------------------------------ UI

    private void BuildUi()
    {
        _covers.Clear();
        _placeholders.Clear();
        _titles.Clear();
        _subtitles.Clear();
        _starButtons.Clear();
        _starIcons.Clear();

        var root = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto") };

        // ---- слой обоев: под всем содержимым окна
        _wallpaperLayer = new Grid { IsHitTestVisible = false };
        Grid.SetRowSpan(_wallpaperLayer, 3);
        root.Children.Add(_wallpaperLayer);
        RefreshWallpaper();

        // стеклянный вид: мягкий акцентный ореол и блик у верхней кромки окна
        if (Skin.Transparent)
        {
            var glass = new Grid { IsHitTestVisible = false };
            Grid.SetRowSpan(glass, 3);

            glass.Children.Add(new Border
            {
                Background = new RadialGradientBrush
                {
                    Center = new RelativePoint(0.5, -0.1, RelativeUnit.Relative),
                    GradientOrigin = new RelativePoint(0.5, -0.35, RelativeUnit.Relative),
                    RadiusX = new RelativeScalar(0.95, RelativeUnit.Relative),
                    RadiusY = new RelativeScalar(1.1, RelativeUnit.Relative),
                    GradientStops =
                    {
                        new GradientStop(Skin.AccentColor, 0),
                        new GradientStop(Color.FromArgb(0, Skin.AccentColor.R, Skin.AccentColor.G, Skin.AccentColor.B), 1),
                    },
                },
                Opacity = Skin.IsLight ? 0.10 : 0.16,
            });

            glass.Children.Add(new Border
            {
                Height = 190,
                VerticalAlignment = VerticalAlignment.Top,
                Background = new LinearGradientBrush
                {
                    StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                    GradientStops =
                    {
                        new GradientStop(Color.Parse(Skin.IsLight ? "#4CFFFFFF" : "#12FFFFFF"), 0),
                        new GradientStop(Color.Parse("#00FFFFFF"), 1),
                    },
                },
            });

            root.Children.Add(glass);
        }

        root.Children.Add(BuildTopBar());

        var contentGrid = new Grid();

        _grid = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(2) };

        _libraryScroll = new ScrollViewer
        {
            Content = _grid,
            Padding = new Thickness(16, 16, 10, 8),
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };
        Ui.SmoothScroll(_libraryScroll, 130);
        contentGrid.Children.Add(_libraryScroll);

        _emptyState = BuildEmptyState();
        contentGrid.Children.Add(_emptyState);

        _detailLayer = new Border
        {
            Background = Skin.Overlay,
            IsVisible = false,
        };
        _detailLayer.PointerPressed += (_, e) =>
        {
            if (ReferenceEquals(e.Source, _detailLayer)) _detailLayer.IsVisible = false;
        };
        contentGrid.Children.Add(_detailLayer);

        Grid.SetRow(contentGrid, 1);
        root.Children.Add(contentGrid);

        root.Children.Add(BuildStatusBar());

        // модальные слои: настройки и «запуск игры» — внутри приложения, во весь размер окна
        _settingsLayer = new Border
        {
            Background = Skin.Overlay,
            IsVisible = false,
            Transitions = new Transitions
            {
                new DoubleTransition
                {
                    Property = Visual.OpacityProperty,
                    Duration = TimeSpan.FromMilliseconds(200),
                    Easing = new Avalonia.Animation.Easings.CubicEaseOut(),
                },
            },
        };
        _settingsLayer.PointerPressed += (_, e) =>
        {
            if (ReferenceEquals(e.Source, _settingsLayer)) CloseSettings();
        };
        Grid.SetRowSpan(_settingsLayer, 3);
        root.Children.Add(_settingsLayer);

        _launchLayer = new Border
        {
            Background = Skin.Overlay,
            IsVisible = false,
            Transitions = new Transitions
            {
                new DoubleTransition
                {
                    Property = Visual.OpacityProperty,
                    Duration = TimeSpan.FromMilliseconds(210),
                    Easing = new Avalonia.Animation.Easings.CubicEaseOut(),
                },
            },
        };
        _launchLayer.PointerPressed += (_, e) => e.Handled = true;
        Grid.SetRowSpan(_launchLayer, 3);
        root.Children.Add(_launchLayer);

        Content = root;
        UpdateCount();
    }

    private Control BuildTopBar()
    {
        var bar = new Border
        {
            Background = Skin.BgDeep,
            Padding = new Thickness(16, 8, 6, 8),
            BorderBrush = Skin.Border,
            BorderThickness = new Thickness(0, 0, 0, 1),
        };

        // за пустые места шапки окно можно таскать мышью, двойной клик — развернуть
        bar.PointerPressed += (_, e) =>
        {
            if (!e.GetCurrentPoint(bar).Properties.IsLeftButtonPressed) return;
            if (IsInteractive(e.Source as Control)) return;
            try { BeginMoveDrag(e); } catch { /* окно ещё не готово */ }
        };
        bar.DoubleTapped += (_, _) => ToggleMaximize();

        var dock = new DockPanel { VerticalAlignment = VerticalAlignment.Center };

        // ---- кнопки окна: свернуть / развернуть / закрыть
        var windowButtons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 2,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 0, 0),
        };
        DockPanel.SetDock(windowButtons, Dock.Right);

        windowButtons.Children.Add(WindowButton(Ui.Icon(Ui.MinimizeGeometry, Skin.TextMuted, 13, 12),
            Minimize, Loc.T("win.minimize")));

        _maximizePath = Ui.Icon(Ui.MaximizeGeometry, Skin.TextMuted, 11, 11, true, 1.4);
        windowButtons.Children.Add(WindowButton(_maximizePath, ToggleMaximize, Loc.T("win.maximize")));

        windowButtons.Children.Add(WindowButton(Ui.Icon(Ui.CloseGeometry, Skin.TextMuted, 11, 11, true, 1.5),
            Close, Loc.T("win.close"), danger: true));

        dock.Children.Add(windowButtons);

        // ---- сканировать + настройки
        var rightPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            VerticalAlignment = VerticalAlignment.Center,
        };
        DockPanel.SetDock(rightPanel, Dock.Right);

        var scanLabel = Ui.T(Loc.T("btn.scan"), 13, Brushes.White, FontWeight.SemiBold);
        _scanButton = Ui.Clickable(scanLabel, () => _ = StartScanAsync(), Skin.Accent,
            Skin.AccentHover, new CornerRadius(6), new Thickness(16, 9));
        rightPanel.Children.Add(_scanButton);

        var settingsButton = Ui.Clickable(Ui.Icon(Ui.GearGeometry, Skin.TextMuted, 17, 17),
            ShowSettings, Skin.PanelAlt, Skin.CardHover, new CornerRadius(6), new Thickness(10, 8));
        Ui.Tip(settingsButton, Loc.T("btn.settings"));
        rightPanel.Children.Add(settingsButton);

        dock.Children.Add(rightPanel);

        // ---- слева: логотип + поиск + сортировка
        var leftPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 14,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var brand = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var logoImage = LoadBrandImage(20);
        if (logoImage != null)
        {
            logoImage.VerticalAlignment = VerticalAlignment.Center;
            brand.Children.Add(logoImage);
        }

        var logo = Ui.T(Loc.T("app.name"), 16, Skin.Text, FontWeight.Bold);
        brand.Children.Add(logo);
        leftPanel.Children.Add(brand);

        _searchBox = Ui.SearchBox(_search, Loc.T("search.watermark"), text =>
        {
            _search = text ?? "";
            _settings.LastSearch = _search;

            _searchTimer?.Stop();
            _searchTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(110) };
            _searchTimer.Tick += (_, _) =>
            {
                _searchTimer?.Stop();
                RenderGrid(true);
            };
            _searchTimer.Start();
        });
        _searchBox.Width = 280;
        _searchBox.Margin = new Thickness(10, 0, 0, 0);
        leftPanel.Children.Add(_searchBox);

        var sortDropdown = new Dropdown(
            new[]
            {
                Loc.T("sort.title"),
                Loc.T("sort.added"),
                Loc.T("sort.played"),
                Loc.T("sort.favorite"),
            },
            _settings.SortMode switch
            {
                "added" => 1,
                "played" => 2,
                "favorite" => 3,
                _ => 0,
            },
            190, 34, 12.5);

        sortDropdown.SelectionChanged += index =>
        {
            _settings.SortMode = index switch
            {
                1 => "added",
                2 => "played",
                3 => "favorite",
                _ => "title",
            };
            Storage.SaveSettings(_settings);
            RenderGrid(true);
        };
        leftPanel.Children.Add(sortDropdown);

        _favChipLabel = Ui.T("★  " + Loc.T("filter.fav"), 12,
            _favoritesOnly ? Brushes.White : Skin.TextMuted, FontWeight.Medium);
        _favChipLabel.Transitions = new Transitions
        {
            new Avalonia.Animation.BrushTransition
            {
                Property = TextBlock.ForegroundProperty,
                Duration = TimeSpan.FromMilliseconds(180),
            },
        };

        _favChip = Ui.Clickable(_favChipLabel,
            () =>
            {
                _favoritesOnly = !_favoritesOnly;
                ApplyFavChipState();
                RenderGrid(true);
            },
            _favoritesOnly ? Skin.Accent : Skin.PanelAlt,
            _favoritesOnly ? Skin.AccentHover : Skin.CardHover,
            new CornerRadius(6), new Thickness(12, 8));
        leftPanel.Children.Add(_favChip);

        dock.Children.Add(leftPanel);
        bar.Child = dock;
        return bar;
    }

    // ------------------------------------------------------------ окно

    private Border WindowButton(Control icon, Action onClick, string tip, bool danger = false)
    {
        var hover = danger ? new SolidColorBrush(Color.Parse("#E81123")) : Skin.CardHover;
        var button = Ui.Clickable(icon, onClick, Brushes.Transparent, hover, new CornerRadius(4), new Thickness(0));
        button.Width = 44;
        button.Height = 28;

        if (danger && icon is AvaloniaPath path)
        {
            path.Transitions = new Transitions
            {
                new Avalonia.Animation.BrushTransition
                {
                    Property = Avalonia.Controls.Shapes.Shape.StrokeProperty,
                    Duration = TimeSpan.FromMilliseconds(150),
                },
            };
            button.PointerEntered += (_, _) => path.Stroke = Brushes.White;
            button.PointerExited += (_, _) => path.Stroke = Skin.TextMuted;
        }

        Ui.Tip(button, tip);
        return button;
    }

    private void Minimize() => WindowState = WindowState.Minimized;

    private void ToggleMaximize() =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void UpdateMaximizeIcon()
    {
        if (_maximizePath == null) return;
        _maximizePath.Data = Geometry.Parse(WindowState == WindowState.Maximized
            ? Ui.RestoreGeometry
            : Ui.MaximizeGeometry);
    }

    /// <summary>Клик по интерактивному элементу шапки не должен перетаскивать окно.</summary>
    private static bool IsInteractive(Control? source)
    {
        var current = source;
        int guard = 0;
        while (current != null && guard++ < 14)
        {
            if (current.Tag as string == "clickable") return true;
            if (current is TextBox or ComboBox or Button or CheckBox or ListBox or ScrollViewer or Dropdown) return true;

            current = current.Parent as Control ?? (current as Visual)?.GetVisualParent() as Control;
        }
        return false;
    }

    private Control BuildStatusBar()
    {
        var border = new Border
        {
            Background = Skin.BgDeep,
            Padding = new Thickness(18, 8),
            BorderBrush = Skin.Border,
            BorderThickness = new Thickness(0, 1, 0, 0),
        };

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };

        _progress = new ProgressStrip(180, 4);
        _progress.SetActive(false);
        grid.Children.Add(_progress);

        _statusLabel = Ui.T(Loc.T("status.ready"), 12, Skin.TextDim);
        _statusLabel.Margin = new Thickness(14, 0, 0, 0);
        Grid.SetColumn(_statusLabel, 1);
        grid.Children.Add(_statusLabel);

        _countLabel = Ui.T("", 12, Skin.TextMuted);
        Grid.SetColumn(_countLabel, 2);
        grid.Children.Add(_countLabel);

        border.Child = grid;
        Grid.SetRow(border, 2);
        return border;
    }

    private Control BuildEmptyState()
    {
        var panel = new StackPanel
        {
            Spacing = 14,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MaxWidth = 560,
        };

        panel.Children.Add(new Border
        {
            Width = 84,
            Height = 84,
            CornerRadius = new CornerRadius(22),
            Background = Skin.AccentSoft,
            HorizontalAlignment = HorizontalAlignment.Center,
            Child = Ui.Icon(Ui.PlayGeometry, Brushes.White, 30, 34),
        });

        _emptyTitle = Ui.T(Loc.T("empty.title"), 20, Skin.Text, FontWeight.SemiBold);
        _emptyTitle.HorizontalAlignment = HorizontalAlignment.Center;
        panel.Children.Add(_emptyTitle);

        var hint = Ui.T(Loc.T("empty.hint"), 13, Skin.TextMuted, wrap: TextWrapping.Wrap);
        _emptyHint = hint;
        hint.TextAlignment = TextAlignment.Center;
        hint.HorizontalAlignment = HorizontalAlignment.Center;
        panel.Children.Add(hint);

        var button = Ui.PrimaryButton(Loc.T("empty.btn"), ShowSettings);
        button.HorizontalAlignment = HorizontalAlignment.Center;
        panel.Children.Add(button);

        var host = new Grid();
        host.Children.Add(panel);
        return host;
    }

    // -------------------------------------------------------------- rendering

    private IEnumerable<GameEntry> VisibleGames()
    {
        IEnumerable<GameEntry> q = _games;

        if (!string.IsNullOrWhiteSpace(_search))
        {
            var s = _search.Trim();
            q = q.Where(g => g.Title.Contains(s, StringComparison.CurrentCultureIgnoreCase)
                             || g.FolderName.Contains(s, StringComparison.OrdinalIgnoreCase)
                             || (g.SteamName ?? "").Contains(s, StringComparison.CurrentCultureIgnoreCase));
        }

        if (_favoritesOnly) q = q.Where(g => g.Favorite);

        q = _settings.SortMode switch
        {
            "added" => q.OrderByDescending(g => g.AddedUtc),
            "played" => q.OrderByDescending(g => g.LastPlayedUtc ?? DateTime.MinValue),
            "favorite" => q.OrderByDescending(g => g.Favorite).ThenBy(g => g.Title, StringComparer.CurrentCultureIgnoreCase),
            _ => q.OrderBy(g => g.Title, StringComparer.CurrentCultureIgnoreCase),
        };

        return q.ToList();
    }

    private void RenderGrid()
    {
        RenderGrid(false);
    }

    /// <summary>
    /// Подобрать ширину плашки и число колонок так, чтобы ряд заполнял окно
    /// ровно: отступы между плашками чуть больше, а «пустого» места справа нет.
    /// </summary>
    private void UpdateCardMetrics()
    {
        // ширина, которую реально занимает сетка (без полей прокрутки и отступов)
        var avail = _grid?.Bounds.Width ?? 0;
        if (avail <= 1) avail = (Bounds.Width > 1 ? Bounds.Width : 1280) - 32;
        avail -= 2;                                     // запас на округление

        // колонок столько, сколько влезает минимальных плашек, а ширина такая,
        // чтобы ряд занимал всю доступную ширину (справа не остаётся «хвоста»)
        var columns = Math.Max(1, (int)Math.Floor(avail / (CardMinWidth + CardGap)));
        var width = Math.Floor(avail / columns) - CardGap;
        width = Math.Clamp(width, CardMinWidth, CardMaxWidth);

        _cardWidth = width;
        _cardHeight = Math.Round(width * 1.5);
    }

    private void OnWindowResized(object? sender, SizeChangedEventArgs e)
    {
        _resizeTimer?.Stop();
        _resizeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(90) };
        _resizeTimer.Tick += (_, _) =>
        {
            _resizeTimer?.Stop();

            var before = _cardWidth;
            UpdateCardMetrics();
            if (Math.Abs(before - _cardWidth) > 0.5) RenderGrid(false);
        };
        _resizeTimer.Start();
    }

    /// <summary>Перерисовать сетку; <paramref name="animated"/> — плавное появление списка.</summary>
    private void RenderGrid(bool animated)
    {
        _covers.Clear();
        _placeholders.Clear();
        _titles.Clear();
        _subtitles.Clear();
        _grid.Children.Clear();

        UpdateCardMetrics();

        var games = VisibleGames().ToList();

        for (int i = 0; i < games.Count; i++)
        {
            _grid.Children.Add(BuildCard(games[i], i));
        }

        var wasEmpty = _emptyState.IsVisible;
        _emptyState.IsVisible = games.Count == 0;

        // у фильтра «Избранное» пустое состояние своё
        if (_emptyState.IsVisible && _emptyTitle != null && _emptyHint != null)
        {
            _emptyTitle.Text = Loc.T(_favoritesOnly ? "empty.favTitle" : "empty.title");
            _emptyHint.Text = Loc.T(_favoritesOnly ? "empty.favHint" : "empty.hint");
        }

        if (_emptyState.IsVisible && !wasEmpty) Ui.FadeSlideIn(_emptyState, 0, 220, 10);
        UpdateCount();

        if (animated && games.Count > 0)
        {
            // список «оседает» вниз, карточки проявляются каскадом
            _grid.RenderTransform = new TranslateTransform(0, 14);
            _grid.Transitions = new Transitions
            {
                new TransformOperationsTransition
                {
                    Property = Visual.RenderTransformProperty,
                    Duration = TimeSpan.FromMilliseconds(240),
                    Easing = new Avalonia.Animation.Easings.CubicEaseOut(),
                },
            };

            Dispatcher.UIThread.Post(() => _grid.RenderTransform = new TranslateTransform(0, 0));
        }
    }

    private Control BuildCard(GameEntry game, int index)
    {
        var coverGrid = new Grid { Width = _cardWidth, Height = _cardHeight, ClipToBounds = true };

        var bitmap = Ui.LoadBitmap(game.CoverFile);
        var image = new Image
        {
            Source = bitmap,
            Stretch = Stretch.UniformToFill,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        coverGrid.Children.Add(image);

        var placeholder = BuildPlaceholder(game);
        placeholder.IsVisible = bitmap == null;
        coverGrid.Children.Add(placeholder);

        var hover = BuildHoverOverlay(game);
        coverGrid.Children.Add(hover);

        // ---- звезда «в избранное»: появляется в углу карточки при наведении
        var starGlyph = Ui.Icon(Ui.StarGeometry, game.Favorite ? Skin.Accent : Brushes.White, 12, 12);
        var starButton = Ui.Clickable(starGlyph, () => ToggleFavorite(game),
            new SolidColorBrush(Color.Parse("#C4070B11")),
            new SolidColorBrush(Color.Parse("#E60B111A")),
            new CornerRadius(8), new Thickness(6));
        starButton.Width = 28;
        starButton.Height = 28;
        starButton.HorizontalAlignment = HorizontalAlignment.Right;
        starButton.VerticalAlignment = VerticalAlignment.Top;
        starButton.Margin = new Thickness(0, 6, 6, 6);
        starButton.Opacity = game.Favorite ? 1 : 0;
        starButton.IsHitTestVisible = game.Favorite;
        starButton.Transitions = new Transitions
        {
            new DoubleTransition { Property = Visual.OpacityProperty, Duration = TimeSpan.FromMilliseconds(170) },
            new BrushTransition { Property = Border.BackgroundProperty, Duration = TimeSpan.FromMilliseconds(170) },
        };
        Ui.Tip(starButton, Loc.T("card.favorite"));
        coverGrid.Children.Add(starButton);

        var textInner = new StackPanel { Margin = new Thickness(9, 7, 9, 9), Spacing = 2 };
        var title = Ui.T(game.Title, 12.5, Skin.Text, FontWeight.SemiBold);
        title.MaxWidth = _cardWidth - 18;
        var subtitle = Ui.T(Subtitle(game), 10.5, Skin.TextDim);
        subtitle.MaxWidth = _cardWidth - 18;
        textInner.Children.Add(title);
        textInner.Children.Add(subtitle);

        // в стеклянном режиме плашка снизу — полупрозрачное стекло поверх обоев
        var textPanel = new Border
        {
            Background = Skin.CardFooter,
            BorderBrush = Skin.CardFooterEdge,
            BorderThickness = new Thickness(0, 1, 0, 0),
            CornerRadius = new CornerRadius(0, 0, 9, 9),
            Child = textInner,
        };

        var stack = new StackPanel();
        stack.Children.Add(coverGrid);
        stack.Children.Add(textPanel);

        // тонкая светлая грань поверх карточки — «стеклянный» край
        var glassFrame = new Border
        {
            BorderBrush = Skin.GlassEdge,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(9),
            IsHitTestVisible = false,
        };

        var cardBody = new Grid();
        cardBody.Children.Add(stack);
        cardBody.Children.Add(glassFrame);

        var card = new Border
        {
            Child = cardBody,
            Background = Skin.Card,
            CornerRadius = new CornerRadius(9),
            Margin = new Thickness(0, 0, CardGap, CardGap),
            Width = _cardWidth,
            Cursor = new Cursor(StandardCursorType.Hand),
            ClipToBounds = true,
            BoxShadow = Skin.CardShadow,
            RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative),
        };

        card.Transitions = new Transitions
        {
            new BrushTransition { Property = Border.BackgroundProperty, Duration = TimeSpan.FromMilliseconds(140) },
        };

        void ShowStar(bool show)
        {
            starButton.Opacity = show || game.Favorite ? 1 : 0;
            starButton.IsHitTestVisible = show || game.Favorite;
        }

        card.PointerEntered += (_, _) =>
        {
            card.Background = Skin.CardHover;
            hover.Opacity = 1;
            hover.IsHitTestVisible = true;
            ShowStar(true);
        };
        card.PointerExited += (_, _) =>
        {
            card.Background = Skin.Card;
            hover.Opacity = 0;
            hover.IsHitTestVisible = false;
            ShowStar(false);
        };
        card.PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(card).Properties.IsRightButtonPressed) return;
            e.Handled = true;
            ShowDetail(game);
        };

        var cardMenu = BuildContextMenu(game);
        card.AddHandler(InputElement.PointerPressedEvent, (_, e) =>
        {
            if (!e.GetCurrentPoint(card).Properties.IsRightButtonPressed) return;
            e.Handled = true;
            cardMenu.Show(card, e.GetPosition(card));
        }, RoutingStrategies.Tunnel);

        Ui.FadeIn(card, Math.Min(index, 16) * 14, 260);

        _covers[game.Id] = image;
        _placeholders[game.Id] = placeholder;
        _titles[game.Id] = title;
        _subtitles[game.Id] = subtitle;
        _starButtons[game.Id] = starButton;
        _starIcons[game.Id] = starGlyph;

        return card;
    }

    private static Border BuildIconButton(string text, string iconData, double iconW, double iconH, Action onClick)
    {
        var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 9 };
        content.Children.Add(Ui.Icon(iconData, Brushes.White, iconW, iconH));
        content.Children.Add(Ui.T(text, 13, Brushes.White, FontWeight.SemiBold));

        return Ui.Clickable(content, onClick, Skin.Accent,
            new SolidColorBrush(Color.Parse("#57B0FF")), new CornerRadius(6), new Thickness(18, 9));
    }

    private Control BuildHoverOverlay(GameEntry game)
    {
        var playGlyph = Ui.Icon(Ui.PlayGeometry, Brushes.White, 30, 34);

        var playLabel = Ui.T(Loc.T("btn.play"), 12, Brushes.White, FontWeight.SemiBold);
        playLabel.HorizontalAlignment = HorizontalAlignment.Center;

        var panel = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            Spacing = 6,
        };
        panel.Children.Add(playGlyph);
        panel.Children.Add(playLabel);

        var overlay = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#C00B111A")),
            Opacity = 0,
            IsHitTestVisible = false,
            Cursor = new Cursor(StandardCursorType.Hand),
            Child = panel,
            Transitions = new Transitions
            {
                new DoubleTransition { Property = Visual.OpacityProperty, Duration = TimeSpan.FromMilliseconds(150) },
            },
        };

        overlay.PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(overlay).Properties.IsRightButtonPressed) return;
            e.Handled = true;
            Ui.Bounce(panel, 0.82, 90);
            Launch(game);
        };

        return overlay;
    }

    private Control BuildPlaceholder(GameEntry game)
    {
        var initials = new string(game.Title
            .Split(new[] { ' ', ':', '-' }, StringSplitOptions.RemoveEmptyEntries)
            .Take(2)
            .Select(w => char.ToUpperInvariant(w[0]))
            .ToArray());

        if (string.IsNullOrEmpty(initials)) initials = "?";

        var panel = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            Spacing = 8,
        };

        var letters = Ui.T(initials, 40, Skin.PlaceholderText, FontWeight.Bold);
        letters.HorizontalAlignment = HorizontalAlignment.Center;
        panel.Children.Add(letters);

        var name = Ui.T(game.Title, 11, Skin.PlaceholderTextDim, wrap: TextWrapping.Wrap);
        name.HorizontalAlignment = HorizontalAlignment.Center;
        name.TextAlignment = TextAlignment.Center;
        name.MaxWidth = _cardWidth - 30;
        name.MaxLines = 3;
        panel.Children.Add(name);

        return new Border
        {
            Background = Skin.PlaceholderGradientA,
            Child = panel,
        };
    }

    private string Subtitle(GameEntry game)
    {
        if (!game.ExeExists) return Loc.T("lbl.unknown");
        if (game.LastPlayedUtc.HasValue) return game.LastPlayedUtc.Value.ToLocalTime().ToString("dd.MM.yyyy");
        return Loc.T("lbl.never");
    }

    private Menu BuildContextMenu(GameEntry game)
    {
        var menu = new Menu();

        menu.Item(Loc.T("ctx.play"), () => Launch(game));
        if (Platform.SupportsElevation) menu.Item(Loc.T("ctx.admin"), () => Launch(game, true));
        menu.Separator();
        menu.Item(Loc.T("ctx.folder"), () => Shell.OpenFolder(game.FolderPath));
        menu.Item(Loc.T("ctx.cover"), () => ChangeCover(game));
        menu.Item(Loc.T("ctx.rename"), () => Rename(game));
        menu.Item(Loc.T("ctx.fav"), () => ToggleFavorite(game));
        menu.Separator();
        menu.Item(Loc.T("ctx.remove"), () => RemoveGame(game), true);

        return menu;
    }

    // ---------------------------------------------------------------- detail

    private void ShowDetail(GameEntry game)
    {
        var card = new Border
        {
            Background = Skin.Panel,
            CornerRadius = new CornerRadius(14),
            MaxWidth = 920,
            Margin = new Thickness(30),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            BoxShadow = new BoxShadows(new BoxShadow
            {
                OffsetX = 0,
                OffsetY = 12,
                Blur = 40,
                Color = Color.Parse("#99000000"),
            }),
        };

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("300,*") };

        // cover
        var coverHost = new Grid { Height = 450, ClipToBounds = true };
        var bitmap = Ui.LoadBitmap(game.CoverFile);
        if (bitmap != null)
        {
            coverHost.Children.Add(new Image { Source = bitmap, Stretch = Stretch.UniformToFill });
        }
        else
        {
            coverHost.Children.Add(BuildPlaceholder(game));
        }

        var coverBorder = new Border
        {
            CornerRadius = new CornerRadius(14, 0, 0, 14),
            ClipToBounds = true,
            Child = coverHost,
        };
        grid.Children.Add(coverBorder);

        // right side
        var right = new StackPanel { Margin = new Thickness(26, 22, 26, 22), Spacing = 12 };

        var headerRow = new DockPanel();
        var closeButton = Ui.Clickable(Ui.Icon(Ui.CloseGeometry, Skin.TextMuted, 12, 12, true, 1.8),
            () => _detailLayer.IsVisible = false, Skin.PanelAlt, Skin.CardHover, new CornerRadius(6),
            new Thickness(10, 10));
        DockPanel.SetDock(closeButton, Dock.Right);
        headerRow.Children.Add(closeButton);

        var titleBlock = Ui.T(game.Title, 24, Skin.Text, FontWeight.Bold, TextWrapping.Wrap);
        titleBlock.Margin = new Thickness(0, 0, 12, 0);
        headerRow.Children.Add(titleBlock);
        right.Children.Add(headerRow);

        if (game.SteamAppId.HasValue || !string.IsNullOrWhiteSpace(game.SteamName))
        {
            var source = game.SteamName != null ? $"Steam · {game.SteamName}" : $"Steam app {game.SteamAppId}";
            right.Children.Add(Ui.T(source, 11.5, Skin.TextDim));
        }

        right.Children.Add(BuildMetaTable(game));

        // buttons
        var primaryRow = new WrapPanel { Orientation = Orientation.Horizontal };
        Border? playBtn = null;
        playBtn = BuildIconButton(Loc.T("btn.play"), Ui.PlayGeometry, 13, 15, () =>
        {
            if (playBtn != null) Ui.Bounce(playBtn, 0.88, 100);
            Launch(game);
        });
        playBtn.Margin = new Thickness(0, 4, 10, 8);
        primaryRow.Children.Add(playBtn);

        // «от админа» — только Windows, в Linux/macOS такого понятия нет
        if (Platform.SupportsElevation)
        {
            var adminBtn = Ui.GhostButton(Loc.T("btn.playAdmin"), () => Launch(game, true));
            adminBtn.Margin = new Thickness(0, 4, 10, 8);
            primaryRow.Children.Add(adminBtn);
        }

        var folderBtn = Ui.GhostButton(Loc.T("btn.openFolder"), () => Shell.OpenFolder(game.FolderPath));
        folderBtn.Margin = new Thickness(0, 4, 10, 8);
        primaryRow.Children.Add(folderBtn);
        right.Children.Add(primaryRow);

        var secondaryRow = new WrapPanel { Orientation = Orientation.Horizontal };
        var coverBtn = Ui.GhostButton(Loc.T("btn.changeCover"), () => ChangeCover(game));
        coverBtn.Margin = new Thickness(0, 0, 10, 8);
        secondaryRow.Children.Add(coverBtn);

        var renameBtn = Ui.GhostButton(Loc.T("btn.rename"), () => Rename(game));
        renameBtn.Margin = new Thickness(0, 0, 10, 8);
        secondaryRow.Children.Add(renameBtn);

        var favBtn = Ui.GhostButton((game.Favorite ? "★  " : "☆  ") + Loc.T(game.Favorite ? "btn.unfavorite" : "btn.favorite"),
            () => ToggleFavorite(game));
        favBtn.Margin = new Thickness(0, 0, 10, 8);
        secondaryRow.Children.Add(favBtn);

        var removeBtn = Ui.GhostButton(Loc.T("btn.remove"), () => RemoveGame(game), Skin.Danger);
        removeBtn.Margin = new Thickness(0, 0, 10, 8);
        secondaryRow.Children.Add(removeBtn);

        right.Children.Add(secondaryRow);

        var rightScroll = new ScrollViewer
        {
            Content = right,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            MaxHeight = 450,
        };
        Ui.SmoothScroll(rightScroll, 80);
        Grid.SetColumn(rightScroll, 1);
        grid.Children.Add(rightScroll);

        card.Child = grid;
        card.RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative);
        card.Transitions = new Transitions
        {
            new TransformOperationsTransition
            {
                Property = Visual.RenderTransformProperty,
                Duration = TimeSpan.FromMilliseconds(190),
                Easing = new Avalonia.Animation.Easings.CubicEaseOut(),
            },
        };

        _detailLayer.Child = card;
        _detailLayer.Transitions = new Transitions
        {
            new DoubleTransition
            {
                Property = Visual.OpacityProperty,
                Duration = TimeSpan.FromMilliseconds(200),
                Easing = new Avalonia.Animation.Easings.CubicEaseOut(),
            },
        };

        _detailLayer.Opacity = 0;
        card.RenderTransform = new ScaleTransform(0.965, 0.965);
        _detailLayer.IsVisible = true;

        Dispatcher.UIThread.Post(() =>
        {
            _detailLayer.Opacity = 1;
        });
    }

    private Control BuildMetaTable(GameEntry game)
    {
        var panel = new StackPanel { Spacing = 5, Margin = new Thickness(0, 4, 0, 0) };

        void Row(string label, string value)
        {
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("130,*") };
            var l = Ui.T(label, 11.5, Skin.TextDim);
            l.VerticalAlignment = VerticalAlignment.Top;
            row.Children.Add(l);
            var v = Ui.T(value, 11.5, Skin.TextMuted, wrap: TextWrapping.Wrap);
            Grid.SetColumn(v, 1);
            row.Children.Add(v);
            panel.Children.Add(row);
        }

        Row(Loc.T("lbl.path"), game.FolderPath);
        Row(Loc.T("lbl.exe"), string.IsNullOrEmpty(game.ExePath) ? "—" : Path.GetFileName(game.ExePath));
        Row(Loc.T("lbl.size"), game.SizeBytes > 0 ? Scanner.FormatSize(game.SizeBytes) : Loc.T("lbl.calculating"));
        Row(Loc.T("lbl.added"), game.AddedUtc.ToLocalTime().ToString("dd.MM.yyyy"));
        Row(Loc.T("lbl.lastplayed"), game.LastPlayedUtc.HasValue
            ? game.LastPlayedUtc.Value.ToLocalTime().ToString("dd.MM.yyyy HH:mm")
            : Loc.T("lbl.never"));
        Row(Loc.T("lbl.plays"), game.PlayCount.ToString());

        if (!game.ExeExists)
            panel.Children.Add(Ui.T("⚠  " + Loc.T("lbl.unknown"), 11.5, Skin.Danger));

        return panel;
    }

    // -------------------------------------------------------------- actions

    private void Launch(GameEntry game, bool asAdmin = false)
    {
        if (!game.ExeExists)
        {
            _ = Dialogs.ShowMessageAsync(this, "Ludex", $"{Loc.T("status.launchError")}\n{game.ExePath}");
            return;
        }

        ShowLaunchOverlay(game);

        // небольшая пауза, чтобы окно запуска успело красиво проявиться
        DispatcherTimer.RunOnce(() =>
        {
            if (Shell.LaunchGame(game.ExePath, out var error, asAdmin))
            {
                game.LastPlayedUtc = DateTime.UtcNow;
                game.PlayCount++;
                Storage.SaveGames(_games);
                if (_subtitles.TryGetValue(game.Id, out var sub)) sub.Text = Subtitle(game);
                SetStatus(Loc.F("status.launched", game.Title));

                HideLaunchOverlay(1500, () =>
                {
                    if (_detailLayer.IsVisible) ShowDetail(game);
                });
            }
            else
            {
                HideLaunchOverlay(0);
                SetStatus(Loc.T("status.launchError"));
                DispatcherTimer.RunOnce(() => _ = Dialogs.ShowMessageAsync(this, "Ludex",
                    $"{Loc.T("status.launchError")}\n{error}"), TimeSpan.FromMilliseconds(320));
            }
        }, TimeSpan.FromMilliseconds(520));
    }

    // -------------------------------------------------- окно запуска игры

    private void ShowLaunchOverlay(GameEntry game)
    {
        // крупная вертикальная плашка: сверху обложка, снизу статус и «бегущая» полоска
        var coverHost = new Grid { Height = 356, ClipToBounds = true };

        var bitmap = Ui.LoadBitmap(game.CoverFile);
        if (bitmap != null)
        {
            coverHost.Children.Add(new Image
            {
                Source = bitmap,
                Stretch = Stretch.UniformToFill,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
            });

            // мягкое затемнение нижнего края, чтобы карточка «сходилась» с подложкой
            coverHost.Children.Add(new Border
            {
                Background = new LinearGradientBrush
                {
                    StartPoint = new RelativePoint(0, 0.62, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                    GradientStops =
                    {
                        new GradientStop(Color.Parse("#00000000"), 0),
                        new GradientStop(Color.Parse("#80000000"), 1),
                    },
                },
            });
        }
        else
        {
            coverHost.Children.Add(BuildPlaceholder(game));
        }

        var cover = new Border
        {
            CornerRadius = new CornerRadius(14, 14, 0, 0),
            ClipToBounds = true,
            Child = coverHost,
        };

        var title = Ui.T(game.Title, 17, Skin.Text, FontWeight.SemiBold, TextWrapping.Wrap);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        title.TextAlignment = TextAlignment.Center;

        var status = Ui.T(Loc.T("launch.title"), 12.5, Skin.TextMuted);
        status.HorizontalAlignment = HorizontalAlignment.Center;

        // «двигающаяся полоска»: бегущий блик по жёлобу
        const double trackWidth = 248;
        const double shimmerWidth = 88;

        var shift = new TranslateTransform(0, 0);
        var shimmer = new Border
        {
            Width = shimmerWidth,
            Height = 4,
            CornerRadius = new CornerRadius(2),
            Background = Skin.Accent,
            HorizontalAlignment = HorizontalAlignment.Left,
            RenderTransform = shift,
        };

        var track = new Border
        {
            Width = trackWidth,
            Height = 4,
            CornerRadius = new CornerRadius(2),
            Background = Skin.PanelAlt,
            ClipToBounds = true,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 10, 0, 2),
            Child = shimmer,
        };

        _launchTimer?.Stop();
        var phase = 0.0;
        _launchTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _launchTimer.Tick += (_, _) =>
        {
            phase += 0.019;
            var wave = (Math.Sin(phase) + 1) / 2; // 0..1 туда-обратно
            shift.X = wave * (trackWidth - shimmerWidth - 6);
        };
        _launchTimer.Start();

        var hint = Ui.T(Loc.T("launch.hint"), 10.5, Skin.TextDim);
        hint.HorizontalAlignment = HorizontalAlignment.Center;

        var bottom = new StackPanel
        {
            Margin = new Thickness(24, 16, 24, 20),
            Spacing = 5,
        };
        bottom.Children.Add(title);
        bottom.Children.Add(status);
        bottom.Children.Add(track);
        bottom.Children.Add(hint);

        var stack = new StackPanel();
        stack.Children.Add(cover);
        stack.Children.Add(bottom);

        var card = new Border
        {
            Width = 320,
            Background = Skin.Panel,
            BorderBrush = Skin.GlassEdge,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(14),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative),
            BoxShadow = new BoxShadows(new BoxShadow
            {
                OffsetX = 0,
                OffsetY = 16,
                Blur = 46,
                Color = Color.Parse("#AA000000"),
            }),
            Transitions = new Transitions
            {
                new TransformOperationsTransition
                {
                    Property = Visual.RenderTransformProperty,
                    Duration = TimeSpan.FromMilliseconds(230),
                    Easing = new Avalonia.Animation.Easings.CubicEaseOut(),
                },
            },
            Child = stack,
        };

        _launchLayer.Child = card;
        _launchLayer.Opacity = 0;
        card.RenderTransform = new ScaleTransform(0.93, 0.93);
        _launchLayer.IsVisible = true;

        Dispatcher.UIThread.Post(() =>
        {
            _launchLayer.Opacity = 1;
        });
    }

    private void HideLaunchOverlay(int delayMs = 0, Action? after = null)
    {
        _launchTimer?.Stop();
        _launchTimer = null;

        DispatcherTimer.RunOnce(() =>
        {
            _launchLayer.Opacity = 0;
            DispatcherTimer.RunOnce(() =>
            {
                _launchLayer.IsVisible = false;
                _launchLayer.Child = null;
                after?.Invoke();
            }, TimeSpan.FromMilliseconds(230));
        }, TimeSpan.FromMilliseconds(delayMs));
    }

    private void ToggleFavorite(GameEntry game)
    {
        game.Favorite = !game.Favorite;
        Storage.SaveGames(_games);

        // пересобирать всю сетку нужно только если меняется порядок карточек
        var resort = (_favoritesOnly && !game.Favorite) ||
                     string.Equals(_settings.SortMode, "favorite", StringComparison.Ordinal);

        if (resort) RenderGrid();
        else UpdateFavoriteVisuals(game);

        if (_detailLayer.IsVisible) ShowDetail(game);
    }

    private void UpdateFavoriteVisuals(GameEntry game)
    {
        if (_starIcons.TryGetValue(game.Id, out var icon))
            icon.Fill = game.Favorite ? Skin.Accent : Brushes.White;

        if (_starButtons.TryGetValue(game.Id, out var star))
            star.Opacity = 1;

    }

    private async void Rename(GameEntry game)
    {
        var name = await Dialogs.PromptAsync(this, Loc.T("dlg.renameTitle"), game.Title);
        if (string.IsNullOrWhiteSpace(name)) return;

        game.Title = name;
        game.Locked = true;
        Storage.SaveGames(_games);

        if (_titles.TryGetValue(game.Id, out var t)) t.Text = game.Title;
        if (_subtitles.TryGetValue(game.Id, out var s)) s.Text = Subtitle(game);
        if (_detailLayer.IsVisible) ShowDetail(game);
    }

    private async void ChangeCover(GameEntry game)
    {
        var window = new CoverWindow(game.Title, _settings.SteamGridDbKey, game.Id);
        await window.ShowDialog(this);

        if (window.ChosenCoverPath != null)
        {
            game.CoverFile = window.ChosenCoverPath;
            game.CoverIsPortrait = window.ChosenIsPortrait;
            game.Locked = true;
            Storage.SaveGames(_games);
            UpdateCover(game);
            if (_detailLayer.IsVisible) ShowDetail(game);
        }
        else if (window.ResetRequested)
        {
            game.Locked = false;
            game.CoverFile = null;
            Storage.SaveGames(_games);
            UpdateCover(game);
            _ = FetchArtworkForAsync(new List<GameEntry> { game });
        }
    }

    private void UpdateCover(GameEntry game)
    {
        var bitmap = Ui.LoadBitmap(game.CoverFile);

        if (_covers.TryGetValue(game.Id, out var image))
        {
            image.Source = bitmap;
            if (bitmap != null) Ui.FadeIn(image, 0, 320);
        }

        if (_placeholders.TryGetValue(game.Id, out var placeholder))
            placeholder.IsVisible = bitmap == null;
    }

    private async void RemoveGame(GameEntry game)
    {
        var ok = await Dialogs.ConfirmAsync(this, Loc.T("dlg.removeTitle"),
            Loc.F("dlg.removeText", game.Title), Loc.T("btn.remove"), Loc.T("btn.cancel"));
        if (!ok) return;

        try
        {
            foreach (var file in Directory.EnumerateFiles(AppPaths.CoversDir, game.Id + "_*"))
                File.Delete(file);
        }
        catch
        {
            // ignored
        }

        _games.Remove(game);
        Storage.SaveGames(_games);
        _detailLayer.IsVisible = false;
        RenderGrid();
        SetStatus(Loc.T("status.removed"));
    }

    private void ShowSettings()
    {
        _settingsPanel = new SettingsPanel(_settings);
        _settingsPanel.Saved += OnSettingsSaved;
        _settingsPanel.Closed += CloseSettings;
        _settingsPanel.AppearanceChanged += OnAppearanceChanged;
        _settingsPanel.WallpaperChanged += OnWallpaperChanged;

        _settingsPanel.HorizontalAlignment = HorizontalAlignment.Center;
        _settingsPanel.VerticalAlignment = VerticalAlignment.Center;
        _settingsPanel.Margin = new Thickness(24);

        _settingsLayer.Child = _settingsPanel;
        _settingsLayer.Opacity = 0;
        _settingsLayer.IsVisible = true;

        _settingsPanel.RenderTransform = new ScaleTransform(0.965, 0.965);
        Dispatcher.UIThread.Post(() =>
        {
            _settingsLayer.Opacity = 1;
            _settingsPanel.RenderTransform = new ScaleTransform(1, 1);
        });
    }

    private void CloseSettings()
    {
        if (!_settingsLayer.IsVisible) return;

        _settingsLayer.Opacity = 0;
        _settingsPanel.RenderTransform = new ScaleTransform(0.98, 0.98);
        DispatcherTimer.RunOnce(() =>
        {
            _settingsLayer.IsVisible = false;
            _settingsLayer.Child = null;
            _settingsPanel = null;

            // кастомизация применяется сразу: перекрашиваем окно после закрытия панели
            if (_appearanceDirty)
            {
                _appearanceDirty = false;
                BuildUi();
                RenderGrid();
            }
        }, TimeSpan.FromMilliseconds(170));
    }

    private void OnSettingsSaved()
    {
        var scan = _settingsPanel?.ScanRequested == true;

        Loc.Lang = _settings.Language;
        Skin.Apply(_settings.Theme, _settings.Accent, _settings.Transparent);
        ApplyTransparency();
        Background = Skin.Bg;
        Storage.SaveSettings(_settings);

        _settingsLayer.IsVisible = false;
        _settingsLayer.Child = null;
        _settingsPanel = null;
        _appearanceDirty = false;

        BuildUi();
        RenderGrid();

        if (scan) _ = StartScanAsync();
    }

    // ------------------------------------------------------- оформление окна

    /// <summary>Сквозное просвечивание рабочего стола (сохраняем «стеклянный» вид).</summary>
    private void ApplyTransparency()
    {
        try
        {
            TransparencyLevelHint = _settings.Transparent
                ? new[] { WindowTransparencyLevel.AcrylicBlur, WindowTransparencyLevel.Blur }
                : new[] { WindowTransparencyLevel.None };
        }
        catch (Exception ex)
        {
            AppPaths.Log("Transparency hint failed: " + ex.Message);
        }
    }

    private void OnAppearanceChanged()
    {
        Skin.Apply(_settings.Theme, _settings.Accent, _settings.Transparent);
        ApplyTransparency();
        Background = Skin.Bg;

        // подложки модальных слоёв тоже должны сменить тему
        if (_settingsLayer != null) _settingsLayer.Background = Skin.Overlay;
        if (_detailLayer != null) _detailLayer.Background = Skin.Overlay;
        if (_launchLayer != null) _launchLayer.Background = Skin.Overlay;

        _appearanceDirty = true; // после закрытия настроек перестроим окно в новых цветах
        RefreshWallpaper();
    }

    private void OnWallpaperChanged()
    {
        Storage.SaveSettings(_settings);
        RefreshWallpaper();
    }

    /// <summary>Перестроить слой обоев (картинка/GIF + мягкое затемнение поверх).</summary>
    private void RefreshWallpaper()
    {
        if (_wallpaperLayer == null) return;

        _wallpaperLayer.Children.Clear();

        var previousStream = _wallpaperStream;
        _wallpaperStream = null;
        if (previousStream != null)
        {
            try { previousStream.Dispose(); } catch { /* ignored */ }
        }

        var visual = BuildWallpaperVisual();
        if (visual == null) return;

        _wallpaperLayer.Children.Add(visual);

        // затемнение: сверху слабее, книзу сильнее — подписи остаются читаемыми
        var baseColor = Skin.IsLight ? Color.Parse("#EDF1F6") : Skin.BgColor;
        _wallpaperLayer.Children.Add(new Border
        {
            Background = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(Color.FromArgb(Skin.Transparent ? (byte)0x6E : (byte)0xB8,
                        baseColor.R, baseColor.G, baseColor.B), 0),
                    new GradientStop(Color.FromArgb(Skin.Transparent ? (byte)0xB4 : (byte)0xE8,
                        baseColor.R, baseColor.G, baseColor.B), 1),
                },
            },
        });
    }

    private Control? BuildWallpaperVisual()
    {
        var path = _settings.BackgroundPath;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;

        if (path.EndsWith(".gif", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                // GifImage принимает поток/Uri: держим поток открытым, пока GIF на экране
                _wallpaperStream = File.OpenRead(path);
                return new Avalonia.Labs.Gif.GifImage
                {
                    Source = _wallpaperStream,
                    Stretch = Stretch.UniformToFill,
                    IterationCount = IterationCount.Infinite,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch,
                };
            }
            catch (Exception ex)
            {
                AppPaths.Log("Wallpaper gif failed: " + ex.Message);
                return null;
            }
        }

        var bitmap = Ui.LoadBitmap(path);
        if (bitmap == null) return null;

        return new Image
        {
            Source = bitmap,
            Stretch = Stretch.UniformToFill,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
    }

    // -------------------------------------------------------------- scanning

    private async void OnOpened(object? sender, EventArgs e)
    {
        _ = ComputeSizesAsync();

        if (_settings.Folders.Count == 0 && _games.Count == 0)
        {
            SetStatus(Loc.T("status.noFolders"));
            return;
        }

        if (_settings.AutoScanOnStartup || _games.Count == 0)
            await StartScanAsync();
    }

    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        _workCts?.Cancel();
        _settings.Language = Loc.Lang;
        _settings.WindowWidth = Width;
        _settings.WindowHeight = Height;
        _settings.WindowMaximized = WindowState == WindowState.Maximized;
        Storage.SaveSettings(_settings);
        Storage.SaveGames(_games);
    }

    private void ApplyFavChipState()
    {
        if (_favChip != null)
            Ui.SetColors(_favChip,
                _favoritesOnly ? Skin.Accent : Skin.PanelAlt,
                _favoritesOnly ? Skin.AccentHover : Skin.CardHover);

        if (_favChipLabel != null)
            _favChipLabel.Foreground = _favoritesOnly ? Brushes.White : Skin.TextMuted;
    }

    /// <summary>Строка состояния меняется с мягким проявлением, а не «прыжком» текста.</summary>
    private void SetStatus(string text)
    {
        if (_statusLabel == null) return;
        if (_statusLabel.Text == text) return;

        _statusLabel.Transitions = new Transitions
        {
            new DoubleTransition
            {
                Property = Visual.OpacityProperty,
                Duration = TimeSpan.FromMilliseconds(110),
            },
        };

        _statusLabel.Opacity = 0.25;
        DispatcherTimer.RunOnce(() =>
        {
            _statusLabel.Text = text;
            _statusLabel.Opacity = 1;
        }, TimeSpan.FromMilliseconds(90));
    }

    private void UpdateCount()
    {
        if (_countLabel == null) return;
        var visible = _grid?.Children.Count ?? 0;
        _countLabel.Text = Loc.F("status.games", _games.Count);
    }

    private async Task StartScanAsync()
    {
        if (_scanning) return;

        if (_settings.Folders.Count == 0)
        {
            SetStatus(Loc.T("status.noFolders"));
            ShowSettings();
            return;
        }

        _scanning = true;
        _scanButton.Opacity = 0.55;
        _scanButton.IsEnabled = false;

        _workCts?.Cancel();
        _workCts = new CancellationTokenSource();
        var ct = _workCts.Token;

        _progress.SetActive(true);
        SetStatus(Loc.T("status.scanFolders"));

        try
        {
            var folders = _settings.Folders.ToList();
            var extra = _settings.ExtraIgnoreExe.ToList();

            var hits = await Task.Run(() => Scanner.Scan(
                folders,
                message => Dispatcher.UIThread.Post(() => SetStatus(message)),
                extra,
                null,
                () => ct.IsCancellationRequested), ct);

            if (ct.IsCancellationRequested) return;

            var added = MergeHits(hits);
            Storage.SaveGames(_games);
            RenderGrid();
            SetStatus(Loc.F("status.found", hits.Count) + (added > 0 ? $"  (+{added})" : ""));

            await FetchArtworkForAsync(_games.Where(g => string.IsNullOrEmpty(g.CoverFile) || !File.Exists(g.CoverFile)).ToList());
            Storage.SaveGames(_games);

            _ = ComputeSizesAsync();
        }
        catch (OperationCanceledException)
        {
            SetStatus(Loc.T("status.ready"));
        }
        catch (Exception ex)
        {
            AppPaths.Log("Scan failed: " + ex);
            SetStatus(Loc.T("status.launchError"));
        }
        finally
        {
            _scanning = false;
            _progress.SetActive(false);
            _scanButton.Opacity = 1;
            _scanButton.IsEnabled = true;
        }
    }

    private int MergeHits(List<ScanHit> hits)
    {
        int added = 0;
        var existing = new Dictionary<string, GameEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var g in _games)
        {
            if (!existing.ContainsKey(g.FolderPath)) existing[g.FolderPath] = g;
        }

        foreach (var hit in hits)
        {
            if (existing.TryGetValue(hit.FolderPath, out var game))
            {
                game.ExePath = hit.ExePath;
                if (!game.Locked) game.Title = hit.Title;
            }
            else
            {
                var entry = new GameEntry
                {
                    Title = hit.Title,
                    FolderPath = hit.FolderPath,
                    ExePath = hit.ExePath,
                    AddedUtc = DateTime.UtcNow,
                };
                _games.Add(entry);
                existing[hit.FolderPath] = entry;
                added++;
            }
        }

        // forget games whose folder is gone
        _games.RemoveAll(g => !Directory.Exists(g.FolderPath));

        return added;
    }

    private async Task FetchArtworkForAsync(List<GameEntry> games)
    {
        if (games.Count == 0) return;

        var ct = _workCts?.Token ?? CancellationToken.None;
        var semaphore = new SemaphoreSlim(3);
        int done = 0;
        int total = games.Count;
        string? key = _settings.SteamGridDbKey;

        _progress.SetActive(true);

        var tasks = games.Select(async game =>
        {
            await semaphore.WaitAsync();
            try
            {
                if (ct.IsCancellationRequested) return;

                var (path, portrait, appId, matchedName) =
                    await Artwork.AutoArtworkAsync(game.Title, key, game.Id, ct);

                if (matchedName != null)
                {
                    game.SteamName = matchedName;
                    if (appId.HasValue) game.SteamAppId = appId;
                }

                if (path != null)
                {
                    game.CoverFile = path;
                    game.CoverIsPortrait = portrait;

                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        UpdateCover(game);
                        if (_detailLayer.IsVisible) ShowDetail(game);
                    });
                }

                Storage.SaveGames(_games);
            }
            catch (Exception ex)
            {
                AppPaths.Log("Artwork failed for " + game.Title + ": " + ex.Message);
            }
            finally
            {
                semaphore.Release();
                var current = Interlocked.Increment(ref done);
                await Dispatcher.UIThread.InvokeAsync(() => SetStatus(Loc.F("status.art", current, total)));
            }
        });

        try
        {
            await Task.WhenAll(tasks);
        }
        catch
        {
            // ignored
        }

        _progress.SetActive(false);
        SetStatus(Loc.T("status.ready"));
    }

    private async Task ComputeSizesAsync()
    {
        var pending = _games.Where(g => g.SizeBytes <= 0).ToList();
        if (pending.Count == 0) return;

        var semaphore = new SemaphoreSlim(2);
        var tasks = pending.Select(async game =>
        {
            await semaphore.WaitAsync();
            try
            {
                var size = await Task.Run(() => Scanner.DirectorySize(game.FolderPath));
                if (size > 0) game.SizeBytes = size;
            }
            catch
            {
                // ignored
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);
        Storage.SaveGames(_games);
    }
}
