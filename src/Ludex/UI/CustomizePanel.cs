using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Ludex.Services;

namespace Ludex.UI;

/// <summary>«Кастомизация»: тема оформления, цветовая палитра, прозрачность и обои на фон.</summary>
public sealed class CustomizePanel : Border
{
    private readonly AppSettings _settings;

    private TextBox _urlBox = null!;
    private Border _applyBtn = null!;
    private TextBlock _status = null!;

    private string _url = "";
    private string? _lastError;

    /// <summary>Пользователь нажал «Назад» — вернуться на страницу настроек.</summary>
    public event Action? Closed;

    /// <summary>Тема/палитра/прозрачность изменились — нужно перекрасить окно.</summary>
    public event Action? AppearanceChanged;

    /// <summary>Обои установлены или удалены — нужно перестроить фон.</summary>
    public event Action? WallpaperChanged;

    public CustomizePanel(AppSettings settings)
    {
        _settings = settings;

        MaxWidth = 640;
        MaxHeight = 730;
        Background = Skin.Panel;
        BorderBrush = Skin.GlassEdge;
        BorderThickness = new Thickness(1);
        CornerRadius = new CornerRadius(14);
        RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative);
        Transitions = new Transitions
        {
            new Avalonia.Animation.TransformOperationsTransition
            {
                Property = Visual.RenderTransformProperty,
                Duration = TimeSpan.FromMilliseconds(200),
                Easing = new Avalonia.Animation.Easings.CubicEaseOut(),
            },
        };
        BoxShadow = new BoxShadows(new BoxShadow
        {
            OffsetX = 0,
            OffsetY = 12,
            Blur = 36,
            Color = Color.Parse("#99000000"),
        });

        Build();
    }

    // ------------------------------------------------------------------ UI

    private void Build()
    {
        _url = _urlBox?.Text ?? _url;

        // панель могла быть создана в другой теме — перекрашиваем подложку
        Background = Skin.Panel;

        var header = new DockPanel { Margin = new Thickness(26, 20, 20, 0) };

        var close = Ui.Clickable(Ui.Icon(Ui.CloseGeometry, Skin.TextMuted, 12, 12, true, 1.8),
            () => Closed?.Invoke(), Skin.PanelAlt, Skin.CardHover, new CornerRadius(6), new Thickness(10, 10));
        close.VerticalAlignment = VerticalAlignment.Top;
        DockPanel.SetDock(close, Dock.Right);
        header.Children.Add(close);

        var titles = new StackPanel { Spacing = 3 };
        titles.Children.Add(Ui.T(Loc.T("custom.title"), 20, Skin.Text, FontWeight.Bold));
        titles.Children.Add(Ui.T(Loc.T("settings.customizeHint"), 11.5, Skin.TextDim, wrap: TextWrapping.Wrap));
        header.Children.Add(titles);

        var body = new StackPanel { Margin = new Thickness(26, 14, 26, 4), Spacing = 12 };

        // ---- тема
        body.Children.Add(Ui.T(Loc.T("custom.theme"), 13.5, Skin.Text, FontWeight.SemiBold));
        var themeRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        themeRow.Children.Add(Chip(Loc.T("theme.dark"), _settings.Theme != "light", () => SetTheme("dark")));
        themeRow.Children.Add(Chip(Loc.T("theme.light"), _settings.Theme == "light", () => SetTheme("light")));
        body.Children.Add(themeRow);

        // ---- палитра: пользователь сам выбирает цвет на круге
        body.Children.Add(Ui.T(Loc.T("custom.palette"), 13.5, Skin.Text, FontWeight.SemiBold));
        body.Children.Add(Ui.T(Loc.T("custom.paletteHint"), 11, Skin.TextDim, wrap: TextWrapping.Wrap));

        var picker = new ColorPicker();
        picker.SetColor(Skin.AccentColor);

        // во время движения — только живой предпросмотр, чтобы ничего не подвисало
        picker.ColorChanged += color =>
            Skin.Apply(_settings.Theme, Skin.ColorHex(color), _settings.Transparent);

        // закончили выбор — сохраняем
        picker.ColorCommitted += color =>
        {
            var hex = Skin.ColorHex(color);
            if (string.Equals(_settings.Accent, hex, StringComparison.OrdinalIgnoreCase)) return;

            _settings.Accent = hex;
            Skin.Apply(_settings.Theme, _settings.Accent, _settings.Transparent);
            Storage.SaveSettings(_settings);
            AppearanceChanged?.Invoke();
        };

        body.Children.Add(picker);

        body.Children.Add(Ui.Divider());

        // ---- прозрачность
        var transparency = new Toggle(Loc.T("custom.transparent"), _settings.Transparent, v =>
        {
            _settings.Transparent = v;
            Skin.Apply(_settings.Theme, _settings.Accent, _settings.Transparent);
            Storage.SaveSettings(_settings);
            AppearanceChanged?.Invoke();
        }, fontSize: 13);
        body.Children.Add(transparency);
        body.Children.Add(Ui.T(Loc.T("custom.transparentHint"), 11, Skin.TextDim, wrap: TextWrapping.Wrap));

        // ---- обои
        body.Children.Add(Ui.T(Loc.T("custom.wallpaper"), 13.5, Skin.Text, FontWeight.SemiBold));
        body.Children.Add(Ui.T(Loc.T("custom.wallpaperHint"), 11, Skin.TextDim, wrap: TextWrapping.Wrap));

        _urlBox = new TextBox
        {
            Text = _url,
            FontSize = 12.5,
            Height = 36,
            Background = Skin.PanelAlt,
            Foreground = Skin.Text,
            BorderBrush = Skin.Border,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10, 0, 10, 0),
            VerticalContentAlignment = VerticalAlignment.Center,
            Watermark = "https://…/wallpaper.jpg",
        };
        _urlBox.TextChanged += (_, _) => _url = _urlBox.Text ?? "";
        Ui.AnimateTextBox(_urlBox);
        body.Children.Add(_urlBox);

        var wallpaperRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            Margin = new Thickness(0, 2, 0, 0),
        };

        _applyBtn = Ui.Clickable(Ui.T(Loc.T("custom.apply"), 13, Skin.OnAccent, FontWeight.SemiBold),
            ApplyWallpaper, Skin.Accent, Skin.AccentHover, new CornerRadius(6), new Thickness(18, 9));
        wallpaperRow.Children.Add(_applyBtn);
        wallpaperRow.Children.Add(Ui.GhostButton(Loc.T("custom.clearWallpaper"), RemoveWallpaper));
        body.Children.Add(wallpaperRow);

        _status = Ui.T(string.IsNullOrWhiteSpace(_settings.BackgroundPath) ? "" : Loc.T("custom.wallpaperSet"),
            11.5, Skin.TextDim, wrap: TextWrapping.Wrap);
        body.Children.Add(_status);

        // ---- назад
        var footer = new StackPanel
        {
            Margin = new Thickness(26, 4, 26, 20),
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        footer.Children.Add(Ui.GhostButton(Loc.T("settings.back"), () => Closed?.Invoke()));
        DockPanel.SetDock(footer, Dock.Bottom);

        var scroll = new ScrollViewer
        {
            Content = body,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Margin = new Thickness(0, 12, 0, 0),
        };
        Ui.SmoothScroll(scroll);

        var dock = new DockPanel();
        DockPanel.SetDock(header, Dock.Top);
        dock.Children.Add(header);
        dock.Children.Add(footer);
        dock.Children.Add(scroll);

        Child = dock;
        Ui.FadeSlideIn(dock, 0, 200, 6);
    }

    private Border Chip(string text, bool active, Action onClick)
    {
        return Ui.Clickable(Ui.T(text, 12.5, active ? Skin.OnAccent : Skin.Text, FontWeight.Medium),
            onClick,
            active ? Skin.Accent : Skin.PanelAlt,
            active ? Skin.AccentHover : Skin.CardHover,
            new CornerRadius(4), new Thickness(18, 8));
    }

    // -------------------------------------------------------------- actions

    private void SetTheme(string theme)
    {
        if (string.Equals(_settings.Theme, theme, StringComparison.OrdinalIgnoreCase)) return;

        _settings.Theme = theme;
        Skin.Apply(_settings.Theme, _settings.Accent, _settings.Transparent);
        Storage.SaveSettings(_settings);

        Build();
        AppearanceChanged?.Invoke();
    }

    private async void ApplyWallpaper()
    {
        var url = (_urlBox?.Text ?? "").Trim();
        if (url.Length == 0)
        {
            SetStatus(Loc.T("custom.wallpaperError"));
            return;
        }

        _applyBtn.Opacity = 0.6;
        _applyBtn.IsEnabled = false;
        SetStatus(Loc.T("custom.downloading"));

        var (path, error) = await Task.Run(() => Backgrounds.SetFromUrlAsync(url)).ConfigureAwait(true);
        _lastError = error;

        _applyBtn.Opacity = 1;
        _applyBtn.IsEnabled = true;

        if (path != null)
        {
            _settings.BackgroundPath = path;
            Storage.SaveSettings(_settings);
            SetStatus(Loc.T("custom.wallpaperSet"));
            WallpaperChanged?.Invoke();
        }
        else
        {
            SetStatus(Loc.T(_lastError == "page" ? "custom.wallpaperPageError" : "custom.wallpaperError"));
        }
    }

    private void RemoveWallpaper()
    {
        Backgrounds.Clear();
        _settings.BackgroundPath = null;
        Storage.SaveSettings(_settings);

        WallpaperChanged?.Invoke();
        Build();
        SetStatus("");
    }

    private void SetStatus(string text)
    {
        if (_status != null) _status.Text = text;
    }
}
