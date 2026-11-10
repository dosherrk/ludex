using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Ludex.Services;

namespace Ludex.UI;

/// <summary>
/// Панель «Интеграции»: список лаунчеров с найденными играми и переключателями.
/// Открывается из боковой панели под «Настройками».
/// </summary>
public sealed class IntegrationsPanel : Border
{
    private readonly AppSettings _settings;
    private readonly List<string> _enabled;

    private readonly Dictionary<string, TextBlock> _statusTexts = new();
    private readonly Dictionary<string, Toggle> _toggles = new();
    private List<LauncherState> _states = new();

    private StackPanel _rows = null!;
    private TextBlock _summary = null!;
    private Border _rescanButton = null!;
    private bool _scanning;

    /// <summary>Пользователь попросил закрыть панель.</summary>
    public event Action? Closed;

    /// <summary>Набор подключённых лаунчеров изменился — библиотеку нужно пересобрать.</summary>
    public event Action? Changed;

    public IntegrationsPanel(AppSettings settings)
    {
        _settings = settings;
        _enabled = new List<string>(settings.Launchers);

        MaxWidth = 720;
        MaxHeight = 700;
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
        _ = LoadAsync();
    }

    private void BuildContent()
    {
        var header = new DockPanel { Margin = new Thickness(24, 20, 20, 0) };
        var close = Ui.Clickable(Ui.Icon(Ui.CloseGeometry, Skin.TextMuted, 12, 12, true, 1.8),
            () => Closed?.Invoke(), Skin.PanelAlt, Skin.CardHover, new CornerRadius(6), new Thickness(10, 10));
        close.VerticalAlignment = VerticalAlignment.Top;
        DockPanel.SetDock(close, Dock.Right);
        header.Children.Add(close);
        header.Children.Add(Ui.T(Loc.T("integrations.title"), 21, Skin.Text, FontWeight.Bold));

        var body = new StackPanel { Margin = new Thickness(24, 14, 24, 16), Spacing = 12 };
        body.Children.Add(Ui.T(Loc.T("integrations.hint"), 11.5, Skin.TextDim, wrap: TextWrapping.Wrap));

        _summary = Ui.T("", 11.5, Skin.Accent, FontWeight.Medium);
        body.Children.Add(_summary);

        _rows = new StackPanel { Spacing = 8 };
        foreach (var descriptor in Launchers.All)
        {
            if (!Launchers.IsSupported(descriptor.Key)) continue;
            _rows.Children.Add(BuildRow(descriptor.Key));
        }

        // прокрутка одна — общая, на всю панель: вложенный ScrollViewer обрезал текст снизу
        body.Children.Add(_rows);

        body.Children.Add(Ui.Divider());

        body.Children.Add(Ui.T(Loc.T("integrations.how"), 12.5, Skin.Text, FontWeight.SemiBold));
        body.Children.Add(Ui.T(Loc.T("integrations.howHint"), 11, Skin.TextDim, wrap: TextWrapping.Wrap));
        body.Children.Add(Ui.T(Loc.T("integrations.applyHint"), 11, Skin.TextDim, wrap: TextWrapping.Wrap));

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            Margin = new Thickness(24, 14, 24, 20),
            HorizontalAlignment = HorizontalAlignment.Right,
        };

        _rescanButton = Ui.GhostButton(Loc.T("btn.rescanAll"), () => _ = LoadAsync(force: true));
        buttons.Children.Add(_rescanButton);
        buttons.Children.Add(Ui.PrimaryButton(Loc.T("btn.close"), () => Closed?.Invoke()));

        var layout = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(header, Dock.Top);
        DockPanel.SetDock(buttons, Dock.Bottom);
        layout.Children.Add(header);
        layout.Children.Add(buttons);
        layout.Children.Add(new ScrollViewer
        {
            Content = body,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            Background = Brushes.Transparent,
        });

        Child = layout;
        RefreshSummary();
    }

    private Control BuildRow(string key)
    {
        var name = Ui.T(Loc.T("launcher." + key), 13, Skin.Text, FontWeight.Medium);
        var hint = Ui.T(Loc.T("launcher." + key + ".hint"), 10.5, Skin.TextDim, wrap: TextWrapping.Wrap);

        var textColumn = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        textColumn.Children.Add(name);
        textColumn.Children.Add(hint);

        // фирменный знак лаунчера вместо двух букв
        var badge = LauncherLogo.Create(key, 32);

        var status = Ui.T(Loc.T("integrations.checking"), 10.5, Skin.TextDim);
        status.VerticalAlignment = VerticalAlignment.Center;
        status.TextAlignment = TextAlignment.Right;
        _statusTexts[key] = status;

        var toggle = new Toggle("", _enabled.Contains(key, StringComparer.OrdinalIgnoreCase), on =>
        {
            if (on)
            {
                if (!_enabled.Contains(key, StringComparer.OrdinalIgnoreCase)) _enabled.Add(key);
            }
            else
            {
                _enabled.RemoveAll(x => string.Equals(x, key, StringComparison.OrdinalIgnoreCase));
            }

            _settings.Launchers = new List<string>(_enabled);
            RefreshSummary();
            Changed?.Invoke();
        }, true, 12.5);
        toggle.VerticalAlignment = VerticalAlignment.Center;
        _toggles[key] = toggle;

        var right = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        right.Children.Add(status);
        right.Children.Add(toggle);

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
        var badgeCell = new Decorator { Child = badge, Margin = new Thickness(0, 0, 12, 0) };
        grid.Children.Add(badgeCell);
        Grid.SetColumn(textColumn, 1);
        grid.Children.Add(textColumn);
        Grid.SetColumn(right, 2);
        grid.Children.Add(right);

        return new Border
        {
            Background = Skin.PanelAlt,
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12, 10),
            Child = grid,
        };
    }

    private async Task LoadAsync(bool force = false)
    {
        if (_scanning) return;
        _scanning = true;

        // проверка выполняется один раз за запуск: повторно — только по кнопке «Обновить»
        var cached = !force && Launchers.HasSessionCache;

        if (!cached)
        {
            _rescanButton.Opacity = 0.55;
            _rescanButton.IsEnabled = false;
            foreach (var key in _statusTexts.Keys) _statusTexts[key].Text = Loc.T("integrations.checking");
        }

        try
        {
            var states = cached
                ? Launchers.ScanAll(_enabled)
                : await Task.Run(() => Launchers.RescanAll(_enabled));
            _states = states;

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                foreach (var state in states)
                {
                    if (!_statusTexts.TryGetValue(state.Key, out var status)) continue;

                    status.Text = state.Error != null
                        ? Loc.T("integrations.error")
                        : !state.Installed
                            ? Loc.T("integrations.notInstalled")
                            : state.Count == 0
                                ? Loc.T("integrations.empty")
                                : Loc.F("integrations.found", state.Count);

                    status.Foreground = state.Installed && state.Count > 0 ? Skin.TextMuted : Skin.TextDim;

                    // нечего подключать — переключатель неактивен
                    if (_toggles.TryGetValue(state.Key, out var toggle))
                        toggle.Opacity = state.Installed ? 1 : 0.5;
                }

                RefreshSummary();
            });
        }
        catch (Exception ex)
        {
            AppPaths.Log("Integrations scan: " + ex.Message);
        }
        finally
        {
            _scanning = false;
            _rescanButton.Opacity = 1;
            _rescanButton.IsEnabled = true;
        }
    }

    private void RefreshSummary()
    {
        var connected = _states.Count(s => _enabled.Contains(s.Key, StringComparer.OrdinalIgnoreCase));
        var games = _states.Where(s => _enabled.Contains(s.Key, StringComparer.OrdinalIgnoreCase)).Sum(s => s.Count);
        _summary.Text = Loc.F("integrations.enabled", connected, games);
    }
}
