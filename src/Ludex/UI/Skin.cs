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

public static class Skin
{
	public static Color BgColor;

	public static Color PanelColor;

	public static Color CardColor;

	public static Color AccentColor;

	public static IBrush Bg;

	public static IBrush BgDeep;

	public static IBrush Panel;

	public static IBrush PanelAlt;

	public static IBrush Card;

	public static IBrush CardHover;

	public static IBrush CardFooter;

	public static IBrush CardPlate;

	public static IBrush CardFooterEdge;

	public static IBrush Accent;

	public static IBrush AccentHover;

	public static IBrush AccentSoft;

	public static IBrush Text;

	public static IBrush TextMuted;

	public static IBrush TextDim;

	public static IBrush Danger;

	public static IBrush DangerSoft;

	public static IBrush Border;

	public static IBrush Overlay;

	public static IBrush Shadow;

	public static IBrush GlassEdge;

	public static IBrush GlassSheen;

	public static IBrush OnAccent;

	public static IBrush PlaceholderText;

	public static IBrush PlaceholderTextDim;

	public static IBrush PlaceholderGradientA;

	public static BoxShadows CardShadow;

	public static IBrush ToastBg;

	public static IBrush ToastEdge;

	public static readonly (string Id, string Ru, string En, string Dark, string Light)[] Accents;

	public static string ThemeName { get; private set; }

	public static string AccentName { get; private set; }

	public static bool Transparent { get; private set; }

	public static bool IsLight => ThemeName == "light";

	private static void UpdateOnAccent()
	{
		OnAccent = (IBrush)new SolidColorBrush(((0.2126 * (double)(int)((Color)AccentColor).R + 0.7152 * (double)(int)((Color)AccentColor).G + 0.0722 * (double)(int)((Color)AccentColor).B) / 255.0 > 0.6) ? Color.Parse("#0B0B0D") : Colors.White, 1.0);
	}

	public static void ApplyAccent(string? accent)
	{
		if (string.IsNullOrWhiteSpace(accent))
		{
			return;
		}
		string name = accent.Trim().ToLowerInvariant();
		Color color = default(Color);
		Color val;
		if (name.StartsWith("#") && Color.TryParse(name, out color))
		{
			val = (IsLight ? FitForLightTheme(color) : EnsureReadable(color));
		}
		else
		{
			(string, string, string, string, string) tuple = Array.Find(Accents, ((string Id, string Ru, string En, string Dark, string Light) a) => a.Id == name);
			if (tuple.Item1 == null)
			{
				return;
			}
			val = Color.Parse(IsLight ? tuple.Item5 : tuple.Item4);
		}
		AccentColor = val;
		Accent = (IBrush)new SolidColorBrush(val, 1.0);
		AccentHover = (IBrush)new SolidColorBrush(Lighten(val, 0.14), 1.0);
		AccentSoft = (IBrush)new SolidColorBrush(Alpha(val, 56), 1.0);
	}

	static Skin()
	{
		ThemeName = "dark";
		AccentName = "white";
		Transparent = true;
		BgColor = Color.Parse("#121215");
		PanelColor = Color.Parse("#1A1A1F");
		CardColor = Color.Parse("#1A1A1F");
		AccentColor = Color.Parse("#3B9EFF");
		Bg = (IBrush)(object)Brushes.Transparent;
		BgDeep = (IBrush)(object)Brushes.Transparent;
		Panel = (IBrush)(object)Brushes.Transparent;
		PanelAlt = (IBrush)(object)Brushes.Transparent;
		Card = (IBrush)(object)Brushes.Transparent;
		CardHover = (IBrush)(object)Brushes.Transparent;
		CardFooter = (IBrush)(object)Brushes.Transparent;
		CardPlate = (IBrush)(object)Brushes.Transparent;
		CardFooterEdge = (IBrush)(object)Brushes.Transparent;
		Accent = (IBrush)(object)Brushes.Transparent;
		AccentHover = (IBrush)(object)Brushes.Transparent;
		AccentSoft = (IBrush)(object)Brushes.Transparent;
		Text = (IBrush)(object)Brushes.Transparent;
		TextMuted = (IBrush)(object)Brushes.Transparent;
		TextDim = (IBrush)(object)Brushes.Transparent;
		Danger = (IBrush)(object)Brushes.Transparent;
		DangerSoft = (IBrush)(object)Brushes.Transparent;
		Border = (IBrush)(object)Brushes.Transparent;
		Overlay = (IBrush)(object)Brushes.Transparent;
		Shadow = (IBrush)(object)Brushes.Transparent;
		GlassEdge = (IBrush)(object)Brushes.Transparent;
		GlassSheen = (IBrush)(object)Brushes.Transparent;
		OnAccent = (IBrush)(object)Brushes.White;
		PlaceholderText = (IBrush)(object)Brushes.Transparent;
		PlaceholderTextDim = (IBrush)(object)Brushes.Transparent;
		PlaceholderGradientA = (IBrush)(object)Brushes.Transparent;
		CardShadow = default(BoxShadows);
		ToastBg = (IBrush)(object)Brushes.Transparent;
		ToastEdge = (IBrush)(object)Brushes.Transparent;
		Accents = new(string, string, string, string, string)[8]
		{
			("white", "Белый", "White", "#FFFFFF", "#111114"),
			("blue", "Синий", "Blue", "#3B9EFF", "#1F7FD6"),
			("violet", "Фиолетовый", "Violet", "#9B6BFF", "#7C4DFF"),
			("green", "Зелёный", "Green", "#35C48A", "#1FA472"),
			("orange", "Оранжевый", "Orange", "#FF9A4D", "#E07B1F"),
			("pink", "Розовый", "Pink", "#F06292", "#D64A7B"),
			("red", "Красный", "Red", "#E2574C", "#C8443A"),
			("turquoise", "Бирюзовый", "Teal", "#2ECFD3", "#12A0A6")
		};
		Apply("dark", "white", true);
	}

	private static Color Alpha(Color color, byte alpha)
	{
		return Color.FromArgb(alpha, ((Color)color).R, ((Color)color).G, ((Color)color).B);
	}

	public static void Apply(string? theme, string? accent = null, bool? transparent = null)
	{
		ThemeName = (string.Equals(theme, "light", StringComparison.OrdinalIgnoreCase) ? "light" : "dark");
		AccentName = (string.IsNullOrWhiteSpace(accent) ? "white" : accent.Trim().ToLowerInvariant());
		if (transparent.HasValue)
		{
			Transparent = transparent.Value;
		}
		Application current = Application.Current;
		if (current != null)
		{
			ThemeVariant val = (IsLight ? ThemeVariant.Light : ThemeVariant.Dark);
			if (current.RequestedThemeVariant != val)
			{
				current.RequestedThemeVariant = val;
			}
		}
		Color color = default(Color);
		if (AccentName.StartsWith("#") && Color.TryParse(AccentName, out color))
		{
			Color val2 = EnsureReadable(color);
			Color val3 = FitForLightTheme(color);
			AccentColor = (IsLight ? val3 : val2);
		}
		else
		{
			(string, string, string, string, string) tuple = Array.Find(Accents, ((string Id, string Ru, string En, string Dark, string Light) a) => a.Id == AccentName);
			if (tuple.Item1 == null)
			{
				AccentName = "white";
				tuple = Accents[0];
			}
			AccentColor = Color.Parse(IsLight ? tuple.Item5 : tuple.Item4);
		}
		byte alpha = (byte)(Transparent ? 238u : 255u);
		byte alpha2 = (byte)(Transparent ? 242u : 255u);
		byte alpha3 = (byte)(Transparent ? 244u : 255u);
		byte alpha4 = (byte)(Transparent ? 234u : 255u);
		byte alpha5 = (byte)(Transparent ? 26u : 255u);
		_ = Transparent;
		_ = Transparent;
		if (IsLight)
		{
			BgColor = Color.Parse("#EDF1F6");
			PanelColor = Color.Parse("#FFFFFF");
			CardColor = Color.Parse("#FFFFFF");
			Bg = (IBrush)new SolidColorBrush(Alpha(BgColor, alpha), 1.0);
			BgDeep = (IBrush)new SolidColorBrush(Alpha(Color.Parse("#FFFFFF"), alpha2), 1.0);
			Panel = (IBrush)new SolidColorBrush(Alpha(PanelColor, alpha3), 1.0);
			PanelAlt = (IBrush)new SolidColorBrush(Alpha(Color.Parse("#E7EDF4"), alpha4), 1.0);
			Card = (IBrush)new SolidColorBrush(Alpha(CardColor, alpha5), 1.0);
			CardHover = (IBrush)new SolidColorBrush(Alpha(Color.Parse("#F3F8FD"), (byte)(Transparent ? 153u : 244u)), 1.0);
			IBrush cardFooter;
			if (!Transparent)
			{
				cardFooter = (IBrush)new SolidColorBrush(Color.Parse("#FFFFFF"), 1.0);
			}
			else
			{
				LinearGradientBrush val4 = new LinearGradientBrush
				{
					StartPoint = new RelativePoint(0.0, 0.0, (RelativeUnit)0),
					EndPoint = new RelativePoint(0.0, 1.0, (RelativeUnit)0)
				};
				((AvaloniaList<GradientStop>)(object)((GradientBrush)val4).GradientStops).Add(new GradientStop(Alpha(Color.Parse("#FFFFFF"), 228), 0.0));
				((AvaloniaList<GradientStop>)(object)((GradientBrush)val4).GradientStops).Add(new GradientStop(Alpha(Color.Parse("#EDF3FA"), 198), 1.0));
				cardFooter = (IBrush)val4;
			}
			CardFooter = cardFooter;
			CardFooterEdge = (IBrush)new SolidColorBrush(Transparent ? Color.Parse("#B3FFFFFF") : Color.Parse("#E7EDF4"), 1.0);
			CardPlate = (IBrush)new SolidColorBrush(Color.Parse("#FFFFFF"), 1.0);
			GlassEdge = (IBrush)new SolidColorBrush(Color.Parse("#8CFFFFFF"), 1.0);
			LinearGradientBrush val5 = new LinearGradientBrush
			{
				StartPoint = new RelativePoint(0.0, 0.0, (RelativeUnit)0),
				EndPoint = new RelativePoint(0.0, 1.0, (RelativeUnit)0)
			};
			((AvaloniaList<GradientStop>)(object)((GradientBrush)val5).GradientStops).Add(new GradientStop(Color.Parse("#59FFFFFF"), 0.0));
			((AvaloniaList<GradientStop>)(object)((GradientBrush)val5).GradientStops).Add(new GradientStop(Color.Parse("#00FFFFFF"), 0.45));
			GlassSheen = (IBrush)val5;
			Accent = (IBrush)new SolidColorBrush(AccentColor, 1.0);
			AccentHover = (IBrush)new SolidColorBrush(Lighten(AccentColor, 0.14), 1.0);
			AccentSoft = (IBrush)new SolidColorBrush(Alpha(AccentColor, 56), 1.0);
			Text = (IBrush)new SolidColorBrush(Color.Parse("#16202B"), 1.0);
			TextMuted = (IBrush)new SolidColorBrush(Color.Parse("#4E6072"), 1.0);
			TextDim = (IBrush)new SolidColorBrush(Color.Parse("#7C8C9C"), 1.0);
			Danger = (IBrush)new SolidColorBrush(Color.Parse("#C8443A"), 1.0);
			DangerSoft = (IBrush)new SolidColorBrush(Color.Parse("#2EC8443A"), 1.0);
			Border = (IBrush)new SolidColorBrush(Color.Parse("#D5DEE8"), 1.0);
			Overlay = (IBrush)new SolidColorBrush(Color.Parse("#CCF1F5FA"), 1.0);
			Shadow = (IBrush)new SolidColorBrush(Color.Parse("#2E000000"), 1.0);
			PlaceholderText = (IBrush)new SolidColorBrush(Color.Parse("#8FA6BC"), 1.0);
			PlaceholderTextDim = (IBrush)new SolidColorBrush(Color.Parse("#7C8C9C"), 1.0);
			LinearGradientBrush val6 = new LinearGradientBrush
			{
				StartPoint = new RelativePoint(0.0, 0.0, (RelativeUnit)0),
				EndPoint = new RelativePoint(1.0, 1.0, (RelativeUnit)0)
			};
			((AvaloniaList<GradientStop>)(object)((GradientBrush)val6).GradientStops).Add(new GradientStop(Color.Parse("#DCE7F2"), 0.0));
			((AvaloniaList<GradientStop>)(object)((GradientBrush)val6).GradientStops).Add(new GradientStop(Color.Parse("#C2D3E6"), 1.0));
			PlaceholderGradientA = (IBrush)val6;
			BoxShadow val7 = new BoxShadow
			{
				OffsetX = 0.0,
				OffsetY = 2.0,
				Blur = 10.0,
				Color = Color.Parse("#22000000"),
			};
			CardShadow = new BoxShadows(val7);
			ToastBg = (IBrush)new SolidColorBrush(Alpha(Color.Parse("#FFFFFF"), (byte)(Transparent ? 242u : 255u)), 1.0);
			ToastEdge = (IBrush)new SolidColorBrush(Color.Parse("#28202A33"), 1.0);
		}
		else
		{
			BgColor = Color.Parse("#08090C");
			PanelColor = Color.Parse("#101319");
			CardColor = Color.Parse("#12151B");
			Bg = (IBrush)new SolidColorBrush(Alpha(BgColor, alpha), 1.0);
			BgDeep = (IBrush)new SolidColorBrush(Alpha(Color.Parse("#0C0C0F"), alpha2), 1.0);
			Panel = (IBrush)new SolidColorBrush(Alpha(PanelColor, alpha3), 1.0);
			PanelAlt = (IBrush)new SolidColorBrush(Alpha(Color.Parse("#232329"), alpha4), 1.0);
			Card = (IBrush)new SolidColorBrush(Alpha(CardColor, alpha5), 1.0);
			CardHover = (IBrush)new SolidColorBrush(Alpha(Color.Parse("#2C2C34"), (byte)(Transparent ? 112u : 245u)), 1.0);
			IBrush cardFooter2;
			if (!Transparent)
			{
				cardFooter2 = (IBrush)new SolidColorBrush(Color.Parse("#0C0E13"), 1.0);
			}
			else
			{
				LinearGradientBrush val8 = new LinearGradientBrush
				{
					StartPoint = new RelativePoint(0.0, 0.0, (RelativeUnit)0),
					EndPoint = new RelativePoint(0.0, 1.0, (RelativeUnit)0)
				};
				((AvaloniaList<GradientStop>)(object)((GradientBrush)val8).GradientStops).Add(new GradientStop(Alpha(Color.Parse("#1B1B21"), 194), 0.0));
				((AvaloniaList<GradientStop>)(object)((GradientBrush)val8).GradientStops).Add(new GradientStop(Alpha(Color.Parse("#0F0F12"), 160), 1.0));
				cardFooter2 = (IBrush)val8;
			}
			CardFooter = cardFooter2;
			CardFooterEdge = (IBrush)new SolidColorBrush(Transparent ? Color.Parse("#26FFFFFF") : Color.Parse("#1C2029"), 1.0);
			CardPlate = (IBrush)new SolidColorBrush(Color.Parse("#121215"), 1.0);
			LinearGradientBrush val9 = new LinearGradientBrush
			{
				StartPoint = new RelativePoint(0.0, 0.0, (RelativeUnit)0),
				EndPoint = new RelativePoint(0.0, 1.0, (RelativeUnit)0)
			};
			((AvaloniaList<GradientStop>)(object)((GradientBrush)val9).GradientStops).Add(new GradientStop(Color.Parse("#14FFFFFF"), 0.0));
			((AvaloniaList<GradientStop>)(object)((GradientBrush)val9).GradientStops).Add(new GradientStop(Color.Parse("#00FFFFFF"), 0.45));
			GlassSheen = (IBrush)val9;
			GlassEdge = (IBrush)new SolidColorBrush(Color.Parse("#17FFFFFF"), 1.0);
			Accent = (IBrush)new SolidColorBrush(AccentColor, 1.0);
			AccentHover = (IBrush)new SolidColorBrush(Lighten(AccentColor, 0.16), 1.0);
			AccentSoft = (IBrush)new SolidColorBrush(Alpha(AccentColor, 64), 1.0);
			Text = (IBrush)new SolidColorBrush(Color.Parse("#F7F7F8"), 1.0);
			TextMuted = (IBrush)new SolidColorBrush(Color.Parse("#A9A9B2"), 1.0);
			TextDim = (IBrush)new SolidColorBrush(Color.Parse("#76767F"), 1.0);
			Danger = (IBrush)new SolidColorBrush(Color.Parse("#E2574C"), 1.0);
			DangerSoft = (IBrush)new SolidColorBrush(Color.Parse("#28E2574C"), 1.0);
			Border = (IBrush)new SolidColorBrush(Color.Parse("#26262C"), 1.0);
			Overlay = (IBrush)new SolidColorBrush(Color.Parse("#D9030407"), 1.0);
			Shadow = (IBrush)new SolidColorBrush(Color.Parse("#7A000000"), 1.0);
			PlaceholderText = (IBrush)new SolidColorBrush(Color.Parse("#3E3E46"), 1.0);
			PlaceholderTextDim = (IBrush)new SolidColorBrush(Color.Parse("#85858E"), 1.0);
			LinearGradientBrush val10 = new LinearGradientBrush
			{
				StartPoint = new RelativePoint(0.0, 0.0, (RelativeUnit)0),
				EndPoint = new RelativePoint(1.0, 1.0, (RelativeUnit)0)
			};
			((AvaloniaList<GradientStop>)(object)((GradientBrush)val10).GradientStops).Add(new GradientStop(Color.Parse("#232329"), 0.0));
			((AvaloniaList<GradientStop>)(object)((GradientBrush)val10).GradientStops).Add(new GradientStop(Color.Parse("#121215"), 1.0));
			PlaceholderGradientA = (IBrush)val10;
			BoxShadow val11 = new BoxShadow
			{
				OffsetX = 0.0,
				OffsetY = 3.0,
				Blur = 12.0,
				Color = Color.Parse("#66000000"),
			};
			CardShadow = new BoxShadows(val11);
			ToastBg = (IBrush)new SolidColorBrush(Alpha(Color.Parse("#171A21"), (byte)(Transparent ? 247u : 255u)), 1.0);
			ToastEdge = (IBrush)new SolidColorBrush(Color.Parse("#26FFFFFF"), 1.0);
		}
		UpdateOnAccent();
	}

	private static Color FitForLightTheme(Color color)
	{
		Color val = Darken(color, 0.16);
		double num = 0.16;
		while (Lum(val) > 0.3 && num < 1.2)
		{
			num += 0.04;
			val = Darken(color, num);
		}
		return val;
	}

	private static double Lum(Color c)
	{
		return (0.2126 * (double)(int)((Color)c).R + 0.7152 * (double)(int)((Color)c).G + 0.0722 * (double)(int)((Color)c).B) / 255.0;
	}

	private static Color Darken(Color color, double amount)
	{
		return Color.FromArgb(((Color)color).A, D(((Color)color).R), D(((Color)color).G), D(((Color)color).B));
		byte D(byte v)
		{
			return (byte)Math.Clamp((double)(int)v - 255.0 * amount, 0.0, 255.0);
		}
	}

	private static Color EnsureReadable(Color color)
	{
		double num = (0.2126 * (double)(int)((Color)color).R + 0.7152 * (double)(int)((Color)color).G + 0.0722 * (double)(int)((Color)color).B) / 255.0;
		if (!(num >= 0.42))
		{
			return Lighten(color, 0.42 - num);
		}
		return color;
	}

	public static string ColorHex(Color color)
	{
		return $"#{((Color)color).R:X2}{((Color)color).G:X2}{((Color)color).B:X2}".ToLowerInvariant();
	}

	private static Color Lighten(Color color, double amount)
	{
		return Color.FromArgb(((Color)color).A, L(((Color)color).R), L(((Color)color).G), L(((Color)color).B));
		byte L(byte v)
		{
			return (byte)Math.Clamp((double)(int)v + 255.0 * amount, 0.0, 255.0);
		}
	}
}
