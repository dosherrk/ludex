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

public static class AppPaths
{
	private static string? _root;

	public static string Root
	{
		get
		{
			if (_root != null)
			{
				return _root;
			}
			string text = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
			if (string.IsNullOrWhiteSpace(text))
			{
				text = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), ".local", "share");
			}
			_root = System.IO.Path.Combine(text, "Ludex");
			if (!Directory.Exists(_root))
			{
				string text2 = System.IO.Path.Combine(text, "GameVault");
				if (Directory.Exists(text2))
				{
					try
					{
						CopyDirectory(text2, _root);
						Log("Migrated data from " + text2);
					}
					catch (Exception ex)
					{
						Log("Migration failed: " + ex.Message);
					}
				}
			}
			Directory.CreateDirectory(_root);
			return _root;
		}
	}

	public static string SettingsFile => System.IO.Path.Combine(Root, "settings.json");

	public static string LibraryFile => System.IO.Path.Combine(Root, "library.json");

	public static string CoversDir
	{
		get
		{
			string text = System.IO.Path.Combine(Root, "covers");
			Directory.CreateDirectory(text);
			return text;
		}
	}

	public static string LogFile => System.IO.Path.Combine(Root, "log.txt");

	private static void CopyDirectory(string from, string to)
	{
		Directory.CreateDirectory(to);
		string[] directories = Directory.GetDirectories(from, "*", SearchOption.AllDirectories);
		for (int i = 0; i < directories.Length; i++)
		{
			Directory.CreateDirectory(directories[i].Replace(from, to));
		}
		directories = Directory.GetFiles(from, "*", SearchOption.AllDirectories);
		string[] array = directories;
		foreach (string obj in array)
		{
			File.Copy(obj, obj.Replace(from, to), overwrite: true);
		}
	}

	/// <summary>
	/// Второй журнал, рядом с программой. Первый (%LOCALAPPDATA%\Ludex\log.txt) может
	/// не создаться — например, если папку удалил установщик или её закрывает политика.
	/// Тогда диагностика старта терялась, и падение выглядело как «процесс без окна».
	/// </summary>
	public static string FallbackLogFile
	{
		get
		{
			try
			{
				string dir = System.IO.Path.GetDirectoryName(Environment.ProcessPath ?? "") ?? "";
				if (dir.Length > 0)
				{
					return System.IO.Path.Combine(dir, "ludex-startup.log");
				}
			}
			catch
			{
			}
			return System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ludex-startup.log");
		}
	}

	/// <summary>Все места, где искать журнал (для сообщений пользователю).</summary>
	public static string LogFilesHint => LogFile + "\n" + FallbackLogFile;

	public static void Log(string message)
	{
		string line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  {message}{Environment.NewLine}";
		try
		{
			File.AppendAllText(LogFile, line);
		}
		catch
		{
			try
			{
				File.AppendAllText(FallbackLogFile, line);
			}
			catch
			{
			}
		}
	}
}
