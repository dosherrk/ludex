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

public sealed class LauncherGame
{
	public string Launcher = "";

	public string Title = "";

	public string Folder = "";

	public string Exe = "";

	public int? AppId;

	public string ExternalId = "";
}

public sealed class LauncherState
{
	public string Key = "";

	public bool Installed;

	public string? Error;

	public List<LauncherGame> Games = new List<LauncherGame>();

	public int Count => Games.Count;
}

public static class Launchers
{
	public sealed record Descriptor(string Key, bool WindowsOnly);

	public static readonly Descriptor[] All = new Descriptor[8]
	{
		new Descriptor("steam", WindowsOnly: false),
		new Descriptor("epic", WindowsOnly: false),
		new Descriptor("gog", WindowsOnly: false),
		new Descriptor("ubisoft", WindowsOnly: true),
		new Descriptor("ea", WindowsOnly: true),
		new Descriptor("battlenet", WindowsOnly: true),
		new Descriptor("heroic", WindowsOnly: false),
		new Descriptor("lutris", WindowsOnly: false)
	};

	private static List<LauncherState>? _session;

	private static readonly string[] ToolPatterns = new string[12]
	{
		"redistributable", "steamworks", "steam linux runtime", "steam runtime", "proton", "steamvr", "steam audio", "epic online services", "unreal engine", "steam controller",
		"easyanticheat", "battleye"
	};

	private static readonly int[] ToolAppIds = new int[9] { 228980, 1070560, 1391110, 1628350, 1493710, 2180100, 1826330, 1161040, 250820 };

	private static readonly string[] ExeIgnore = new string[24]
	{
		"unins", "setup", "install", "vcredist", "dxsetup", "directx", "dotnet", "crashreport", "crashpad", "unitycrashhandler",
		"launcher", "eossdk", "eac", "helper", "updater", "update", "repair", "config", "touchup", "prereq",
		"redist", "activation", "notification", "benchmark"
	};

	public static bool HasSessionCache => _session != null;

	private static string Home => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

	private static string ProgramData => Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

	private static string AppData => Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

	public static bool IsSupported(string key)
	{
		string key2 = key;
		Descriptor descriptor = All.FirstOrDefault((Descriptor x) => x.Key == key2);
		if (descriptor == null)
		{
			return false;
		}
		if (descriptor.WindowsOnly)
		{
			return OperatingSystem.IsWindows();
		}
		return true;
	}

	public static List<LauncherState> ScanAll(IEnumerable<string> enabled)
	{
		return _session ?? (_session = DetectAll());
	}

	public static List<LauncherState> RescanAll(IEnumerable<string> enabled)
	{
		return _session = DetectAll();
	}

	private static List<LauncherState> DetectAll()
	{
		List<LauncherState> list = new List<LauncherState>();
		Descriptor[] all = All;
		foreach (Descriptor descriptor in all)
		{
			if (IsSupported(descriptor.Key))
			{
				LauncherState launcherState = new LauncherState
				{
					Key = descriptor.Key
				};
				try
				{
					(bool Installed, List<LauncherGame> Games) tuple = Detect(descriptor.Key);
					bool item = tuple.Installed;
					List<LauncherGame> item2 = tuple.Games;
					launcherState.Installed = item;
					launcherState.Games = item2;
				}
				catch (Exception ex)
				{
					launcherState.Error = ex.Message;
					AppPaths.Log("Launchers " + descriptor.Key + ": " + ex.Message);
				}
				launcherState.Games = (from gr in launcherState.Games.Where((LauncherGame g) => !string.IsNullOrWhiteSpace(g.Title) && !string.IsNullOrWhiteSpace(g.Folder)).GroupBy<LauncherGame, string>((LauncherGame g) => g.Folder, StringComparer.OrdinalIgnoreCase)
					select gr.First()).ToList();
				if (!launcherState.Installed && launcherState.Games.Count > 0)
				{
					launcherState.Installed = true;
				}
				list.Add(launcherState);
			}
		}
		return list;
	}

	public static LauncherState ScanOne(string key)
	{
		LauncherState launcherState = new LauncherState
		{
			Key = key
		};
		try
		{
			(bool Installed, List<LauncherGame> Games) tuple = Detect(key);
			bool item = tuple.Installed;
			List<LauncherGame> item2 = tuple.Games;
			launcherState.Installed = item;
			launcherState.Games = item2;
		}
		catch (Exception ex)
		{
			launcherState.Error = ex.Message;
		}
		return launcherState;
	}

	public static List<LauncherGame> Import(IEnumerable<string> enabled)
	{
		List<LauncherGame> list = new List<LauncherGame>();
		foreach (LauncherState item in ScanAll(enabled))
		{
			if (enabled.Contains<string>(item.Key, StringComparer.OrdinalIgnoreCase))
			{
				AppPaths.Log($"Launchers {item.Key}: {item.Games.Count} игр" + ((item.Error != null) ? (", ошибка: " + item.Error) : ""));
				list.AddRange(item.Games);
			}
		}
		return list;
	}

	private static (bool Installed, List<LauncherGame> Games) Detect(string key)
	{
		return key switch
		{
			"steam" => Steam(), 
			"epic" => Epic(), 
			"gog" => Gog(), 
			"ubisoft" => Ubisoft(), 
			"ea" => Ea(), 
			"battlenet" => BattleNet(), 
			"heroic" => Heroic(), 
			"lutris" => Lutris(), 
			_ => (Installed: false, Games: new List<LauncherGame>()), 
		};
	}

	public static bool LooksInstalled(string? folder)
	{
		if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
		{
			return false;
		}
		try
		{
			int num = 0;
			foreach (string item in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
			{
				if (++num > 4000)
				{
					return true;
				}
				FileInfo fileInfo = new FileInfo(item);
				string text = fileInfo.Extension.ToLowerInvariant();
				bool flag;
				switch (text)
				{
				case ".exe":
				case ".sh":
				case ".appimage":
				case ".x86_64":
				case ".x86":
				case ".run":
					flag = true;
					break;
				default:
					flag = false;
					break;
				}
				if (flag)
				{
					return true;
				}
				if (fileInfo.Length >= 20971520)
				{
					return true;
				}
				if (text.Length != 0 || fileInfo.Length < 1048576 || OperatingSystem.IsWindows())
				{
					continue;
				}
				try
				{
					if ((File.GetUnixFileMode(item) & (UnixFileMode.OtherExecute | UnixFileMode.GroupExecute | UnixFileMode.UserExecute)) != 0)
					{
						return true;
					}
				}
				catch
				{
				}
			}
			return false;
		}
		catch
		{
			return false;
		}
	}

	public static bool LooksInstalledStrict(string? folder, long minBytes = 314572800L)
	{
		if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
		{
			return false;
		}
		bool flag = false;
		long num = 0L;
		bool flag2;
		try
		{
			foreach (string item in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
			{
				FileInfo fileInfo;
				try
				{
					fileInfo = new FileInfo(item);
				}
				catch
				{
					continue;
				}
				num += fileInfo.Length;
				if (!flag)
				{
					string text = fileInfo.Extension.ToLowerInvariant();
					switch (text)
					{
					case ".exe":
					case ".sh":
					case ".appimage":
					case ".x86_64":
					case ".x86":
					case ".run":
						flag2 = true;
						break;
					default:
						flag2 = false;
						break;
					}
					if (flag2)
					{
						flag = true;
					}
					else if (text.Length == 0 && fileInfo.Length >= 1048576 && !OperatingSystem.IsWindows())
					{
						try
						{
							if ((File.GetUnixFileMode(item) & (UnixFileMode.OtherExecute | UnixFileMode.GroupExecute | UnixFileMode.UserExecute)) != 0)
							{
								flag = true;
							}
						}
						catch
						{
						}
					}
				}
				if (!flag || num < minBytes)
				{
					continue;
				}
				flag2 = true;
				goto end_IL_0017;
			}
			flag2 = false;
			end_IL_0017:;
		}
		catch
		{
			flag2 = false;
		}
		return flag2;
	}

	public static bool IsToolOrRuntime(string? title, int? appId = null)
	{
		if (appId.HasValue && Array.IndexOf(ToolAppIds, appId.Value) >= 0)
		{
			return true;
		}
		if (string.IsNullOrWhiteSpace(title))
		{
			return false;
		}
		string @object = title.ToLowerInvariant();
		return ToolPatterns.Any(@object.Contains);
	}

	private static IEnumerable<string> Existing(params string[] paths)
	{
		return paths.Where((string p) => !string.IsNullOrWhiteSpace(p) && Directory.Exists(p));
	}

	private static IEnumerable<string> ExistingFiles(params string[] paths)
	{
		return paths.Where((string p) => !string.IsNullOrWhiteSpace(p) && File.Exists(p));
	}

	private static List<string> CollectLibraries(string steamRoot)
	{
		List<string> ordered = new List<string>();
		HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		Queue<string> queue = new Queue<string>();
		Add(steamRoot);
		while (queue.Count > 0)
		{
			string path2 = queue.Dequeue();
			foreach (string item in ExistingFiles(System.IO.Path.Combine(path2, "steamapps", "libraryfolders.vdf"), System.IO.Path.Combine(path2, "config", "libraryfolders.vdf")))
			{
				try
				{
					string input = File.ReadAllText(item);
					foreach (Match item2 in Regex.Matches(input, "\"path\"\\s*\"([^\"]+)\""))
					{
						Add(item2.Groups[1].Value.Replace("\\\\", "\\"));
					}
					foreach (Match item3 in Regex.Matches(input, "\"\\d+\"\\s*\"([^\"]+)\""))
					{
						string text = item3.Groups[1].Value.Replace("\\\\", "\\");
						if (text.Contains(':') || text.StartsWith('/'))
						{
							Add(text);
						}
					}
				}
				catch (Exception ex)
				{
					AppPaths.Log("libraryfolders.vdf: " + ex.Message);
				}
			}
		}
		return ordered;
		void Add(string? path)
		{
			if (!string.IsNullOrWhiteSpace(path))
			{
				string text2 = path.Trim().TrimEnd('\\', '/');
				if (text2.Length != 0 && seen.Add(text2))
				{
					ordered.Add(text2);
					queue.Enqueue(text2);
				}
			}
		}
	}

	private static (bool, List<LauncherGame>) Steam()
	{
		List<string> list = new List<string>();
		if (OperatingSystem.IsWindows())
		{
			string[] array = new string[4]
			{
				RegRead("HKEY_CURRENT_USER\\Software\\Valve\\Steam", "SteamPath"),
				RegRead("HKEY_LOCAL_MACHINE\\SOFTWARE\\WOW6432Node\\Valve\\Steam", "InstallPath"),
				"C:\\Program Files (x86)\\Steam",
				"C:\\Program Files\\Steam"
			};
			foreach (string text in array)
			{
				if (!string.IsNullOrWhiteSpace(text))
				{
					list.Add(text);
				}
			}
		}
		else if (OperatingSystem.IsMacOS())
		{
			list.Add(System.IO.Path.Combine(Home, "Library/Application Support/Steam"));
		}
		else
		{
			list.AddRange(new string[4]
			{
				System.IO.Path.Combine(Home, ".steam/steam"),
				System.IO.Path.Combine(Home, ".local/share/Steam"),
				System.IO.Path.Combine(Home, ".var/app/com.valvesoftware.Steam/.local/share/Steam"),
				System.IO.Path.Combine(Home, "snap/steam/common/.local/share/Steam")
			});
		}
		string text2 = Existing(list.ToArray()).FirstOrDefault();
		if (text2 == null)
		{
			return (false, new List<LauncherGame>());
		}
		List<string> list2 = CollectLibraries(text2);
		AppPaths.Log($"Steam: библиотек {list2.Count} — " + string.Join(" | ", list2));
		List<LauncherGame> list3 = new List<LauncherGame>();
		HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (string item in list2.Distinct<string>(StringComparer.OrdinalIgnoreCase))
		{
			string text3 = System.IO.Path.Combine(item, "steamapps");
			if (!Directory.Exists(text3))
			{
				continue;
			}
			foreach (string item2 in Directory.EnumerateFiles(text3, "appmanifest_*.acf"))
			{
				try
				{
					string text4 = File.ReadAllText(item2);
					string text5 = Field(text4, "name");
					string text6 = Field(text4, "installdir");
					if (string.IsNullOrWhiteSpace(text5) || string.IsNullOrWhiteSpace(text6))
					{
						continue;
					}
					string text7 = System.IO.Path.Combine(text3, "common", text6);
					if (!string.IsNullOrWhiteSpace(text7) && LooksInstalled(text7))
					{
						int result;
						int? num = (int.TryParse(Field(text4, "appid"), out result) ? new int?(result) : null);
						if (int.TryParse(Field(text4, "StateFlags"), out var result2) && (result2 & 4) == 0)
						{
							AppPaths.Log("Steam: пропущена неустановленная игра " + text5);
							continue;
						}
						if (IsToolOrRuntime(text5, num))
						{
							AppPaths.Log("Steam: пропущен служебный компонент " + text5);
							continue;
						}
						hashSet.Add(text7);
						list3.Add(new LauncherGame
						{
							Launcher = "steam",
							Title = Clean(text5),
							Folder = text7,
							Exe = FindExe(text7),
							AppId = num,
							ExternalId = (num?.ToString() ?? "")
						});
					}
				}
				catch (Exception ex)
				{
					AppPaths.Log("appmanifest: " + ex.Message);
				}
			}
			string path = System.IO.Path.Combine(text3, "common");
			if (!Directory.Exists(path))
			{
				continue;
			}
			foreach (string item3 in Directory.EnumerateDirectories(path))
			{
				if (hashSet.Contains(item3))
				{
					continue;
				}
				hashSet.Add(item3);
				string fileName = System.IO.Path.GetFileName(item3.TrimEnd(System.IO.Path.DirectorySeparatorChar));
				if (!string.IsNullOrWhiteSpace(fileName) && !fileName.StartsWith('.') && !IsToolOrRuntime(fileName))
				{
					if (!LooksInstalledStrict(item3, 314572800L))
					{
						AppPaths.Log("Steam: пропущена папка без манифеста " + fileName);
						continue;
					}
					list3.Add(new LauncherGame
					{
						Launcher = "steam",
						Title = Clean(fileName),
						Folder = item3,
						Exe = FindExe(item3)
					});
				}
			}
		}
		return (list3.Count > 0 || text2 != null, list3);
	}

	private static string Field(string text, string key)
	{
		Match match = Regex.Match(text, "\"" + Regex.Escape(key) + "\"\\s*\"([^\"]*)\"", RegexOptions.IgnoreCase);
		if (!match.Success)
		{
			return "";
		}
		return match.Groups[1].Value;
	}

	private static (bool, List<LauncherGame>) Epic()
	{
		List<LauncherGame> list = new List<LauncherGame>();
		bool item = false;
		foreach (string item2 in Existing(System.IO.Path.Combine(ProgramData, "Epic", "EpicGamesLauncher", "Data", "Manifests")))
		{
			item = true;
			foreach (string item3 in Directory.EnumerateFiles(item2, "*.item"))
			{
				try
				{
					using JsonDocument jsonDocument = JsonDocument.Parse(File.ReadAllText(item3));
					JsonElement rootElement = jsonDocument.RootElement;
					string text = Str(rootElement, "DisplayName");
					string text2 = Str(rootElement, "InstallLocation");
					string text3 = Str(rootElement, "LaunchExecutable");
					string externalId = Str(rootElement, "AppName");
					if (!string.IsNullOrWhiteSpace(text) && !string.IsNullOrWhiteSpace(text2) && !IsToolOrRuntime(text) && LooksInstalled(text2))
					{
						list.Add(new LauncherGame
						{
							Launcher = "epic",
							Title = Clean(text),
							Folder = text2,
							ExternalId = externalId,
							Exe = ((text3 != "") ? System.IO.Path.Combine(text2, text3.Replace('/', System.IO.Path.DirectorySeparatorChar)) : FindExe(text2))
						});
					}
				}
				catch (Exception ex)
				{
					AppPaths.Log("epic item: " + ex.Message);
				}
			}
		}
		foreach (string item4 in ExistingFiles(System.IO.Path.Combine(Home, ".config", "legendary", "installed.json")))
		{
			item = true;
			list.AddRange(ReadInstalledMap(item4, "epic"));
		}
		return (item, list);
	}

	private static (bool, List<LauncherGame>) Gog()
	{
		List<LauncherGame> list = new List<LauncherGame>();
		bool item = false;
		if (OperatingSystem.IsWindows())
		{
			string[] array = new string[2] { "HKEY_LOCAL_MACHINE\\SOFTWARE\\WOW6432Node\\GOG.com\\Games", "HKEY_LOCAL_MACHINE\\SOFTWARE\\GOG.com\\Games" };
			foreach (string text in array)
			{
				foreach (string item2 in RegSubKeys(text))
				{
					item = true;
					string text2 = RegRead(text + "\\" + item2, "gameName");
					string text3 = RegRead(text + "\\" + item2, "path");
					string text4 = RegRead(text + "\\" + item2, "exe");
					if (!string.IsNullOrWhiteSpace(text3) && LooksInstalled(text3))
					{
						list.Add(new LauncherGame
						{
							Launcher = "gog",
							Title = Clean(string.IsNullOrWhiteSpace(text2) ? System.IO.Path.GetFileName(text3.TrimEnd('\\', '/')) : text2),
							Folder = text3,
							Exe = ((!string.IsNullOrWhiteSpace(text4) && File.Exists(text4)) ? text4 : FindExe(text3))
						});
					}
				}
			}
		}
		return (item, list);
	}

	private static (bool, List<LauncherGame>) Ubisoft()
	{
		if (!OperatingSystem.IsWindows())
		{
			return (false, new List<LauncherGame>());
		}
		List<LauncherGame> list = new List<LauncherGame>();
		bool flag = false;
		string[] array = new string[2] { "HKEY_LOCAL_MACHINE\\SOFTWARE\\WOW6432Node\\Ubisoft\\Launcher\\Installs", "HKEY_LOCAL_MACHINE\\SOFTWARE\\Ubisoft\\Launcher\\Installs" };
		foreach (string text in array)
		{
			foreach (string item in RegSubKeys(text))
			{
				flag = true;
				string text2 = RegRead(text + "\\" + item, "InstallDir");
				if (!string.IsNullOrWhiteSpace(text2) && LooksInstalled(text2))
				{
					list.Add(new LauncherGame
					{
						Launcher = "ubisoft",
						Title = Clean(System.IO.Path.GetFileName(text2.TrimEnd('\\', '/'))),
						Folder = text2,
						Exe = FindExe(text2)
					});
				}
			}
		}
		if (!flag)
		{
			flag = Directory.Exists(System.IO.Path.Combine(ProgramData, "Ubisoft Game Launcher"));
		}
		return (flag, list);
	}

	private static (bool, List<LauncherGame>) Ea()
	{
		if (!OperatingSystem.IsWindows())
		{
			return (false, new List<LauncherGame>());
		}
		List<LauncherGame> list = new List<LauncherGame>();
		bool item = false;
		string[] array = new string[3] { "HKEY_LOCAL_MACHINE\\SOFTWARE\\WOW6432Node\\EA Games", "HKEY_LOCAL_MACHINE\\SOFTWARE\\WOW6432Node\\Origin Games", "HKEY_LOCAL_MACHINE\\SOFTWARE\\EA Games" };
		foreach (string text in array)
		{
			foreach (string item2 in RegSubKeys(text))
			{
				item = true;
				string text2 = RegRead(text + "\\" + item2, "Install Dir") ?? RegRead(text + "\\" + item2, "InstallDir");
				if (!string.IsNullOrWhiteSpace(text2) && LooksInstalled(text2))
				{
					string text3 = RegRead(text + "\\" + item2, "DisplayName");
					list.Add(new LauncherGame
					{
						Launcher = "ea",
						Title = Clean(string.IsNullOrWhiteSpace(text3) ? System.IO.Path.GetFileName(text2.TrimEnd('\\', '/')) : text3),
						Folder = text2,
						Exe = FindExe(text2)
					});
				}
			}
		}
		return (item, list);
	}

	private static (bool, List<LauncherGame>) BattleNet()
	{
		if (!OperatingSystem.IsWindows())
		{
			return (false, new List<LauncherGame>());
		}
		List<LauncherGame> list = new List<LauncherGame>();
		bool item = false;
		foreach (string item2 in RegSubKeys("HKEY_LOCAL_MACHINE\\SOFTWARE\\WOW6432Node\\Blizzard Entertainment"))
		{
			string text = RegRead("HKEY_LOCAL_MACHINE\\SOFTWARE\\WOW6432Node\\Blizzard Entertainment\\" + item2, "InstallPath");
			if (!string.IsNullOrWhiteSpace(text))
			{
				item = true;
				if (!string.IsNullOrWhiteSpace(text) && LooksInstalled(text))
				{
					list.Add(new LauncherGame
					{
						Launcher = "battlenet",
						Title = Clean(item2),
						Folder = text,
						Exe = FindExe(text)
					});
				}
			}
		}
		return (item, list);
	}

	private static (bool, List<LauncherGame>) Heroic()
	{
		List<LauncherGame> list = new List<LauncherGame>();
		bool item = false;
		foreach (string item2 in Existing(System.IO.Path.Combine(Home, ".config", "heroic", "store_cache"), System.IO.Path.Combine(Home, ".var/app/com.heroicgameslauncher.hgl/config/heroic/store_cache")))
		{
			item = true;
			foreach (string item3 in Directory.EnumerateFiles(item2, "*_library.json"))
			{
				string text = System.IO.Path.GetFileName(item3).Replace("_library.json", "");
				string launcher = text switch
				{
					"legendary" => "epic", 
					"gog" => "gog", 
					"nile" => "amazon", 
					_ => text, 
				};
				try
				{
					using JsonDocument jsonDocument = JsonDocument.Parse(File.ReadAllText(item3));
					if (jsonDocument.RootElement.ValueKind != JsonValueKind.Object)
					{
						continue;
					}
					foreach (JsonProperty item4 in jsonDocument.RootElement.EnumerateObject())
					{
						JsonElement value = item4.Value;
						if (value.ValueKind != JsonValueKind.Object)
						{
							continue;
						}
						string text2 = Str(value, "install_path");
						if (string.IsNullOrWhiteSpace(text2) && value.TryGetProperty("install", out var value2) && value2.ValueKind == JsonValueKind.Object)
						{
							text2 = Str(value2, "install_path");
						}
						if (!string.IsNullOrWhiteSpace(text2) && LooksInstalled(text2))
						{
							string text3 = Str(value, "title");
							if (string.IsNullOrWhiteSpace(text3))
							{
								text3 = item4.Name;
							}
							list.Add(new LauncherGame
							{
								Launcher = launcher,
								Title = Clean(text3),
								Folder = text2,
								Exe = FindExe(text2)
							});
						}
					}
				}
				catch (Exception ex)
				{
					AppPaths.Log("heroic " + text + ": " + ex.Message);
				}
			}
		}
		return (item, list);
	}

	private static (bool, List<LauncherGame>) Lutris()
	{
		IEnumerable<string> enumerable = Existing(System.IO.Path.Combine(Home, ".config", "lutris", "games"), System.IO.Path.Combine(Home, ".local", "share", "lutris", "games"), System.IO.Path.Combine(Home, ".var/app/net.lutris.Lutris/config/lutris/games"));
		List<LauncherGame> list = new List<LauncherGame>();
		bool item = false;
		foreach (string item2 in enumerable)
		{
			item = true;
			foreach (string item3 in Directory.EnumerateFiles(item2, "*.yml"))
			{
				try
				{
					string text = null;
					string text2 = null;
					string text3 = null;
					foreach (string item4 in File.ReadLines(item3))
					{
						string text4 = item4.TrimStart();
						if (text4.StartsWith("name:"))
						{
							string text5 = text4;
							text = Unquote(text5.Substring(5, text5.Length - 5));
						}
						else if (text4.StartsWith("exe:"))
						{
							string text6 = text4;
							text2 = Unquote(text6.Substring(4, text6.Length - 4));
						}
						else if (text4.StartsWith("directory:"))
						{
							string text7 = text4;
							text3 = Unquote(text7.Substring(10, text7.Length - 10));
						}
					}
					if (!string.IsNullOrWhiteSpace(text))
					{
						string text8 = ((!string.IsNullOrWhiteSpace(text3) && Directory.Exists(text3)) ? text3 : ((text2 != null && File.Exists(text2)) ? (System.IO.Path.GetDirectoryName(text2) ?? "") : ""));
						if (!string.IsNullOrWhiteSpace(text8) && LooksInstalled(text8))
						{
							list.Add(new LauncherGame
							{
								Launcher = "lutris",
								Title = Clean(text),
								Folder = text8,
								Exe = ((text2 != null && File.Exists(text2)) ? text2 : FindExe(text8))
							});
						}
					}
				}
				catch (Exception ex)
				{
					AppPaths.Log("lutris yml: " + ex.Message);
				}
			}
		}
		return (item, list);
	}

	private static string Unquote(string value)
	{
		return value.Trim().Trim('\'', '"');
	}

	private static string Clean(string title)
	{
		return Regex.Replace(title.Replace("™", "").Replace("®", "").Replace("©", "")
			.Trim(), "\\s{2,}", " ");
	}

	private static string Str(JsonElement obj, string name)
	{
		if (!obj.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String)
		{
			return "";
		}
		return value.GetString() ?? "";
	}

	private static IEnumerable<LauncherGame> ReadInstalledMap(string file, string launcher)
	{
		List<LauncherGame> list = new List<LauncherGame>();
		try
		{
			using JsonDocument jsonDocument = JsonDocument.Parse(File.ReadAllText(file));
			if (jsonDocument.RootElement.ValueKind != JsonValueKind.Object)
			{
				return list;
			}
			foreach (JsonProperty item in jsonDocument.RootElement.EnumerateObject())
			{
				JsonElement value = item.Value;
				if (value.ValueKind != JsonValueKind.Object)
				{
					continue;
				}
				string text = Str(value, "install_path");
				if (!string.IsNullOrWhiteSpace(text) && LooksInstalled(text))
				{
					string text2 = Str(value, "title");
					if (string.IsNullOrWhiteSpace(text2))
					{
						text2 = item.Name;
					}
					string text3 = Str(value, "executable");
					list.Add(new LauncherGame
					{
						Launcher = launcher,
						Title = Clean(text2),
						Folder = text,
						Exe = ((!string.IsNullOrWhiteSpace(text3) && File.Exists(text3)) ? text3 : FindExe(text)),
						ExternalId = ((launcher == "epic") ? item.Name : "")
					});
				}
			}
		}
		catch (Exception ex)
		{
			AppPaths.Log("installed.json: " + ex.Message);
		}
		return list;
	}

	private static string FindExe(string folder)
	{
		try
		{
			List<string> list = new List<string>();
			foreach (string item in Directory.EnumerateFiles(folder, "*.exe"))
			{
				list.Add(item);
			}
			foreach (string item2 in Directory.EnumerateDirectories(folder).Take(24))
			{
				bool flag;
				switch (System.IO.Path.GetFileName(item2).ToLowerInvariant())
				{
				case "redist":
				case "_commonredist":
				case "support":
				case "docs":
				case "soundtrack":
					flag = true;
					break;
				default:
					flag = false;
					break;
				}
				if (!flag)
				{
					try
					{
						list.AddRange(Directory.EnumerateFiles(item2, "*.exe"));
					}
					catch
					{
					}
				}
			}
			return (from p in list
				where !ExeIgnore.Any((string bad) => System.IO.Path.GetFileName(p).ToLowerInvariant().Contains(bad))
				orderby System.IO.Path.GetFileName(p).Length descending, p
				select p).FirstOrDefault() ?? list.FirstOrDefault() ?? "";
		}
		catch
		{
			return "";
		}
	}

	private static IEnumerable<string> RegSubKeys(string path)
	{
		if (!OperatingSystem.IsWindows())
		{
			yield break;
		}
		string[] subKeyNames;
		try
		{
			using RegistryKey registryKey = Registry.LocalMachine.OpenSubKey(path.Replace("HKEY_LOCAL_MACHINE\\", "").Replace("HKEY_CURRENT_USER\\", ""));
			if (registryKey == null)
			{
				yield break;
			}
			subKeyNames = registryKey.GetSubKeyNames();
		}
		catch
		{
			yield break;
		}
		string[] array = subKeyNames;
		for (int i = 0; i < array.Length; i++)
		{
			yield return array[i];
		}
	}

	private static string? RegRead(string path, string name)
	{
		if (!OperatingSystem.IsWindows())
		{
			return null;
		}
		try
		{
			RegistryKey obj = (path.StartsWith("HKEY_CURRENT_USER", StringComparison.OrdinalIgnoreCase) ? Registry.CurrentUser : Registry.LocalMachine);
			string name2 = Regex.Replace(path, "^HKEY_(LOCAL_MACHINE|CURRENT_USER)\\\\", "", RegexOptions.IgnoreCase);
			using RegistryKey registryKey = obj.OpenSubKey(name2);
			string text = registryKey?.GetValue(name) as string;
			return string.IsNullOrWhiteSpace(text) ? null : text;
		}
		catch
		{
			return null;
		}
	}
}
