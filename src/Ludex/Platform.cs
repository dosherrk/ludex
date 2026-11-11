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

public static class Platform
{
	private const long MinGameFileSize = 65536L;

	private static readonly HashSet<string> GameExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "", ".appimage", ".sh", ".x86_64", ".x86", ".run", ".bin", ".elf", ".exe" };

	private static readonly string[] LibraryNoise = new string[21]
	{
		".so", ".so.", ".dll", ".dylib", ".py", ".jar", ".desktop", ".png", ".jpg", ".jpeg",
		".webp", ".svg", ".txt", ".md", ".json", ".xml", ".cfg", ".ini", ".log", ".pak",
		".dat"
	};

	public static bool IsWindows => OperatingSystem.IsWindows();

	public static bool IsMac => OperatingSystem.IsMacOS();

	public static bool IsLinux => OperatingSystem.IsLinux();

	public static bool SupportsElevation => IsWindows;

	public static string DisplayName
	{
		get
		{
			if (!IsWindows)
			{
				if (!IsMac)
				{
					return "Linux";
				}
				return "macOS";
			}
			return "Windows";
		}
	}

	public static List<FileInfo> GameFiles(DirectoryInfo dir)
	{
		List<FileInfo> list = new List<FileInfo>();
		try
		{
			if (IsWindows)
			{
				list.AddRange(dir.GetFiles("*.exe", SearchOption.TopDirectoryOnly));
				return list;
			}
			FileInfo[] files = dir.GetFiles("*", SearchOption.TopDirectoryOnly);
			foreach (FileInfo fileInfo in files)
			{
				string extension = System.IO.Path.GetExtension(fileInfo.Name);
				if (GameExtensions.Contains(extension))
				{
					string lower = fileInfo.Name.ToLowerInvariant();
					if (!LibraryNoise.Any((string n) => lower.Contains(n, StringComparison.Ordinal)) && IsExecutable(fileInfo) && fileInfo.Length >= 65536)
					{
						list.Add(fileInfo);
					}
				}
			}
		}
		catch
		{
		}
		return list;
	}

	public static List<DirectoryInfo> MacBundles(DirectoryInfo dir)
	{
		if (!IsMac)
		{
			return new List<DirectoryInfo>();
		}
		try
		{
			return dir.GetDirectories("*.app", SearchOption.TopDirectoryOnly).ToList();
		}
		catch
		{
			return new List<DirectoryInfo>();
		}
	}

	public static bool IsBundle(string path)
	{
		if (IsMac)
		{
			return path.EndsWith(".app", StringComparison.OrdinalIgnoreCase);
		}
		return false;
	}

	private static bool IsExecutable(FileInfo file)
	{
		if (OperatingSystem.IsWindows())
		{
			return true;
		}
		try
		{
			return (File.GetUnixFileMode(file.FullName) & (UnixFileMode.OtherExecute | UnixFileMode.GroupExecute | UnixFileMode.UserExecute)) != 0;
		}
		catch
		{
			return false;
		}
	}

	public static void MakeExecutable(string path)
	{
		if (OperatingSystem.IsWindows())
		{
			return;
		}
		try
		{
			UnixFileMode unixFileMode = File.GetUnixFileMode(path);
			UnixFileMode unixFileMode2 = UnixFileMode.OtherExecute | UnixFileMode.GroupExecute | UnixFileMode.UserExecute;
			if ((unixFileMode & unixFileMode2) != unixFileMode2)
			{
				File.SetUnixFileMode(path, unixFileMode | unixFileMode2);
			}
		}
		catch (Exception ex)
		{
			AppPaths.Log("chmod failed " + path + ": " + ex.Message);
		}
	}
}
