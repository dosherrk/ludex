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

public sealed class CustomizePanel : Border
{
	private readonly AppSettings _settings;

	private string? _liveAccent;

	private TextBox _urlBox;

	private Border _applyBtn;

	private TextBlock _status;

	private string _url = "";

	private string? _lastError;

	public event Action? Closed;

	public event Action? AppearanceChanged;

	public event Action? WallpaperChanged;

	public CustomizePanel(AppSettings settings)
	{
		_settings = settings;
		((Layoutable)this).MaxWidth = 640.0;
		((Layoutable)this).MaxHeight = 730.0;
		((Border)this).Background = Skin.Panel;
		((Border)this).CornerRadius = new CornerRadius(14.0);
		this.RenderTransformOrigin = new RelativePoint(0.5, 0.5, (RelativeUnit)0);
		Transitions val = new Transitions();
		((AvaloniaList<ITransition>)val).Add((ITransition)new TransformOperationsTransition
		{
			Property = (AvaloniaProperty)(object)Visual.RenderTransformProperty,
			Duration = TimeSpan.FromMilliseconds(200.0),
			Easing = (Easing)new CubicEaseOut()
		});
		((Animatable)this).Transitions = val;
		BoxShadow val2 = new BoxShadow
		{
			OffsetX = 0.0,
			OffsetY = 12.0,
			Blur = 36.0,
			Color = Color.Parse("#99000000"),
		};
		((Border)this).BoxShadow = new BoxShadows(val2);
		Build();
	}

	private void Build()
	{
		TextBox urlBox = _urlBox;
		_url = ((urlBox != null) ? urlBox.Text : null) ?? _url;
		((Border)this).Background = Skin.Panel;
		DockPanel val = new DockPanel
		{
			Margin = new Thickness(26.0, 20.0, 20.0, 0.0)
		};
		Border val2 = Ui.Clickable((Control)(object)Ui.Icon("M1,1 L11,11 M11,1 L1,11", Skin.TextMuted, 12.0, 12.0, stroke: true), (Action?)delegate
		{
			this.Closed?.Invoke();
		}, Skin.PanelAlt, Skin.CardHover, (CornerRadius?)new CornerRadius(6.0), (Thickness?)new Thickness(10.0, 10.0), (IBrush?)null, (Thickness?)null);
		((Layoutable)val2).VerticalAlignment = (VerticalAlignment)1;
		DockPanel.SetDock((Control)(object)val2, (Dock)2);
		((AvaloniaList<Control>)(object)((Panel)val).Children).Add((Control)(object)val2);
		StackPanel val3 = new StackPanel
		{
			Spacing = 3.0
		};
		((AvaloniaList<Control>)(object)((Panel)val3).Children).Add((Control)(object)Ui.T(Loc.T("custom.title"), 20.0, Skin.Text, (FontWeight)700, (TextWrapping)0));
		((AvaloniaList<Control>)(object)((Panel)val3).Children).Add((Control)(object)Ui.T(Loc.T("settings.customizeHint"), 11.5, Skin.TextDim, null, (TextWrapping)1));
		((AvaloniaList<Control>)(object)((Panel)val).Children).Add((Control)(object)val3);
		StackPanel val4 = new StackPanel
		{
			Margin = new Thickness(26.0, 14.0, 26.0, 4.0),
			Spacing = 12.0
		};
		((AvaloniaList<Control>)(object)((Panel)val4).Children).Add((Control)(object)Ui.T(Loc.T("custom.theme"), 13.5, Skin.Text, (FontWeight)600, (TextWrapping)0));
		StackPanel val5 = new StackPanel
		{
			Orientation = (Orientation)0,
			Spacing = 10.0
		};
		((AvaloniaList<Control>)(object)((Panel)val5).Children).Add((Control)(object)Chip(Loc.T("theme.dark"), _settings.Theme != "light", delegate
		{
			SetTheme("dark");
		}));
		((AvaloniaList<Control>)(object)((Panel)val5).Children).Add((Control)(object)Chip(Loc.T("theme.light"), _settings.Theme == "light", delegate
		{
			SetTheme("light");
		}));
		((AvaloniaList<Control>)(object)((Panel)val4).Children).Add((Control)(object)val5);
		((AvaloniaList<Control>)(object)((Panel)val4).Children).Add((Control)(object)Ui.T(Loc.T("custom.palette"), 13.5, Skin.Text, (FontWeight)600, (TextWrapping)0));
		((AvaloniaList<Control>)(object)((Panel)val4).Children).Add((Control)(object)Ui.T(Loc.T("custom.paletteHint"), 11.0, Skin.TextDim, null, (TextWrapping)1));
		ColorPicker colorPicker = new ColorPicker();
		colorPicker.SetColor(Skin.AccentColor);
		colorPicker.ColorChanged += delegate(Color color)
		{
			string text2 = Skin.ColorHex(color);
			if (!string.Equals(text2, _liveAccent, StringComparison.OrdinalIgnoreCase))
			{
				_liveAccent = text2;
				Skin.ApplyAccent(text2);
			}
		};
		colorPicker.ColorCommitted += delegate(Color color)
		{
			string text = Skin.ColorHex(color);
			if (!string.Equals(_settings.Accent, text, StringComparison.OrdinalIgnoreCase))
			{
				_settings.Accent = text;
				_liveAccent = text;
				Skin.Apply(_settings.Theme, _settings.Accent, _settings.Transparent);
				Storage.SaveSettings(_settings);
				this.AppearanceChanged?.Invoke();
			}
		};
		((AvaloniaList<Control>)(object)((Panel)val4).Children).Add((Control)(object)colorPicker);
		Toggle toggle = new Toggle(Loc.T("custom.transparent"), _settings.Transparent, delegate(bool v)
		{
			_settings.Transparent = v;
			Skin.Apply(_settings.Theme, _settings.Accent, _settings.Transparent);
			Storage.SaveSettings(_settings);
			this.AppearanceChanged?.Invoke();
		}, interactive: true, 13.0);
		((AvaloniaList<Control>)(object)((Panel)val4).Children).Add((Control)(object)toggle);
		((AvaloniaList<Control>)(object)((Panel)val4).Children).Add((Control)(object)Ui.T(Loc.T("custom.transparentHint"), 11.0, Skin.TextDim, null, (TextWrapping)1));
		((AvaloniaList<Control>)(object)((Panel)val4).Children).Add((Control)(object)Ui.T(Loc.T("custom.wallpaper"), 13.5, Skin.Text, (FontWeight)600, (TextWrapping)0));
		((AvaloniaList<Control>)(object)((Panel)val4).Children).Add((Control)(object)Ui.T(Loc.T("custom.wallpaperHint"), 11.0, Skin.TextDim, null, (TextWrapping)1));
		_urlBox = new TextBox
		{
			Text = _url,
			FontSize = 12.5,
			Height = 36.0,
			Background = Skin.PanelAlt,
			Foreground = Skin.Text,
			BorderBrush = Skin.Border,
			BorderThickness = new Thickness(1.0),
			CornerRadius = new CornerRadius(6.0),
			Padding = new Thickness(10.0, 0.0, 10.0, 0.0),
			VerticalContentAlignment = (VerticalAlignment)2,
			Watermark = "https://…/wallpaper.jpg"
		};
		_urlBox.TextChanged += delegate
		{
			_url = _urlBox.Text ?? "";
		};
		Ui.AnimateTextBox(_urlBox);
		((AvaloniaList<Control>)(object)((Panel)val4).Children).Add((Control)(object)_urlBox);
		StackPanel val6 = new StackPanel
		{
			Orientation = (Orientation)0,
			Spacing = 10.0,
			Margin = new Thickness(0.0, 2.0, 0.0, 0.0)
		};
		_applyBtn = Ui.Clickable((Control)(object)Ui.T(Loc.T("custom.apply"), 13.0, Skin.OnAccent, (FontWeight)600, (TextWrapping)0), (Action?)ApplyWallpaper, Skin.Accent, Skin.AccentHover, (CornerRadius?)new CornerRadius(6.0), (Thickness?)new Thickness(18.0, 9.0), (IBrush?)null, (Thickness?)null);
		((AvaloniaList<Control>)(object)((Panel)val6).Children).Add((Control)(object)_applyBtn);
		((AvaloniaList<Control>)(object)((Panel)val6).Children).Add((Control)(object)Ui.GhostButton(Loc.T("custom.clearWallpaper"), RemoveWallpaper));
		((AvaloniaList<Control>)(object)((Panel)val4).Children).Add((Control)(object)val6);
		_status = Ui.T(string.IsNullOrWhiteSpace(_settings.BackgroundPath) ? "" : Loc.T("custom.wallpaperSet"), 11.5, Skin.TextDim, null, (TextWrapping)1);
		((AvaloniaList<Control>)(object)((Panel)val4).Children).Add((Control)(object)_status);
		StackPanel val7 = new StackPanel
		{
			Margin = new Thickness(26.0, 4.0, 26.0, 20.0),
			Orientation = (Orientation)0,
			HorizontalAlignment = (HorizontalAlignment)3
		};
		((AvaloniaList<Control>)(object)((Panel)val7).Children).Add((Control)(object)Ui.GhostButton(Loc.T("settings.back"), delegate
		{
			this.Closed?.Invoke();
		}));
		DockPanel.SetDock((Control)(object)val7, (Dock)1);
		ScrollViewer val8 = new ScrollViewer
		{
			Content = val4,
			VerticalScrollBarVisibility = (ScrollBarVisibility)1,
			Margin = new Thickness(0.0, 12.0, 0.0, 0.0)
		};
		Ui.SmoothScroll(val8);
		DockPanel val9 = new DockPanel();
		DockPanel.SetDock((Control)(object)val, (Dock)3);
		((AvaloniaList<Control>)(object)((Panel)val9).Children).Add((Control)(object)val);
		((AvaloniaList<Control>)(object)((Panel)val9).Children).Add((Control)(object)val7);
		((AvaloniaList<Control>)(object)((Panel)val9).Children).Add((Control)(object)val8);
		((Decorator)this).Child = (Control)(object)val9;
		Ui.FadeSlideIn((Visual)(object)val9, 0, 200, 6.0);
	}

	private Border Chip(string text, bool active, Action onClick)
	{
		return Ui.Clickable((Control)(object)Ui.T(text, 12.5, active ? Skin.OnAccent : Skin.Text, (FontWeight)500, (TextWrapping)0), onClick, active ? Skin.Accent : Skin.PanelAlt, active ? Skin.AccentHover : Skin.CardHover, (CornerRadius?)new CornerRadius(4.0), (Thickness?)new Thickness(18.0, 8.0), (IBrush?)null, (Thickness?)null);
	}

	private void SetTheme(string theme)
	{
		if (!string.Equals(_settings.Theme, theme, StringComparison.OrdinalIgnoreCase))
		{
			_settings.Theme = theme;
			Skin.Apply(_settings.Theme, _settings.Accent, _settings.Transparent);
			Storage.SaveSettings(_settings);
			Build();
			this.AppearanceChanged?.Invoke();
		}
	}

	private async void ApplyWallpaper()
	{
		TextBox urlBox = _urlBox;
		string url = (((urlBox != null) ? urlBox.Text : null) ?? "").Trim();
		if (url.Length == 0)
		{
			SetStatus(Loc.T("custom.wallpaperError"));
			return;
		}
		_applyBtn.Opacity = 0.6;
		((InputElement)_applyBtn).IsEnabled = false;
		SetStatus(Loc.T("custom.downloading"));
		(string, string) obj = await Task.Run(() => Backgrounds.SetFromUrlAsync(url)).ConfigureAwait(continueOnCapturedContext: true);
		string item = obj.Item1;
		string item2 = obj.Item2;
		_lastError = item2;
		_applyBtn.Opacity = 1.0;
		((InputElement)_applyBtn).IsEnabled = true;
		if (item != null)
		{
			_settings.BackgroundPath = item;
			Storage.SaveSettings(_settings);
			SetStatus(Loc.T("custom.wallpaperSet"));
			this.WallpaperChanged?.Invoke();
		}
		else
		{
			SetStatus(Loc.T((_lastError == "page") ? "custom.wallpaperPageError" : "custom.wallpaperError"));
		}
	}

	private void RemoveWallpaper()
	{
		Backgrounds.Clear();
		_settings.BackgroundPath = null;
		Storage.SaveSettings(_settings);
		this.WallpaperChanged?.Invoke();
		Build();
		SetStatus("");
	}

	private void SetStatus(string text)
	{
		if (_status != null)
		{
			_status.Text = text;
		}
	}
}
