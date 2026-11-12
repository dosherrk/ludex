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

public static class AutoScanner
{
	public sealed class Hit
	{
		public string Folder = "";

		public string Exe = "";

		public string Title = "";

		public string Root = "";

		public string Origin = "";
	}

	public sealed class Report
	{
		public int FoldersVisited;

		public bool Cancelled;

		public List<Hit> Hits { get; } = new List<Hit>();


		public List<string> Roots { get; } = new List<string>();


		public List<string> Skipped { get; } = new List<string>();


		public string Summary => $"автопоиск: папок {FoldersVisited}, кандидатов {Hits.Count}, источников {Roots.Count}, пропущено {Skipped.Count}";
	}

	private const int MaxDepth = 6;

	private const int SourceExtraDepth = 8;

	private const long MinUnityExeBytes = 307200L;

	private const long MinGameBytes = 734003200L;

	private const long MinMarkedGameBytes = 209715200L;

	private const long MinLauncherGameBytes = 157286400L;

	private static readonly HashSet<string> SkipFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
	{
		"windows", "winsxs", "system32", "syswow64", "system volume information", "$recycle.bin", "$windows.~bt", "$windows.~ws", "$sysreset", "$winreagent",
		"recovery", "perflogs", "msocache", "programdata", "appdata", "temp", "tmp", "cache", "caches", "logs",
		"log", "crashdumps", "memory.dmp", "minidump", "reports", "wer", "softwaredistribution", "driverstore", "drivers", "installer",
		"installers", "uninstall", "uninst", "update", "updates", "updater", "patch", "backup", "backups", "previous",
		"old", "oldversions", "recoverystore", "redist", "redistributable", "commonredist", "_commonredist", "directx", "dotnet", "vcredist",
		"microsoft.net", "windows kits", "windowsapps", "windowspowershell", "assembly", "gac", "runtimes", "dotnetcore", "nuget", "packages",
		"nugetpackages", "site-packages", "python3", "python3.10", "python3.11", "python3.12", "python311", "python312", "__pycache__", "node_modules",
		"bower_components", "vendor", "dist", "build", "obj", "target", "out", ".git", ".svn", ".hg",
		".vs", ".vscode", ".idea", ".venv", "venv", "env", "conda-meta", "sdk", "sdks", "toolchains",
		"ndk", "jdk", "jre", "jbr", "gradle", "maven", "m2", "llvm", "mingw", "cygwin",
		"msys64", "cmake", "ninja", "clang", "gcc", "compiler", "debuggers", "include", "headers", "lib",
		"libs", "lib32", "lib64", "share", "doc", "docs", "documentation", "manuals", "manual", "help",
		"samples", "examples", "templates", "tests", "test", "unittests", "benchmarks", "benchmark", "perf", "coverage",
		"userdata", "browser", "chrome", "chromium", "firefox", "edge", "opera", "brave", "microsoft", "office",
		"onedrive", "teams", "outlook", "skype", "discord", "steam", "epicgames", "onedrivetemp", "dropbox", "googledrive",
		"yandexdisk", "downloads_temp", "recycle", "recycler", "thumbnails", "icons", "cursors", "themes", "music", "видео",
		"video", "фото", "photos", "pictures", "movies", "steamapps", "compatdata", "shadercache", "downloading", "workshop",
		"tempdata", "mygames", "мои игры", "savedgames", "savegames", "saves", "profiles", "programs", "program", "apps",
		"soft", "software", "resources", "locales", "swiftshader"
	};

	private static readonly string[] NeverWalk = new string[25]
	{
		"AppData", "Library", "Applications", "snap", "flatpak", "usr", "var", "boot", "etc", "dev",
		"proc", "sys", "run", "opt", "srv", "media", "mnt", "Program Files", "Program Files (x86)", "ProgramData",
		"Users", "System32", "SysWOW64", "Programs", "Program"
	};

	private static readonly string[] SystemPaths = new string[27]
	{
		"\\windows\\", "\\program files", "\\programdata\\", "\\appdata\\", "\\windowsapps\\", "\\microsoft\\windows defender", "\\steam\\bin", "\\steam\\package", "\\steam\\steamapps\\", "\\$recycle.bin",
		"\\system volume information", "\\common files\\", "\\tools\\", "\\team tools\\", "\\diagnosticshub", "\\visual studio", "\\windows kits", "\\dotnet\\", "\\microsoft visual studio\\", "\\driverstore\\",
		"\\system32\\", "\\syswow64\\", "\\programs\\", "\\program\\", "\\apps\\", "\\soft\\", "\\software\\"
	};

	private static readonly string[] KnownFolders = new string[24]
	{
		"Games", "Game", "Игры", "My Games", "GOG Games", "Epic Games", "SteamLibrary", "Steam", "Ubisoft Game Launcher", "EA Games",
		"Origin Games", "Battle.net", "XboxGames", "PC Games", "Games Library", "GOG.com", "GOG", "Riot Games", "Rockstar Games", "Amazon Games",
		"Itch Games", "Portable Games", "Program Files (x86)\\Steam", "Program Files\\Steam"
	};

	private static readonly HashSet<string> GenericFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
	{
		"bin", "binaries", "binary", "x64", "x86", "win64", "win32", "win", "windows", "game",
		"games", "data", "build", "release", "retail", "system", "engine", "client", "app", "application",
		"content", "exe", "runtime", "platform", "linux", "macos", "shipping", "gamedata", "gamefiles", "игра",
		"binaries-win64", "bin64", "win_x64", "winx64", "gog", "steam", "epic", "win-arm64ec", "arm64", "arm64ec",
		"aarch64", "win64arm", "field", "assets", "paks", "levels", "maps", "media", "support", "config",
		"settings", "common", "steamapps", "downloading", "temp", "saves", "profiles", "mods", "plugins", "resources"
	};

	private static readonly string[] HardIgnore = new string[96]
	{
		"unins", "uninstall", "unwise", "setup", "install", "installer", "vcredist", "vc_redist", "vcsetup", "dxsetup",
		"dxwebsetup", "directx", "dotnetfx", "ndp4", "oalinst", "physx", "ue4prereq", "ue5prereq", "prereq", "redist",
		"_redist", "crashreport", "crashpad", "crashhandler", "crashsender", "unitycrashhandler", "steamerrorreporter", "steamsetup", "steamservice", "notification_helper",
		"7za", "ffmpeg", "ffplay", "qt5", "qt6", "qtwebengineprocess", "epicwebhelper", "eossdk", "eulauncher", "launcher_installer",
		"updater", "patcher", "unpack", "extract", "readme", "webview", "helper", "activation", "register", "server",
		"dedicated", "nvcontainer", "nvngx", "nvidia", "igfx", "intel", "rtkaud", "svchost", "rundll32", "taskmgr",
		"regedit", "cmd", "powershell", "wscript", "cscript", "msiexec", "mshta", "conhost", "dllhost", "explorer",
		"notepad", "mspaint", "calc", "java", "javaw", "python", "pythonw", "node", "npm", "git",
		"curl", "wget", "tar", "7zg", "7zfm", "winrar", "unrar", "bandizip", "everything", "processhacker",
		"procexp", "adb", "fastboot", "scrcpy", "bluestacks", "ldplayer"
	};

	private static readonly string[] LauncherExes = new string[21]
	{
		"steam", "steamwebhelper", "epicgameslauncher", "galaxyclient", "ubisoftconnect", "upc", "eadesktop", "origin", "battle.net", "battlenet",
		"heroic", "lutris", "goggalaxy", "riotclientservices", "riotclientux", "bethesda", "rockstargameslauncher", "socialclub", "amazongames", "itch",
		"playnite"
	};

	private static readonly string[] NeverGameWords = new string[149]
	{
		"vpn", "proxy", "прокси", "driver", "драйвер", "antivirus", "антивирус", "defender", "firewall", "брандмауэр",
		"runtime", "redistributable", "redist", "framework", "sdk", "launcher", "лаунчер", "updater", "обновление", "installer",
		"setup", "uninstall", "service", "служба", "agent", "daemon", "host", "helper", "extension", "расширение",
		"diagnostics", "диагностика", "classification", "sense", "telemetry", "tool", "tools", "утилита", "utility", "utilities",
		"console", "terminal", "shell", "editor", "редактор", "converter", "конвертер", "viewer", "просмотр", "player",
		"browser", "браузер", "client", "клиент", "manager", "менеджер", "control panel", "sound", "audio", "mic",
		"camera", "камера", "screen", "screenshot", "clipboard", "aac", "motherboard", "bios", "firmware", "прошивка",
		"chipset", "gpu", "cpu", "ram", "network", "сеть", "wifi", "bluetooth", "print", "printer",
		"сканер", "scanner", "office", "excel", "word", "photoshop", "code", "studio", "ide", "debugger",
		"synapse", "armoury", "aura", "rgb", "fan", "cooling", "temperature", "monitor", "porthole", "kimi",
		"electron", "overwolf", "medal", "plarium", "desktop app", "operagx", "yandex", "vivaldi", "teams", "zoom",
		"slack", "notion", "obsidian", "evernote", "dropbox", "onedrive", "resilio", "syncthing", "anydesk", "teamviewer",
		"rustdesk", "parsec", "sunshine", "moonlight", "duo", "python", "anaconda", "jupyter", "vscode", "sublime",
		"postman", "dbeaver", "docker", "virtualbox", "vmware", "qemu", "wireshark", "fiddler", "burp", "nmap",
		"ldplayer", "mumu", "noxplayer", "gameloop", "emulator", "media player", "spotify", "deezer", "tidal"
	};

	private static readonly string[] SystemNameMarkers = new string[14]
	{
		"windows defender", "defender advanced", "threat protection", "windows security", "microsoft windows", "windows nt", "microsoft.net", "visual c++", "directx", "dotnet",
		"msedge", "internet explorer", "windows media", "powershell"
	};

	private static readonly HashSet<string> IgnoreSubstrings = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "wallpaper", "soundpad", "voicemod", "crosshair", "porthole" };

	public static Report Scan(Action<string>? onProgress = null, Func<bool>? isCancelled = null)
	{
		Action<string> onProgress2 = onProgress;
		Func<bool> isCancelled2 = isCancelled;
		Report report = new Report();
		HashSet<string> seenFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		try
		{
			Progress(Loc.T("auto.step.launchers"));
			foreach (string item in LauncherGamesFolders())
			{
				if (Directory.Exists(item))
				{
					AddRoot(item, "launcher");
				}
			}
			Progress(Loc.T("auto.step.known"));
			foreach (string item2 in Drives())
			{
				if (Cancelled())
				{
					report.Cancelled = true;
					return report;
				}
				string[] knownFolders = KnownFolders;
				foreach (string path in knownFolders)
				{
					string text2 = System.IO.Path.Combine(item2, path);
					if (Directory.Exists(text2))
					{
						AddRoot(text2, "known");
					}
				}
			}
			Progress(Loc.T("auto.step.drives"));
			foreach (string item3 in Drives())
			{
				if (Cancelled())
				{
					report.Cancelled = true;
					return report;
				}
				bool flag = IsSystemDrive(item3);
				Walk(new DirectoryInfo(item3), 0, flag ? 3 : 6, "drive", report, seenFolders, Progress, Cancelled);
			}
			foreach (string item4 in UserFolders())
			{
				if (Cancelled())
				{
					report.Cancelled = true;
					return report;
				}
				if (Directory.Exists(item4))
				{
					Walk(new DirectoryInfo(item4), 0, 3, "user", report, seenFolders, Progress, Cancelled);
				}
			}
		}
		catch (Exception ex)
		{
			AppPaths.Log("AutoScanner: " + ex);
		}
		AppPaths.Log(report.Summary);
		foreach (string item5 in report.Skipped.Take(24))
		{
			AppPaths.Log("  · пропущено: " + item5);
		}
		return report;
		void AddRoot(string folder, string origin)
		{
			if (!report.Roots.Contains<string>(folder, StringComparer.OrdinalIgnoreCase))
			{
				report.Roots.Add(folder);
			}
			Walk(new DirectoryInfo(folder), 0, 6, origin, report, seenFolders, Progress, Cancelled);
		}
		bool Cancelled()
		{
			Func<bool> func = isCancelled2;
			if (func == null || !func())
			{
				return report.Cancelled;
			}
			return true;
		}
		void Progress(string text)
		{
			try
			{
				onProgress2?.Invoke(text);
			}
			catch
			{
			}
		}
	}

	private static void Walk(DirectoryInfo dir, int depth, int maxDepth, string origin, Report report, HashSet<string> seenFolders, Action<string> progress, Func<bool> cancelled)
	{
		if (cancelled())
		{
			report.Cancelled = true;
		}
		else
		{
			if (report.Hits.Count > 4000 || IsInsideFoundGame(report, dir.FullName))
			{
				return;
			}
			if (depth == 0 && maxDepth < 8)
			{
				maxDepth = 8;
			}
			report.FoldersVisited++;
			if (report.FoldersVisited % 40 == 0)
			{
				progress(Loc.F("auto.progress", report.FoldersVisited, report.Hits.Count));
			}
			DirectoryInfo[] array;
			try
			{
				array = (from d in dir.GetDirectories()
					where (d.Attributes & (FileAttributes.Hidden | FileAttributes.System)) == 0 && (d.Attributes & FileAttributes.ReparsePoint) == 0
					where !SkipFolders.Contains(d.Name)
					where !NeverWalk.Contains<string>(d.Name, StringComparer.OrdinalIgnoreCase)
					where !IsSystemFolder(d.Name)
					where !d.Name.StartsWith(".", StringComparison.Ordinal)
					select d).OrderBy<DirectoryInfo, string>((DirectoryInfo d) => d.Name, StringComparer.OrdinalIgnoreCase).ToArray();
			}
			catch (UnauthorizedAccessException)
			{
				report.Skipped.Add(dir.FullName + " — нет прав");
				return;
			}
			catch
			{
				return;
			}
			if (!seenFolders.Contains(dir.FullName))
			{
				Hit hit = DetectGame(dir, origin);
				if (hit != null && !AlreadyFound(report, hit))
				{
					seenFolders.Add(dir.FullName);
					report.Hits.Add(hit);
					progress(Loc.F("auto.foundOne", hit.Title, report.Hits.Count));
					return;
				}
			}
			if (depth >= maxDepth)
			{
				return;
			}
			DirectoryInfo[] array2 = array;
			foreach (DirectoryInfo dir2 in array2)
			{
				if (cancelled())
				{
					report.Cancelled = true;
					break;
				}
				Walk(dir2, depth + 1, maxDepth, origin, report, seenFolders, progress, cancelled);
			}
		}
	}

	private static bool AlreadyFound(Report report, Hit hit)
	{
		Hit hit2 = hit;
		if (!report.Hits.Any((Hit h) => SameFolder(h.Folder, hit2.Folder)))
		{
			return report.Hits.Any((Hit h) => string.Equals(h.Title, hit2.Title, StringComparison.OrdinalIgnoreCase));
		}
		return true;
	}

	private static bool SameFolder(string a, string b)
	{
		return string.Equals(a.TrimEnd('\\', '/'), b.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase);
	}

	private static bool IsInsideFoundGame(Report report, string folder)
	{
		string text = folder.TrimEnd('\\', '/');
		foreach (Hit hit in report.Hits)
		{
			string text2 = (string.IsNullOrEmpty(hit.Root) ? hit.Folder : hit.Root).TrimEnd('\\', '/');
			if (text.Equals(text2, StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
			ReadOnlySpan<char> readOnlySpan = text2;
			char reference = System.IO.Path.DirectorySeparatorChar;
			if (text.StartsWith(string.Concat(readOnlySpan, new ReadOnlySpan<char>(ref reference)), StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
			if (text.StartsWith(text2 + "/", StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
		}
		return false;
	}

	private static Hit? DetectGame(DirectoryInfo dir, string origin)
	{
		DirectoryInfo dir2 = dir;
		if (IgnoreSubstrings.Any((string x) => dir2.Name.Contains(x, StringComparison.OrdinalIgnoreCase)))
		{
			return null;
		}
		if (LooksLikeToolFolder(dir2.Name))
		{
			return null;
		}
		if (IsSystemPath(dir2.FullName))
		{
			return null;
		}
		if (IsSystemName(dir2.Name))
		{
			return null;
		}
		if (LooksLikeDesktopApp(dir2))
		{
			return null;
		}
		if (dir2.Parent != null && LooksLikeDesktopApp(dir2.Parent))
		{
			return null;
		}
		FileInfo[] array;
		try
		{
			array = (from f in dir2.GetFiles()
				where f.Extension.Equals(".exe", StringComparison.OrdinalIgnoreCase)
				select f).ToArray();
		}
		catch
		{
			return null;
		}
		if (array.Length == 0)
		{
			return null;
		}
		List<FileInfo> list = (from f in array
			where !IsIgnoredExe(f.Name)
			where f.Length >= 2097152 || (f.Length >= 307200 && IsUnityPlayerExe(dir2, f))
			orderby f.Length descending
			select f).ToList();
		if (list.Count == 0)
		{
			return null;
		}
		list = list.Where((FileInfo f) => !IsSystemName(System.IO.Path.GetFileNameWithoutExtension(f.Name))).ToList();
		if (list.Count == 0)
		{
			return null;
		}
		string text = TitleFor(dir2);
		if (string.IsNullOrWhiteSpace(text) || text.Length < 2)
		{
			return null;
		}
		if (HasNeverGameWord(text) || HasNeverGameWord(System.IO.Path.GetFileNameWithoutExtension(list[0].Name)))
		{
			return null;
		}
		if (Scanner.LooksLikeProgram(text, list[0].FullName))
		{
			return null;
		}
		if (IsInstallerName(list[0].Name))
		{
			return null;
		}
		DirectoryInfo directoryInfo = GameRoot(dir2);
		bool flag = HasGameMarker(directoryInfo);
		bool flag2;
		switch (origin)
		{
		case "launcher":
		case "steam":
		case "epic":
		case "known":
			flag2 = true;
			break;
		default:
			flag2 = false;
			break;
		}
		long num = (flag2 ? 157286400 : (flag ? 209715200 : 734003200));
		if (FolderDataSize(directoryInfo, num) < num)
		{
			return null;
		}
		if (LooksTechnical(text))
		{
			return null;
		}
		return new Hit
		{
			Folder = dir2.FullName,
			Exe = list[0].FullName,
			Title = Scanner.CleanTitle(text),
			Root = directoryInfo.FullName,
			Origin = origin
		};
	}

	private static DirectoryInfo? TitleFolder(DirectoryInfo dir)
	{
		DirectoryInfo directoryInfo = dir;
		for (int i = 0; i < 6; i++)
		{
			if (directoryInfo == null)
			{
				break;
			}
			string name = directoryInfo.Name;
			if (name.Length >= 2 && !GenericFolders.Contains(name) && !LooksLikePlatformFolder(name) && !SkipFolders.Contains(name) && !LooksLikeToolFolder(name) && !KnownFolders.Contains<string>(name, StringComparer.OrdinalIgnoreCase) && !LooksLikeDriveRoot(directoryInfo.FullName) && !LooksLikeEngineSubtree(directoryInfo))
			{
				return directoryInfo;
			}
			directoryInfo = directoryInfo.Parent;
		}
		return null;
	}

	private static string TitleFor(DirectoryInfo dir)
	{
		return TitleFolder(dir)?.Name ?? "";
	}

	private static DirectoryInfo GameRoot(DirectoryInfo dir)
	{
		DirectoryInfo directoryInfo = dir;
		DirectoryInfo directoryInfo2 = TitleFolder(dir);
		for (int i = 0; i < 6; i++)
		{
			if (directoryInfo == null)
			{
				break;
			}
			if (HasGameMarker(directoryInfo))
			{
				return directoryInfo;
			}
			if (directoryInfo2 != null && SameFolder(directoryInfo.FullName, directoryInfo2.FullName))
			{
				return directoryInfo;
			}
			directoryInfo = directoryInfo.Parent;
		}
		return directoryInfo2 ?? dir;
	}

	private static bool LooksLikeEngineSubtree(DirectoryInfo dir)
	{
		try
		{
			HashSet<string> names = (from d in dir.GetDirectories()
				select d.Name).ToHashSet<string>(StringComparer.OrdinalIgnoreCase);
			if (Has(new string[1] { "Binaries" }) && Has(new string[4] { "Content", "Engine", "Saved", "Config" }))
			{
				return true;
			}
			if (Has(new string[1] { "Engine" }) && Has(new string[3] { "Saved", "Config", "Intermediate" }))
			{
				return true;
			}
			return false;
			bool Has(string[] any)
			{
				return any.Any((string x) => names.Contains(x));
			}
		}
		catch
		{
			return false;
		}
	}

	private static bool LooksLikePlatformFolder(string name)
	{
		return Regex.IsMatch(name, "^(bin|binaries|win|windows|linux|mac|macos|osx)?[-_ ]?(x86|x64|win32|win64|arm|arm64|arm64ec|aarch64|32|64|release|retail)?[-_ ]?(x86|x64|win32|win64|arm64|arm64ec|aarch64|release|retail)?$", RegexOptions.IgnoreCase);
	}

	private static bool HasGameMarker(DirectoryInfo root)
	{
		try
		{
			string[] engineDirs = new string[10] { "data", "content", "engine", "gamedata", "assets", "paks", "levels", "maps", "binaries", "game" };
			if (root.GetDirectories().Any((DirectoryInfo d) => engineDirs.Contains<string>(d.Name, StringComparer.OrdinalIgnoreCase)))
			{
				return true;
			}
			if (IsUnityGame(root))
			{
				return true;
			}
			string[] dataExt = new string[11]
			{
				".pak", ".assets", ".bundle", ".vpk", ".arc", ".uasset", ".pck", ".wad", ".mpq", ".big",
				".forge"
			};
			return root.EnumerateFiles().Any((FileInfo f) => dataExt.Contains<string>(f.Extension, StringComparer.OrdinalIgnoreCase) && f.Length >= 52428800);
		}
		catch
		{
			return false;
		}
	}

	private static bool IsUnityGame(DirectoryInfo dir)
	{
		try
		{
			foreach (string item in from d in dir.GetDirectories()
				select d.Name into n
				where n.EndsWith("_Data", StringComparison.OrdinalIgnoreCase)
				select n)
			{
				DirectoryInfo directoryInfo = new DirectoryInfo(System.IO.Path.Combine(dir.FullName, item));
				if (directoryInfo.EnumerateFiles("UnityPlayer.dll", SearchOption.TopDirectoryOnly).Any())
				{
					return true;
				}
				if (directoryInfo.EnumerateDirectories("Managed", SearchOption.TopDirectoryOnly).Any())
				{
					return true;
				}
			}
			return dir.EnumerateFiles("UnityPlayer.dll", SearchOption.TopDirectoryOnly).Any();
		}
		catch
		{
			return false;
		}
	}

	private static bool IsUnityPlayerExe(DirectoryInfo dir, FileInfo exe)
	{
		try
		{
			if (!IsUnityGame(dir))
			{
				return false;
			}
			string name = System.IO.Path.GetFileNameWithoutExtension(exe.Name);
			if (string.Equals(name, dir.Name, StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
			return (from d in dir.GetDirectories()
				select d.Name).Any(delegate(string d)
			{
				if (d.EndsWith("_Data", StringComparison.OrdinalIgnoreCase))
				{
					int length = "_Data".Length;
					return string.Equals(d.Substring(0, d.Length - length), name, StringComparison.OrdinalIgnoreCase);
				}
				return false;
			});
		}
		catch
		{
			return false;
		}
	}

	private static long FolderDataSize(DirectoryInfo dir, long limit, int depth = 0)
	{
		long num = 0L;
		try
		{
			foreach (FileInfo item in dir.EnumerateFiles())
			{
				num += item.Length;
				if (num >= limit)
				{
					return num;
				}
			}
			if (depth >= 3)
			{
				return num;
			}
			foreach (DirectoryInfo item2 in dir.EnumerateDirectories())
			{
				if (!SkipFolders.Contains(item2.Name))
				{
					num += FolderDataSize(item2, limit, depth + 1);
					if (num >= limit)
					{
						return num;
					}
				}
			}
		}
		catch
		{
		}
		return num;
	}

	private static bool HasNeverGameWord(string text)
	{
		if (string.IsNullOrWhiteSpace(text))
		{
			return false;
		}
		string text2 = Scanner.Normalize(text);
		if (text2.Length == 0)
		{
			return false;
		}
		string[] neverGameWords = NeverGameWords;
		foreach (string text3 in neverGameWords)
		{
			string text4 = Scanner.Normalize(text3);
			if (text4.Length < 3)
			{
				continue;
			}
			if (text4.Length >= 6)
			{
				if (text2.Contains(text4, StringComparison.Ordinal))
				{
					return true;
				}
			}
			else if (Regex.IsMatch(text, "(^|[^\\p{L}\\p{N}])" + Regex.Escape(text3) + "($|[^\\p{L}\\p{N}])", RegexOptions.IgnoreCase))
			{
				return true;
			}
		}
		return false;
	}

	private static bool IsSystemName(string text)
	{
		string lower = text.ToLowerInvariant();
		return SystemNameMarkers.Any((string m) => lower.Contains(m, StringComparison.Ordinal));
	}

	private static bool LooksTechnical(string title)
	{
		string text = Scanner.Normalize(title);
		if (text.Length < 4)
		{
			return true;
		}
		if (Regex.IsMatch(text, "^(x|win)?(86|64|32)$"))
		{
			return true;
		}
		if (Regex.IsMatch(text, "^(app|application|game|client|launcher|run|start|play|main)[a-z0-9]{0,6}$"))
		{
			return true;
		}
		int num = text.Count(char.IsLetter);
		int num2 = text.Count(char.IsDigit);
		if (num >= 3)
		{
			return num2 >= num;
		}
		return true;
	}

	private static bool IsIgnoredExe(string fileName)
	{
		string name = System.IO.Path.GetFileNameWithoutExtension(fileName).ToLowerInvariant();
		if (HardIgnore.Any((string bad) => WordMatch(name, bad)))
		{
			return true;
		}
		return LauncherExes.Any((string bad) => name == bad);
	}

	private static bool WordMatch(string name, string word)
	{
		if (word.Length == 0)
		{
			return false;
		}
		if (word.Length >= 6)
		{
			return name.Contains(word, StringComparison.Ordinal);
		}
		for (int num = name.IndexOf(word, StringComparison.Ordinal); num >= 0; num = name.IndexOf(word, num + 1, StringComparison.Ordinal))
		{
			if (num == 0)
			{
				return true;
			}
			bool num2 = !char.IsLetterOrDigit(name[num - 1]);
			int num3 = num + word.Length;
			bool flag = num3 >= name.Length || !char.IsLetterOrDigit(name[num3]);
			if (num2 && flag)
			{
				return true;
			}
		}
		return false;
	}

	public static bool IsPlausibleGameFolder(string folder)
	{
		if (string.IsNullOrWhiteSpace(folder))
		{
			return false;
		}
		string fileName = System.IO.Path.GetFileName(folder.TrimEnd('\\', '/'));
		if (IsSystemPath(folder))
		{
			return false;
		}
		if (IsSystemName(folder) || IsSystemName(fileName))
		{
			return false;
		}
		if (LooksLikeToolFolder(fileName) || HasNeverGameWord(fileName))
		{
			return false;
		}
		if (IsInstallerName(fileName))
		{
			return false;
		}
		if (LooksLikeUserProfileJunk(folder))
		{
			return false;
		}
		return true;
	}

	private static bool IsSystemPath(string fullPath)
	{
		string path = (fullPath + "\\").ToLowerInvariant();
		return SystemPaths.Any((string marker) => path.Contains(marker));
	}

	private static bool IsSystemFolder(string name)
	{
		if (!name.Equals("Program Files", StringComparison.OrdinalIgnoreCase) && !name.Equals("Program Files (x86)", StringComparison.OrdinalIgnoreCase) && !name.Equals("ProgramData", StringComparison.OrdinalIgnoreCase) && !name.Equals("Programs", StringComparison.OrdinalIgnoreCase) && !name.Equals("Program", StringComparison.OrdinalIgnoreCase))
		{
			return name.Equals("Users", StringComparison.OrdinalIgnoreCase);
		}
		return true;
	}

	private static bool LooksLikeDesktopApp(DirectoryInfo dir)
	{
		string[] markers = new string[11]
		{
			"icudtl.dat", "v8_context_snapshot.bin", "snapshot_blob.bin", "LICENSES.chromium.html", "chrome_100_percent.pak", "chrome_200_percent.pak", "libEGL.dll", "libGLESv2.dll", "d3dcompiler_47.dll", "resources.pak",
			"natives_blob.bin"
		};
		try
		{
			return HasElectronFiles(dir, markers) || (dir.Parent != null && HasElectronFiles(dir.Parent, markers));
		}
		catch
		{
			return false;
		}
	}

	private static bool HasElectronFiles(DirectoryInfo dir, string[] markers)
	{
		string[] markers2 = markers;
		try
		{
			if (dir.EnumerateFiles("*.asar", SearchOption.TopDirectoryOnly).Any())
			{
				return true;
			}
			DirectoryInfo directoryInfo = dir.GetDirectories("resources", SearchOption.TopDirectoryOnly).FirstOrDefault();
			if (directoryInfo != null && directoryInfo.EnumerateFiles("*.asar", SearchOption.TopDirectoryOnly).Any())
			{
				return true;
			}
			return dir.EnumerateFiles("*", SearchOption.TopDirectoryOnly).Count((FileInfo f) => markers2.Contains<string>(f.Name, StringComparer.OrdinalIgnoreCase)) >= 2;
		}
		catch
		{
			return false;
		}
	}

	private static bool LooksLikeToolFolder(string name)
	{
		string name2 = name;
		return new string[23]
		{
			"driver", "runtime", "redist", "updater", "installer", "setup", "utility", "toolkit", "tool", "sdk",
			"service", "agent", "daemon", "sdkmanager", "console", "command line", "profile", "firmware", "bios", "diagnostic",
			"benchmark", "test", "sample"
		}.Any((string w) => name2.Contains(w, StringComparison.OrdinalIgnoreCase));
	}

	private static bool IsInstallerName(string fileName)
	{
		string name = System.IO.Path.GetFileNameWithoutExtension(fileName).ToLowerInvariant();
		return new string[8] { "setup", "install", "update", "patch", "bootstrapper", "redist", "prereq", "unins" }.Any((string w) => name.Contains(w, StringComparison.Ordinal));
	}

	private static IEnumerable<string> Drives()
	{
		DriveInfo[] drives;
		try
		{
			drives = DriveInfo.GetDrives();
		}
		catch
		{
			yield break;
		}
		DriveInfo[] array = drives;
		DriveInfo[] array2 = array;
		foreach (DriveInfo driveInfo in array2)
		{
			string fullName;
			try
			{
				if ((uint)(driveInfo.DriveType - 2) > 1u || !driveInfo.IsReady)
				{
					continue;
				}
				fullName = driveInfo.RootDirectory.FullName;
				goto IL_0077;
			}
			catch
			{
			}
			continue;
			IL_0077:
			yield return fullName;
		}
	}

	private static bool IsSystemDrive(string root)
	{
		try
		{
			string folderPath = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
			return !string.IsNullOrEmpty(folderPath) && folderPath.StartsWith(root, StringComparison.OrdinalIgnoreCase);
		}
		catch
		{
			return false;
		}
	}

	private static IEnumerable<string> UserFolders()
	{
		string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
		if (string.IsNullOrEmpty(home))
		{
			yield break;
		}
		// «Загрузки» намеренно не обходим: там лежат установщики, архивы и утилиты,
		// и в библиотеку попадали посторонние файлы (например TqWsProxy_windows.exe).
		// «Документы» — тоже: игры туда не ставят, зато там проекты и сохранения.
		string[] array = new string[2] { "Desktop", "Рабочий стол" };
		string[] array2 = array;
		foreach (string path in array2)
		{
			string text = System.IO.Path.Combine(home, path);
			if (Directory.Exists(text))
			{
				yield return text;
			}
		}
	}

	/// <summary>
	/// Папка внутри профиля пользователя, но не игровая библиотека: Загрузки, Документы,
	/// рабочий стол, AppData и прочее. Вторая линия защиты — даже если такой путь попал
	/// в обход другим маршрутом, игрой он не считается.
	/// </summary>
	public static bool LooksLikeUserProfileJunk(string? path)
	{
		if (string.IsNullOrWhiteSpace(path))
		{
			return false;
		}
		try
		{
			string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
			if (string.IsNullOrWhiteSpace(home))
			{
				return false;
			}
			string full = System.IO.Path.GetFullPath(path).TrimEnd('\\', '/');
			string root = System.IO.Path.GetFullPath(home).TrimEnd('\\', '/');
			if (!full.StartsWith(root + System.IO.Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
			{
				return false;
			}
			string rest = full.Substring(root.Length).TrimStart('\\', '/');
			string first = rest.Split(new char[2] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
			string[] junk = new string[9] { "Downloads", "Загрузки", "Documents", "Документы", "Desktop", "Рабочий стол", "AppData", "OneDrive", "Изображения" };
			return junk.Any((string name) => name.Equals(first, StringComparison.OrdinalIgnoreCase));
		}
		catch
		{
			return false;
		}
	}

	private static bool LooksLikeDriveRoot(string path)
	{
		try
		{
			string text = path.TrimEnd('\\', '/');
			return System.IO.Path.GetPathRoot(text)?.TrimEnd('\\', '/') == text;
		}
		catch
		{
			return false;
		}
	}

	private static IEnumerable<string> LauncherGamesFolders()
	{
		List<string> list = new List<string>();
		string[] array = new string[2]
		{
			System.IO.Path.Combine(SafeFolder(Environment.SpecialFolder.ProgramFilesX86), "Steam"),
			System.IO.Path.Combine(SafeFolder(Environment.SpecialFolder.ProgramFiles), "Steam")
		};
		foreach (string text in array)
		{
			if (!Directory.Exists(text))
			{
				continue;
			}
			string path = System.IO.Path.Combine(text, "steamapps", "libraryfolders.vdf");
			if (File.Exists(path))
			{
				try
				{
					foreach (Match item in Regex.Matches(File.ReadAllText(path), "\"path\"\\s+\"([^\"]+)\""))
					{
						string text2 = System.IO.Path.Combine(item.Groups[1].Value.Replace("\\\\", "\\"), "steamapps", "common");
						if (Directory.Exists(text2))
						{
							list.Add(text2);
						}
					}
				}
				catch
				{
				}
			}
			string text3 = System.IO.Path.Combine(text, "steamapps", "common");
			if (Directory.Exists(text3))
			{
				list.Add(text3);
			}
		}
		foreach (string item2 in Drives())
		{
			array = new string[6]
			{
				System.IO.Path.Combine(item2, "SteamLibrary", "steamapps", "common"),
				System.IO.Path.Combine(item2, "Epic Games"),
				System.IO.Path.Combine(item2, "GOG Games"),
				System.IO.Path.Combine(item2, "GOG.com"),
				System.IO.Path.Combine(item2, "EA Games"),
				System.IO.Path.Combine(item2, "Origin Games")
			};
			foreach (string text4 in array)
			{
				if (Directory.Exists(text4))
				{
					list.Add(text4);
				}
			}
		}
		return list.Distinct<string>(StringComparer.OrdinalIgnoreCase);
	}

	private static string SafeFolder(Environment.SpecialFolder folder)
	{
		try
		{
			return Environment.GetFolderPath(folder);
		}
		catch
		{
			return "";
		}
	}
}
