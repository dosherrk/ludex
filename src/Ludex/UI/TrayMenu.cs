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

public sealed class TrayMenu
{
	public NativeMenu Menu { get; } = new NativeMenu();


	public event Action? ShowWindowRequested;

	public event Action? CatalogRequested;

	public event Action? LibraryRequested;

	public event Action? SettingsRequested;

	public event Action? ExitRequested;

	public TrayMenu()
	{
		Build();
	}

	public void Build()
	{
		Menu.Items.Clear();
		Menu.Items.Add((NativeMenuItemBase)(object)Header("Ludex — библиотека игр"));
		Menu.Items.Add((NativeMenuItemBase)(object)Item(Loc.T("tray.open"), delegate
		{
			this.ShowWindowRequested?.Invoke();
		}));
		Menu.Items.Add((NativeMenuItemBase)new NativeMenuItemSeparator());
		Menu.Items.Add((NativeMenuItemBase)(object)Item(Loc.T("nav.catalog"), delegate
		{
			this.CatalogRequested?.Invoke();
		}));
		Menu.Items.Add((NativeMenuItemBase)(object)Item(Loc.T("nav.library"), delegate
		{
			this.LibraryRequested?.Invoke();
		}));
		Menu.Items.Add((NativeMenuItemBase)(object)Item(Loc.T("nav.settings"), delegate
		{
			this.SettingsRequested?.Invoke();
		}));
		Menu.Items.Add((NativeMenuItemBase)new NativeMenuItemSeparator());
		Menu.Items.Add((NativeMenuItemBase)(object)Item(Loc.T("tray.exit"), delegate
		{
			this.ExitRequested?.Invoke();
		}));
	}

	private static NativeMenuItem Header(string text)
	{
		return new NativeMenuItem(text)
		{
			IsEnabled = false
		};
	}

	private static NativeMenuItem Item(string text, Action action)
	{
		Action action2 = action;
		NativeMenuItem val = new NativeMenuItem(text);
		val.Click += delegate
		{
			action2();
		};
		return val;
	}
}
