using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Ludex.Services;

/// <summary>Точные пути находок, которые не нужно предлагать на подтверждение.</summary>
public static class ReviewIgnorePaths
{
    private static StringComparer Comparer => OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    public static string? Normalize(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path)) return null;
        try { return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path)); }
        catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException || ex is PathTooLongException) { return null; }
    }

    public static bool Contains(IEnumerable<string>? ignored, string? path)
    {
        string? key = Normalize(path);
        return key != null && ignored != null && ignored.Any(item => Comparer.Equals(Normalize(item), key));
    }

    public static bool Add(AppSettings settings, IEnumerable<string> paths)
    {
        settings.IgnoredReviewPaths ??= new List<string>();
        var before = settings.IgnoredReviewPaths.ToList();
        foreach (string path in paths)
        {
            string? key = Normalize(path);
            if (key == null) { settings.IgnoredReviewPaths = before; return false; }
            if (!Contains(settings.IgnoredReviewPaths, key)) settings.IgnoredReviewPaths.Add(key);
        }
        if (settings.IgnoredReviewPaths.Count == before.Count) return true;
        if (Storage.TrySaveSettings(settings)) return true;
        settings.IgnoredReviewPaths = before;
        return false;
    }

    public static bool Clear(AppSettings settings)
    {
        var before = settings.IgnoredReviewPaths ?? new List<string>();
        settings.IgnoredReviewPaths = new List<string>();
        if (Storage.TrySaveSettings(settings)) return true;
        settings.IgnoredReviewPaths = before;
        return false;
    }
}
