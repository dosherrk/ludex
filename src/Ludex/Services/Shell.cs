using System;
using System.Diagnostics;
using System.IO;

namespace Ludex.Services;

public static class Shell
{
    /// <summary>
    /// Запуск игры: .exe в Windows, бинарник/AppImage в Linux, .app-бандл в macOS.
    /// Повышение прав (UAC) существует только в Windows — на других системах флаг игнорируется.
    /// </summary>
    public static bool LaunchGame(string path, out string error, bool asAdmin = false)
    {
        error = "";
        try
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                error = "empty path";
                return false;
            }

            if (Platform.IsBundle(path))
            {
                if (!Directory.Exists(path))
                {
                    error = "bundle not found";
                    return false;
                }

                Process.Start(new ProcessStartInfo("open", $"\"{path}\"") { UseShellExecute = false });
                return true;
            }

            if (!File.Exists(path))
            {
                error = "exe not found";
                return false;
            }

            // распакованный из архива бинарник может быть без бита выполнения
            if (!Platform.IsWindows) Platform.MakeExecutable(path);

            var psi = new ProcessStartInfo
            {
                FileName = path,
                WorkingDirectory = Path.GetDirectoryName(path) ?? "",
                UseShellExecute = true,
            };

            if (asAdmin && Platform.SupportsElevation) psi.Verb = "runas";

            Process.Start(psi);
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            AppPaths.Log("LaunchGame failed: " + ex);
            return false;
        }
    }

    public static void OpenFolder(string path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path)) return;

            if (Platform.IsWindows)
                Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
            else if (Platform.IsMac)
                Process.Start(new ProcessStartInfo("open", $"\"{path}\"") { UseShellExecute = false });
            else
                Process.Start(new ProcessStartInfo("xdg-open", $"\"{path}\"") { UseShellExecute = false });
        }
        catch (Exception ex)
        {
            AppPaths.Log("OpenFolder failed: " + ex.Message);
        }
    }

    public static void OpenUrl(string url)
    {
        try
        {
            if (Platform.IsWindows)
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                return;
            }

            // macOS и Linux: свой открыватель, как и для папок
            var opener = Platform.IsMac ? "open" : "xdg-open";
            Process.Start(new ProcessStartInfo(opener, $"\"{url}\"") { UseShellExecute = false });
        }
        catch (Exception ex)
        {
            AppPaths.Log("OpenUrl failed: " + ex.Message);

            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception inner)
            {
                AppPaths.Log("OpenUrl fallback failed: " + inner.Message);
            }
        }
    }
}
