using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Ludex.Services;

/// <summary>Игра, найденная в чужом лаунчере (Steam, Epic, GOG, Heroic, Lutris...).</summary>
public sealed class LauncherGame
{
    public string Launcher = "";
    public string Title = "";
    public string Folder = "";
    public string Exe = "";

    /// <summary>Steam AppId (из манифеста) — по нему обложка берётся напрямую с CDN.</summary>
    public int? AppId;

    /// <summary>Идентификатор внутри лаунчера (Epic AppName, GOG product id).</summary>
    public string ExternalId = "";
}

/// <summary>Итог проверки одного лаунчера: установлен ли, сколько игр нашлось.</summary>
public sealed class LauncherState
{
    public string Key = "";
    public bool Installed;
    public string? Error;
    public List<LauncherGame> Games = new();

    public int Count => Games.Count;
}

/// <summary>
/// Импорт игр из других лаунчеров. Никаких паролей и облачных API: читаем только
/// то, что лаунчеры и так хранят на диске (манифесты, реестр, конфиги Heroic и Lutris).
/// Поэтому «подключение» = разрешение забрать локальный список игр.
/// </summary>
public static class Launchers
{
    public sealed record Descriptor(string Key, bool WindowsOnly);

    /// <summary>Порядок важен: в панели интеграций список идёт так же.</summary>
    public static readonly Descriptor[] All =
    {
        new("steam", false),
        new("epic", false),
        new("gog", false),
        new("ubisoft", true),
        new("ea", true),
        new("battlenet", true),
        new("heroic", false),
        new("lutris", false),
    };

    /// <summary>Лаунчер доступен на этой системе (только Windows-интеграции скрываем на Linux/macOS).</summary>
    public static bool IsSupported(string key)
    {
        var d = All.FirstOrDefault(x => x.Key == key);
        if (d == null) return false;
        return !d.WindowsOnly || OperatingSystem.IsWindows();
    }

    /// <summary>Результат проверки за текущий запуск: лаунчеры ищутся один раз, не при каждом открытии панели.</summary>
    private static List<LauncherState>? _session;

    public static bool HasSessionCache => _session != null;

    /// <summary>Проверка при первом обращении; дальше отдаётся уже найденное.</summary>
    public static List<LauncherState> ScanAll(IEnumerable<string> enabled) => _session ??= DetectAll();

    /// <summary>Принудительная перепроверка (кнопка «Обновить» в интеграциях).</summary>
    public static List<LauncherState> RescanAll(IEnumerable<string> enabled) => _session = DetectAll();

    private static List<LauncherState> DetectAll()
    {
        var states = new List<LauncherState>();

        foreach (var d in All)
        {
            if (!IsSupported(d.Key)) continue;

            var state = new LauncherState { Key = d.Key };
            try
            {
                var (installed, games) = Detect(d.Key);
                state.Installed = installed;
                state.Games = games;
            }
            catch (Exception ex)
            {
                state.Error = ex.Message;
                AppPaths.Log("Launchers " + d.Key + ": " + ex.Message);
            }

            state.Games = state.Games
                .Where(g => !string.IsNullOrWhiteSpace(g.Title) && !string.IsNullOrWhiteSpace(g.Folder))
                .GroupBy(g => g.Folder, StringComparer.OrdinalIgnoreCase)
                .Select(gr => gr.First())
                .ToList();

            if (!state.Installed && state.Games.Count > 0) state.Installed = true;
            states.Add(state);
        }

        return states;
    }

    /// <summary>Проверка одного лаунчера (например, Steam при первом запуске).</summary>
    public static LauncherState ScanOne(string key)
    {
        var state = new LauncherState { Key = key };
        try
        {
            var (installed, games) = Detect(key);
            state.Installed = installed;
            state.Games = games;
        }
        catch (Exception ex)
        {
            state.Error = ex.Message;
        }

        return state;
    }

    /// <summary>Игры только включённых лаунчеров — для библиотеки.</summary>
    public static List<LauncherGame> Import(IEnumerable<string> enabled)
    {
        var games = new List<LauncherGame>();
        foreach (var state in ScanAll(enabled))
        {
            if (!enabled.Contains(state.Key, StringComparer.OrdinalIgnoreCase)) continue;

            // в лог: сколько игр нашлось в каждом лаунчере — проще разбирать «нашлись не все»
            AppPaths.Log($"Launchers {state.Key}: {state.Games.Count} игр" +
                         (state.Error != null ? ", ошибка: " + state.Error : ""));
            games.AddRange(state.Games);
        }

        return games;
    }

    // ------------------------------------------------------------ определение

    private static (bool Installed, List<LauncherGame> Games) Detect(string key) => key switch
    {
        "steam" => Steam(),
        "epic" => Epic(),
        "gog" => Gog(),
        "ubisoft" => Ubisoft(),
        "ea" => Ea(),
        "battlenet" => BattleNet(),
        "heroic" => Heroic(),
        "lutris" => Lutris(),
        _ => (false, new List<LauncherGame>()),
    };

    /// <summary>
    /// Похоже ли это на настоящую установку: в папке есть исполняемый файл или крупный
    /// файл данных. Так отсекаются «хвосты» от давно удалённых игр — пустые каталоги
    /// и папки с парой логов, из-за которых в библиотеке появлялись игры, которых нет.
    /// </summary>
    public static bool LooksInstalled(string? folder)
    {
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder)) return false;

        try
        {
            var checkedCount = 0;
            foreach (var file in Directory.EnumerateFiles(folder!, "*", SearchOption.AllDirectories))
            {
                if (++checkedCount > 4000) return true; // папка явно большая — считаем установленной

                var info = new FileInfo(file);
                var ext = info.Extension.ToLowerInvariant();

                if (ext is ".exe" or ".sh" or ".appimage" or ".x86_64" or ".x86" or ".run") return true;
                if (info.Length >= 20L * 1024 * 1024) return true;

                if (ext.Length == 0 && info.Length >= 1L * 1024 * 1024 && !OperatingSystem.IsWindows())
                {
                    try
                    {
                        var mode = File.GetUnixFileMode(file);
                        if ((mode & (UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute)) != 0)
                            return true;
                    }
                    catch
                    {
                        // на Windows режима нет — просто продолжаем
                    }
                }
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Строгая версия проверки — для папок Steam без манифеста: нужен исполняемый файл
    /// и хотя бы немного данных. Так отсекаются «хвосты» от удалённых игр, в которых
    /// осталось несколько крупных файлов, но самой игры уже нет.
    /// </summary>
    public static bool LooksInstalledStrict(string? folder, long minBytes = 300L * 1024 * 1024)
    {
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder)) return false;

        var hasExe = false;
        long total = 0;

        try
        {
            foreach (var file in Directory.EnumerateFiles(folder!, "*", SearchOption.AllDirectories))
            {
                FileInfo info;
                try { info = new FileInfo(file); } catch { continue; }
                total += info.Length;

                if (!hasExe)
                {
                    var ext = info.Extension.ToLowerInvariant();
                    if (ext is ".exe" or ".sh" or ".appimage" or ".x86_64" or ".x86" or ".run")
                    {
                        hasExe = true;
                    }
                    else if (ext.Length == 0 && info.Length >= 1L * 1024 * 1024 && !OperatingSystem.IsWindows())
                    {
                        try
                        {
                            var mode = File.GetUnixFileMode(file);
                            if ((mode & (UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute)) != 0)
                                hasExe = true;
                        }
                        catch
                        {
                            // на Windows режима нет — пропускаем
                        }
                    }
                }

                if (hasExe && total >= minBytes) return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Служебные компоненты Steam/Epic: не игры, в библиотеке им не место.</summary>
    private static readonly string[] ToolPatterns =
    {
        "redistributable", "steamworks", "steam linux runtime", "steam runtime",
        "proton", "steamvr", "steam audio", "epic online services", "unreal engine",
        "steam controller", "easyanticheat", "battleye",
    };

    private static readonly int[] ToolAppIds =
    {
        228980,  // Steamworks Common Redistributables
        1070560, 1391110, 1628350, // Steam Linux Runtime
        1493710, 2180100, 1826330, 1161040, // Proton
        250820,   // SteamVR
    };

    public static bool IsToolOrRuntime(string? title, int? appId = null)
    {
        if (appId.HasValue && Array.IndexOf(ToolAppIds, appId.Value) >= 0) return true;
        if (string.IsNullOrWhiteSpace(title)) return false;

        var name = title!.ToLowerInvariant();
        return ToolPatterns.Any(name.Contains);
    }

    private static string Home => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    private static string ProgramData => Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

    private static string AppData => Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

    private static IEnumerable<string> Existing(params string[] paths) =>
        paths.Where(p => !string.IsNullOrWhiteSpace(p) && Directory.Exists(p));

    /// <summary>То же для файлов: Directory.Exists на пути к файлу всегда false.</summary>
    private static IEnumerable<string> ExistingFiles(params string[] paths) =>
        paths.Where(p => !string.IsNullOrWhiteSpace(p) && File.Exists(p));

    // ------------------------------------------------------------------ Steam

    /// <summary>
    /// Все библиотеки Steam: основная плюс всё, что перечислено в libraryfolders.vdf.
    /// Steam держит этот файл в двух местах (steamapps и config), а сами библиотеки
    /// часто лежат на других дисках — поэтому обходим файлы транзитивно и разбираем
    /// оба формата (новый с "path" и старый с номером).
    /// </summary>
    private static List<string> CollectLibraries(string steamRoot)
    {
        var ordered = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<string>();

        void Add(string? path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            var clean = path!.Trim().TrimEnd('\\', '/');
            if (clean.Length == 0) return;
            if (!seen.Add(clean)) return;
            ordered.Add(clean);
            queue.Enqueue(clean);
        }

        Add(steamRoot);

        while (queue.Count > 0)
        {
            var library = queue.Dequeue();

            foreach (var vdf in ExistingFiles(
                         Path.Combine(library, "steamapps", "libraryfolders.vdf"),
                         Path.Combine(library, "config", "libraryfolders.vdf")))
            {
                try
                {
                    var text = File.ReadAllText(vdf);

                    // новый формат: "path" "D:\\SteamLibrary"
                    foreach (Match m in Regex.Matches(text, "\"path\"\\s*\"([^\"]+)\""))
                        Add(m.Groups[1].Value.Replace("\\\\", "\\"));

                    // старый формат: "1" "D:\\SteamLibrary"
                    foreach (Match m in Regex.Matches(text, "\"\\d+\"\\s*\"([^\"]+)\""))
                    {
                        var value = m.Groups[1].Value.Replace("\\\\", "\\");
                        if (value.Contains(':') || value.StartsWith('/')) Add(value);
                    }
                }
                catch (Exception ex)
                {
                    AppPaths.Log("libraryfolders.vdf: " + ex.Message);
                }
            }
        }

        return ordered;
    }

    private static (bool, List<LauncherGame>) Steam()
    {
        var roots = new List<string>();

        if (OperatingSystem.IsWindows())
        {
            foreach (var path in new[]
                     {
                         RegRead(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath"),
                         RegRead(@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath"),
                         @"C:\Program Files (x86)\Steam",
                         @"C:\Program Files\Steam",
                     })
            {
                if (!string.IsNullOrWhiteSpace(path)) roots.Add(path!);
            }
        }
        else if (OperatingSystem.IsMacOS())
        {
            roots.Add(Path.Combine(Home, "Library/Application Support/Steam"));
        }
        else
        {
            roots.AddRange(new[]
            {
                Path.Combine(Home, ".steam/steam"),
                Path.Combine(Home, ".local/share/Steam"),
                Path.Combine(Home, ".var/app/com.valvesoftware.Steam/.local/share/Steam"),
                Path.Combine(Home, "snap/steam/common/.local/share/Steam"),
            });
        }

        var steamRoot = Existing(roots.ToArray()).FirstOrDefault();
        if (steamRoot == null) return (false, new List<LauncherGame>());

        var libraries = CollectLibraries(steamRoot);
        AppPaths.Log($"Steam: библиотек {libraries.Count} — " + string.Join(" | ", libraries));

        var games = new List<LauncherGame>();
        var known = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var lib in libraries.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var apps = Path.Combine(lib, "steamapps");
            if (!Directory.Exists(apps)) continue;

            foreach (var manifest in Directory.EnumerateFiles(apps, "appmanifest_*.acf"))
            {
                try
                {
                    var text = File.ReadAllText(manifest);
                    var name = Field(text, "name");
                    var dir = Field(text, "installdir");
                    if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(dir)) continue;

                    var folder = Path.Combine(apps, "common", dir);
                    if (string.IsNullOrWhiteSpace(folder) || !LooksInstalled(folder)) continue;

                    int? appId = int.TryParse(Field(text, "appid"), out var parsed) ? parsed : null;

                    // StateFlags: бит 4 = игра действительно установлена (1026 и подобные тоже с ним)
                    if (int.TryParse(Field(text, "StateFlags"), out var stateFlags) && (stateFlags & 4) == 0)
                    {
                        AppPaths.Log($"Steam: пропущена неустановленная игра {name}");
                        continue;
                    }

                    if (IsToolOrRuntime(name, appId))
                    {
                        AppPaths.Log($"Steam: пропущен служебный компонент {name}");
                        continue;
                    }

                    known.Add(folder);

                    games.Add(new LauncherGame
                    {
                        Launcher = "steam",
                        Title = Clean(name),
                        Folder = folder,
                        Exe = FindExe(folder),
                        AppId = appId,
                        ExternalId = appId?.ToString() ?? "",
                    });
                }
                catch (Exception ex)
                {
                    AppPaths.Log("appmanifest: " + ex.Message);
                }
            }

            // вторая попытка: папки в common без манифеста (перенос библиотеки, ручная установка)
            var common = Path.Combine(apps, "common");
            if (!Directory.Exists(common)) continue;

            foreach (var folder in Directory.EnumerateDirectories(common))
            {
                if (known.Contains(folder)) continue;
                known.Add(folder);

                var title = Path.GetFileName(folder.TrimEnd(Path.DirectorySeparatorChar));
                if (string.IsNullOrWhiteSpace(title) || title.StartsWith('.')) continue;
                if (IsToolOrRuntime(title)) continue;

                // без манифеста верим только явной установке: есть .exe и хотя бы 300 МБ данных
                if (!LooksInstalledStrict(folder))
                {
                    AppPaths.Log($"Steam: пропущена папка без манифеста {title}");
                    continue;
                }

                games.Add(new LauncherGame
                {
                    Launcher = "steam",
                    Title = Clean(title),
                    Folder = folder,
                    Exe = FindExe(folder),
                });
            }
        }

        return (games.Count > 0 || steamRoot != null, games);
    }

    private static string Field(string text, string key)
    {
        var m = Regex.Match(text, "\"" + Regex.Escape(key) + "\"\\s*\"([^\"]*)\"", RegexOptions.IgnoreCase);
        return m.Success ? m.Groups[1].Value : "";
    }

    // ------------------------------------------------------------------- Epic

    private static (bool, List<LauncherGame>) Epic()
    {
        var games = new List<LauncherGame>();
        var installed = false;

        foreach (var manifests in Existing(
                     Path.Combine(ProgramData, "Epic", "EpicGamesLauncher", "Data", "Manifests")))
        {
            installed = true;
            foreach (var item in Directory.EnumerateFiles(manifests, "*.item"))
            {
                try
                {
                    using var doc = JsonDocument.Parse(File.ReadAllText(item));
                    var root = doc.RootElement;
                    var title = Str(root, "DisplayName");
                    var location = Str(root, "InstallLocation");
                    var launch = Str(root, "LaunchExecutable");
                    var appName = Str(root, "AppName");
                    if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(location)) continue;
                    if (IsToolOrRuntime(title) || !LooksInstalled(location)) continue;

                    games.Add(new LauncherGame
                    {
                        Launcher = "epic",
                        Title = Clean(title),
                        Folder = location,
                        ExternalId = appName,
                        Exe = launch != "" ? Path.Combine(location, launch.Replace('/', Path.DirectorySeparatorChar)) : FindExe(location),
                    });
                }
                catch (Exception ex)
                {
                    AppPaths.Log("epic item: " + ex.Message);
                }
            }
        }

        // Legendary (официальный CLI Epic для Linux) хранит установленные игры в JSON.
        foreach (var file in ExistingFiles(Path.Combine(Home, ".config", "legendary", "installed.json")))
        {
            installed = true;
            games.AddRange(ReadInstalledMap(file, "epic"));
        }

        return (installed, games);
    }

    // -------------------------------------------------------------------- GOG

    private static (bool, List<LauncherGame>) Gog()
    {
        var games = new List<LauncherGame>();
        var installed = false;

        if (OperatingSystem.IsWindows())
        {
            foreach (var path in new[]
                     {
                         @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\GOG.com\Games",
                         @"HKEY_LOCAL_MACHINE\SOFTWARE\GOG.com\Games",
                     })
            {
                foreach (var sub in RegSubKeys(path))
                {
                    installed = true;
                    var title = RegRead($@"{path}\{sub}", "gameName");
                    var folder = RegRead($@"{path}\{sub}", "path");
                    var exe = RegRead($@"{path}\{sub}", "exe");
                    if (string.IsNullOrWhiteSpace(folder) || !LooksInstalled(folder)) continue;

                    games.Add(new LauncherGame
                    {
                        Launcher = "gog",
                        Title = Clean(string.IsNullOrWhiteSpace(title) ? Path.GetFileName(folder.TrimEnd('\\', '/')) : title),
                        Folder = folder,
                        Exe = !string.IsNullOrWhiteSpace(exe) && File.Exists(exe) ? exe : FindExe(folder),
                    });
                }
            }
        }

        return (installed, games);
    }

    // ---------------------------------------------------------------- Ubisoft

    private static (bool, List<LauncherGame>) Ubisoft()
    {
        if (!OperatingSystem.IsWindows()) return (false, new List<LauncherGame>());

        var games = new List<LauncherGame>();
        var installed = false;

        foreach (var path in new[]
                 {
                     @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Ubisoft\Launcher\Installs",
                     @"HKEY_LOCAL_MACHINE\SOFTWARE\Ubisoft\Launcher\Installs",
                 })
        {
            foreach (var sub in RegSubKeys(path))
            {
                installed = true;
                var folder = RegRead($@"{path}\{sub}", "InstallDir");
                if (string.IsNullOrWhiteSpace(folder) || !LooksInstalled(folder)) continue;

                // в реестре только числовой id — имя берём по папке установки
                games.Add(new LauncherGame
                {
                    Launcher = "ubisoft",
                    Title = Clean(Path.GetFileName(folder.TrimEnd('\\', '/'))),
                    Folder = folder,
                    Exe = FindExe(folder),
                });
            }
        }

        if (!installed)
        {
            var launcher = Path.Combine(ProgramData, "Ubisoft Game Launcher");
            installed = Directory.Exists(launcher);
        }

        return (installed, games);
    }

    // ------------------------------------------------------------ EA app/Origin

    private static (bool, List<LauncherGame>) Ea()
    {
        if (!OperatingSystem.IsWindows()) return (false, new List<LauncherGame>());

        var games = new List<LauncherGame>();
        var installed = false;

        foreach (var path in new[]
                 {
                     @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\EA Games",
                     @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Origin Games",
                     @"HKEY_LOCAL_MACHINE\SOFTWARE\EA Games",
                 })
        {
            foreach (var sub in RegSubKeys(path))
            {
                installed = true;
                var folder = RegRead($@"{path}\{sub}", "Install Dir")
                             ?? RegRead($@"{path}\{sub}", "InstallDir");
                if (string.IsNullOrWhiteSpace(folder) || !LooksInstalled(folder)) continue;

                var name = RegRead($@"{path}\{sub}", "DisplayName");
                games.Add(new LauncherGame
                {
                    Launcher = "ea",
                    Title = Clean(string.IsNullOrWhiteSpace(name) ? Path.GetFileName(folder.TrimEnd('\\', '/')) : name),
                    Folder = folder,
                    Exe = FindExe(folder),
                });
            }
        }

        return (installed, games);
    }

    // -------------------------------------------------------------- Battle.net

    private static (bool, List<LauncherGame>) BattleNet()
    {
        if (!OperatingSystem.IsWindows()) return (false, new List<LauncherGame>());

        var games = new List<LauncherGame>();
        var installed = false;

        const string root = @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Blizzard Entertainment";
        foreach (var sub in RegSubKeys(root))
        {
            var folder = RegRead($@"{root}\{sub}", "InstallPath");
            if (string.IsNullOrWhiteSpace(folder)) continue;
            installed = true;
            if (string.IsNullOrWhiteSpace(folder) || !LooksInstalled(folder)) continue;

            games.Add(new LauncherGame
            {
                Launcher = "battlenet",
                Title = Clean(sub),
                Folder = folder,
                Exe = FindExe(folder),
            });
        }

        return (installed, games);
    }

    // ------------------------------------------------------- Heroic (Linux/mac)

    private static (bool, List<LauncherGame>) Heroic()
    {
        var games = new List<LauncherGame>();
        var installed = false;

        var cache = new[]
        {
            Path.Combine(Home, ".config", "heroic", "store_cache"),
            Path.Combine(Home, ".var/app/com.heroicgameslauncher.hgl/config/heroic/store_cache"),
        };

        foreach (var dir in Existing(cache))
        {
            installed = true;
            foreach (var file in Directory.EnumerateFiles(dir, "*_library.json"))
            {
                var store = Path.GetFileName(file).Replace("_library.json", "");
                var launcher = store switch
                {
                    "legendary" => "epic",
                    "gog" => "gog",
                    "nile" => "amazon",
                    _ => store,
                };

                try
                {
                    using var doc = JsonDocument.Parse(File.ReadAllText(file));
                    if (doc.RootElement.ValueKind != JsonValueKind.Object) continue;

                    foreach (var prop in doc.RootElement.EnumerateObject())
                    {
                        var value = prop.Value;
                        if (value.ValueKind != JsonValueKind.Object) continue;

                        var folder = Str(value, "install_path");
                        if (string.IsNullOrWhiteSpace(folder))
                        {
                            if (value.TryGetProperty("install", out var install) && install.ValueKind == JsonValueKind.Object)
                                folder = Str(install, "install_path");
                        }

                        if (string.IsNullOrWhiteSpace(folder) || !LooksInstalled(folder)) continue;

                        var title = Str(value, "title");
                        if (string.IsNullOrWhiteSpace(title)) title = prop.Name;

                        games.Add(new LauncherGame
                        {
                            Launcher = launcher,
                            Title = Clean(title),
                            Folder = folder,
                            Exe = FindExe(folder),
                        });
                    }
                }
                catch (Exception ex)
                {
                    AppPaths.Log("heroic " + store + ": " + ex.Message);
                }
            }
        }

        return (installed, games);
    }

    // ------------------------------------------------------- Lutris (Linux)

    private static (bool, List<LauncherGame>) Lutris()
    {
        var dirs = Existing(
            Path.Combine(Home, ".config", "lutris", "games"),
            Path.Combine(Home, ".local", "share", "lutris", "games"),
            Path.Combine(Home, ".var/app/net.lutris.Lutris/config/lutris/games"));

        var games = new List<LauncherGame>();
        var installed = false;

        foreach (var dir in dirs)
        {
            installed = true;
            foreach (var file in Directory.EnumerateFiles(dir, "*.yml"))
            {
                try
                {
                    string? title = null, exe = null, folder = null;
                    foreach (var line in File.ReadLines(file))
                    {
                        var trimmed = line.TrimStart();
                        if (trimmed.StartsWith("name:")) title = Unquote(trimmed[5..]);
                        else if (trimmed.StartsWith("exe:")) exe = Unquote(trimmed[4..]);
                        else if (trimmed.StartsWith("directory:")) folder = Unquote(trimmed[10..]);
                    }

                    if (string.IsNullOrWhiteSpace(title)) continue;

                    var dirPath = !string.IsNullOrWhiteSpace(folder) && Directory.Exists(folder)
                        ? folder
                        : exe != null && File.Exists(exe)
                            ? Path.GetDirectoryName(exe) ?? ""
                            : "";

                    if (string.IsNullOrWhiteSpace(dirPath) || !LooksInstalled(dirPath)) continue;

                    games.Add(new LauncherGame
                    {
                        Launcher = "lutris",
                        Title = Clean(title),
                        Folder = dirPath,
                        Exe = exe != null && File.Exists(exe) ? exe : FindExe(dirPath),
                    });
                }
                catch (Exception ex)
                {
                    AppPaths.Log("lutris yml: " + ex.Message);
                }
            }
        }

        return (installed, games);
    }

    // ------------------------------------------------------------- помощники

    private static string Unquote(string value) => value.Trim().Trim('\'', '"');

    private static string Clean(string title)
    {
        var cleaned = title.Replace("™", "").Replace("®", "").Replace("©", "").Trim();
        return Regex.Replace(cleaned, "\\s{2,}", " ");
    }

    private static string Str(JsonElement obj, string name) =>
        obj.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";

    private static IEnumerable<LauncherGame> ReadInstalledMap(string file, string launcher)
    {
        var list = new List<LauncherGame>();
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(file));
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return list;

            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                var value = prop.Value;
                if (value.ValueKind != JsonValueKind.Object) continue;

                var folder = Str(value, "install_path");
                if (string.IsNullOrWhiteSpace(folder) || !LooksInstalled(folder)) continue;

                var title = Str(value, "title");
                if (string.IsNullOrWhiteSpace(title)) title = prop.Name;

                var exe = Str(value, "executable");
                list.Add(new LauncherGame
                {
                    Launcher = launcher,
                    Title = Clean(title),
                    Folder = folder,
                    Exe = !string.IsNullOrWhiteSpace(exe) && File.Exists(exe) ? exe : FindExe(folder),
                });
            }
        }
        catch (Exception ex)
        {
            AppPaths.Log("installed.json: " + ex.Message);
        }

        return list;
    }

    /// <summary>Ищем исполняемый файл игры: не глубже двух уровней, без установщиков и хелперов.</summary>
    private static readonly string[] ExeIgnore =
    {
        "unins", "setup", "install", "vcredist", "dxsetup", "directx", "dotnet", "crashreport", "crashpad",
        "unitycrashhandler", "launcher", "eossdk", "eac", "helper", "updater", "update", "repair", "config",
        "touchup", "prereq", "redist", "activation", "notification", "benchmark",
    };

    private static string FindExe(string folder)
    {
        try
        {
            var candidates = new List<string>();

            foreach (var file in Directory.EnumerateFiles(folder, "*.exe"))
                candidates.Add(file);

            foreach (var sub in Directory.EnumerateDirectories(folder).Take(24))
            {
                var name = Path.GetFileName(sub).ToLowerInvariant();
                if (name is "redist" or "_commonredist" or "support" or "docs" or "soundtrack") continue;
                try
                {
                    candidates.AddRange(Directory.EnumerateFiles(sub, "*.exe"));
                }
                catch
                {
                    // папка недоступна — пропускаем
                }
            }

            var best = candidates
                .Where(p => !ExeIgnore.Any(bad => Path.GetFileName(p).ToLowerInvariant().Contains(bad)))
                .OrderByDescending(p => Path.GetFileName(p).Length)
                .ThenBy(p => p)
                .FirstOrDefault();

            return best ?? candidates.FirstOrDefault() ?? "";
        }
        catch
        {
            return "";
        }
    }

    private static IEnumerable<string> RegSubKeys(string path)
    {
        if (!OperatingSystem.IsWindows()) yield break;

        string[] names;
        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                path.Replace(@"HKEY_LOCAL_MACHINE\", "").Replace(@"HKEY_CURRENT_USER\", ""));
            if (key == null) yield break;
            names = key.GetSubKeyNames();
        }
        catch
        {
            yield break;
        }

        foreach (var name in names) yield return name;
    }

    private static string? RegRead(string path, string name)
    {
        if (!OperatingSystem.IsWindows()) return null;

        try
        {
            var hive = path.StartsWith("HKEY_CURRENT_USER", StringComparison.OrdinalIgnoreCase)
                ? Microsoft.Win32.Registry.CurrentUser
                : Microsoft.Win32.Registry.LocalMachine;
            var sub = Regex.Replace(path, "^HKEY_(LOCAL_MACHINE|CURRENT_USER)\\\\", "", RegexOptions.IgnoreCase);

            using var key = hive.OpenSubKey(sub);
            var value = key?.GetValue(name) as string;
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
        catch
        {
            return null;
        }
    }
}
