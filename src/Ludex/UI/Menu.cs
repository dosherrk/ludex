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

namespace Ludex.UI;

public sealed class Menu
{
	private readonly StackPanel _list;

	private readonly Border _panel;

	private OverlayPanel? _overlay;

	public Menu()
	{
		_list = new StackPanel
		{
			Spacing = 1.0
		};
		Border val = new Border
		{
			Background = (IBrush)new SolidColorBrush(Skin.PanelColor, 1.0),
			CornerRadius = new CornerRadius(10.0),
			Padding = new Thickness(6.0),
			MinWidth = 208.0,
			Child = (Control)(object)_list,
			Opacity = 0.0,
			RenderTransformOrigin = new RelativePoint(0.5, 0.0, (RelativeUnit)0),
			RenderTransform = (ITransform)new ScaleTransform(0.97, 0.97)
		};
		BoxShadow val2 = new BoxShadow
		{
			OffsetX = 0.0,
			OffsetY = 10.0,
			Blur = 28.0,
			Color = Color.Parse("#77000000"),
		};
		val.BoxShadow = new BoxShadows(val2);
		Transitions val3 = new Transitions();
		((AvaloniaList<ITransition>)val3).Add((ITransition)new DoubleTransition
		{
			Property = (AvaloniaProperty)(object)Visual.OpacityProperty,
			Duration = TimeSpan.FromMilliseconds(150.0),
			Easing = (Easing)new CubicEaseOut()
		});
		((AvaloniaList<ITransition>)val3).Add((ITransition)new TransformOperationsTransition
		{
			Property = (AvaloniaProperty)(object)Visual.RenderTransformProperty,
			Duration = TimeSpan.FromMilliseconds(170.0),
			Easing = (Easing)new CubicEaseOut()
		});
		((Animatable)val).Transitions = val3;
		_panel = val;
	}

	public Menu Item(string text, Action action, bool danger = false)
	{
		Action action2 = action;
		TextBlock val = Ui.T(text, 13.0, danger ? Skin.Danger : Skin.Text, (FontWeight)400, (TextWrapping)0);
		Transitions val2 = new Transitions();
		((AvaloniaList<ITransition>)val2).Add((ITransition)new BrushTransition
		{
			Property = (AvaloniaProperty)(object)TextBlock.ForegroundProperty,
			Duration = TimeSpan.FromMilliseconds(130.0)
		});
		((Animatable)val).Transitions = val2;
		Border val3 = new Border
		{
			Child = (Control)(object)val,
			Background = (IBrush)(object)Brushes.Transparent,
			CornerRadius = new CornerRadius(6.0),
			Padding = new Thickness(11.0, 7.0),
			Cursor = new Cursor((StandardCursorType)9),
			Opacity = 0.0
		};
		Transitions val4 = new Transitions();
		((AvaloniaList<ITransition>)val4).Add((ITransition)new BrushTransition
		{
			Property = (AvaloniaProperty)(object)Border.BackgroundProperty,
			Duration = TimeSpan.FromMilliseconds(130.0)
		});
		((AvaloniaList<ITransition>)val4).Add((ITransition)new DoubleTransition
		{
			Property = (AvaloniaProperty)(object)Visual.OpacityProperty,
			Duration = TimeSpan.FromMilliseconds(150.0),
			Easing = (Easing)new CubicEaseOut()
		});
		((Animatable)val3).Transitions = val4;
		Border row = val3;
		((InputElement)row).PointerEntered += delegate
		{
			row.Background = (danger ? Skin.DangerSoft : Skin.CardHover);
		};
		((InputElement)row).PointerExited += delegate
		{
			row.Background = (IBrush)(object)Brushes.Transparent;
		};
		((InputElement)row).PointerReleased += delegate(object? _, PointerReleasedEventArgs e)
		{
			if ((int)e.InitialPressMouseButton == 1)
			{
				((RoutedEventArgs)e).Handled = true;
				Close();
				action2();
			}
		};
		((AvaloniaList<Control>)(object)((Panel)_list).Children).Add((Control)(object)row);
		return this;
	}

	public Menu Separator()
	{
		Border val = new Border
		{
			Height = 1.0,
			Margin = new Thickness(4.0, 4.0),
			Background = Skin.Border,
			Opacity = 0.0
		};
		Transitions val2 = new Transitions();
		((AvaloniaList<ITransition>)val2).Add((ITransition)new DoubleTransition
		{
			Property = (AvaloniaProperty)(object)Visual.OpacityProperty,
			Duration = TimeSpan.FromMilliseconds(160.0)
		});
		((Animatable)val).Transitions = val2;
		Border val3 = val;
		((AvaloniaList<Control>)(object)((Panel)_list).Children).Add((Control)(object)val3);
		return this;
	}

	public void Show(Control target, Point position)
	{
		_panel.Opacity = 0.0;
		_panel.RenderTransform = (ITransform)new ScaleTransform(0.97, 0.97);
		IEnumerator<Control> enumerator = ((AvaloniaList<Control>)(object)((Panel)_list).Children).GetEnumerator();
		try
		{
			while (enumerator.MoveNext())
			{
				Control current = enumerator.Current;
				if (current != null)
				{
					current.Opacity = 0.0;
				}
			}
		}
		finally
		{
			((IDisposable)enumerator).Dispose();
		}
		_overlay = OverlayPanel.Show(target, (Control)(object)_panel, position);
		if (_overlay == null)
		{
			return;
		}
		_overlay.Closed += delegate
		{
			_overlay = null;
		};
		Dispatcher.UIThread.Post((Action)delegate
		{
			_panel.Opacity = 1.0;
			_panel.RenderTransform = (ITransform)new ScaleTransform(1.0, 1.0);
		}, DispatcherPriority.Input);
		int num = 20;
		enumerator = ((AvaloniaList<Control>)(object)((Panel)_list).Children).GetEnumerator();
		try
		{
			while (enumerator.MoveNext())
			{
				Visual current2 = (Visual)(object)enumerator.Current;
				if (current2 != null)
				{
					Visual vis = current2;
					DispatcherTimer.RunOnce((Action)delegate
					{
						vis.Opacity = 1.0;
					}, TimeSpan.FromMilliseconds(num), default(DispatcherPriority));
					num += 16;
				}
			}
		}
		finally
		{
			((IDisposable)enumerator).Dispose();
		}
	}

	public void Close()
	{
		_panel.Opacity = 0.0;
		_panel.RenderTransform = (ITransform)new ScaleTransform(0.98, 0.98);
		OverlayPanel overlay = _overlay;
		_overlay = null;
		if (overlay != null)
		{
			DispatcherTimer.RunOnce((Action)delegate
			{
				overlay.Close();
			}, TimeSpan.FromMilliseconds(130.0), default(DispatcherPriority));
		}
	}
}
