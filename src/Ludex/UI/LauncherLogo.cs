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

public static class LauncherLogo
{
	private static readonly Dictionary<string, Bitmap?> Cache = new Dictionary<string, Bitmap>();

	public static Control Create(string key, double size = 32.0)
	{
		(string From, string To) tuple = Palette(key);
		string item = tuple.From;
		string item2 = tuple.To;
		Border val = new Border
		{
			Width = size,
			Height = size,
			CornerRadius = new CornerRadius(size * 0.26),
			ClipToBounds = true,
			VerticalAlignment = (VerticalAlignment)2
		};
		LinearGradientBrush val2 = new LinearGradientBrush
		{
			StartPoint = new RelativePoint(0.0, 0.0, (RelativeUnit)0),
			EndPoint = new RelativePoint(1.0, 1.0, (RelativeUnit)0)
		};
		((AvaloniaList<GradientStop>)(object)((GradientBrush)val2).GradientStops).Add(new GradientStop(Color.Parse(item), 0.0));
		((AvaloniaList<GradientStop>)(object)((GradientBrush)val2).GradientStops).Add(new GradientStop(Color.Parse(item2), 1.0));
		val.Background = (IBrush)val2;
		Border val3 = val;
		Bitmap val4 = Load(key);
		if (val4 != null)
		{
			((Decorator)val3).Child = (Control)new Image
			{
				Source = (IImage)(object)val4,
				Stretch = (Stretch)2,
				Margin = new Thickness(size * 0.07),
				HorizontalAlignment = (HorizontalAlignment)2,
				VerticalAlignment = (VerticalAlignment)2
			};
		}
		else
		{
			TextBlock val5 = Ui.T((key.Length >= 2) ? key.Substring(0, 2).ToUpperInvariant() : key.ToUpperInvariant(), 10.0, (IBrush?)(object)Brushes.White, (FontWeight)700, (TextWrapping)0);
			((Layoutable)val5).HorizontalAlignment = (HorizontalAlignment)2;
			((Layoutable)val5).VerticalAlignment = (VerticalAlignment)2;
			((Decorator)val3).Child = (Control)(object)val5;
		}
		return (Control)(object)val3;
	}

	private static Bitmap? Load(string key)
	{
		if (Cache.TryGetValue(key, out Bitmap value))
		{
			return value;
		}
		Bitmap val = null;
		try
		{
			using Stream stream = AssetLoader.Open(new Uri("avares://Ludex/Assets/logos/" + key + ".png"), (Uri)null);
			val = new Bitmap(stream);
		}
		catch (Exception ex)
		{
			AppPaths.Log("logo " + key + ": " + ex.Message);
		}
		Cache[key] = val;
		return val;
	}

	private static (string From, string To) Palette(string key)
	{
		return key switch
		{
			"steam" => (From: "#2A475E", To: "#101C27"), 
			"epic" => (From: "#2C2C2C", To: "#0A0A0A"), 
			"gog" => (From: "#A13BA8", To: "#5A1B60"), 
			"ubisoft" => (From: "#26262E", To: "#08080C"), 
			"ea" => (From: "#2B2B2B", To: "#0B0B0D"), 
			"battlenet" => (From: "#4E95DB", To: "#1B4C86"), 
			"heroic" => (From: "#7B4BE0", To: "#3A1E78"), 
			"lutris" => (From: "#FFB04A", To: "#D97706"), 
			_ => (From: "#3A3A46", To: "#1A1A22"), 
		};
	}
}
