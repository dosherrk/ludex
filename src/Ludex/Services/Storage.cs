using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Ludex.Services;

public static class Storage
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static readonly object Sync = new();

    public static AppSettings LoadSettings()
    {
        try
        {
            if (File.Exists(AppPaths.SettingsFile))
            {
                var json = File.ReadAllText(AppPaths.SettingsFile);
                var s = JsonSerializer.Deserialize<AppSettings>(json, Options);
                if (s != null) return s;
            }
        }
        catch (Exception ex)
        {
            AppPaths.Log("LoadSettings failed: " + ex);
        }

        return new AppSettings();
    }

    public static void SaveSettings(AppSettings settings)
    {
        lock (Sync)
        {
            try
            {
                File.WriteAllText(AppPaths.SettingsFile, JsonSerializer.Serialize(settings, Options));
            }
            catch (Exception ex)
            {
                AppPaths.Log("SaveSettings failed: " + ex);
            }
        }
    }

    public static List<GameEntry> LoadGames()
    {
        try
        {
            if (File.Exists(AppPaths.LibraryFile))
            {
                var json = File.ReadAllText(AppPaths.LibraryFile);
                var lib = JsonSerializer.Deserialize<LibraryFile>(json, Options);
                if (lib?.Games != null) return lib.Games;
            }
        }
        catch (Exception ex)
        {
            AppPaths.Log("LoadGames failed: " + ex);
        }

        return new List<GameEntry>();
    }

    public static void SaveGames(System.Collections.Generic.IEnumerable<GameEntry> games)
    {
        lock (Sync)
        {
            try
            {
                var lib = new LibraryFile { Games = new List<GameEntry>(games) };
                var tmp = AppPaths.LibraryFile + ".tmp";
                File.WriteAllText(tmp, JsonSerializer.Serialize(lib, Options));
                File.Move(tmp, AppPaths.LibraryFile, true);
            }
            catch (Exception ex)
            {
                AppPaths.Log("SaveGames failed: " + ex);
            }
        }
    }
}
