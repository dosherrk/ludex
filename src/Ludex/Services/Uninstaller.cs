using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Ludex.Services;

public static class Uninstaller
{
    // Никогда не удалять общую библиотеку или типичную внутреннюю папку игры.
    private static readonly HashSet<string> SharedFolders = new(StringComparer.OrdinalIgnoreCase)
    {
        "games", "game", "игры", "игра", "my games", "мои игры", "steamapps", "common",
        "steam", "epic games", "gog games", "xboxgames", "program files", "program files (x86)",
        "users", "пользователи", "desktop", "рабочий стол", "downloads", "загрузки",
        "documents", "документы", "windows", "programdata", "bin", "binaries", "win64", "win32",
        "x64", "x86", "content", "data", "engine", "paks"
    };

    private static StringComparison PathComparison => OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private static string NormalizePath(string path) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));

    public static bool ContainsPath(string folder, string path)
    {
        string root = NormalizePath(folder);
        string child = NormalizePath(path);
        return child.Equals(root, PathComparison) ||
            child.StartsWith(Path.EndsInDirectorySeparator(root) ? root : root + Path.DirectorySeparatorChar, PathComparison);
    }

    public static bool IsSafeToDelete(string? folderPath, out string reason)
    {
        reason = "";
        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
        { reason = "folder-not-found"; return false; }
        try
        {
            string path = NormalizePath(folderPath);
            string root = NormalizePath(Path.GetPathRoot(path) ?? path);
            if (path.Equals(root, PathComparison)) { reason = "drive-root"; return false; }
            if (SharedFolders.Contains(Path.GetFileName(path))) { reason = "shared-folder"; return false; }
            foreach (Environment.SpecialFolder special in new[] {
                Environment.SpecialFolder.UserProfile, Environment.SpecialFolder.DesktopDirectory,
                Environment.SpecialFolder.MyDocuments, Environment.SpecialFolder.MyPictures,
                Environment.SpecialFolder.MyMusic, Environment.SpecialFolder.MyVideos,
                Environment.SpecialFolder.Windows, Environment.SpecialFolder.System,
                Environment.SpecialFolder.ProgramFiles, Environment.SpecialFolder.ProgramFilesX86,
                Environment.SpecialFolder.CommonApplicationData, Environment.SpecialFolder.ApplicationData,
                Environment.SpecialFolder.LocalApplicationData })
            {
                string protectedPath = Environment.GetFolderPath(special);
                if (!string.IsNullOrWhiteSpace(protectedPath) && ContainsPath(path, protectedPath))
                { reason = "protected-folder"; return false; }
            }
            if (ContainsPath(path, AppPaths.Root) || ContainsPath(AppPaths.Root, path) ||
                ContainsPath(path, AppContext.BaseDirectory) || ContainsPath(AppContext.BaseDirectory, path))
            { reason = "app-folder"; return false; }
            // Не переходить по ссылкам/junction в пути к удаляемой папке.
            for (DirectoryInfo? d = new DirectoryInfo(path); d != null; d = d.Parent)
                if ((d.Attributes & FileAttributes.ReparsePoint) != 0)
                { reason = "link"; return false; }
            return true;
        }
        catch (Exception) { reason = "bad-path"; return false; }
    }

    public static bool IsValidGameFolder(string folder, GameEntry game, IEnumerable<GameEntry> otherGames, out string reason)
    {
        if (!IsSafeToDelete(folder, out reason)) return false;
        try
        {
            if (string.IsNullOrWhiteSpace(game.ExePath) || !Path.IsPathFullyQualified(game.ExePath) ||
                !ContainsPath(folder, game.ExePath))
            { reason = "exe-outside"; return false; }
            foreach (GameEntry other in otherGames)
            {
                if (ReferenceEquals(other, game) || other.Id == game.Id) continue;
                if ((!string.IsNullOrWhiteSpace(other.ExePath) && Path.IsPathFullyQualified(other.ExePath) && ContainsPath(folder, other.ExePath)) ||
                    (!string.IsNullOrWhiteSpace(other.FolderPath) && Path.IsPathFullyQualified(other.FolderPath) && ContainsPath(folder, other.FolderPath)))
                { reason = "other-game"; return false; }
            }
            return true;
        }
        catch (Exception) { reason = "bad-path"; return false; }
    }

    public static async Task<(bool ok, string error)> RemoveFolderAsync(string folder)
    {
        if (!IsSafeToDelete(folder, out string reason)) return (false, reason);
        return await Task.Run(() =>
        {
            for (int i = 0; i < 5; i++)
            {
                try
                {
                    Directory.Delete(folder, recursive: true);
                    return (true, "");
                }
                catch (Exception ex)
                {
                    // Directory.Exists может вернуть false при отказе в доступе: это
                    // не доказательство удаления и не повод скрывать карточку.
                    if (i == 4) return (false, ex.Message);
                    Thread.Sleep(400 * (i + 1));
                }
            }
            return (false, "Failed to delete folder");
        });
    }
}
