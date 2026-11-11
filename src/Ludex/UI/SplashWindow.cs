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

public sealed class SplashWindow : Window
{
	private const double TrackWidth = 220.0;

	private const double Segment = 64.0;

	private readonly Border _fill;

	private readonly DispatcherTimer _timer;

	private readonly DateTime _started = DateTime.UtcNow;

	private bool _finished;

	public SplashWindow()
	{
		((Window)this).Title = "Ludex";
		((Window)this).SystemDecorations = (SystemDecorations)0;
		((Window)this).CanResize = false;
		((Window)this).ShowInTaskbar = false;
		((Window)this).WindowStartupLocation = (WindowStartupLocation)1;
		((Layoutable)this).Width = 340.0;
		((Layoutable)this).Height = 190.0;
		((TemplatedControl)this).Background = (IBrush)new SolidColorBrush(Skin.BgColor, 1.0);
		((WindowBase)this).Topmost = false;
		((Window)this).ShowActivated = false;
		((InputElement)this).Focusable = false;
		_fill = new Border
		{
			Width = 64.0,
			Height = 3.0,
			CornerRadius = new CornerRadius(2.0),
			Background = Skin.Accent,
			HorizontalAlignment = (HorizontalAlignment)1
		};
		Border val = new Border
		{
			Width = 220.0,
			Height = 3.0,
			CornerRadius = new CornerRadius(2.0),
			Background = Skin.PanelAlt,
			HorizontalAlignment = (HorizontalAlignment)0,
			ClipToBounds = true
		};
		Canvas val2 = new Canvas
		{
			Width = 220.0,
			Height = 3.0
		};
		((AvaloniaList<Control>)(object)((Panel)val2).Children).Add((Control)(object)_fill);
		((Decorator)val).Child = (Control)val2;
		Border val3 = val;
		TextBlock val4 = Ui.T("LUDEX", 21.0, Skin.Text, (FontWeight)700, (TextWrapping)0);
		val4.TextAlignment = (TextAlignment)1;
		((Layoutable)val4).HorizontalAlignment = (HorizontalAlignment)0;
		val4.LetterSpacing = 1.2;
		StackPanel val5 = new StackPanel
		{
			Width = 220.0,
			Spacing = 14.0,
			HorizontalAlignment = (HorizontalAlignment)2,
			VerticalAlignment = (VerticalAlignment)2
		};
		((AvaloniaList<Control>)(object)((Panel)val5).Children).Add((Control)(object)val4);
		((AvaloniaList<Control>)(object)((Panel)val5).Children).Add((Control)(object)val3);
		StackPanel child = val5;
		((ContentControl)this).Content = (object)new Border
		{
			Background = (IBrush)new SolidColorBrush(Skin.BgColor, 1.0),
			BorderBrush = Skin.Border,
			BorderThickness = new Thickness(1.0),
			Child = (Control)(object)child
		};
		_timer = new DispatcherTimer
		{
			Interval = TimeSpan.FromMilliseconds(16.0)
		};
		_timer.Tick += delegate
		{
			Animate();
		};
		_timer.Start();
	}

	private void Animate()
	{
		if (!_finished)
		{
			double num = (DateTime.UtcNow - _started).TotalSeconds % 1.4 / 1.4;
			double num2 = ((num < 0.5) ? (num * 2.0) : ((1.0 - num) * 2.0));
			Canvas.SetLeft((AvaloniaObject)(object)_fill, 156.0 * num2);
			_fill.Opacity = 0.35 + 0.65 * Math.Sin(Math.PI * num2);
		}
	}

	public void OpenInto()
	{
		if (_finished)
		{
			return;
		}
		Control stack = ((Decorator)(Border)((ContentControl)this).Content).Child;
		Border val = (Border)((ContentControl)this).Content;
		CubicEaseOut easing = new CubicEaseOut();
		Stopwatch clock = Stopwatch.StartNew();
		int result;
		int ms = ((int.TryParse(Environment.GetEnvironmentVariable("LUDEX_REVEAL_MS"), out result) && result > 0) ? result : 320);
		ScaleTransform scale = new ScaleTransform(1.0, 1.0);
		val.RenderTransformOrigin = RelativePoint.Center;
		val.RenderTransform = (ITransform)(object)scale;
		_timer.Stop();
		DispatcherTimer timer = new DispatcherTimer
		{
			Interval = TimeSpan.FromMilliseconds(12.0)
		};
		timer.Tick += delegate
		{
			double num = Math.Min(1.0, clock.Elapsed.TotalMilliseconds / (double)ms);
			double num2 = ((Easing)easing).Ease(num);
			Canvas.SetLeft((AvaloniaObject)(object)_fill, 156.0 * Math.Min(1.0, num * 1.6));
			((Layoutable)_fill).Width = 64.0 + 156.0 * Math.Min(1.0, num * 1.6);
			_fill.Opacity = 1.0;
			stack.Opacity = 1.0 - num2 * 0.55;
			scale.ScaleX = 1.0 + 0.06 * num2;
			scale.ScaleY = 1.0 + 0.06 * num2;
			if (num >= 1.0)
			{
				timer.Stop();
			}
		};
		timer.Start();
	}

	public void Finish()
	{
		if (_finished)
		{
			return;
		}
		_finished = true;
		_timer.Stop();
		try
		{
			((Window)this).Close();
		}
		catch
		{
		}
	}
}
