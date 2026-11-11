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

public sealed class Toggle : Border
{
	private readonly Border _track;

	private readonly Border _knob;

	private readonly TextBlock _label;

	private bool _value;

	private DispatcherTimer? _move;

	public bool Value
	{
		get
		{
			return _value;
		}
		set
		{
			if (_value != value)
			{
				_value = value;
				Apply();
			}
		}
	}

	public Toggle(string text, bool value, Action<bool>? changed = null, bool interactive = true, double fontSize = 12.5)
	{
		Action<bool> changed2 = changed;
		_value = value;
		Border val = new Border
		{
			Width = 16.0,
			Height = 16.0,
			CornerRadius = new CornerRadius(8.0)
		};
		IBrush background = (IBrush)(value ? ((object)Skin.OnAccent) : ((object)Brushes.White));
		val.Background = background;
		((Layoutable)val).HorizontalAlignment = (HorizontalAlignment)1;
		((Layoutable)val).VerticalAlignment = (VerticalAlignment)2;
		((Layoutable)val).Margin = new Thickness(3.0, 0.0, 0.0, 0.0);
		val.RenderTransform = (ITransform)new TranslateTransform((double)(value ? 18 : 0), 0.0);
		BoxShadow val2 = new BoxShadow
		{
			OffsetY = 1.0,
			Blur = 4.0,
			Color = Color.Parse("#66000000"),
		};
		val.BoxShadow = new BoxShadows(val2);
		_knob = val;
		_track = new Border
		{
			Width = 40.0,
			Height = 22.0,
			CornerRadius = new CornerRadius(11.0),
			Background = (value ? Skin.Accent : Skin.PanelAlt),
			BorderBrush = (value ? Skin.Accent : Skin.Border),
			BorderThickness = new Thickness(1.0),
			VerticalAlignment = (VerticalAlignment)2,
			Child = (Control)(object)_knob
		};
		_label = Ui.T(text, fontSize, interactive ? Skin.TextMuted : Skin.TextDim, (FontWeight)500, (TextWrapping)0);
		TextBlock label = _label;
		Transitions val3 = new Transitions();
		((AvaloniaList<ITransition>)val3).Add((ITransition)new BrushTransition
		{
			Property = (AvaloniaProperty)(object)TextBlock.ForegroundProperty,
			Duration = TimeSpan.FromMilliseconds(150.0)
		});
		((Animatable)label).Transitions = val3;
		StackPanel val4 = new StackPanel
		{
			Orientation = (Orientation)0,
			Spacing = 11.0
		};
		((AvaloniaList<Control>)(object)((Panel)val4).Children).Add((Control)(object)_track);
		((AvaloniaList<Control>)(object)((Panel)val4).Children).Add((Control)(object)_label);
		((Decorator)this).Child = (Control)(object)val4;
		((Decorator)this).Padding = new Thickness(0.0, 3.0);
		((Border)this).Background = (IBrush)(object)Brushes.Transparent;
		((Layoutable)this).HorizontalAlignment = (HorizontalAlignment)1;
		this.Opacity = (interactive ? 1.0 : 0.55);
		((InputElement)this).Cursor = (Cursor)((!interactive) ? ((object)Cursor.Default) : ((object)new Cursor((StandardCursorType)9)));
		if (!interactive)
		{
			return;
		}
		((InputElement)this).PointerEntered += delegate
		{
			_label.Foreground = Skin.Text;
		};
		((InputElement)this).PointerExited += delegate
		{
			_label.Foreground = Skin.TextMuted;
		};
		((InputElement)this).PointerPressed += delegate(object? _, PointerPressedEventArgs e)
		{
			PointerPoint currentPoint = ((PointerEventArgs)e).GetCurrentPoint((Visual)(object)this);
			PointerPointProperties properties = ((PointerPoint)currentPoint).Properties;
			if (((PointerPointProperties)properties).IsLeftButtonPressed)
			{
				((RoutedEventArgs)e).Handled = true;
				Value = !_value;
				changed2?.Invoke(Value);
			}
		};
	}

	public void SetSilently(bool value)
	{
		_value = value;
		Apply();
	}

	private void Apply()
	{
		_track.Background = (_value ? Skin.Accent : Skin.PanelAlt);
		_track.BorderBrush = (_value ? Skin.Accent : Skin.Border);
		Border knob = _knob;
		IBrush background = (IBrush)(_value ? ((object)Skin.OnAccent) : ((object)Brushes.White));
		knob.Background = background;
		ITransform renderTransform = _knob.RenderTransform;
		TranslateTransform val = (TranslateTransform)(object)((renderTransform is TranslateTransform) ? renderTransform : null);
		double from = ((val != null) ? val.X : ((double)((!_value) ? 18 : 0)));
		double to = (_value ? 18.0 : 0.0);
		DispatcherTimer? move = _move;
		if (move != null)
		{
			move.Stop();
		}
		DateTime started = DateTime.UtcNow;
		_move = new DispatcherTimer
		{
			Interval = TimeSpan.FromMilliseconds(12.0)
		};
		_move.Tick += delegate
		{
			double num = Math.Min(1.0, (DateTime.UtcNow - started).TotalMilliseconds / 90.0);
			double num2 = ((num < 0.5) ? (2.0 * num * num) : (1.0 - Math.Pow(-2.0 * num + 2.0, 2.0) / 2.0));
			_knob.RenderTransform = (ITransform)new TranslateTransform(from + (to - from) * num2, 0.0);
			if (num >= 1.0)
			{
				DispatcherTimer? move2 = _move;
				if (move2 != null)
				{
					move2.Stop();
				}
			}
		};
		_move.Start();
	}
}
