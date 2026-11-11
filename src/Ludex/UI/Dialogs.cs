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

public static class Dialogs
{
	private static Window CreateShell(string title, double width, Control content)
	{
		Control content2 = content;
		Window val = new Window
		{
			Title = title,
			Width = width,
			SizeToContent = (SizeToContent)2,
			CanResize = false,
			WindowStartupLocation = (WindowStartupLocation)2,
			Background = Skin.Panel,
			ShowInTaskbar = false,
			Content = content2
		};
		Ui.FadeSlideIn((Visual)(object)content2, 0, 180, 8.0);
		((TopLevel)val).Opened += delegate
		{
			Ui.FadeSlideIn((Visual)(object)content2, 0, 170, 8.0);
		};
		return val;
	}

	public static async Task<bool> ConfirmAsync(Window owner, string title, string text, string okText, string cancelText)
	{
		bool result = false;
		Window window = null;
		TextBlock val = Ui.T(text, 13.0, Skin.TextMuted, null, (TextWrapping)1);
		((Layoutable)val).MaxWidth = 420.0;
		StackPanel val2 = new StackPanel
		{
			Margin = new Thickness(24.0),
			Spacing = 20.0
		};
		((AvaloniaList<Control>)(object)((Panel)val2).Children).Add((Control)(object)Ui.T(title, 18.0, Skin.Text, (FontWeight)600, (TextWrapping)0));
		((AvaloniaList<Control>)(object)((Panel)val2).Children).Add((Control)(object)val);
		StackPanel val3 = new StackPanel
		{
			Orientation = (Orientation)0,
			HorizontalAlignment = (HorizontalAlignment)3,
			Spacing = 10.0
		};
		((AvaloniaList<Control>)(object)((Panel)val3).Children).Add((Control)(object)Ui.GhostButton(cancelText, delegate
		{
			Window obj2 = window;
			if (obj2 != null)
			{
				obj2.Close();
			}
		}));
		((AvaloniaList<Control>)(object)((Panel)val3).Children).Add((Control)(object)Ui.PrimaryButton(okText, delegate
		{
			result = true;
			Window obj = window;
			if (obj != null)
			{
				obj.Close();
			}
		}));
		((AvaloniaList<Control>)(object)((Panel)val2).Children).Add((Control)(object)val3);
		window = CreateShell(title, 480.0, (Control)(object)val2);
		await window.ShowDialog(owner);
		return result;
	}

	public static async Task<string?> PromptAsync(Window owner, string title, string initial)
	{
		string result = null;
		Window window = null;
		TextBox box = new TextBox
		{
			Text = initial,
			FontSize = 14.0,
			Height = 38.0,
			Background = Skin.PanelAlt,
			Foreground = Skin.Text,
			BorderBrush = Skin.Border,
			BorderThickness = new Thickness(1.0),
			CornerRadius = new CornerRadius(6.0),
			Padding = new Thickness(10.0, 0.0, 10.0, 0.0),
			VerticalContentAlignment = (VerticalAlignment)2
		};
		Ui.AnimateTextBox(box);
		StackPanel val = new StackPanel
		{
			Margin = new Thickness(24.0),
			Spacing = 16.0
		};
		((AvaloniaList<Control>)(object)((Panel)val).Children).Add((Control)(object)Ui.T(title, 18.0, Skin.Text, (FontWeight)600, (TextWrapping)0));
		((AvaloniaList<Control>)(object)((Panel)val).Children).Add((Control)(object)box);
		StackPanel val2 = new StackPanel
		{
			Orientation = (Orientation)0,
			HorizontalAlignment = (HorizontalAlignment)3,
			Spacing = 10.0
		};
		((AvaloniaList<Control>)(object)((Panel)val2).Children).Add((Control)(object)Ui.GhostButton(Loc.T("btn.cancel"), delegate
		{
			Window obj2 = window;
			if (obj2 != null)
			{
				obj2.Close();
			}
		}));
		((AvaloniaList<Control>)(object)((Panel)val2).Children).Add((Control)(object)Ui.PrimaryButton(Loc.T("btn.save"), Accept));
		((AvaloniaList<Control>)(object)((Panel)val).Children).Add((Control)(object)val2);
		window = CreateShell(title, 460.0, (Control)(object)val);
		((InputElement)box).KeyDown += delegate(object? _, KeyEventArgs e)
		{
			if ((int)e.Key == 6)
			{
				Accept();
			}
			else if ((int)e.Key == 13)
			{
				window.Close();
			}
		};
		((TopLevel)window).Opened += delegate
		{
			((InputElement)box).Focus((NavigationMethod)0, (KeyModifiers)0);
			box.SelectAll();
		};
		await window.ShowDialog(owner);
		return string.IsNullOrWhiteSpace(result) ? null : result;
		void Accept()
		{
			result = box.Text?.Trim();
			Window obj = window;
			if (obj != null)
			{
				obj.Close();
			}
		}
	}

	public static void ShowError(Window owner, string text)
	{
		ShowMessageAsync(owner, "Ludex", text);
	}

	public static async Task ShowMessageAsync(Window owner, string title, string text)
	{
		Window window = null;
		StackPanel val = new StackPanel
		{
			Margin = new Thickness(24.0),
			Spacing = 18.0
		};
		((AvaloniaList<Control>)(object)((Panel)val).Children).Add((Control)(object)Ui.T(title, 18.0, Skin.Text, (FontWeight)600, (TextWrapping)0));
		TextBlock val2 = Ui.T(text, 13.0, Skin.TextMuted, null, (TextWrapping)1);
		((Layoutable)val2).MaxWidth = 420.0;
		((AvaloniaList<Control>)(object)((Panel)val).Children).Add((Control)(object)val2);
		Border val3 = Ui.PrimaryButton("OK", delegate
		{
			Window obj = window;
			if (obj != null)
			{
				obj.Close();
			}
		});
		((Layoutable)val3).HorizontalAlignment = (HorizontalAlignment)3;
		((AvaloniaList<Control>)(object)((Panel)val).Children).Add((Control)(object)val3);
		window = CreateShell(title, 470.0, (Control)(object)val);
		await window.ShowDialog(owner);
	}

	public static async Task<bool> UpdateAvailableAsync(Window owner, UpdateInfo info)
	{
		bool result = false;
		Window window = null;
		StackPanel val = new StackPanel
		{
			Margin = new Thickness(24.0),
			Spacing = 18.0
		};
		((AvaloniaList<Control>)(object)((Panel)val).Children).Add((Control)(object)Ui.T(Loc.T("update.title"), 18.0, Skin.Text, (FontWeight)600, (TextWrapping)0));
		string text = Loc.F("update.available", info.Version.ToString(3), Updater.CurrentVersionText);
		if (!string.IsNullOrEmpty(info.AssetName))
		{
			text = text + "\n\n" + info.AssetName + ((info.SizeText.Length > 0) ? (" · " + info.SizeText) : "");
		}
		TextBlock val2 = Ui.T(text, 13.0, Skin.TextMuted, null, (TextWrapping)1);
		((Layoutable)val2).MaxWidth = 440.0;
		((AvaloniaList<Control>)(object)((Panel)val).Children).Add((Control)(object)val2);
		StackPanel val3 = new StackPanel
		{
			Orientation = (Orientation)0,
			HorizontalAlignment = (HorizontalAlignment)3,
			Spacing = 10.0
		};
		((AvaloniaList<Control>)(object)((Panel)val3).Children).Add((Control)(object)Ui.GhostButton(Loc.T("update.later"), delegate
		{
			Window obj2 = window;
			if (obj2 != null)
			{
				obj2.Close();
			}
		}));
		((AvaloniaList<Control>)(object)((Panel)val3).Children).Add((Control)(object)Ui.PrimaryButton(Loc.T("update.now"), delegate
		{
			result = true;
			Window obj = window;
			if (obj != null)
			{
				obj.Close();
			}
		}));
		((AvaloniaList<Control>)(object)((Panel)val).Children).Add((Control)(object)val3);
		window = CreateShell(Loc.T("update.title"), 480.0, (Control)(object)val);
		await window.ShowDialog(owner);
		return result;
	}

	public static async Task ShowProgressAsync(Window owner, string title, string hint, Func<IProgress<double>, Task> work)
	{
		Func<IProgress<double>, Task> work2 = work;
		Exception error = null;
		Window window = null;
		ProgressBar bar = new ProgressBar
		{
			Height = 6.0,
			Minimum = 0.0,
			Maximum = 1.0,
			Value = 0.0,
			Background = Skin.PanelAlt,
			Foreground = Skin.Accent,
			CornerRadius = new CornerRadius(3.0)
		};
		StackPanel val = new StackPanel
		{
			Margin = new Thickness(24.0),
			Spacing = 16.0
		};
		((AvaloniaList<Control>)(object)((Panel)val).Children).Add((Control)(object)Ui.T(title, 18.0, Skin.Text, (FontWeight)600, (TextWrapping)0));
		TextBlock val2 = Ui.T(hint, 12.5, Skin.TextMuted, null, (TextWrapping)1);
		((Layoutable)val2).MaxWidth = 420.0;
		((AvaloniaList<Control>)(object)((Panel)val).Children).Add((Control)(object)val2);
		((AvaloniaList<Control>)(object)((Panel)val).Children).Add((Control)(object)bar);
		window = CreateShell(title, 460.0, (Control)(object)val);
		window.CanResize = false;
		Progress<double> progress = new Progress<double>(delegate(double v)
		{
			((RangeBase)bar).Value = Math.Clamp(double.IsNaN(v) ? 0.0 : v, 0.0, 1.0);
		});
		((TopLevel)window).Opened += async delegate
		{
			try
			{
				await work2(progress);
			}
			catch (Exception ex)
			{
				error = ex;
			}
			Dispatcher.UIThread.Post((Action)delegate
			{
				window.Close();
			}, default(DispatcherPriority));
		};
		await window.ShowDialog(owner);
		if (error != null)
		{
			throw error;
		}
	}
}
