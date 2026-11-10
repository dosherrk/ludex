using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Ludex.Services;

namespace Ludex.UI;

/// <summary>Настройки как встроенная панель внутри главного окна (без отдельного окна Windows).</summary>
public sealed class SettingsPanel : Border
{
    private readonly AppSettings _original;
    private readonly List<string> _folders;

    private StackPanel _folderRows = null!;
    private int _selectedFolder = -1;
    private TextBox _keyBox = null!;
    private TextBox _ignoreBox = null!;
    private Dropdown _langCombo = null!;
    private Dropdown _engineCombo = null!;
    private Toggle _autoScan = null!;
    private Toggle _autoStart = null!;
    private Toggle _autoUpdate = null!;
    private TextBlock _folderCount = null!;

    private string _language;
    private string _searchEngine;
    private string _theme;
    private string _keyText;
    private string _ignoreText;
    private bool _scanOnStartup;
    private bool _runAtStartup;
    private bool _checkUpdates;

    public bool ScanRequested { get; private set; }

    public event Action? Saved;
    public event Action? Closed;

    /// <summary>Тема/палитра/прозрачность поменялись — окно нужно перекрасить сразу.</summary>
    public event Action? AppearanceChanged;

    /// <summary>Обои установлены или убраны.</summary>
    public event Action? WallpaperChanged;

    /// <summary>Пользователь попросил проверить обновления прямо сейчас.</summary>
    public event Action? UpdateCheckRequested;

    /// <summary>Вернуть игры, скрытые «навсегда» после удаления из библиотеки.</summary>
    public event Action? HiddenRestored;

    public SettingsPanel(AppSettings settings)
    {
        _original = settings;
        _folders = new List<string>(settings.Folders);
        _language = settings.Language;
        _searchEngine = settings.SearchEngine;
        _theme = settings.Theme;
        _keyText = settings.SteamGridDbKey ?? "";
        _ignoreText = string.Join(", ", settings.ExtraIgnoreExe);
        _scanOnStartup = settings.AutoScanOnStartup;
        _runAtStartup = Autostart.IsEnabled();
        _checkUpdates = settings.CheckUpdatesOnStartup;

        MaxWidth = 860;
        MaxHeight = 720;
        Background = Skin.Panel;
        CornerRadius = new CornerRadius(14);
        RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative);
        Transitions = new Avalonia.Animation.Transitions
        {
            new Avalonia.Animation.TransformOperationsTransition
            {
                Property = Visual.RenderTransformProperty,
                Duration = TimeSpan.FromMilliseconds(190),
                Easing = new Avalonia.Animation.Easings.CubicEaseOut(),
            },
        };
        BoxShadow = new BoxShadows(new BoxShadow
        {
            OffsetX = 0,
            OffsetY = 14,
            Blur = 44,
            Color = Color.Parse("#AA000000"),
        });

        BuildContent();
    }

    private bool settings_hasHidden() => _original.HiddenGames.Count > 0;

    private void BuildContent()
    {
        // header
        var header = new DockPanel { Margin = new Thickness(26, 22, 22, 0) };
        var close = Ui.Clickable(Ui.Icon(Ui.CloseGeometry, Skin.TextMuted, 12, 12, true, 1.8),
            () => Closed?.Invoke(), Skin.PanelAlt, Skin.CardHover, new CornerRadius(6), new Thickness(10, 10));
        close.VerticalAlignment = VerticalAlignment.Top;
        DockPanel.SetDock(close, Dock.Right);
        header.Children.Add(close);
        header.Children.Add(Ui.T(Loc.T("settings.title"), 21, Skin.Text, FontWeight.Bold));

        // тело
        var body = new StackPanel { Margin = new Thickness(26, 18, 26, 8), Spacing = 16 };

        // ---- папки
        var foldersHeader = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        foldersHeader.Children.Add(Ui.T(Loc.T("settings.folders"), 14, Skin.Text, FontWeight.SemiBold));
        var addBtn = Ui.GhostButton("+  " + Loc.T("settings.addFolder"), () => _ = AddFoldersAsync());
        addBtn.HorizontalAlignment = HorizontalAlignment.Right;
        Grid.SetColumn(addBtn, 1);
        foldersHeader.Children.Add(addBtn);
        body.Children.Add(foldersHeader);

        body.Children.Add(Ui.T(Loc.T("settings.foldersHint"), 11.5, Skin.TextDim, wrap: TextWrapping.Wrap));

        _folderRows = new StackPanel { Spacing = 2, Margin = new Thickness(5) };
        var folderScroll = new ScrollViewer
        {
            Height = 132,
            Content = _folderRows,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            Background = Brushes.Transparent,
        };
        Ui.SmoothScroll(folderScroll, 70);
        var folderFrame = new Border
        {
            Height = 132,
            Background = Skin.PanelAlt,
            BorderBrush = Skin.Border,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Child = folderScroll,
            ClipToBounds = true,
        };
        body.Children.Add(folderFrame);
        RefreshFolderRows();

        var listButtons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        listButtons.Children.Add(Ui.GhostButton(Loc.T("btn.removeSel"), RemoveSelected));
        _folderCount = Ui.T(_folders.Count == 0 ? Loc.T("settings.noFolders") : _folders.Count.ToString(),
            11.5, Skin.TextDim);
        _folderCount.Margin = new Thickness(6, 0, 0, 0);
        listButtons.Children.Add(_folderCount);

        if (settings_hasHidden())
        {
            var restore = Ui.GhostButton(
                Loc.F("settings.restoreHidden", _original.HiddenGames.Count),
                () => HiddenRestored?.Invoke());
            restore.Margin = new Thickness(12, 0, 0, 0);
            listButtons.Children.Add(restore);
        }

        body.Children.Add(listButtons);

        if (settings_hasHidden())
            body.Children.Add(Ui.T(Loc.T("settings.restoreHiddenHint"), 11, Skin.TextDim, wrap: TextWrapping.Wrap));

        body.Children.Add(Ui.Divider());

        // ---- вход в «Кастомизацию» + язык (слева)
        var lookRow = new WrapPanel { Orientation = Orientation.Horizontal };

        var customContent = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 9 };
        customContent.Children.Add(Ui.Icon(Ui.PaletteGeometry, Skin.Accent, 17, 17));
        customContent.Children.Add(Ui.T(Loc.T("settings.customize"), 13, Skin.Text, FontWeight.Medium));

        var customButton = Ui.Clickable(customContent, ShowCustomize, Skin.PanelAlt, Skin.CardHover,
            new CornerRadius(6), new Thickness(14, 9));
        customButton.Margin = new Thickness(0, 0, 26, 8);
        lookRow.Children.Add(customButton);

        var langLabel = Ui.T(Loc.T("settings.language"), 13, Skin.TextMuted);
        langLabel.Margin = new Thickness(0, 0, 10, 8);
        lookRow.Children.Add(langLabel);

        _langCombo = new Dropdown(new[] { "Русский", "English" }, _language == "en" ? 1 : 0, 150, 34, 13);
        _langCombo.Margin = new Thickness(0, 0, 0, 8);
        _langCombo.SelectionChanged += index => _language = index == 1 ? "en" : "ru";
        lookRow.Children.Add(_langCombo);

        body.Children.Add(lookRow);
        body.Children.Add(Ui.T(Loc.T("settings.customizeHint"), 11, Skin.TextDim, wrap: TextWrapping.Wrap));

        // ---- поиск в браузере
        var searchRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        var searchLabel = Ui.T(Loc.T("settings.webSearch"), 13, Skin.TextMuted);
        searchLabel.VerticalAlignment = VerticalAlignment.Center;
        searchRow.Children.Add(searchLabel);

        _engineCombo = new Dropdown(new[]
        {
            Loc.T("settings.engineGoogle"), Loc.T("settings.engineYandex"), Loc.T("settings.engineDdg"),
        }, _searchEngine switch { "yandex" => 1, "ddg" => 2, _ => 0 }, 170, 34, 13);
        _engineCombo.SelectionChanged += index => _searchEngine = index switch { 1 => "yandex", 2 => "ddg", _ => "google" };
        searchRow.Children.Add(_engineCombo);

        body.Children.Add(searchRow);
        body.Children.Add(Ui.T(Loc.T("settings.webSearchHint"), 11, Skin.TextDim, wrap: TextWrapping.Wrap));

        // ---- ключ SteamGridDB
        body.Children.Add(Ui.T(Loc.T("settings.sgdb"), 13, Skin.Text, FontWeight.SemiBold));
        _keyBox = new TextBox
        {
            Text = _keyText,
            FontSize = 12.5,
            Height = 34,
            Background = Skin.PanelAlt,
            Foreground = Skin.Text,
            BorderBrush = Skin.Border,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10, 0, 10, 0),
            VerticalContentAlignment = VerticalAlignment.Center,
            Watermark = "0123456789abcdef0123456789abcdef",
        };
        Ui.AnimateTextBox(_keyBox);
        body.Children.Add(_keyBox);
        body.Children.Add(Ui.T(Loc.T("settings.sgdbHint"), 11, Skin.TextDim, wrap: TextWrapping.Wrap));

        // ---- список игнорируемых exe
        body.Children.Add(Ui.T(Loc.T("settings.ignore"), 13, Skin.Text, FontWeight.SemiBold));
        _ignoreBox = new TextBox
        {
            Text = _ignoreText,
            FontSize = 12.5,
            Height = 34,
            Background = Skin.PanelAlt,
            Foreground = Skin.Text,
            BorderBrush = Skin.Border,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10, 0, 10, 0),
            VerticalContentAlignment = VerticalAlignment.Center,
            Watermark = "launcher, start, play",
        };
        Ui.AnimateTextBox(_ignoreBox);
        body.Children.Add(_ignoreBox);
        body.Children.Add(Ui.T(Loc.T("settings.ignoreHint"), 11, Skin.TextDim, wrap: TextWrapping.Wrap));

        body.Children.Add(Ui.Divider());

        // ---- поведение
        _autoScan = new Toggle(Loc.T("settings.autoscan"), _scanOnStartup,
            v => _scanOnStartup = v);
        body.Children.Add(_autoScan);

        _autoStart = new Toggle(Loc.T("settings.autostart"), _runAtStartup,
            v => _runAtStartup = v, Autostart.IsSupported);
        body.Children.Add(_autoStart);

        body.Children.Add(Ui.T(Autostart.IsSupported
                ? Loc.T("settings.autostartHint")
                : Loc.T("settings.autostartUnsupported"),
            11, Skin.TextDim, wrap: TextWrapping.Wrap));

        // ---- обновления
        _autoUpdate = new Toggle(Loc.T("settings.autoupdate"), _checkUpdates,
            v => _checkUpdates = v);
        body.Children.Add(_autoUpdate);

        var checkNow = Ui.GhostButton(Loc.T("settings.checkNow"), () => UpdateCheckRequested?.Invoke());
        checkNow.HorizontalAlignment = HorizontalAlignment.Left;
        body.Children.Add(checkNow);
        body.Children.Add(Ui.T(Loc.T("settings.autoupdateHint"), 11.5, Skin.TextDim, wrap: TextWrapping.Wrap));

        var dataBtn = Ui.GhostButton(Loc.T("settings.openData"), () => Shell.OpenFolder(AppPaths.Root));
        dataBtn.HorizontalAlignment = HorizontalAlignment.Left;
        body.Children.Add(dataBtn);

        // ---- кнопки
        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(26, 6, 26, 22),
        };
        buttons.Children.Add(Ui.GhostButton(Loc.T("btn.cancel"), () => Closed?.Invoke()));
        buttons.Children.Add(Ui.GhostButton(Loc.T("btn.scan"), () =>
        {
            ScanRequested = true;
            Save();
        }));
        buttons.Children.Add(Ui.PrimaryButton(Loc.T("btn.save"), Save));

        var dock = new DockPanel();
        DockPanel.SetDock(header, Dock.Top);
        dock.Children.Add(header);
        DockPanel.SetDock(buttons, Dock.Bottom);
        dock.Children.Add(buttons);

        var scroll = new ScrollViewer
        {
            Content = body,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Margin = new Thickness(0, 10, 0, 0),
        };
        Ui.SmoothScroll(scroll);
        dock.Children.Add(scroll);

        Child = dock;
    }

    private void Rebuild()
    {
        _keyText = _keyBox?.Text ?? _keyText;
        _ignoreText = _ignoreBox?.Text ?? _ignoreText;
        _theme = _original.Theme;
        Background = Skin.Panel;

        var previous = Child;
        BuildContent();

        // возвращаемся с «Кастомизации» — страница настроек проявляется плавно
        if (previous is CustomizePanel && Child is Visual fresh)
            Ui.FadeSlideIn(fresh, 0, 190, 6);
    }

    /// <summary>Подменить страницу с мягкой анимацией появления.</summary>
    private void SwapChild(Control next)
    {
        Child = next;
        Ui.FadeSlideIn(next, 0, 200, 8);
    }

    /// <summary>Показать «Кастомизацию» вместо страницы настроек.</summary>
    private void ShowCustomize()
    {
        var panel = new CustomizePanel(_original);
        panel.AppearanceChanged += () => AppearanceChanged?.Invoke();
        panel.WallpaperChanged += () => WallpaperChanged?.Invoke();
        panel.Closed += () =>
        {
            Rebuild();
            AppearanceChanged?.Invoke();
        };

        SwapChild(panel);
    }

    private void RefreshFolders()
    {
        RefreshFolderRows();
        _folderCount.Text = _folders.Count == 0 ? Loc.T("settings.noFolders") : _folders.Count.ToString();
    }

    /// <summary>Свой список папок: строки с плавной подсветкой и выбранным состоянием.</summary>
    private void RefreshFolderRows()
    {
        if (_folderRows == null) return;

        _folderRows.Children.Clear();

        if (_folders.Count == 0)
        {
            var empty = Ui.T(Loc.T("settings.noFolders"), 12, Skin.TextDim);
            empty.Margin = new Thickness(8, 8, 8, 8);
            _folderRows.Children.Add(empty);
            return;
        }

        for (var i = 0; i < _folders.Count; i++)
        {
            var index = i;
            var text = Ui.T(_folders[i], 12.5, Skin.Text, FontWeight.Normal);
            text.Margin = new Thickness(9, 7, 9, 7);

            var row = new Border
            {
                Child = text,
                Background = index == _selectedFolder ? Skin.AccentSoft : Brushes.Transparent,
                CornerRadius = new CornerRadius(6),
                Cursor = new Cursor(StandardCursorType.Hand),
                Transitions = new Avalonia.Animation.Transitions
                {
                    new Avalonia.Animation.BrushTransition
                    {
                        Property = Border.BackgroundProperty,
                        Duration = TimeSpan.FromMilliseconds(140),
                    },
                },
            };

            row.PointerEntered += (_, _) =>
            {
                if (_selectedFolder != index) row.Background = Skin.CardHover;
            };
            row.PointerExited += (_, _) =>
            {
                row.Background = _selectedFolder == index ? Skin.AccentSoft : Brushes.Transparent;
            };
            row.PointerReleased += (_, e) =>
            {
                if (e.InitialPressMouseButton != MouseButton.Left) return;
                e.Handled = true;
                _selectedFolder = index;
                RefreshFolderRows();
            };

            _folderRows.Children.Add(row);
        }
    }

    private async Task AddFoldersAsync()
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var picked = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = Loc.T("settings.addFolder"),
            AllowMultiple = true,
        });

        foreach (var folder in picked)
        {
            var path = folder.TryGetLocalPath();
            if (string.IsNullOrEmpty(path)) continue;
            path = path.TrimEnd('\\', '/');
            if (_folders.Any(f => string.Equals(f, path, StringComparison.OrdinalIgnoreCase))) continue;
            _folders.Add(path);
        }

        RefreshFolders();
    }

    private void RemoveSelected()
    {
        if (_selectedFolder < 0 || _selectedFolder >= _folders.Count) return;

        _folders.RemoveAt(_selectedFolder);
        if (_selectedFolder >= _folders.Count) _selectedFolder = _folders.Count - 1;
        RefreshFolders();
    }

    private void Save()
    {
        _original.Folders = new List<string>(_folders);
        _original.Language = _language;
        _original.SearchEngine = _searchEngine;
        _original.Theme = _theme;
        _original.SteamGridDbKey = string.IsNullOrWhiteSpace(_keyBox.Text) ? null : _keyBox.Text.Trim();
        _original.AutoScanOnStartup = _autoScan.Value;
        _original.CheckUpdatesOnStartup = _autoUpdate.Value;
        _original.ExtraIgnoreExe = (_ignoreBox.Text ?? "")
            .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim())
            .Where(x => x.Length > 0)
            .ToList();

        if (Autostart.IsSupported && Autostart.IsEnabled() != _runAtStartup)
        {
            if (!Autostart.Set(_runAtStartup)) AppPaths.Log("Autostart toggle failed");
        }

        Saved?.Invoke();
    }
}
