using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Ludex.Services;

/// <summary>
/// Удаление игры с диска: сначала пробуем найти деинсталлятор в папке игры
/// (unins000.exe, uninstall.exe, Uninstall.app, uninstall.sh и подобные),
/// а если его нет — удаляем саму папку игры.
/// </summary>
public static class Uninstaller
{
    private static readonly string[] NameHints =
    {
        "uninstall", "unins000", "unins001", "uninst", "деинстал", "удалить игру",
    };

    private static readonly string[] FileExtensions =
    {
        ".exe", ".sh", ".command", ".bat", ".cmd", ".run",
    };

    /// <summary>Деинсталлятор в папке игры или <c>null</c>, если его нет.</summary>
    public static string? Find(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath)) return null;

        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            MaxRecursionDepth = 2,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.System,
        };

        var candidates = new List<string>();

        try
        {
            foreach (var path in Directory.EnumerateFileSystemEntries(folderPath, "*", options))
            {
                var name = Path.GetFileName(path).ToLowerInvariant();
                if (!NameHints.Any(hint => name.Contains(hint))) continue;

                var isBundle = name.EndsWith(".app", StringComparison.Ordinal) && Directory.Exists(path);
                if (!isBundle && !FileExtensions.Any(ext => name.EndsWith(ext, StringComparison.Ordinal))) continue;

                candidates.Add(path);
            }
        }
        catch
        {
            return null;
        }

        return candidates
            .OrderBy(Rank)
            .ThenBy(path => path.Length)
            .FirstOrDefault();
    }

    /// <summary>0 — самый «настоящий» деинсталлятор, чем больше, тем менее вероятный.</summary>
    private static int Rank(string path)
    {
        var name = Path.GetFileName(path).ToLowerInvariant();
        if (name.StartsWith("unins000")) return 0;
        if (name.StartsWith("uninstall.exe")) return 2;
        if (name.EndsWith(".app", StringComparison.Ordinal)) return 3;
        if (name.EndsWith(".exe", StringComparison.Ordinal)) return 4;
        if (name.EndsWith(".sh", StringComparison.Ordinal)) return 5;
        return 6;
    }

    /// <summary>Проверка, что папку вообще можно удалять: не корень диска и не домашний каталог.</summary>
    public static bool IsSafeToDelete(string? folderPath, out string reason)
    {
        reason = "";
        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
        {
            reason = "folder-not-found";
            return false;
        }

        string full;
        try
        {
            full = Path.GetFullPath(folderPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        catch
        {
            reason = "bad-path";
            return false;
        }

        var root = Path.GetPathRoot(full)?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) ?? "";
        if (string.IsNullOrEmpty(full) || string.Equals(full, root, StringComparison.OrdinalIgnoreCase))
        {
            reason = "drive-root";
            return false;
        }

        // путь из одного сегмента (C:\Games, /games) считаем слишком общим
        var depth = full.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
            StringSplitOptions.RemoveEmptyEntries).Length;
        if (depth < 2)
        {
            reason = "too-shallow";
            return false;
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrEmpty(home) &&
            string.Equals(full, home.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
        {
            reason = "home";
            return false;
        }

        // папка с данными самого Ludex — точно не игра
        var appRoot = AppPaths.Root.TrimEnd(Path.DirectorySeparatorChar);
        if (full.StartsWith(appRoot, StringComparison.OrdinalIgnoreCase) ||
            appRoot.StartsWith(full, StringComparison.OrdinalIgnoreCase))
        {
            reason = "app-folder";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Удаляет игру с диска. Возвращает <c>usedUninstaller</c> — нашёлся ли деинсталлятор,
    /// и текст ошибки (пустой при успехе).
    /// </summary>
    public static async Task<(bool ok, bool usedUninstaller, string error)> RemoveAsync(
        GameEntry game, Action<string>? status = null)
    {
        var folder = game.FolderPath;

        if (!IsSafeToDelete(folder, out var reason))
            return (false, false, reason);

        var uninstaller = Find(folder);

        if (uninstaller != null)
        {
            status?.Invoke("uninstaller-started");
            var started = TryRunUninstaller(uninstaller, folder);
            if (started)
            {
                await WaitForFolderOrExitAsync(folder, uninstaller, TimeSpan.FromMinutes(10));
            }
        }

        if (!Directory.Exists(folder)) return (true, uninstaller != null, "");

        status?.Invoke("deleting-folder");
        var error = DeleteFolderWithRetries(folder);
        return (error.Length == 0, uninstaller != null, error);
    }

    private static bool TryRunUninstaller(string path, string folder)
    {
        try
        {
            var info = new ProcessStartInfo
            {
                WorkingDirectory = Directory.Exists(folder) ? folder : Path.GetDirectoryName(path) ?? folder,
                UseShellExecute = true,
            };

            var name = Path.GetFileName(path).ToLowerInvariant();

            if (name.EndsWith(".app", StringComparison.Ordinal) && Platform.IsMac)
            {
                // macOS: .app-бандл открываем через open
                info.FileName = "open";
                info.Arguments = $"\"{path}\"";
                info.UseShellExecute = false;
            }
            else if (name.EndsWith(".sh", StringComparison.Ordinal) ||
                     name.EndsWith(".command", StringComparison.Ordinal) ||
                     name.EndsWith(".run", StringComparison.Ordinal))
            {
                info.FileName = "/bin/sh";
                info.Arguments = $"\"{path}\"";
                info.UseShellExecute = false;
            }
            else
            {
                info.FileName = path;
            }

            Process.Start(info);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Ждём, пока папка исчезнет (деинсталлятор доработает) — но не дольше таймаута.</summary>
    private static async Task WaitForFolderOrExitAsync(string folder, string uninstallerPath, TimeSpan timeout)
    {
        var processName = Path.GetFileNameWithoutExtension(uninstallerPath);
        var deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline)
        {
            await Task.Delay(700);

            if (!Directory.Exists(folder)) return;

            // деинсталлятор закрылся — дальше просто чистим остатки
            try
            {
                var running = Process.GetProcessesByName(processName);
                if (running.Length == 0) return;
                foreach (var p in running) p.Dispose();
            }
            catch
            {
                return;
            }
        }
    }

    private static string DeleteFolderWithRetries(string folder)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                Directory.Delete(folder, true);
                return "";
            }
            catch (Exception ex)
            {
                if (attempt == 4) return ex.Message;
                System.Threading.Thread.Sleep(400 * (attempt + 1));
            }
        }

        return "";
    }
}
