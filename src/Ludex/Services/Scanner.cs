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

public sealed class ScanHit
{
	public string FolderPath = "";

	public string ExePath = "";

	public string Title = "";
}

public static class Scanner
{
	private const int MaxDepth = 6;

	private static readonly string[] HardIgnore = new string[56]
	{
		"unins", "uninstall", "unwise", "setup", "install", "installer", "vcredist", "vc_redist", "vcsetup", "dxsetup",
		"dxwebsetup", "dxsetup", "directx", "dotnetfx", "ndp4", "oalinst", "physx", "ue4prereq", "ue5prereq", "prereq",
		"redist", "_redist", "crashreport", "crashpad", "crashhandler", "crashsender", "unitycrashhandler", "steamerrorreporter", "steamsetup", "steamservice",
		"notification_helper", "7za", "7z", "ffmpeg", "ffplay", "python", "pythonw", "qt5", "qt6", "qtwebengineprocess",
		"epicwebhelper", "eossdk", "eac", "eulauncher", "launcher_installer", "update", "updater", "patcher", "unpack", "extract",
		"report", "help", "manual", "readme", "webview", "helper"
	};

	private static readonly string[] SoftIgnore = new string[15]
	{
		"config", "configuration", "settings", "benchmark", "server", "dedicated", "test", "editor", "launcher", "bootstrapper",
		"download", "repair", "swiftshader", "gpu", "diagnostics"
	};

	private static readonly HashSet<string> SkipFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
	{
		"commonredist", "_commonredist", "redist", "redistributables", "directx", "dotnet", "vcredist", "support", "docs", "documentation",
		"manuals", "manual", "soundtrack", "ost", "artbook", "bonus", "system volume information", "$recycle.bin", "recycler", "windows",
		"temp", "tmp", "cache", "$windows.~bt", "$windows.~ws", "node_modules", ".git", "saves", "savegame", "savegames",
		"profiles", "lib", "lib32", "lib64", "plugins", "redist", "redistributable", "locales", "steamapps", "compatdata",
		"pkg"
	};

	private static readonly HashSet<string> GenericFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
	{
		"bin", "binaries", "binary", "x64", "x86", "win64", "win32", "win", "windows", "game",
		"games", "data", "build", "release", "retail", "system", "engine", "client", "app", "application",
		"content", "exe", "runtime", "platform", "linux", "macos", "shipping", "gamedata", "gamefiles"
	};

	private static readonly string[] ProgramWords = new string[115]
	{
		"wallpaperengine", "wallpaper", "livelywallpaper", "обои", "soundpad", "soundpad", "voicemod", "crosshair", "прицел", "aimlab",
		"aimtrainer", "aimbot", "trainer", "тренер", "cheatengine", "cheat", "чит", "obsstudio", "obs64", "streamlabs",
		"prismlive", "xsplit", "discord", "teamspeak", "mumble", "mumble", "skype", "zoom", "teams", "slack",
		"telegram", "whatsapp", "spotify", "foobar", "winamp", "vlc", "mpc", "mediaplayer", "potplayer", "kmplayer",
		"itunes", "fraps", "rtss", "rivatuner", "afterburner", "cpuz", "gpuz", "hwinfo", "aida64", "speccy",
		"benchmark", "cinebench", "furmark", "unigine", "3dmark", "novabench", "userbenchmark", "драйвер", "realtek", "nvidia",
		"geforce", "amdsoftware", "radeon", "armoury", "icue", "razer", "logitech", "corsair", "steelseries", "bloody",
		"a4tech", "antivirus", "kaspersky", "eset", "avast", "avg", "malwarebytes", "defender", "ccleaner", "cleaner",
		"winrar", "winzip", "7zip", "bandizip", "notepad", "putty", "winscp", "photoshop", "gimp", "krita",
		"blender", "audacity", "reaper", "flstudio", "obsidian", "chrome", "firefox", "opera", "browser", "браузер",
		"utorrent", "qbit", "anydesk", "teamviewer", "rustdesk", "parsec", "sunshine", "moonlight", "editor", "редактор",
		"visualstudio", "rider", "pycharm", "intellij", "netbeans"
	};

	private static readonly string[] ProgramWordsExact = new string[6] { "aim", "obs", "cheat", "чит", "vpn", "proxifier" };

	private static readonly HashSet<string> JunkTokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
	{
		"repack", "rip", "portable", "preinstalled", "pre", "installed", "crack", "cracked", "crackfix", "gog",
		"steam", "epic", "fitgirl", "dodi", "xatab", "igruha", "mechaniks", "mechanics", "rune", "codex",
		"plaza", "skidrow", "reloaded", "prophet", "empress", "tenoke", "elamigos", "razor1911", "goldberg", "online",
		"multi", "multiplayer", "update", "dlc", "patch", "fix", "rus", "eng", "ger", "fre",
		"ita", "spa", "pol", "jpn", "chi", "multi12", "multi10", "byte", "torrent", "rutracker",
		"nnm", "club", "by", "rg", "r", "g", "mechanics", "kaos", "tbl", "br",
		"nocd", "drm", "free", "full", "iso", "pc", "version", "ver", "final"
	};

	public static List<ScanHit> Scan(IReadOnlyList<string> roots, Action<string>? onFolder = null, IReadOnlyCollection<string>? extraIgnore = null, Action? onCancelledCheck = null, Func<bool>? isCancelled = null)
	{
		Func<bool> isCancelled2 = isCancelled;
		Action<string> onFolder2 = onFolder;
		List<ScanHit> hits = new List<ScanHit>();
		HashSet<string> seenExe = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		int visited = 0;
		List<string> ignore = new List<string>(HardIgnore);
		if (extraIgnore != null)
		{
			foreach (string item in extraIgnore)
			{
				if (!string.IsNullOrWhiteSpace(item))
				{
					ignore.Add(item.Trim().ToLowerInvariant());
				}
			}
		}
		foreach (string root in roots)
		{
			if (!string.IsNullOrWhiteSpace(root) && Directory.Exists(root))
			{
				try
				{
					DirectoryInfo directoryInfo = new DirectoryInfo(root);
					Walk(directoryInfo, 0, isRoot: true, directoryInfo);
				}
				catch (Exception ex)
				{
					AppPaths.Log("Scan root failed " + root + ": " + ex.Message);
				}
			}
		}
		return hits;
		void Walk(DirectoryInfo dir, int depth, bool isRoot, DirectoryInfo rootDir)
		{
			if ((isCancelled2 == null || !isCancelled2()) && hits.Count <= 4000)
			{
				visited++;
				if (onFolder2 != null && visited % 25 == 0)
				{
					onFolder2(Loc.F("scan.progress", visited));
				}
				DirectoryInfo[] array;
				try
				{
					array = (from d in dir.GetDirectories()
						where (d.Attributes & (FileAttributes.Hidden | FileAttributes.System)) == 0
						where !SkipFolders.Contains(d.Name)
						where !Platform.IsMac || !d.Name.EndsWith(".app", StringComparison.OrdinalIgnoreCase)
						select d).OrderBy<DirectoryInfo, string>((DirectoryInfo d) => d.Name, StringComparer.OrdinalIgnoreCase).ToArray();
				}
				catch
				{
					return;
				}
				if (!isRoot)
				{
					if (Platform.IsMac)
					{
						DirectoryInfo directoryInfo2 = Platform.MacBundles(dir).FirstOrDefault((DirectoryInfo d) => !IsIgnored(d.Name, ignore));
						if (directoryInfo2 != null && seenExe.Add(directoryInfo2.FullName))
						{
							hits.Add(new ScanHit
							{
								FolderPath = dir.FullName,
								ExePath = directoryInfo2.FullName,
								Title = CleanTitle(System.IO.Path.GetFileNameWithoutExtension(directoryInfo2.Name))
							});
							return;
						}
					}
					List<FileInfo> list = (from f in Platform.GameFiles(dir).ToArray()
						where !IsIgnored(f.Name, ignore)
						select f).ToList();
					if (list.Count > 0)
					{
						FileInfo fileInfo = PickBest(list, dir.Name);
						if (fileInfo != null && seenExe.Add(fileInfo.FullName))
						{
							var (raw, folderPath) = BestTitleSource(dir, fileInfo, rootDir);
							hits.Add(new ScanHit
							{
								FolderPath = folderPath,
								ExePath = fileInfo.FullName,
								Title = CleanTitle(raw)
							});
						}
						return;
					}
				}
				else
				{
					try
					{
						List<FileInfo> list2 = (from f in Platform.GameFiles(dir)
							where !IsIgnored(f.Name, ignore)
							select f).ToList();
						if (list2.Count > 0 && array.Length == 0)
						{
							FileInfo fileInfo2 = PickBest(list2, dir.Name);
							if (fileInfo2 != null && seenExe.Add(fileInfo2.FullName))
							{
								hits.Add(new ScanHit
								{
									FolderPath = dir.FullName,
									ExePath = fileInfo2.FullName,
									Title = CleanTitle(dir.Name)
								});
							}
							return;
						}
					}
					catch
					{
					}
				}
				if (depth < 6)
				{
					DirectoryInfo[] array2 = array;
					foreach (DirectoryInfo dir2 in array2)
					{
						if (isCancelled2 != null && isCancelled2())
						{
							break;
						}
						Walk(dir2, depth + 1, isRoot: false, rootDir);
					}
				}
			}
		}
	}

	private static bool IsIgnored(string fileName, List<string> ignore)
	{
		string text = System.IO.Path.GetFileNameWithoutExtension(fileName).ToLowerInvariant();
		foreach (string item in ignore)
		{
			if (text.Contains(item, StringComparison.Ordinal))
			{
				return true;
			}
		}
		return false;
	}

	private static FileInfo? PickBest(List<FileInfo> exes, string folderName)
	{
		FileInfo result = null;
		double num = double.MinValue;
		string text = Normalize(folderName);
		foreach (FileInfo ex in exes)
		{
			double num2 = 0.0;
			string text2 = Normalize(System.IO.Path.GetFileNameWithoutExtension(ex.Name));
			double val = (double)ex.Length / 1024.0 / 1024.0;
			num2 += Math.Min(12.0, Math.Log10(Math.Max(val, 1.0) + 1.0) * 4.0);
			if (text2 == text)
			{
				num2 += 14.0;
			}
			else if (text.Length > 2 && (text2.Contains(text) || text.Contains(text2)))
			{
				num2 += 7.0;
			}
			string[] softIgnore = SoftIgnore;
			foreach (string text3 in softIgnore)
			{
				if (text2.Contains(text3, StringComparison.Ordinal))
				{
					double num3 = num2;
					bool flag;
					switch (text3)
					{
					case "launcher":
					case "config":
					case "settings":
						flag = true;
						break;
					default:
						flag = false;
						break;
					}
					num2 = num3 - (double)(flag ? 8 : 6);
				}
			}
			if (text2.StartsWith("unins", StringComparison.Ordinal))
			{
				num2 -= 100.0;
			}
			if (num2 > num)
			{
				num = num2;
				result = ex;
			}
		}
		return result;
	}

	private static (string title, string folder) BestTitleSource(DirectoryInfo dir, FileInfo exe, DirectoryInfo root)
	{
		DirectoryInfo directoryInfo = dir;
		int num = 0;
		while (directoryInfo != null && num++ < 4 && GenericFolders.Contains(directoryInfo.Name) && directoryInfo.Parent != null && !PathsEqual(directoryInfo.FullName, root.FullName))
		{
			directoryInfo = directoryInfo.Parent;
		}
		string text = directoryInfo?.Name ?? dir.Name;
		string item = directoryInfo?.FullName ?? dir.FullName;
		if (string.IsNullOrWhiteSpace(text) || GenericFolders.Contains(text))
		{
			text = System.IO.Path.GetFileNameWithoutExtension(exe.Name);
		}
		return (title: text, folder: item);
	}

	private static bool PathsEqual(string a, string b)
	{
		return string.Equals(a.TrimEnd('\\', '/'), b.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase);
	}

	public static bool LooksLikeProgram(string title, string exePath)
	{
		string text = Normalize(title) + "|" + Normalize(System.IO.Path.GetFileNameWithoutExtension(exePath ?? "")) + "|" + Normalize(System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(exePath ?? "") ?? ""));
		if (text.Trim('|').Length == 0)
		{
			return false;
		}
		string[] programWords = ProgramWords;
		foreach (string value in programWords)
		{
			if (text.Contains(value, StringComparison.Ordinal))
			{
				return true;
			}
		}
		programWords = text.Split(new char[8] { '|', ' ', '-', '_', '.', ',', '(', ')' }, StringSplitOptions.RemoveEmptyEntries);
		foreach (string value2 in programWords)
		{
			if (ProgramWordsExact.Contains<string>(value2, StringComparer.Ordinal))
			{
				return true;
			}
		}
		return false;
	}

	public static string Normalize(string s)
	{
		return Regex.Replace(s.ToLowerInvariant(), "[^a-z0-9а-яё]+", "");
	}

	public static string CleanTitle(string raw)
	{
		if (string.IsNullOrWhiteSpace(raw))
		{
			return raw;
		}
		string[] array = Regex.Replace(Regex.Replace(Regex.Replace(Regex.Replace(Regex.Replace(Regex.Replace(Regex.Replace(Regex.Replace(raw, "\\.(exe|lnk|url|bat)$", "", RegexOptions.IgnoreCase), "\\[[^\\]]{0,80}\\]", " "), "\\{[^}]{0,80}\\}", " "), "\\([^)]{0,80}\\)", " "), "\\b(v|ver|version|build)\\s*\\d+(\\.\\d+){0,4}\\b", " ", RegexOptions.IgnoreCase), "\\b\\d+(\\.\\d+){2,4}\\b", " "), "[_.]+", " "), "\\s*-\\s*", " ").Split(new char[5] { ' ', '\t', '-', '+', ',' }, StringSplitOptions.RemoveEmptyEntries);
		List<string> list = new List<string>();
		string[] array2 = array;
		for (int i = 0; i < array2.Length; i++)
		{
			string text = array2[i].Trim();
			if (text.Length != 0 && !JunkTokens.Contains(text))
			{
				list.Add(text);
			}
		}
		string input = string.Join(" ", list).Trim();
		input = Regex.Replace(input, "\\s{2,}", " ");
		if (input.Length < 2)
		{
			return Regex.Replace(raw, "[_.]+", " ").Trim();
		}
		return input;
	}

	public static long DirectorySize(string path)
	{
		long num = 0L;
		try
		{
			Stack<string> stack = new Stack<string>();
			stack.Push(path);
			int num2 = 0;
			while (stack.Count > 0 && num2++ < 400000)
			{
				string path2 = stack.Pop();
				try
				{
					foreach (string item in Directory.EnumerateDirectories(path2))
					{
						stack.Push(item);
					}
					foreach (string item2 in Directory.EnumerateFiles(path2))
					{
						try
						{
							num += new FileInfo(item2).Length;
						}
						catch
						{
						}
					}
				}
				catch
				{
				}
			}
		}
		catch
		{
		}
		return num;
	}

	public static string FormatSize(long bytes)
	{
		if (bytes <= 0)
		{
			return "—";
		}
		string[] array = new string[5] { "Б", "КБ", "МБ", "ГБ", "ТБ" };
		double num = bytes;
		int num2 = 0;
		while (num >= 1024.0 && num2 < array.Length - 1)
		{
			num /= 1024.0;
			num2++;
		}
		if (num2 > 1)
		{
			return $"{num:0.0} {array[num2]}";
		}
		return $"{num:0} {array[num2]}";
	}
}
