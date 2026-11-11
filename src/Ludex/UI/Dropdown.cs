using Avalonia.Animation.Easings;
using Avalonia.Animation;
using Avalonia.Collections;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Labs.Gif;
using Avalonia.Layout;
using Avalonia.Logging;
using Avalonia.Media.Imaging;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using Avalonia;
using Ludex.Services;
using Ludex.UI;
using Microsoft.Win32;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.IO;
using System.Linq;
using System.Net.Http.Headers;
using System.Net.Http;
using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System;
using Path = Avalonia.Controls.Shapes.Path;

namespace Ludex.UI;

public sealed class Dropdown : Border
{
	private readonly string[] _items;

	private readonly TextBlock _label;

	private readonly Path _chevron;

	private readonly Border _panel;

	private readonly StackPanel _list;

	private readonly double _fontSize;

	private int _selectedIndex;

	private bool _open;

	private OverlayPanel? _overlay;

	public int SelectedIndex
	{
		get
		{
			return _selectedIndex;
		}
		set
		{
			if (value >= 0 && value < _items.Length)
			{
				_selectedIndex = value;
				_label.Text = _items[value];
				RefreshMarks();
			}
		}
	}

	public event Action<int>? SelectionChanged;

	public Dropdown(string[] items, int selectedIndex, double width, double height, double fontSize = 12.5)
	{
		_items = items;
		_fontSize = fontSize;
		_selectedIndex = Math.Clamp(selectedIndex, 0, Math.Max(0, items.Length - 1));
		((Layoutable)this).Width = width;
		((Layoutable)this).Height = height;
		((Border)this).Background = Skin.PanelAlt;
		((Border)this).BorderBrush = Skin.Border;
		((Border)this).BorderThickness = new Thickness(1.0);
		((Border)this).CornerRadius = new CornerRadius(7.0);
		((Decorator)this).Padding = new Thickness(11.0, 0.0, 8.0, 0.0);
		((InputElement)this).Cursor = new Cursor((StandardCursorType)9);
		this.RenderTransformOrigin = new RelativePoint(0.5, 0.5, (RelativeUnit)0);
		Transitions val = new Transitions();
		((AvaloniaList<ITransition>)val).Add((ITransition)new BrushTransition
		{
			Property = (AvaloniaProperty)(object)Border.BackgroundProperty,
			Duration = TimeSpan.FromMilliseconds(150.0)
		});
		((AvaloniaList<ITransition>)val).Add((ITransition)new TransformOperationsTransition
		{
			Property = (AvaloniaProperty)(object)Visual.RenderTransformProperty,
			Duration = TimeSpan.FromMilliseconds(140.0),
			Easing = (Easing)new CubicEaseOut()
		});
		((Animatable)this).Transitions = val;
		_label = Ui.T((items.Length != 0) ? items[_selectedIndex] : "", fontSize, Skin.Text, (FontWeight)500, (TextWrapping)0);
		((Layoutable)_label).VerticalAlignment = (VerticalAlignment)2;
		_chevron = Ui.Icon("M1,3 L6,8 L11,3", Skin.TextMuted, 12.0, 9.0, stroke: true, 1.7);
		_chevron.RenderTransformOrigin = new RelativePoint(0.5, 0.5, (RelativeUnit)0);
		Path chevron = _chevron;
		Transitions val2 = new Transitions();
		((AvaloniaList<ITransition>)val2).Add((ITransition)new TransformOperationsTransition
		{
			Property = (AvaloniaProperty)(object)Visual.RenderTransformProperty,
			Duration = TimeSpan.FromMilliseconds(220.0),
			Easing = (Easing)new CubicEaseInOut()
		});
		((Animatable)chevron).Transitions = val2;
		Grid val3 = new Grid
		{
			ColumnDefinitions = new ColumnDefinitions("*,Auto")
		};
		((AvaloniaList<Control>)(object)((Panel)val3).Children).Add((Control)(object)_label);
		Viewbox val4 = new Viewbox
		{
			Width = 12.0,
			Height = 9.0,
			Child = (Control)(object)_chevron
		};
		((Layoutable)val4).VerticalAlignment = (VerticalAlignment)2;
		Grid.SetColumn((Control)(object)val4, 1);
		((AvaloniaList<Control>)(object)((Panel)val3).Children).Add((Control)(object)val4);
		((Decorator)this).Child = (Control)(object)val3;
		_list = new StackPanel
		{
			Spacing = 2.0
		};
		Border val5 = new Border
		{
			Background = (IBrush)new SolidColorBrush(Skin.PanelColor, 1.0),
			CornerRadius = new CornerRadius(10.0),
			Padding = new Thickness(6.0),
			MinWidth = width,
			Child = (Control)(object)_list,
			Opacity = 0.0,
			RenderTransform = (ITransform)new TranslateTransform(0.0, -8.0)
		};
		BoxShadow val6 = new BoxShadow
		{
			OffsetX = 0.0,
			OffsetY = 10.0,
			Blur = 26.0,
			Color = Color.Parse("#66000000"),
		};
		val5.BoxShadow = new BoxShadows(val6);
		Transitions val7 = new Transitions();
		((AvaloniaList<ITransition>)val7).Add((ITransition)new DoubleTransition
		{
			Property = (AvaloniaProperty)(object)Visual.OpacityProperty,
			Duration = TimeSpan.FromMilliseconds(90.0),
			Easing = (Easing)new CubicEaseOut()
		});
		((AvaloniaList<ITransition>)val7).Add((ITransition)new TransformOperationsTransition
		{
			Property = (AvaloniaProperty)(object)Visual.RenderTransformProperty,
			Duration = TimeSpan.FromMilliseconds(110.0),
			Easing = (Easing)new CubicEaseOut()
		});
		((Animatable)val5).Transitions = val7;
		_panel = val5;
		BuildItems();
		((InputElement)this).PointerEntered += delegate
		{
			((Border)this).Background = Skin.CardHover;
		};
		((InputElement)this).PointerExited += delegate
		{
			((Border)this).Background = Skin.PanelAlt;
		};
		((InputElement)this).PointerPressed += delegate(object? _, PointerPressedEventArgs e)
		{
			PointerPoint currentPoint = ((PointerEventArgs)e).GetCurrentPoint((Visual)(object)this);
			PointerPointProperties properties = ((PointerPoint)currentPoint).Properties;
			if (((PointerPointProperties)properties).IsLeftButtonPressed)
			{
				((RoutedEventArgs)e).Handled = true;
				Toggle();
			}
		};
	}

	public void Toggle()
	{
		if (_open)
		{
			Close();
		}
		else
		{
			Open();
		}
	}

	private void Open()
	{
		if (_open)
		{
			return;
		}
		_open = true;
		((Layoutable)_panel).MinWidth = Math.Max(((Layoutable)this).Width, 180.0);
		RefreshMarks();
		_panel.Opacity = 0.0;
		_panel.RenderTransform = (ITransform)new TranslateTransform(0.0, -8.0);
		Border panel = _panel;
		Rect bounds = this.Bounds;
		_overlay = OverlayPanel.Show((Control)(object)this, (Control)(object)panel, new Point(0.0, ((Rect)bounds).Height + 6.0));
		if (_overlay == null)
		{
			_open = false;
			return;
		}
		_overlay.Closed += OnOverlayClosed;
		_chevron.RenderTransform = (ITransform)new RotateTransform(180.0);
		Dispatcher.UIThread.Post((Action)delegate
		{
			_panel.Opacity = 1.0;
			_panel.RenderTransform = (ITransform)new TranslateTransform(0.0, 0.0);
		}, DispatcherPriority.Input);
	}

	private void OnOverlayClosed()
	{
		_overlay = null;
		_open = false;
		_chevron.RenderTransform = (ITransform)new RotateTransform(0.0);
	}

	private void Close()
	{
		if (!_open)
		{
			return;
		}
		_open = false;
		_panel.Opacity = 0.0;
		_panel.RenderTransform = (ITransform)new TranslateTransform(0.0, -6.0);
		_chevron.RenderTransform = (ITransform)new RotateTransform(0.0);
		OverlayPanel overlay = _overlay;
		if (overlay != null)
		{
			DispatcherTimer.RunOnce((Action)delegate
			{
				overlay.Close();
			}, TimeSpan.FromMilliseconds(140.0), default(DispatcherPriority));
		}
	}

	private void BuildItems()
	{
		((AvaloniaList<Control>)(object)((Panel)_list).Children).Clear();
		for (int i = 0; i < _items.Length; i++)
		{
			int index = i;
			TextBlock val = Ui.T(_items[i], _fontSize, Skin.Text, (FontWeight)400, (TextWrapping)0);
			Path val2 = Ui.Icon("M1,5 L4.4,8.4 L11,1.6", Skin.Accent, 12.0, 10.0, stroke: true, 1.9);
			val2.IsVisible = index == _selectedIndex;
			Grid val3 = new Grid
			{
				ColumnDefinitions = new ColumnDefinitions("*,Auto")
			};
			((AvaloniaList<Control>)(object)((Panel)val3).Children).Add((Control)(object)val);
			Grid.SetColumn((Control)(object)val2, 1);
			((AvaloniaList<Control>)(object)((Panel)val3).Children).Add((Control)(object)val2);
			Border val4 = new Border
			{
				Child = (Control)(object)val3,
				Background = (IBrush)(object)Brushes.Transparent,
				CornerRadius = new CornerRadius(6.0),
				Padding = new Thickness(10.0, 9.0),
				Cursor = new Cursor((StandardCursorType)9),
				MinWidth = Math.Max(((Layoutable)this).Width - 12.0, 160.0)
			};
			Transitions val5 = new Transitions();
			((AvaloniaList<ITransition>)val5).Add((ITransition)new BrushTransition
			{
				Property = (AvaloniaProperty)(object)Border.BackgroundProperty,
				Duration = TimeSpan.FromMilliseconds(130.0)
			});
			((Animatable)val4).Transitions = val5;
			Border item = val4;
			((InputElement)item).PointerEntered += delegate
			{
				item.Background = Skin.CardHover;
			};
			((InputElement)item).PointerExited += delegate
			{
				item.Background = (IBrush)(object)Brushes.Transparent;
			};
			((InputElement)item).PointerReleased += delegate(object? _, PointerReleasedEventArgs e)
			{
				((RoutedEventArgs)e).Handled = true;
				Close();
				if (_selectedIndex != index)
				{
					SelectedIndex = index;
					this.SelectionChanged?.Invoke(index);
				}
			};
			((AvaloniaList<Control>)(object)((Panel)_list).Children).Add((Control)(object)item);
		}
	}

	private void RefreshMarks()
	{
		for (int i = 0; i < ((AvaloniaList<Control>)(object)((Panel)_list).Children).Count; i++)
		{
			Control obj = ((AvaloniaList<Control>)(object)((Panel)_list).Children)[i];
			Border val = (Border)(object)((obj is Border) ? obj : null);
			if (val == null)
			{
				continue;
			}
			Control child = ((Decorator)val).Child;
			Grid val2 = (Grid)(object)((child is Grid) ? child : null);
			if (val2 != null && ((AvaloniaList<Control>)(object)((Panel)val2).Children).Count >= 2)
			{
				((Visual)((AvaloniaList<Control>)(object)((Panel)val2).Children)[1]).IsVisible = i == _selectedIndex;
				Control obj2 = ((AvaloniaList<Control>)(object)((Panel)val2).Children)[0];
				TextBlock val3 = (TextBlock)(object)((obj2 is TextBlock) ? obj2 : null);
				if (val3 != null)
				{
					val3.FontWeight = (FontWeight)((i == _selectedIndex) ? 600 : 400);
				}
			}
		}
	}
}
