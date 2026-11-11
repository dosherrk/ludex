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

namespace Ludex;

public sealed class GameEntry
{
	public string Id { get; set; } = Guid.NewGuid().ToString("N");


	public string Title { get; set; } = "";


	public string ExePath { get; set; } = "";


	public string FolderPath { get; set; } = "";


	public long SizeBytes { get; set; }

	public DateTime AddedUtc { get; set; } = DateTime.UtcNow;


	public DateTime? LastPlayedUtc { get; set; }

	public int PlayCount { get; set; }

	public int? SteamAppId { get; set; }

	public string? SteamName { get; set; }

	public string? ExternalId { get; set; }

	public string? CoverFile { get; set; }

	public bool CoverIsPortrait { get; set; }

	public bool Favorite { get; set; }

	public string? Source { get; set; }

	public bool FromLauncher { get; set; }

	public bool Locked { get; set; }

	public bool ExeExists
	{
		get
		{
			if (!string.IsNullOrEmpty(ExePath))
			{
				return File.Exists(ExePath);
			}
			return false;
		}
	}

	public string FolderName => System.IO.Path.GetFileName(FolderPath.TrimEnd('\\', '/'));
}

public sealed class AppSettings
{
	public List<string> Folders { get; set; } = new List<string>();


	public string Language { get; set; } = "ru";


	public string? SteamGridDbKey { get; set; }

	public string SortMode { get; set; } = "title";


	public bool AutoScanOnStartup { get; set; }

	public bool CheckUpdatesOnStartup { get; set; } = true;


	public string Theme { get; set; } = "dark";


	public string Accent { get; set; } = "white";


	public bool Transparent { get; set; } = true;


	public string? BackgroundPath { get; set; }

	public List<string> ExtraIgnoreExe { get; set; } = new List<string>();


	public double WindowWidth { get; set; } = 1280.0;


	public double WindowHeight { get; set; } = 820.0;


	public bool WindowMaximized { get; set; }

	public bool AutoScan { get; set; }

	public bool CloseToTray { get; set; } = true;


	public string LastSearch { get; set; } = "";


	public string SearchEngine { get; set; } = "google";


	public List<string> Launchers { get; set; } = new List<string>();


	public bool LaunchersInitialized { get; set; }

	public List<string> HiddenGames { get; set; } = new List<string>();


	public Dictionary<string, string> HiddenTitles { get; set; } = new Dictionary<string, string>();

}

public sealed class LibraryFile
{
	public List<GameEntry> Games { get; set; } = new List<GameEntry>();

}
