using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Ludex.Services;

/// <summary>Ранжирование кандидатов; размер и длина имени не доказывают назначение файла.</summary>
public static class ExecutableRanking
{
    public sealed record Choice(FileInfo? File, bool NeedsReview);
    private static readonly string[] Auxiliary = { "config", "setting", "benchmark", "server", "dedicated", "editor", "diagnostic", "tool", "repair", "updater", "helper" };
    private static string Normalize(string value) => new string(value.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());

    public static Choice Select(IEnumerable<FileInfo> candidates, string title, string? root = null)
    {
        string normalizedTitle = Normalize(title ?? "");
        var ranked = candidates.Select(f =>
        {
            string name = Normalize(Path.GetFileNameWithoutExtension(f.Name));
            bool exact = normalizedTitle.Length >= 2 && name == normalizedTitle;
            bool auxiliary = !exact && Auxiliary.Any(word => name.Contains(word, StringComparison.Ordinal));
            int score = exact ? 100 : normalizedTitle.Length >= 4 && (name.Contains(normalizedTitle, StringComparison.Ordinal) || normalizedTitle.Contains(name, StringComparison.Ordinal) && name.Length >= 4) ? 40 : 0;
            if (!string.IsNullOrWhiteSpace(root) && string.Equals(Path.GetDirectoryName(f.FullName), root, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)) score += 8;
            if (name.EndsWith("launcher", StringComparison.Ordinal)) score += 3;
            if (auxiliary) score -= 80;
            return (File: f, Score: score, Auxiliary: auxiliary);
        }).OrderByDescending(x => x.Score).ThenBy(x => x.File.FullName, StringComparer.Ordinal).ToList();
        if (ranked.Count == 0) return new Choice(null, false);
        bool uncertain = ranked[0].Auxiliary || (ranked.Count > 1 && ranked[0].Score - ranked[1].Score < 10);
        return new Choice(ranked[0].File, uncertain);
    }
}
