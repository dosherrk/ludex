using System;
using System.Collections.Generic;

namespace Ludex;

public sealed class GameEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = "";
    public string ExePath { get; set; } = "";
    public string FolderPath { get; set; } = "";
    public long SizeBytes { get; set; }
    public DateTime AddedUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LastPlayedUtc { get; set; }
    public int PlayCount { get; set; }

    public int? SteamAppId { get; set; }
    public string? SteamName { get; set; }

    /// <summary>Absolute path to the cached cover image (may be null -> placeholder).</summary>
    public string? CoverFile { get; set; }
    public bool CoverIsPortrait { get; set; }

    public bool Favorite { get; set; }

    /// <summary>True when the user picked the title/artwork manually: a rescan will not overwrite it.</summary>
    public bool Locked { get; set; }

    public bool ExeExists => !string.IsNullOrEmpty(ExePath) && System.IO.File.Exists(ExePath);

    public string FolderName => System.IO.Path.GetFileName(FolderPath.TrimEnd('\\', '/'));
}

public sealed class AppSettings
{
    public List<string> Folders { get; set; } = new();
    public string Language { get; set; } = "ru";
    public string? SteamGridDbKey { get; set; }
    /// <summary>title | added | played | favorite</summary>
    public string SortMode { get; set; } = "title";
    public bool AutoScanOnStartup { get; set; }
    /// <summary>проверять обновления на GitHub при запуске</summary>
    public bool CheckUpdatesOnStartup { get; set; } = true;
    /// <summary>dark | light</summary>
    public string Theme { get; set; } = "dark";
    /// <summary>blue | violet | green | orange | pink | red | turquoise</summary>
    public string Accent { get; set; } = "blue";
    /// <summary>полупрозрачный (glass) стиль интерфейса</summary>
    public bool Transparent { get; set; } = true;
    /// <summary>локальный путь к скачанным обоям</summary>
    public string? BackgroundPath { get; set; }
    public List<string> ExtraIgnoreExe { get; set; } = new();
    public double WindowWidth { get; set; } = 1280;
    public double WindowHeight { get; set; } = 820;
    public bool WindowMaximized { get; set; }
    public string LastSearch { get; set; } = "";
}

public sealed class LibraryFile
{
    public List<GameEntry> Games { get; set; } = new();
}
