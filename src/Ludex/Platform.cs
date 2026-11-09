using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Ludex;

/// <summary>
/// Различия между платформами в одном месте: как искать игры, как их запускать
/// и что из системной интеграции вообще доступно.
/// </summary>
public static class Platform
{
    public static bool IsWindows => OperatingSystem.IsWindows();
    public static bool IsMac => OperatingSystem.IsMacOS();
    public static bool IsLinux => OperatingSystem.IsLinux();

    /// <summary>Запуск от администратора (UAC) существует только в Windows.</summary>
    public static bool SupportsElevation => IsWindows;

    /// <summary>Человекочитаемое имя системы — для подсказок в интерфейсе.</summary>
    public static string DisplayName => IsWindows ? "Windows" : IsMac ? "macOS" : "Linux";

    /// <summary>Минимальный размер файла, чтобы считать его игрой, а не скриптом-обёрткой.</summary>
    private const long MinGameFileSize = 64 * 1024;

    /// <summary>Расширения исполняемых файлов, которые имеет смысл запускать.</summary>
    private static readonly HashSet<string> GameExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        "", ".appimage", ".sh", ".x86_64", ".x86", ".run", ".bin", ".elf", ".exe",
    };

    /// <summary>Файлы, которые никогда не являются игрой.</summary>
    private static readonly string[] LibraryNoise =
    {
        ".so", ".so.", ".dll", ".dylib", ".py", ".jar", ".desktop", ".png", ".jpg", ".jpeg",
        ".webp", ".svg", ".txt", ".md", ".json", ".xml", ".cfg", ".ini", ".log", ".pak", ".dat",
    };

    /// <summary>
    /// Файлы-кандидаты в текущей папке. Windows — *.exe, Linux/macOS — исполняемые
    /// файлы (AppImage, нативные бинарники, .sh-обёртки), отфильтрованные от библиотек.
    /// </summary>
    public static List<FileInfo> GameFiles(DirectoryInfo dir)
    {
        var result = new List<FileInfo>();

        try
        {
            if (IsWindows)
            {
                result.AddRange(dir.GetFiles("*.exe", SearchOption.TopDirectoryOnly));
                return result;
            }

            foreach (var file in dir.GetFiles("*", SearchOption.TopDirectoryOnly))
            {
                var ext = Path.GetExtension(file.Name);
                if (!GameExtensions.Contains(ext)) continue;

                var lower = file.Name.ToLowerInvariant();
                if (LibraryNoise.Any(n => lower.Contains(n, StringComparison.Ordinal))) continue;
                if (!IsExecutable(file)) continue;
                if (file.Length < MinGameFileSize) continue;

                result.Add(file);
            }
        }
        catch
        {
            // нет доступа — просто ничего не нашли
        }

        return result;
    }

    /// <summary>macOS-приложения — это папки *.app с бинарником внутри.</summary>
    public static List<DirectoryInfo> MacBundles(DirectoryInfo dir)
    {
        if (!IsMac) return new List<DirectoryInfo>();

        try
        {
            return dir.GetDirectories("*.app", SearchOption.TopDirectoryOnly).ToList();
        }
        catch
        {
            return new List<DirectoryInfo>();
        }
    }

    public static bool IsBundle(string path) =>
        IsMac && path.EndsWith(".app", StringComparison.OrdinalIgnoreCase);

    private static bool IsExecutable(FileInfo file)
    {
        if (IsWindows) return true;

        try
        {
            var mode = File.GetUnixFileMode(file.FullName);
            return (mode & (UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute)) != 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Ставит бит выполнения — нужен для распакованных из архива файлов.</summary>
    public static void MakeExecutable(string path)
    {
        if (IsWindows) return;

        try
        {
            var mode = File.GetUnixFileMode(path);
            var exec = UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;
            if ((mode & exec) != exec) File.SetUnixFileMode(path, mode | exec);
        }
        catch (Exception ex)
        {
            AppPaths.Log("chmod failed " + path + ": " + ex.Message);
        }
    }
}
