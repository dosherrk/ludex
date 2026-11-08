using System;
using System.IO;

namespace Ludex;

public static class AppPaths
{
    private static string? _root;

    public static string Root
    {
        get
        {
            if (_root != null) return _root;

            string baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrWhiteSpace(baseDir))
                baseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), ".local", "share");

            _root = Path.Combine(baseDir, "Ludex");

            // перенос данных из прежней версии (GameVault): настройки, библиотека, обложки
            if (!Directory.Exists(_root))
            {
                var legacy = Path.Combine(baseDir, "GameVault");
                if (Directory.Exists(legacy))
                {
                    try
                    {
                        CopyDirectory(legacy, _root);
                        Log("Migrated data from " + legacy);
                    }
                    catch (Exception ex)
                    {
                        Log("Migration failed: " + ex.Message);
                    }
                }
            }

            Directory.CreateDirectory(_root);
            return _root;
        }
    }

    private static void CopyDirectory(string from, string to)
    {
        Directory.CreateDirectory(to);

        foreach (var dir in Directory.GetDirectories(from, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(dir.Replace(from, to));

        foreach (var file in Directory.GetFiles(from, "*", SearchOption.AllDirectories))
            File.Copy(file, file.Replace(from, to), true);
    }

    public static string SettingsFile => Path.Combine(Root, "settings.json");
    public static string LibraryFile => Path.Combine(Root, "library.json");
    public static string CoversDir
    {
        get
        {
            var d = Path.Combine(Root, "covers");
            Directory.CreateDirectory(d);
            return d;
        }
    }

    public static string LogFile => Path.Combine(Root, "log.txt");

    public static void Log(string message)
    {
        try
        {
            File.AppendAllText(LogFile, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  {message}{Environment.NewLine}");
        }
        catch
        {
            // ignored
        }
    }
}
