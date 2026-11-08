using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace Ludex.UI;

/// <summary>
/// Выпадающий список в стиле приложения: своя «кнопка», своё всплывающее окно
/// с плавным появлением (проявление + подъём), подсветка строк при наведении
/// и поворот стрелки. Заменяет стандартный ComboBox, у которого нет анимаций.
/// </summary>
public sealed class Dropdown : Border
{
    private readonly string[] _items;
    private readonly TextBlock _label;
    private readonly Avalonia.Controls.Shapes.Path _chevron;
    private readonly Border _panel;
    private readonly StackPanel _list;
    private readonly double _fontSize;

    private int _selectedIndex;
    private bool _open;
    private OverlayPanel? _overlay;

    public event Action<int>? SelectionChanged;

    public Dropdown(string[] items, int selectedIndex, double width, double height, double fontSize = 12.5)
    {
        _items = items;
        _fontSize = fontSize;
        _selectedIndex = Math.Clamp(selectedIndex, 0, Math.Max(0, items.Length - 1));

        Width = width;
        Height = height;
        Background = Skin.PanelAlt;
        BorderBrush = Skin.Border;
        BorderThickness = new Thickness(1);
        CornerRadius = new CornerRadius(7);
        Padding = new Thickness(11, 0, 8, 0);
        Cursor = new Cursor(StandardCursorType.Hand);
        RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative);
        Transitions = new Avalonia.Animation.Transitions
        {
            new Avalonia.Animation.BrushTransition
            {
                Property = Border.BackgroundProperty,
                Duration = TimeSpan.FromMilliseconds(150),
            },
            new Avalonia.Animation.TransformOperationsTransition
            {
                Property = Visual.RenderTransformProperty,
                Duration = TimeSpan.FromMilliseconds(140),
                Easing = new Avalonia.Animation.Easings.CubicEaseOut(),
            },
        };

        _label = Ui.T(items.Length > 0 ? items[_selectedIndex] : "", fontSize, Skin.Text, FontWeight.Medium);
        _label.VerticalAlignment = VerticalAlignment.Center;

        _chevron = Ui.Icon("M1,3 L6,8 L11,3", Skin.TextMuted, 12, 9, true, 1.7);
        _chevron.RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative);
        _chevron.Transitions = new Avalonia.Animation.Transitions
        {
            new Avalonia.Animation.TransformOperationsTransition
            {
                Property = Visual.RenderTransformProperty,
                Duration = TimeSpan.FromMilliseconds(220),
                Easing = new Avalonia.Animation.Easings.CubicEaseInOut(),
            },
        };

        var content = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        content.Children.Add(_label);

        var chevronBox = new Viewbox { Width = 12, Height = 9, Child = _chevron };
        chevronBox.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(chevronBox, 1);
        content.Children.Add(chevronBox);

        Child = content;

        _list = new StackPanel { Spacing = 2 };
        _panel = new Border
        {
            Background = new SolidColorBrush(Skin.PanelColor),
            BorderBrush = Skin.GlassEdge,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(6),
            MinWidth = width,
            Child = _list,
            Opacity = 0,
            RenderTransform = new TranslateTransform(0, -8),
            BoxShadow = new BoxShadows(new BoxShadow
            {
                OffsetX = 0,
                OffsetY = 10,
                Blur = 26,
                Color = Color.Parse("#66000000"),
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

        BuildItems();

        PointerEntered += (_, _) => Background = Skin.CardHover;
        PointerExited += (_, _) => Background = Skin.PanelAlt;
        PointerPressed += (_, e) =>
        {
            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
            e.Handled = true;
            RenderTransform = new ScaleTransform(0.975, 0.975);
            DispatcherTimer.RunOnce(() => RenderTransform = new ScaleTransform(1, 1),
                TimeSpan.FromMilliseconds(90));
            Toggle();
        };
    }

    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            if (value < 0 || value >= _items.Length) return;

            _selectedIndex = value;
            _label.Text = _items[value];
            RefreshMarks();
        }
    }

    public void Toggle()
    {
        if (_open) Close();
        else Open();
    }

    private void Open()
    {
        if (_open) return;

        _open = true;
        _panel.MinWidth = Math.Max(Width, 180);
        RefreshMarks();

        _panel.Opacity = 0;
        _panel.RenderTransform = new TranslateTransform(0, -8);

        // панель показываем внутри окна (без отдельного всплывающего окна ОС),
        // поэтому вокруг неё нет светлой подложки-рамки
        _overlay = OverlayPanel.Show(this, _panel, new Point(0, Bounds.Height + 6));
        if (_overlay == null)
        {
            _open = false;
            return;
        }

        _overlay.Closed += OnOverlayClosed;
        _chevron.RenderTransform = new RotateTransform(180);

        Dispatcher.UIThread.Post(() =>
        {
            _panel.Opacity = 1;
            _panel.RenderTransform = new TranslateTransform(0, 0);
        }, DispatcherPriority.Input);
    }

    private void OnOverlayClosed()
    {
        _overlay = null;
        _open = false;
        _chevron.RenderTransform = new RotateTransform(0);
    }

    private void Close()
    {
        if (!_open) return;

        _open = false;
        _panel.Opacity = 0;
        _panel.RenderTransform = new TranslateTransform(0, -6);
        _chevron.RenderTransform = new RotateTransform(0);

        var overlay = _overlay;
        if (overlay == null) return;

        DispatcherTimer.RunOnce(() => overlay.Close(), TimeSpan.FromMilliseconds(140));
    }

    private void BuildItems()
    {
        _list.Children.Clear();

        for (int i = 0; i < _items.Length; i++)
        {
            var index = i;
            var label = Ui.T(_items[i], _fontSize, Skin.Text, FontWeight.Normal);
            var check = Ui.Icon("M1,5 L4.4,8.4 L11,1.6", Skin.Accent, 12, 10, true, 1.9);
            check.IsVisible = index == _selectedIndex;

            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
            row.Children.Add(label);
            Grid.SetColumn(check, 1);
            row.Children.Add(check);

            var item = new Border
            {
                Child = row,
                Background = Brushes.Transparent,
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(10, 9),
                Cursor = new Cursor(StandardCursorType.Hand),
                MinWidth = Math.Max(Width - 12, 160),
                Transitions = new Avalonia.Animation.Transitions
                {
                    new Avalonia.Animation.BrushTransition
                    {
                        Property = Border.BackgroundProperty,
                        Duration = TimeSpan.FromMilliseconds(130),
                    },
                },
            };

            item.PointerEntered += (_, _) => item.Background = Skin.CardHover;
            item.PointerExited += (_, _) => item.Background = Brushes.Transparent;
            item.PointerReleased += (_, e) =>
            {
                e.Handled = true;
                Close();

                if (_selectedIndex == index) return;

                SelectedIndex = index;
                SelectionChanged?.Invoke(index);
            };

            _list.Children.Add(item);
        }
    }

    private void RefreshMarks()
    {
        for (int i = 0; i < _list.Children.Count; i++)
        {
            if (_list.Children[i] is not Border { Child: Grid row }) continue;
            if (row.Children.Count < 2) continue;

            row.Children[1].IsVisible = i == _selectedIndex;

            if (row.Children[0] is TextBlock text)
                text.FontWeight = i == _selectedIndex ? FontWeight.SemiBold : FontWeight.Normal;
        }
    }
}
