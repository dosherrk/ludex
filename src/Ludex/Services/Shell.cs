using System;
using System.Diagnostics;
using System.IO;

namespace Ludex.Services;

public static class Shell
{
    public static bool LaunchExe(string exePath, out string error, bool asAdmin = false)
    {
        error = "";
        try
        {
            if (!File.Exists(exePath))
            {
                error = "exe not found";
                return false;
            }

            var psi = new ProcessStartInfo
            {
                FileName = exePath,
                WorkingDirectory = Path.GetDirectoryName(exePath) ?? "",
                UseShellExecute = true,
            };

            if (asAdmin) psi.Verb = "runas";

            Process.Start(psi);
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            AppPaths.Log("LaunchExe failed: " + ex);
            return false;
        }
    }

    public static void OpenFolder(string path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path)) return;

            if (OperatingSystem.IsWindows())
                Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
            else
                Process.Start(new ProcessStartInfo("xdg-open", $"\"{path}\"") { UseShellExecute = true });
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
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            AppPaths.Log("OpenUrl failed: " + ex.Message);
        }
    }
}
