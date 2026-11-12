using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Win32;

namespace Ludex.Services;

/// <summary>
/// Определяет браузер по умолчанию, чтобы Ludex не полагался на «ассоциацию Windows»,
/// которую часто перехватывают сторонние программы (или она вообще не настроена,
/// и тогда ссылка просто не открывается).
///
/// Windows: HKCU\...\UrlAssociations\http\UserChoice → ProgId → shell\open\command.
/// Linux/macOS: xdg-settings / xdg-mime, затем разбор .desktop-файла.
///
/// Если браузер определить не удалось, используется прежний путь: ссылка отдаётся
/// оболочке (ShellExecute / xdg-open), которая попробует сама.
/// </summary>
public static class Browser
{
	public sealed class Info
	{
		/// <summary>Понятное название: «Google Chrome», «Mozilla Firefox», «Microsoft Edge».</summary>
		public string Name { get; init; } = "";

		/// <summary>Путь к исполняемому файлу браузера, если он известен.</summary>
		public string? ExePath { get; init; }

		/// <summary>Как запускать браузер: имя файла (для xdg-open) либо путь.</summary>
		public string? LaunchTarget { get; init; }

		public bool Found => !string.IsNullOrWhiteSpace(LaunchTarget);

		public override string ToString() => Found ? Name : "не определён";
	}

	private static readonly object FileLock = new object();

	private static Info? _cached;

	private static DateTime _cachedUtc;

	private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(2);

	private static string CacheFile => System.IO.Path.Combine(AppPaths.Root, "browser.txt");

	/// <summary>Браузер по умолчанию. Результат кэшируется на пару минут.</summary>
	public static Info Detect()
	{
		lock (FileLock)
		{
			if (_cached != null && DateTime.UtcNow - _cachedUtc < CacheTtl)
			{
				return _cached;
			}
		}

		Info result;
		try
		{
			result = OperatingSystem.IsWindows() ? DetectWindows() : DetectUnix();
		}
		catch (Exception ex)
		{
			AppPaths.Log("Browser: " + ex.Message);
			result = new Info();
		}

		lock (FileLock)
		{
			_cached = result;
			_cachedUtc = DateTime.UtcNow;
		}
		if (result.Found)
		{
			AppPaths.Log("Browser: браузер по умолчанию — " + result);
			SaveLastKnown(result);
		}
		return result;
	}

	/// <summary>
	/// Строка для журнала и настроек: «Google Chrome» или «не определён».
	/// </summary>
	public static string Describe() => Detect().ToString();

	/// <summary>Сбрасывает кэш — вызывается, когда настройки браузера могли измениться.</summary>
	public static void Forget()
	{
		lock (FileLock)
		{
			_cached = null;
			_cachedUtc = DateTime.MinValue;
		}
	}

	// ------------------------------------------------------------------ Windows

	private static Info DetectWindows()
	{
		// 1) пользовательская ассоциация для http/https — то, что реально выбрал человек
		foreach (string scheme in new string[3] { "http", "https", "http" })
		{
			string? progId = ReadUserChoice(scheme);
			if (string.IsNullOrWhiteSpace(progId))
			{
				continue;
			}
			Info? info = FromProgId(progId);
			if (info != null)
			{
				return info;
			}
		}

		// 2) запасной путь: ProgId по умолчанию из HKCR
		foreach (string scheme in new string[2] { "http", "https" })
		{
			try
			{
				using RegistryKey? key = Registry.ClassesRoot.OpenSubKey(scheme + "\\shell\\open\\command");
				string? command = key?.GetValue(null) as string;
				Info? info = FromCommand(command);
				if (info != null)
				{
					return info;
				}
			}
			catch
			{
				// ключ недоступен — идём дальше
			}
		}

		// 3) совсем запасной путь: известные браузеры по стандартным местам
		foreach (string name in new string[10] { "Chrome", "Firefox", "Edge", "Opera", "Brave", "Vivaldi", "YandexBrowser", "Chromium", "Safari", "Waterfox" })
		{
			Info? info = FromKnownName(name);
			if (info != null)
			{
				return info;
			}
		}

		// 4) последний известный рабочий браузер (если реестр вдруг прочитать не удалось)
		return LoadLastKnown() ?? new Info();
	}

	private static string? ReadUserChoice(string scheme)
	{
		try
		{
			using RegistryKey? key = Registry.CurrentUser.OpenSubKey(
				"Software\\Microsoft\\Windows\\Shell\\Associations\\UrlAssociations\\" + scheme + "\\UserChoice");
			return key?.GetValue("ProgId") as string;
		}
		catch (Exception ex)
		{
			AppPaths.Log("Browser: UserChoice " + scheme + " — " + ex.Message);
			return null;
		}
	}

	private static Info? FromProgId(string progId)
	{
		string[] roots = new string[2]
		{
			"Software\\Classes\\" + progId + "\\shell\\open\\command",
			progId + "\\shell\\open\\command"
		};
		foreach (string path in roots)
		{
			foreach (RegistryKey baseKey in new RegistryKey[2] { Registry.CurrentUser, Registry.ClassesRoot })
			{
				try
				{
					using RegistryKey? key = baseKey.OpenSubKey(path);
					string? command = key?.GetValue(null) as string;
					Info? info = FromCommand(command);
					if (info != null)
					{
						return info;
					}
				}
				catch
				{
					// нет доступа — пробуем следующий корень
				}
			}
		}
		return null;
	}

	/// <summary>Разбирает «"C:\...\chrome.exe" -- "%1"» в имя и путь.</summary>
	private static Info? FromCommand(string? command)
	{
		if (string.IsNullOrWhiteSpace(command))
		{
			return null;
		}
		string exe = ExtractExecutable(command);
		if (string.IsNullOrWhiteSpace(exe))
		{
			return null;
		}
		if (exe.StartsWith("iexplore", StringComparison.OrdinalIgnoreCase))
		{
			// Internet Explorer выбран «по умолчанию» — чаще это значит, что ассоциацию
			// перехватил кто-то другой, IE давно не браузер по выбору. Идём по списку дальше.
			return null;
		}
		string name = PrettyName(exe);
		if (!File.Exists(exe))
		{
			// путь из реестра устарел (браузер переустановили) — попробуем найти по имени
			return FromKnownName(System.IO.Path.GetFileNameWithoutExtension(exe));
		}
		return new Info
		{
			Name = name,
			ExePath = exe,
			LaunchTarget = exe
		};
	}

	private static string ExtractExecutable(string command)
	{
		string text = command.Trim();
		if (text.StartsWith("\"", StringComparison.Ordinal))
		{
			int end = text.IndexOf('"', 1);
			if (end > 1)
			{
				return text.Substring(1, end - 1);
			}
			return text.Trim('"');
		}
		// Без кавычек путь может содержать пробелы (C:\Program Files (x86)\...),
		// поэтому режем по «.exe», а не по первому пробелу.
		int exeIndex = text.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
		if (exeIndex > 0)
		{
			return text.Substring(0, exeIndex + 4);
		}
		int space = text.IndexOf(' ');
		return space > 0 ? text.Substring(0, space) : text;
	}

	private static readonly Dictionary<string, string[]> KnownPaths = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
	{
		["chrome"] = new string[2] { "Google\\Chrome\\Application\\chrome.exe", "Google\\Chrome Beta\\Application\\chrome.exe" },
		["firefox"] = new string[2] { "Mozilla Firefox\\firefox.exe", "Firefox\\firefox.exe" },
		["msedge"] = new string[2] { "Microsoft\\Edge\\Application\\msedge.exe", "Microsoft\\Edge Beta\\Application\\msedge.exe" },
		["opera"] = new string[2] { "Programs\\Opera\\opera.exe", "Opera\\opera.exe" },
		["brave"] = new string[2] { "BraveSoftware\\Brave-Browser\\Application\\brave.exe", "BraveSoftware\\Brave-Browser-Beta\\Application\\brave.exe" },
		["vivaldi"] = new string[2] { "Vivaldi\\Application\\vivaldi.exe", "Vivaldi\\Application\\vivaldi.exe" },
		["browser"] = new string[2] { "Yandex\\YandexBrowser\\Application\\browser.exe", "Yandex\\YandexBrowser\\Application\\browser.exe" },
		["chromium"] = new string[2] { "Chromium\\Application\\chrome.exe", "Chromium\\Application\\chromium.exe" },
		["waterfox"] = new string[2] { "Waterfox\\waterfox.exe", "Waterfox\\Waterfox.exe" }
	};

	private static Info? FromKnownName(string name)
	{
		if (string.IsNullOrWhiteSpace(name))
		{
			return null;
		}
		string key = name.ToLowerInvariant().Replace(".exe", "", StringComparison.Ordinal);
		if (!KnownPaths.TryGetValue(key, out string[]? relatives))
		{
			return null;
		}
		string[] roots = new string[2]
		{
			Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
			Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
		};
		foreach (string root in roots)
		{
			if (string.IsNullOrWhiteSpace(root))
			{
				continue;
			}
			foreach (string relative in relatives)
			{
				string full = System.IO.Path.Combine(root, relative);
				if (File.Exists(full))
				{
					return new Info
					{
						Name = PrettyName(full),
						ExePath = full,
						LaunchTarget = full
					};
				}
			}
		}
		return null;
	}

	private static string PrettyName(string exe)
	{
		string fileName = System.IO.Path.GetFileNameWithoutExtension(exe).ToLowerInvariant();
		switch (fileName)
		{
			case "chrome":
			case "chromium":
				return "Google Chrome";
			case "firefox":
				return "Mozilla Firefox";
			case "msedge":
				return "Microsoft Edge";
			case "opera":
			case "launcher":
				return exe.Contains("Opera GX", StringComparison.OrdinalIgnoreCase) ? "Opera GX" : "Opera";
			case "brave":
				return "Brave";
			case "vivaldi":
				return "Vivaldi";
			case "browser":
				return "Яндекс Браузер";
			case "waterfox":
				return "Waterfox";
			case "iexplore":
				return "Internet Explorer";
			case "safari":
				return "Safari";
			case "360chrome":
			case "360se":
				return "360 Browser";
			case "wavebrowser":
				return "Wave Browser";
			default:
				return fileName.Length > 0 ? char.ToUpperInvariant(fileName[0]) + fileName.Substring(1) : "браузер";
		}
	}

	// ------------------------------------------------------------- Linux / macOS

	private static Info DetectUnix()
	{
		if (OperatingSystem.IsMacOS())
		{
			return new Info { Name = "Safari", LaunchTarget = "open" };
		}
		string? desktop = RunCapture("xdg-settings", "get default-web-browser");
		if (string.IsNullOrWhiteSpace(desktop))
		{
			string? mime = RunCapture("xdg-mime", "query default x-scheme-handler/http");
			desktop = mime?.Trim();
		}
		desktop = desktop?.Trim();
		if (string.IsNullOrWhiteSpace(desktop))
		{
			return new Info();
		}
		string name = desktop.Replace(".desktop", "", StringComparison.OrdinalIgnoreCase);
		name = name.Contains(' ') ? name : char.ToUpperInvariant(name[0]) + name.Substring(1);
		return new Info
		{
			Name = name,
			LaunchTarget = desktop
		};
	}

	private static string? RunCapture(string fileName, string arguments)
	{
		try
		{
			ProcessStartInfo info = new ProcessStartInfo(fileName, arguments)
			{
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				UseShellExecute = false,
				CreateNoWindow = true
			};
			using Process? process = Process.Start(info);
			if (process == null)
			{
				return null;
			}
			string output = process.StandardOutput.ReadToEnd();
			process.WaitForExit(4000);
			return process.ExitCode == 0 ? output.Trim() : null;
		}
		catch (Exception ex)
		{
			AppPaths.Log($"Browser: {fileName} — {ex.Message}");
			return null;
		}
	}

	// ------------------------------------------------------- последний известный

	private static void SaveLastKnown(Info info)
	{
		try
		{
			Directory.CreateDirectory(AppPaths.Root);
			File.WriteAllText(CacheFile, info.Name + "\n" + (info.ExePath ?? ""), new UTF8Encoding(false));
		}
		catch
		{
			// не критично: это лишь подсказка на случай сбоя реестра
		}
	}

	private static Info? LoadLastKnown()
	{
		try
		{
			if (!File.Exists(CacheFile))
			{
				return null;
			}
			string[] lines = File.ReadAllLines(CacheFile);
			string name = lines.Length > 0 ? lines[0].Trim() : "";
			string exe = lines.Length > 1 ? lines[1].Trim() : "";
			if (exe.Length > 0 && File.Exists(exe))
			{
				return new Info { Name = name.Length > 0 ? name : PrettyName(exe), ExePath = exe, LaunchTarget = exe };
			}
		}
		catch
		{
			// пустой результат — вызывающий подставит поведение по умолчанию
		}
		return null;
	}
}
