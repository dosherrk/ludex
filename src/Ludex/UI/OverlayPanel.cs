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

public sealed class OverlayPanel
{
	private readonly OverlayLayer _layer;

	private readonly Border _shield;

	private readonly Control _panel;

	private bool _closed;

	public event Action? Closed;

	private OverlayPanel(OverlayLayer layer, Control panel, Border shield)
	{
		_layer = layer;
		_panel = panel;
		_shield = shield;
	}

	public static OverlayPanel? Show(Control anchor, Control panel, Point position)
	{
		OverlayLayer layer = OverlayLayer.GetOverlayLayer((Visual)(object)anchor);
		if (layer == null)
		{
			return null;
		}
		Point? val = VisualExtensions.TranslatePoint((Visual)(object)anchor, position, (Visual)(object)layer);
		if (!val.HasValue)
		{
			return null;
		}
		((Layoutable)panel).Measure(Size.Infinity);
		Size desiredSize = ((Layoutable)panel).DesiredSize;
		Rect bounds = layer.Bounds;
		double num;
		if (!(((Rect)bounds).Width > 1.0))
		{
			num = 1280.0;
		}
		else
		{
			bounds = layer.Bounds;
			num = ((Rect)bounds).Width;
		}
		double num2 = num;
		bounds = layer.Bounds;
		double num3;
		if (!(((Rect)bounds).Height > 1.0))
		{
			num3 = 800.0;
		}
		else
		{
			bounds = layer.Bounds;
			num3 = ((Rect)bounds).Height;
		}
		double num4 = num3;
		Point value = val.Value;
		double num5 = Math.Max(6.0, Math.Min(((Point)value).X, num2 - ((Size)desiredSize).Width - 6.0));
		value = val.Value;
		double num6 = Math.Max(6.0, Math.Min(((Point)value).Y, num4 - ((Size)desiredSize).Height - 6.0));
		Border val2 = new Border
		{
			Background = (IBrush)(object)Brushes.Transparent,
			Width = num2,
			Height = num4,
			Focusable = true
		};
		Canvas.SetLeft((AvaloniaObject)(object)val2, 0.0);
		Canvas.SetTop((AvaloniaObject)(object)val2, 0.0);
		Canvas.SetLeft((AvaloniaObject)(object)panel, num5);
		Canvas.SetTop((AvaloniaObject)(object)panel, num6);
		((AvaloniaList<Control>)(object)((Panel)layer).Children).Add((Control)(object)val2);
		((AvaloniaList<Control>)(object)((Panel)layer).Children).Add(panel);
		OverlayPanel handle = new OverlayPanel(layer, panel, val2);
		((InputElement)val2).PointerPressed += delegate(object? _, PointerPressedEventArgs e)
		{
			((RoutedEventArgs)e).Handled = true;
			handle.Close();
		};
		((InputElement)val2).KeyDown += delegate(object? _, KeyEventArgs e)
		{
			if ((int)e.Key == 13)
			{
				((RoutedEventArgs)e).Handled = true;
				handle.Close();
			}
		};
		DispatcherTimer.RunOnce((Action)delegate
		{
			((Control)layer).SizeChanged += handle.OnLayerResized;
		}, TimeSpan.FromMilliseconds(400.0), default(DispatcherPriority));
		((InputElement)val2).Focus((NavigationMethod)0, (KeyModifiers)0);
		return handle;
	}

	private void OnLayerResized(object? sender, SizeChangedEventArgs e)
	{
		Close();
	}

	public void Close()
	{
		if (!_closed)
		{
			_closed = true;
			((Control)_layer).SizeChanged -= OnLayerResized;
			if (((AvaloniaList<Control>)(object)((Panel)_layer).Children).Contains(_panel))
			{
				((AvaloniaList<Control>)(object)((Panel)_layer).Children).Remove(_panel);
			}
			if (((AvaloniaList<Control>)(object)((Panel)_layer).Children).Contains((Control)(object)_shield))
			{
				((AvaloniaList<Control>)(object)((Panel)_layer).Children).Remove((Control)(object)_shield);
			}
			this.Closed?.Invoke();
		}
	}
}
