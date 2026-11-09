using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Ludex.Services;

public sealed class ScanHit
{
    public string FolderPath = "";
    public string ExePath = "";
    public string Title = "";
}

/// <summary>
/// Walks the configured folders and tries to figure out where the games are.
/// A folder counts as a game folder when it contains at least one "plausible" .exe.
/// </summary>
public static class Scanner
{
    private const int MaxDepth = 6;

    /// <summary>Executables that are never a game.</summary>
    private static readonly string[] HardIgnore =
    {
        "unins", "uninstall", "unwise", "setup", "install", "installer", "vcredist", "vc_redist", "vcsetup",
        "dxsetup", "dxwebsetup", "dxsetup", "directx", "dotnetfx", "ndp4", "oalinst", "physx", "ue4prereq",
        "ue5prereq", "prereq", "redist", "_redist", "crashreport", "crashpad", "crashhandler", "crashsender",
        "unitycrashhandler", "steamerrorreporter", "steamsetup", "steamservice", "notification_helper",
        "7za", "7z", "ffmpeg", "ffplay", "python", "pythonw", "qt5", "qt6", "qtwebengineprocess",
        "epicwebhelper", "eossdk", "eac", "eulauncher", "launcher_installer", "update", "updater", "patcher",
        "unpack", "extract", "report", "help", "manual", "readme", "webview", "helper",
    };

    /// <summary>Executables that exist but are rarely the thing you want to launch.</summary>
    private static readonly string[] SoftIgnore =
    {
        "config", "configuration", "settings", "benchmark", "server", "dedicated", "test", "editor",
        "launcher", "bootstrapper", "download", "repair", "swiftshader", "gpu", "diagnostics",
    };

    private static readonly HashSet<string> SkipFolders = new(StringComparer.OrdinalIgnoreCase)
    {
        "commonredist", "_commonredist", "redist", "redistributables", "directx", "dotnet", "vcredist",
        "support", "docs", "documentation", "manuals", "manual", "soundtrack", "ost", "artbook", "bonus",
        "system volume information", "$recycle.bin", "recycler", "windows", "temp", "tmp", "cache",
        "$windows.~bt", "$windows.~ws", "node_modules", ".git", "saves", "savegame", "savegames", "profiles",
        "lib", "lib32", "lib64", "plugins", "redist", "redistributable", "locales", "steamapps", "compatdata", "pkg",
    };

    /// <summary>Folder names that do not describe a game, so we take the name from a parent folder.</summary>
    private static readonly HashSet<string> GenericFolders = new(StringComparer.OrdinalIgnoreCase)
    {
        "bin", "binaries", "binary", "x64", "x86", "win64", "win32", "win", "windows", "game", "games",
        "data", "build", "release", "retail", "system", "engine", "client", "app", "application", "content",
        "exe", "runtime", "platform", "linux", "macos", "shipping", "gamedata", "gamefiles",
    };

    public static List<ScanHit> Scan(IReadOnlyList<string> roots, Action<string>? onFolder = null,
        IReadOnlyCollection<string>? extraIgnore = null, Action? onCancelledCheck = null,
        Func<bool>? isCancelled = null)
    {
        var hits = new List<ScanHit>();
        var seenExe = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int visited = 0;

        var ignore = new List<string>(HardIgnore);
        if (extraIgnore != null)
            foreach (var x in extraIgnore)
                if (!string.IsNullOrWhiteSpace(x))
                    ignore.Add(x.Trim().ToLowerInvariant());

        foreach (var root in roots)
        {
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root)) continue;
            try
            {
                var rootDir = new DirectoryInfo(root);
                Walk(rootDir, 0, true, rootDir);
            }
            catch (Exception ex)
            {
                AppPaths.Log("Scan root failed " + root + ": " + ex.Message);
            }
        }

        return hits;

        void Walk(DirectoryInfo dir, int depth, bool isRoot, DirectoryInfo rootDir)
        {
            if (isCancelled != null && isCancelled()) return;
            if (hits.Count > 4000) return;

            visited++;
            if (onFolder != null && visited % 25 == 0) onFolder(Loc.F("scan.progress", visited));

            DirectoryInfo[] subdirs;
            try
            {
                subdirs = dir.GetDirectories()
                    .Where(d => (d.Attributes & (FileAttributes.Hidden | FileAttributes.System)) == 0)
                    .Where(d => !SkipFolders.Contains(d.Name))
                    .Where(d => !(Platform.IsMac && d.Name.EndsWith(".app", StringComparison.OrdinalIgnoreCase)))
                    .OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }
            catch
            {
                return;
            }

            if (!isRoot)
            {
                if (Platform.IsMac)
                {
                    var bundle = Platform.MacBundles(dir)
                        .FirstOrDefault(d => !IsIgnored(d.Name, ignore));

                    if (bundle != null && seenExe.Add(bundle.FullName))
                    {
                        hits.Add(new ScanHit
                        {
                            FolderPath = dir.FullName,
                            ExePath = bundle.FullName,
                            Title = CleanTitle(Path.GetFileNameWithoutExtension(bundle.Name)),
                        });
                        return;
                    }
                }

                FileInfo[] exes = Platform.GameFiles(dir).ToArray();
                var candidates = exes.Where(f => !IsIgnored(f.Name, ignore)).ToList();
                if (candidates.Count > 0)
                {
                    var best = PickBest(candidates, dir.Name);
                    if (best != null && seenExe.Add(best.FullName))
                    {
                        var (title, folder) = BestTitleSource(dir, best, rootDir);
                        hits.Add(new ScanHit
                        {
                            FolderPath = folder,
                            ExePath = best.FullName,
                            Title = CleanTitle(title),
                        });
                    }
                    return; // do not descend into a game folder
                }
            }
            else
            {
                // The library root itself may hold a single game.
                try
                {
                    var rootExes = Platform.GameFiles(dir)
                        .Where(f => !IsIgnored(f.Name, ignore)).ToList();

                    if (rootExes.Count > 0 && subdirs.Length == 0)
                    {
                        var best = PickBest(rootExes, dir.Name);
                        if (best != null && seenExe.Add(best.FullName))
                        {
                            hits.Add(new ScanHit
                            {
                                FolderPath = dir.FullName,
                                ExePath = best.FullName,
                                Title = CleanTitle(dir.Name),
                            });
                        }
                        return;
                    }
                }
                catch
                {
                    // ignored
                }
            }

            if (depth >= MaxDepth) return;

            foreach (var sub in subdirs)
            {
                if (isCancelled != null && isCancelled()) return;
                Walk(sub, depth + 1, false, rootDir);
            }
        }
    }

    private static bool IsIgnored(string fileName, List<string> ignore)
    {
        var name = Path.GetFileNameWithoutExtension(fileName).ToLowerInvariant();
        foreach (var bad in ignore)
            if (name.Contains(bad, StringComparison.Ordinal))
                return true;
        return false;
    }

    private static FileInfo? PickBest(List<FileInfo> exes, string folderName)
    {
        FileInfo? best = null;
        double bestScore = double.MinValue;

        var folderNorm = Normalize(folderName);

        foreach (var exe in exes)
        {
            double score = 0;
            var name = Path.GetFileNameWithoutExtension(exe.Name);
            var norm = Normalize(name);

            // bigger files are usually the real game binary
            var mb = exe.Length / 1024.0 / 1024.0;
            score += Math.Min(12, Math.Log10(Math.Max(mb, 1) + 1) * 4);

            if (norm == folderNorm) score += 14;
            else if (folderNorm.Length > 2 && (norm.Contains(folderNorm) || folderNorm.Contains(norm))) score += 7;

            foreach (var soft in SoftIgnore)
            {
                if (norm.Contains(soft, StringComparison.Ordinal))
                {
                    score -= soft is "launcher" or "config" or "settings" ? 8 : 6;
                }
            }

            if (norm.StartsWith("unins", StringComparison.Ordinal)) score -= 100;

            if (score > bestScore)
            {
                bestScore = score;
                best = exe;
            }
        }

        return best;
    }

    private static (string title, string folder) BestTitleSource(DirectoryInfo dir, FileInfo exe, DirectoryInfo root)
    {
        // For "Game\bin\x64\game.exe" the real name lives higher up the tree.
        var current = dir;
        int guard = 0;
        while (current != null && guard++ < 4 && GenericFolders.Contains(current.Name)
               && current.Parent != null && !PathsEqual(current.FullName, root.FullName))
        {
            current = current.Parent;
        }

        var folderName = current?.Name ?? dir.Name;
        var folderPath = current?.FullName ?? dir.FullName;

        if (string.IsNullOrWhiteSpace(folderName) || GenericFolders.Contains(folderName))
        {
            folderName = Path.GetFileNameWithoutExtension(exe.Name);
        }

        return (folderName, folderPath);
    }

    private static bool PathsEqual(string a, string b) =>
        string.Equals(a.TrimEnd('\\', '/'), b.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase);

    public static string Normalize(string s)
    {
        return Regex.Replace(s.ToLowerInvariant(), "[^a-z0-9а-яё]+", "");
    }

    private static readonly HashSet<string> JunkTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "repack", "rip", "portable", "preinstalled", "pre", "installed", "crack", "cracked",
        "crackfix", "gog", "steam", "epic", "fitgirl", "dodi", "xatab", "igruha", "mechaniks", "mechanics",
        "rune", "codex", "plaza", "skidrow", "reloaded", "prophet", "empress", "tenoke", "elamigos",
        "razor1911", "goldberg", "online", "multi", "multiplayer", "update", "dlc", "patch", "fix",
        "rus", "eng", "ger", "fre", "ita", "spa", "pol", "jpn", "chi", "multi12", "multi10", "byte",
        "torrent", "rutracker", "nnm", "club", "by", "rg", "r", "g", "mechanics", "kaos", "tbl",
        "br", "nocd", "drm", "free", "full", "iso", "pc", "version", "ver", "final"
    };

    /// <summary>Turns a messy folder name into something close to the real game title.</summary>
    public static string CleanTitle(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return raw;

        var s = raw;

        // cut off file extensions and typical trailing junk
        s = Regex.Replace(s, @"\.(exe|lnk|url|bat)$", "", RegexOptions.IgnoreCase);

        // [FitGirl Repack], (2015), {GOTY}
        s = Regex.Replace(s, @"\[[^\]]{0,80}\]", " ");
        s = Regex.Replace(s, @"\{[^}]{0,80}\}", " ");
        s = Regex.Replace(s, @"\([^)]{0,80}\)", " ");

        // version tags: v1.2.3, 1.0.0.5, "build 1234" (before we split by separators!)
        s = Regex.Replace(s, @"\b(v|ver|version|build)\s*\d+(\.\d+){0,4}\b", " ", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"\b\d+(\.\d+){2,4}\b", " ");

        // separators -> space
        s = Regex.Replace(s, @"[_.]+", " ");
        s = Regex.Replace(s, @"\s*-\s*", " ");

        var tokens = s.Split(new[] { ' ', '\t', '-', '+', ',' }, StringSplitOptions.RemoveEmptyEntries);
        var kept = new List<string>();
        foreach (var t in tokens)
        {
            var trimmed = t.Trim();
            if (trimmed.Length == 0) continue;
            if (JunkTokens.Contains(trimmed)) continue;
            kept.Add(trimmed);
        }

        var result = string.Join(" ", kept).Trim();
        result = Regex.Replace(result, @"\s{2,}", " ");

        // if we stripped everything, fall back to the original
        if (result.Length < 2) return Regex.Replace(raw, @"[_.]+", " ").Trim();

        return result;
    }

    public static long DirectorySize(string path)
    {
        long total = 0;
        try
        {
            var stack = new Stack<string>();
            stack.Push(path);
            int guard = 0;
            while (stack.Count > 0 && guard++ < 400000)
            {
                var dir = stack.Pop();
                try
                {
                    foreach (var sub in Directory.EnumerateDirectories(dir)) stack.Push(sub);
                    foreach (var f in Directory.EnumerateFiles(dir))
                    {
                        try { total += new FileInfo(f).Length; } catch { }
                    }
                }
                catch { }
            }
        }
        catch { }
        return total;
    }

    public static string FormatSize(long bytes)
    {
        if (bytes <= 0) return "—";
        string[] units = { "Б", "КБ", "МБ", "ГБ", "ТБ" };
        double size = bytes;
        int unit = 0;
        while (size >= 1024 && unit < units.Length - 1)
        {
            size /= 1024;
            unit++;
        }
        return unit <= 1 ? $"{size:0} {units[unit]}" : $"{size:0.0} {units[unit]}";
    }
}
