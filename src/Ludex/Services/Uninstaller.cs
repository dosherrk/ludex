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

public static class Uninstaller
{
	private static readonly string[] NameHints = new string[6] { "uninstall", "unins000", "unins001", "uninst", "деинстал", "удалить игру" };

	private static readonly string[] FileExtensions = new string[6] { ".exe", ".sh", ".command", ".bat", ".cmd", ".run" };

	public static string? Find(string folderPath)
	{
		if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
		{
			return null;
		}
		EnumerationOptions enumerationOptions = new EnumerationOptions
		{
			RecurseSubdirectories = true,
			MaxRecursionDepth = 2,
			IgnoreInaccessible = true,
			AttributesToSkip = FileAttributes.System
		};
		List<string> list = new List<string>();
		try
		{
			foreach (string item in Directory.EnumerateFileSystemEntries(folderPath, "*", enumerationOptions))
			{
				string name = System.IO.Path.GetFileName(item).ToLowerInvariant();
				if (NameHints.Any((string hint) => name.Contains(hint)) && ((name.EndsWith(".app", StringComparison.Ordinal) && Directory.Exists(item)) || FileExtensions.Any((string ext) => name.EndsWith(ext, StringComparison.Ordinal))))
				{
					list.Add(item);
				}
			}
		}
		catch
		{
			return null;
		}
		return list.OrderBy(Rank).ThenBy((string path) => path.Length).FirstOrDefault();
	}

	private static int Rank(string path)
	{
		string text = System.IO.Path.GetFileName(path).ToLowerInvariant();
		if (text.StartsWith("unins000"))
		{
			return 0;
		}
		if (text.StartsWith("uninstall.exe"))
		{
			return 2;
		}
		if (text.EndsWith(".app", StringComparison.Ordinal))
		{
			return 3;
		}
		if (text.EndsWith(".exe", StringComparison.Ordinal))
		{
			return 4;
		}
		if (text.EndsWith(".sh", StringComparison.Ordinal))
		{
			return 5;
		}
		return 6;
	}

	public static bool IsSafeToDelete(string? folderPath, out string reason)
	{
		reason = "";
		if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
		{
			reason = "folder-not-found";
			return false;
		}
		string text;
		try
		{
			text = System.IO.Path.GetFullPath(folderPath).TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
		}
		catch
		{
			reason = "bad-path";
			return false;
		}
		string b = System.IO.Path.GetPathRoot(text)?.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar) ?? "";
		if (string.IsNullOrEmpty(text) || string.Equals(text, b, StringComparison.OrdinalIgnoreCase))
		{
			reason = "drive-root";
			return false;
		}
		if (text.Split(new char[2]
		{
			System.IO.Path.DirectorySeparatorChar,
			System.IO.Path.AltDirectorySeparatorChar
		}, StringSplitOptions.RemoveEmptyEntries).Length < 2)
		{
			reason = "too-shallow";
			return false;
		}
		string folderPath2 = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
		if (!string.IsNullOrEmpty(folderPath2) && string.Equals(text, folderPath2.TrimEnd(System.IO.Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
		{
			reason = "home";
			return false;
		}
		string text2 = AppPaths.Root.TrimEnd(System.IO.Path.DirectorySeparatorChar);
		if (text.StartsWith(text2, StringComparison.OrdinalIgnoreCase) || text2.StartsWith(text, StringComparison.OrdinalIgnoreCase))
		{
			reason = "app-folder";
			return false;
		}
		return true;
	}

	public static async Task<(bool ok, bool usedUninstaller, string error)> RemoveAsync(GameEntry game, Action<string>? status = null)
	{
		string folder = game.FolderPath;
		if (!IsSafeToDelete(folder, out string reason))
		{
			return (ok: false, usedUninstaller: false, error: reason);
		}
		string uninstaller = Find(folder);
		if (uninstaller != null)
		{
			status?.Invoke("uninstaller-started");
			if (TryRunUninstaller(uninstaller, folder))
			{
				await WaitForFolderOrExitAsync(folder, uninstaller, TimeSpan.FromMinutes(10.0));
			}
		}
		if (!Directory.Exists(folder))
		{
			return (ok: true, usedUninstaller: uninstaller != null, error: "");
		}
		status?.Invoke("deleting-folder");
		string text = DeleteFolderWithRetries(folder);
		return (ok: text.Length == 0, usedUninstaller: uninstaller != null, error: text);
	}

	private static bool TryRunUninstaller(string path, string folder)
	{
		try
		{
			ProcessStartInfo processStartInfo = new ProcessStartInfo
			{
				WorkingDirectory = (Directory.Exists(folder) ? folder : (System.IO.Path.GetDirectoryName(path) ?? folder)),
				UseShellExecute = true
			};
			string text = System.IO.Path.GetFileName(path).ToLowerInvariant();
			if (text.EndsWith(".app", StringComparison.Ordinal) && Platform.IsMac)
			{
				processStartInfo.FileName = "open";
				processStartInfo.Arguments = "\"" + path + "\"";
				processStartInfo.UseShellExecute = false;
			}
			else if (text.EndsWith(".sh", StringComparison.Ordinal) || text.EndsWith(".command", StringComparison.Ordinal) || text.EndsWith(".run", StringComparison.Ordinal))
			{
				processStartInfo.FileName = "/bin/sh";
				processStartInfo.Arguments = "\"" + path + "\"";
				processStartInfo.UseShellExecute = false;
			}
			else
			{
				processStartInfo.FileName = path;
			}
			Process.Start(processStartInfo);
			return true;
		}
		catch
		{
			return false;
		}
	}

	private static async Task WaitForFolderOrExitAsync(string folder, string uninstallerPath, TimeSpan timeout)
	{
		string processName = System.IO.Path.GetFileNameWithoutExtension(uninstallerPath);
		DateTime deadline = DateTime.UtcNow + timeout;
		while (DateTime.UtcNow < deadline)
		{
			await Task.Delay(700);
			if (!Directory.Exists(folder))
			{
				break;
			}
			try
			{
				Process[] processesByName = Process.GetProcessesByName(processName);
				if (processesByName.Length == 0)
				{
					break;
				}
				Process[] array = processesByName;
				for (int i = 0; i < array.Length; i++)
				{
					array[i].Dispose();
				}
			}
			catch
			{
				break;
			}
		}
	}

	private static string DeleteFolderWithRetries(string folder)
	{
		for (int i = 0; i < 5; i++)
		{
			try
			{
				Directory.Delete(folder, recursive: true);
				return "";
			}
			catch (Exception ex)
			{
				if (i == 4)
				{
					return ex.Message;
				}
				Thread.Sleep(400 * (i + 1));
			}
		}
		return "";
	}
}
