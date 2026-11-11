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

public sealed class ProgressStrip : Border
{
	private readonly Border _glow;

	private readonly DispatcherTimer _timer;

	private readonly double _width;

	private double _phase;

	public ProgressStrip(double width = 180.0, double height = 4.0)
	{
		_width = width;
		((Layoutable)this).Width = width;
		((Layoutable)this).Height = height;
		((Border)this).CornerRadius = new CornerRadius(height / 2.0);
		((Border)this).Background = Skin.PanelAlt;
		this.ClipToBounds = true;
		((Layoutable)this).VerticalAlignment = (VerticalAlignment)2;
		LinearGradientBrush val = new LinearGradientBrush
		{
			StartPoint = new RelativePoint(0.0, 0.5, (RelativeUnit)0),
			EndPoint = new RelativePoint(1.0, 0.5, (RelativeUnit)0)
		};
		((AvaloniaList<GradientStop>)(object)((GradientBrush)val).GradientStops).Add(new GradientStop(Color.Parse("#00FFFFFF"), 0.0));
		((AvaloniaList<GradientStop>)(object)((GradientBrush)val).GradientStops).Add(new GradientStop(Skin.AccentColor, 0.5));
		((AvaloniaList<GradientStop>)(object)((GradientBrush)val).GradientStops).Add(new GradientStop(Color.Parse("#00FFFFFF"), 1.0));
		_glow = new Border
		{
			Width = Math.Max(60.0, width * 0.45),
			Height = height,
			CornerRadius = new CornerRadius(height / 2.0),
			Background = (IBrush)(object)val,
			HorizontalAlignment = (HorizontalAlignment)1
		};
		((Decorator)this).Child = (Control)(object)_glow;
		_timer = new DispatcherTimer
		{
			Interval = TimeSpan.FromMilliseconds(16.0)
		};
		_timer.Tick += delegate
		{
			_phase += 0.018;
			if (_phase > 1.0)
			{
				_phase -= 1.0;
			}
			double num = _width + ((Layoutable)_glow).Width;
			_glow.RenderTransform = (ITransform)new TranslateTransform(_phase * num - ((Layoutable)_glow).Width, 0.0);
		};
	}

	public void SetActive(bool active)
	{
		this.IsVisible = active;
		if (active)
		{
			_phase = 0.0;
			if (!_timer.IsEnabled)
			{
				_timer.Start();
			}
		}
		else
		{
			_timer.Stop();
		}
	}
}
