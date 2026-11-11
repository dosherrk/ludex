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

namespace Ludex.Services;

public static class Storage
{
	private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
	{
		WriteIndented = true,
		Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
	};

	private static readonly object Sync = new object();

	public static AppSettings LoadSettings()
	{
		try
		{
			if (File.Exists(AppPaths.SettingsFile))
			{
				AppSettings appSettings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(AppPaths.SettingsFile), Options);
				if (appSettings != null)
				{
					return appSettings;
				}
			}
		}
		catch (Exception ex)
		{
			AppPaths.Log("LoadSettings failed: " + ex);
		}
		return new AppSettings();
	}

	public static void SaveSettings(AppSettings settings)
	{
		lock (Sync)
		{
			try
			{
				File.WriteAllText(AppPaths.SettingsFile, JsonSerializer.Serialize(settings, Options));
			}
			catch (Exception ex)
			{
				AppPaths.Log("SaveSettings failed: " + ex);
			}
		}
	}

	public static List<GameEntry> LoadGames()
	{
		try
		{
			if (File.Exists(AppPaths.LibraryFile))
			{
				LibraryFile libraryFile = JsonSerializer.Deserialize<LibraryFile>(File.ReadAllText(AppPaths.LibraryFile), Options);
				if (libraryFile?.Games != null)
				{
					return libraryFile.Games;
				}
			}
		}
		catch (Exception ex)
		{
			AppPaths.Log("LoadGames failed: " + ex);
		}
		return new List<GameEntry>();
	}

	public static void SaveGames(IEnumerable<GameEntry> games)
	{
		lock (Sync)
		{
			try
			{
				LibraryFile value = new LibraryFile
				{
					Games = new List<GameEntry>(games)
				};
				string text = AppPaths.LibraryFile + ".tmp";
				File.WriteAllText(text, JsonSerializer.Serialize(value, Options));
				File.Move(text, AppPaths.LibraryFile, overwrite: true);
			}
			catch (Exception ex)
			{
				AppPaths.Log("SaveGames failed: " + ex);
			}
		}
	}
}
