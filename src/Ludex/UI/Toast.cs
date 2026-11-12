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

public enum ToastKind
{
	Process,
	Event
}

public sealed class Toast : Border
{
	private readonly TextBlock _label;

	private readonly Border _dot;

	private readonly ProgressStrip? _strip;

	private bool _busy;

	private int _generation;

	public string Text => _label.Text ?? "";

	public event Action? Changed;

	public Toast(ToastKind kind)
	{
		((Layoutable)this).MaxWidth = 520.0;
		((Layoutable)this).MinHeight = 34.0;
		((Border)this).CornerRadius = new CornerRadius(10.0);
		((Decorator)this).Padding = new Thickness(12.0, 7.0, 14.0, 8.0);
		((Border)this).BorderThickness = new Thickness(1.0);
		((Border)this).Background = Skin.ToastBg;
		((Border)this).BorderBrush = Skin.ToastEdge;
		BoxShadow val = new BoxShadow
		{
			OffsetX = 0.0,
			OffsetY = 8.0,
			Blur = 26.0,
			Color = Color.Parse(Skin.IsLight ? "#33000000" : "#7A000000")
		};
		((Border)this).BoxShadow = new BoxShadows(val);
		this.Opacity = 0.0;
		((InputElement)this).IsHitTestVisible = false;
		((Layoutable)this).VerticalAlignment = (VerticalAlignment)1;
		Transitions val2 = new Transitions();
		((AvaloniaList<ITransition>)val2).Add((ITransition)new DoubleTransition
		{
			Property = (AvaloniaProperty)(object)Visual.OpacityProperty,
			Duration = TimeSpan.FromMilliseconds(180.0),
			Easing = (Easing)new CubicEaseOut()
		});
		((Animatable)this).Transitions = val2;
		_label = Ui.T("", 11.5, Skin.Text, null, (TextWrapping)0);
		_label.TextTrimming = TextTrimming.CharacterEllipsis;
		((Layoutable)_label).MaxWidth = 470.0;
		StackPanel val3 = new StackPanel
		{
			Orientation = (Orientation)0,
			Spacing = 9.0,
			VerticalAlignment = (VerticalAlignment)2
		};
		_dot = new Border
		{
			Width = 7.0,
			Height = 7.0,
			CornerRadius = new CornerRadius(4.0),
			Background = ((kind == ToastKind.Process) ? Skin.Accent : Skin.TextMuted),
			VerticalAlignment = (VerticalAlignment)2
		};
		((AvaloniaList<Control>)(object)((Panel)val3).Children).Add((Control)(object)_dot);
		((AvaloniaList<Control>)(object)((Panel)val3).Children).Add((Control)(object)_label);
		if (kind == ToastKind.Process)
		{
			_strip = new ProgressStrip(46.0, 3.0);
			_strip.SetActive(active: false);
			((Layoutable)_strip).Margin = new Thickness(2.0, 0.0, 0.0, 0.0);
			((Layoutable)_strip).VerticalAlignment = (VerticalAlignment)2;
			((AvaloniaList<Control>)(object)((Panel)val3).Children).Add((Control)(object)_strip);
		}
		((Decorator)this).Child = (Control)(object)val3;
	}

	protected override void OnSizeChanged(SizeChangedEventArgs e)
	{
		base.OnSizeChanged(e);
		this.Changed?.Invoke();
	}

	public void Show(string text)
	{
		if (string.IsNullOrWhiteSpace(text))
		{
			return;
		}
		_label.Text = text;
		if (_busy)
		{
			this.Changed?.Invoke();
			return;
		}
		_busy = true;
		int generation = ++_generation;
		this.IsVisible = true;
		_strip?.SetActive(active: true);
		((Layoutable)this).InvalidateMeasure();
		Dispatcher.UIThread.Post((Action)delegate
		{
			if (generation == _generation && _busy)
			{
				StyledElement parent = ((StyledElement)this).Parent;
				StyledElement obj = ((parent is Control) ? parent : null);
				if (obj != null)
				{
					((Layoutable)obj).InvalidateMeasure();
				}
				this.Changed?.Invoke();
				this.Opacity = 1.0;
			}
		}, DispatcherPriority.Loaded);
	}

	public void Hide()
	{
		if (Busy)
		{
			return;
		}
		int generation = ++_generation;
		_busy = false;
		this.Opacity = 0.0;
		DispatcherTimer.RunOnce((Action)delegate
		{
			if (!_busy && generation == _generation)
			{
				this.IsVisible = false;
				_strip?.SetActive(active: false);
				this.Changed?.Invoke();
			}
		}, TimeSpan.FromMilliseconds(220.0), default(DispatcherPriority));
	}

	public void HideNow()
	{
		_generation++;
		_busy = false;
		this.Opacity = 0.0;
		this.IsVisible = false;
		_strip?.SetActive(active: false);
		this.Changed?.Invoke();
	}

	public void TogglePulse(bool on)
	{
		_strip?.SetActive(on);
	}

	/// <summary>
	/// Ждём ли мы завершения длинной операции (проверка библиотеки, чистка).
	/// Пока ждём — плашка не гаснет сама и не прячется по таймеру.
	/// </summary>
	public bool Busy { get; private set; }

	public void SetBusy(bool busy)
	{
		Busy = busy;
		int generation = ++_generation;
		if (busy)
		{
			_busy = true;
			this.IsVisible = true;
			this.Opacity = 1.0;
			_strip?.SetActive(active: true);
			this.Changed?.Invoke();
			return;
		}
		// конец длинной операции — убираем плашку, но не мгновенно, чтобы не мигала
		Hide();
	}
}
