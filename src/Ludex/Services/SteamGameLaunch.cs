using System;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace Ludex.Services;

/// <summary>Identify installed Steam games without trusting artwork-only SteamAppId values.</summary>
public static class SteamGameLaunch
{
    public static bool TryGetUri(GameEntry game, out string uri)
    {
        uri = "";
        if (!string.Equals(game.Source, "steam", StringComparison.OrdinalIgnoreCase)) return false;
        // A matching local manifest is strongest evidence, including for entries
        // previously merged with a scan and therefore not marked FromLauncher.
        int? id = FindManifestId(game.FolderPath);
        // Import-created entries have ExternalId from the appmanifest. Artwork
        // searches can set SteamAppId, but never ExternalId.
        if (!id.HasValue && game.FromLauncher &&
            int.TryParse(game.ExternalId, NumberStyles.None, CultureInfo.InvariantCulture, out int external) && external > 0)
            id = external;
        if (!id.HasValue) return false;
        uri = "steam://run/" + id.Value.ToString(CultureInfo.InvariantCulture);
        return true;
    }

    private static int? FindManifestId(string? folder)
    {
        if (string.IsNullOrWhiteSpace(folder) || !Path.IsPathFullyQualified(folder)) return null;
        try
        {
            var dir = new DirectoryInfo(Path.GetFullPath(folder));
            var common = dir.Parent;
            var steamapps = common?.Parent;
            if (common == null || steamapps == null ||
                !string.Equals(common.Name, "common", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(steamapps.Name, "steamapps", StringComparison.OrdinalIgnoreCase) ||
                !dir.Exists || !steamapps.Exists) return null;
            var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            int? found = null;
            foreach (string manifest in Directory.EnumerateFiles(steamapps.FullName, "appmanifest_*.acf"))
            {
                string text;
                try { text = File.ReadAllText(manifest); }
                catch (IOException) { continue; }
                catch (UnauthorizedAccessException) { continue; }
                string Field(string key) => Regex.Match(text, "\"" + Regex.Escape(key) + "\"\\s*\"([^\"]*)\"", RegexOptions.IgnoreCase).Groups[1].Value;
                if (!string.Equals(Field("installdir"), dir.Name, comparison) ||
                    !int.TryParse(Field("appid"), NumberStyles.None, CultureInfo.InvariantCulture, out int id) || id <= 0 ||
                    !string.Equals(Path.GetFileName(manifest), $"appmanifest_{id}.acf", StringComparison.OrdinalIgnoreCase)) continue;
                string flags = Field("StateFlags");
                if (int.TryParse(flags, NumberStyles.None, CultureInfo.InvariantCulture, out int state) && (state & 4) == 0) continue;
                if (found.HasValue && found.Value != id) return null;
                found = id;
            }
            return found;
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException || ex is NotSupportedException || ex is System.Security.SecurityException)
        {
            return null;
        }
    }
}
