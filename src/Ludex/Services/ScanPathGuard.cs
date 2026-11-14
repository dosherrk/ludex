using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace Ludex.Services;

/// <summary>Папки загрузок не участвуют в автоматическом сканировании и импорте.</summary>
public static class ScanPathGuard
{
    private static readonly Lazy<string?> Downloads = new Lazy<string?>(FindDownloads);
    private static readonly StringComparison PathComparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    public static bool IsDownloads(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        try
        {
            string full = Path.GetFullPath(path);
            if (IsBlockedLexically(full)) return true;
            // Проверяем цель ссылки до входа в каталог, включая явно выбранные корни.
            var dir = new DirectoryInfo(full);
            if (dir.Exists && (dir.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                var target = dir.ResolveLinkTarget(true);
                if (target != null && IsBlockedLexically(Path.GetFullPath(target.FullName))) return true;
            }
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException || ex is NotSupportedException || ex is System.Security.SecurityException) { }
        return false;
    }

    private static bool IsBlockedLexically(string full)
    {
        if (full.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries)
            .Any(part => part.Equals("Downloads", StringComparison.OrdinalIgnoreCase) || part.Equals("Загрузки", StringComparison.OrdinalIgnoreCase))) return true;
        string? downloads = Downloads.Value;
        if (string.IsNullOrWhiteSpace(downloads)) return false;
        string root = Path.GetFullPath(downloads).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        // XDG использует $HOME для отключённой папки; нельзя заблокировать весь профиль или диск.
        if (root.Length == 0 || root.Equals(home, PathComparison) || root.Equals(Path.GetPathRoot(downloads)?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), PathComparison)) return false;
        return full.Equals(root, PathComparison) || full.StartsWith(root + Path.DirectorySeparatorChar, PathComparison);
    }

    private static string? FindDownloads()
    {
        if (OperatingSystem.IsWindows())
        {
            IntPtr value = IntPtr.Zero;
            try
            {
                Guid id = new Guid("374DE290-123F-4565-9164-39C4925E467B");
                if (SHGetKnownFolderPath(in id, 0, IntPtr.Zero, out value) == 0 && value != IntPtr.Zero)
                    return Marshal.PtrToStringUni(value);
            }
            catch (Exception ex) when (ex is DllNotFoundException || ex is EntryPointNotFoundException || ex is ExternalException) { }
            finally { if (value != IntPtr.Zero) Marshal.FreeCoTaskMem(value); }
        }
        else if (OperatingSystem.IsLinux())
        {
            try
            {
                string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                string config = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME") ?? Path.Combine(home, ".config");
                string file = Path.Combine(config, "user-dirs.dirs");
                if (File.Exists(file))
                {
                    foreach (string line in File.ReadLines(file))
                    {
                        Match match = Regex.Match(line, "^\\s*XDG_DOWNLOAD_DIR\\s*=\\s*\"([^\"]*)\"");
                        if (!match.Success) continue;
                        string path = match.Groups[1].Value.Replace("${HOME}", home).Replace("$HOME", home);
                        if (Path.IsPathFullyQualified(path)) return path;
                    }
                }
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException || ex is System.Security.SecurityException) { }
        }
        string user = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return string.IsNullOrWhiteSpace(user) ? null : Path.Combine(user, "Downloads");
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHGetKnownFolderPath(in Guid folderId, uint flags, IntPtr token, out IntPtr path);
}
