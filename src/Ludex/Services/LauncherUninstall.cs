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

public static class LauncherUninstall
{
	public static string? UrlFor(GameEntry game)
	{
		switch (game.Source)
		{
		case "steam":
		{
			if (game.SteamAppId.HasValue)
			{
				return $"steam://uninstall/{game.SteamAppId.Value}";
			}
			if (int.TryParse(game.ExternalId, out var result))
			{
				return $"steam://uninstall/{result}";
			}
			break;
		}
		case "epic":
			if (!string.IsNullOrWhiteSpace(game.ExternalId))
			{
				return "com.epicgames.launcher://apps/" + Uri.EscapeDataString(game.ExternalId) + "?action=uninstall";
			}
			break;
		case "gog":
			if (!string.IsNullOrWhiteSpace(game.ExternalId))
			{
				return "goggalaxy://openGameView/" + Uri.EscapeDataString(game.ExternalId);
			}
			break;
		}
		return null;
	}

	public static bool Available(GameEntry game)
	{
		return UrlFor(game) != null;
	}

	public static string LauncherName(string? source)
	{
		return source switch
		{
			"steam" => "Steam", 
			"epic" => "Epic Games", 
			"gog" => "GOG Galaxy", 
			_ => "лаунчер", 
		};
	}
}
