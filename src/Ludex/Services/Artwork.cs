using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Ludex.Services;

public sealed class ArtCandidate
{
    public string Url { get; set; } = "";
    public string Source { get; set; } = ""; // steam | steamgriddb | local
    public string Label { get; set; } = "";
    public bool Portrait { get; set; }
    public int? AppId { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
}

public static class Artwork
{
    private const string SteamCdn = "https://cdn.cloudflare.steamstatic.com/steam/apps/";
    private const string SgdbApi = "https://www.steamgriddb.com/api/v2/";

    public static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        var handler = new HttpClientHandler
        {
            AutomaticDecompression = System.Net.DecompressionMethods.All,
        };
        var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(20) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) Ludex/1.4");
        client.DefaultRequestHeaders.Accept.ParseAdd("*/*");
        return client;
    }

    // ---------------------------------------------------------------- Steam

    public static async Task<List<(int appId, string name)>> SearchSteamAsync(string term, CancellationToken ct)
    {
        var result = new List<(int, string)>();
        if (string.IsNullOrWhiteSpace(term)) return result;

        var url = "https://store.steampowered.com/api/storesearch/?term=" +
                  Uri.EscapeDataString(term) + "&l=english&cc=us";
        try
        {
            var json = await Http.GetStringAsync(url, ct).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("items", out var items)) return result;
            foreach (var item in items.EnumerateArray())
            {
                if (item.TryGetProperty("type", out var type) && type.GetString() != "app") continue;
                if (!item.TryGetProperty("id", out var id) || !item.TryGetProperty("name", out var name)) continue;
                result.Add((id.GetInt32(), name.GetString() ?? ""));
            }
        }
        catch (Exception ex)
        {
            AppPaths.Log("SearchSteam failed: " + ex.Message);
        }

        if (result.Count == 0) result.AddRange(await SearchCommunityAsync(term, ct).ConfigureAwait(false));

        return result;
    }

    /// <summary>Запасной поиск: steamcommunity отдаёт JSON без ключей и почти никогда не блокируется.</summary>
    private static async Task<List<(int, string)>> SearchCommunityAsync(string term, CancellationToken ct)
    {
        var result = new List<(int, string)>();
        if (string.IsNullOrWhiteSpace(term)) return result;

        var url = "https://steamcommunity.com/actions/SearchApps/" + Uri.EscapeDataString(term);
        try
        {
            var json = await Http.GetStringAsync(url, ct).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return result;

            foreach (var item in doc.RootElement.EnumerateArray())
            {
                if (!item.TryGetProperty("appid", out var id)) continue;
                if (!item.TryGetProperty("name", out var name)) continue;

                var raw = id.ValueKind == JsonValueKind.String ? id.GetString() : id.GetRawText();
                if (int.TryParse(raw, out var appId)) result.Add((appId, name.GetString() ?? ""));
            }
        }
        catch (Exception ex)
        {
            AppPaths.Log("SearchCommunity failed: " + ex.Message);
        }

        return result;
    }

    public static async Task<(int appId, string name, double score)?> IdentifyAsync(string title, CancellationToken ct)
    {
        var cleaned = Scanner.CleanTitle(title);
        if (cleaned.Length < 2) return null;

        var variants = new List<string> { cleaned };
        var shortTerm = string.Join(" ", cleaned.Split(' ').Take(5));
        if (shortTerm != cleaned) variants.Add(shortTerm);

        foreach (var term in variants)
        {
            var results = await SearchSteamAsync(term, ct).ConfigureAwait(false);
            if (results.Count == 0) continue;

            var best = results
                .Select(r => (r.appId, r.name, score: Similarity(cleaned, r.name)))
                .OrderByDescending(x => x.score)
                .First();

            if (best.score >= 0.45) return best;
        }

        return null;
    }

    public static async Task<List<ArtCandidate>> SteamCandidatesAsync(int appId, string name, CancellationToken ct)
    {
        var list = new List<ArtCandidate>
        {
            new()
            {
                Url = $"{SteamCdn}{appId}/library_600x900.jpg",
                Source = "steam",
                Label = name + " · poster",
                Portrait = true,
                AppId = appId,
                Width = 600,
                Height = 900,
            },
            new()
            {
                Url = $"{SteamCdn}{appId}/library_600x900_2x.jpg",
                Source = "steam",
                Label = name + " · poster 2x",
                Portrait = true,
                AppId = appId,
                Width = 600,
                Height = 900,
            },
            new()
            {
                Url = $"{SteamCdn}{appId}/header.jpg",
                Source = "steam",
                Label = name + " · header",
                Portrait = false,
                AppId = appId,
                Width = 460,
                Height = 215,
            },
            new()
            {
                Url = $"{SteamCdn}{appId}/capsule_616x353.jpg",
                Source = "steam",
                Label = name + " · capsule",
                Portrait = false,
                AppId = appId,
                Width = 616,
                Height = 353,
            },
        };

        await Task.CompletedTask;
        return list;
    }

    // ------------------------------------------------------------ SteamGridDB

    public static async Task<List<ArtCandidate>> SteamGridDbCandidatesAsync(string query, string apiKey, CancellationToken ct)
    {
        var list = new List<ArtCandidate>();
        if (string.IsNullOrWhiteSpace(apiKey)) return list;

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get,
                SgdbApi + "search/autocomplete/" + Uri.EscapeDataString(query));
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey.Trim());
            using var resp = await Http.SendAsync(req, ct).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode) return list;

            var json = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
                return list;

            foreach (var game in data.EnumerateArray().Take(1))
            {
                if (!game.TryGetProperty("id", out var gid)) continue;
                var gameName = game.TryGetProperty("name", out var gn) ? gn.GetString() ?? query : query;

                using var gridReq = new HttpRequestMessage(HttpMethod.Get,
                    SgdbApi + $"grids/game/{gid.GetInt32()}?dimensions=600x900,342x482,460x215&types=static&nsfw=false&humor=false");
                gridReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey.Trim());
                using var gridResp = await Http.SendAsync(gridReq, ct).ConfigureAwait(false);
                if (!gridResp.IsSuccessStatusCode) continue;

                var gridJson = await gridResp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                using var gridDoc = JsonDocument.Parse(gridJson);
                if (!gridDoc.RootElement.TryGetProperty("data", out var grids) || grids.ValueKind != JsonValueKind.Array)
                    continue;

                foreach (var g in grids.EnumerateArray())
                {
                    if (!g.TryGetProperty("url", out var u)) continue;
                    int w = g.TryGetProperty("width", out var wv) ? wv.GetInt32() : 0;
                    int h = g.TryGetProperty("height", out var hv) ? hv.GetInt32() : 0;
                    list.Add(new ArtCandidate
                    {
                        Url = u.GetString() ?? "",
                        Source = "steamgriddb",
                        Label = gameName,
                        Portrait = h > w,
                        Width = w,
                        Height = h,
                    });
                    if (list.Count >= 20) break;
                }
            }
        }
        catch (Exception ex)
        {
            AppPaths.Log("SteamGridDB failed: " + ex.Message);
        }

        return list;
    }

    // --------------------------------------------------------------- public

    /// <summary>Full manual search: SteamGridDB (if key given) + Steam catalog.</summary>
    public static async Task<List<ArtCandidate>> SearchAsync(string query, string? sgdbKey, CancellationToken ct)
    {
        var result = new List<ArtCandidate>();

        if (!string.IsNullOrWhiteSpace(sgdbKey))
        {
            var cleaned = Scanner.CleanTitle(query);
            result.AddRange(await SteamGridDbCandidatesAsync(cleaned, sgdbKey!, ct).ConfigureAwait(false));
        }

        var steam = await SearchSteamAsync(Scanner.CleanTitle(query), ct).ConfigureAwait(false);
        foreach (var (appId, name) in steam.Take(3))
        {
            result.AddRange(await SteamCandidatesAsync(appId, name, ct).ConfigureAwait(false));
        }

        // drop duplicates by url, portrait first
        return result
            .Where(c => !string.IsNullOrWhiteSpace(c.Url))
            .GroupBy(c => c.Url)
            .Select(g => g.First())
            .OrderByDescending(c => c.Portrait)
            .ThenBy(c => c.Source == "steamgriddb" ? 0 : 1)
            .ToList();
    }

    /// <summary>Обложка по известному Steam AppId — без поиска, прямо с CDN.</summary>
    public static async Task<(string? path, bool portrait)> DirectAsync(int appId, string gameId, CancellationToken ct)
    {
        var candidates = await SteamCandidatesAsync(appId, "", ct).ConfigureAwait(false);
        foreach (var candidate in candidates)
        {
            if (ct.IsCancellationRequested) return (null, false);
            var bytes = await FetchBytesAsync(candidate.Url, ct).ConfigureAwait(false);
            if (bytes == null) continue;

            var path = SaveCover(gameId, bytes, candidate.Portrait);
            if (path != null) return (path, candidate.Portrait);
        }

        return (null, false);
    }

    public static async Task<byte[]?> FetchBytesAsync(string url, CancellationToken ct = default)
    {
        try
        {
            using var resp = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode) return null;
            var bytes = await resp.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
            return bytes.Length < 1200 ? null : bytes;
        }
        catch
        {
            return null;
        }
    }

    public static async Task<(string? path, bool portrait, int? appId, string? matchedName)> AutoArtworkAsync(
        string title, string? sgdbKey, string gameId, CancellationToken ct)
    {
        // 1) SteamGridDB first when a key is configured
        if (!string.IsNullOrWhiteSpace(sgdbKey))
        {
            var grid = await SteamGridDbCandidatesAsync(Scanner.CleanTitle(title), sgdbKey!, ct).ConfigureAwait(false);
            var portrait = grid.FirstOrDefault(c => c.Portrait) ?? grid.FirstOrDefault();
            if (portrait != null)
            {
                var bytes = await FetchBytesAsync(portrait.Url, ct).ConfigureAwait(false);
                if (bytes != null)
                {
                    var path = SaveCover(gameId, bytes, portrait.Portrait);
                    if (path != null) return (path, portrait.Portrait, null, null);
                }
            }
        }

        // 2) Steam catalog
        var match = await IdentifyAsync(title, ct).ConfigureAwait(false);
        if (match == null) return (null, false, null, null);

        var candidates = await SteamCandidatesAsync(match.Value.appId, match.Value.name, ct).ConfigureAwait(false);
        foreach (var candidate in candidates)
        {
            var bytes = await FetchBytesAsync(candidate.Url, ct).ConfigureAwait(false);
            if (bytes == null) continue;
            var path = SaveCover(gameId, bytes, candidate.Portrait);
            if (path != null) return (path, candidate.Portrait, match.Value.appId, match.Value.name);
        }

        return (null, false, match.Value.appId, match.Value.name);
    }

    public static string? SaveCover(string gameId, byte[] data, bool portrait)
    {
        try
        {
            // remove previous files of this game
            foreach (var old in Directory.EnumerateFiles(AppPaths.CoversDir, gameId + "_*"))
            {
                try { File.Delete(old); } catch { }
            }

            var file = Path.Combine(AppPaths.CoversDir, $"{gameId}_{(portrait ? "p" : "w")}.img");
            File.WriteAllBytes(file, data);
            return file;
        }
        catch (Exception ex)
        {
            AppPaths.Log("SaveCover failed: " + ex.Message);
            return null;
        }
    }

    public static string? SaveLocalCover(string gameId, string sourceFile, bool portrait)
    {
        try
        {
            var bytes = File.ReadAllBytes(sourceFile);
            return SaveCover(gameId, bytes, portrait);
        }
        catch (Exception ex)
        {
            AppPaths.Log("SaveLocalCover failed: " + ex.Message);
            return null;
        }
    }

    // ------------------------------------------------------------ similarity

    public static double Similarity(string a, string b)
    {
        var na = Scanner.Normalize(Scanner.CleanTitle(a));
        var nb = Scanner.Normalize(b);
        if (na.Length == 0 || nb.Length == 0) return 0;

        if (na == nb) return 1.0;

        var tokensA = Scanner.CleanTitle(a).ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var tokensB = b.ToLowerInvariant().Split(new[] { ' ', ':', '-', '’', '\'', ',', '.' }, StringSplitOptions.RemoveEmptyEntries);

        var setA = new HashSet<string>(tokensA);
        var setB = new HashSet<string>(tokensB);
        int inter = setA.Intersect(setB).Count();
        int union = setA.Union(setB).Count();
        double jaccard = union == 0 ? 0 : (double)inter / union;

        double lev = 1.0 - (double)Levenshtein(na, nb) / Math.Max(na.Length, nb.Length);
        double score = 0.55 * jaccard + 0.45 * Math.Max(0, lev);

        if (nb.StartsWith(na, StringComparison.Ordinal) || na.StartsWith(nb, StringComparison.Ordinal))
            score += 0.12;

        return Math.Clamp(score, 0, 1);
    }

    private static int Levenshtein(string s, string t)
    {
        if (s.Length == 0) return t.Length;
        if (t.Length == 0) return s.Length;

        var prev = new int[t.Length + 1];
        var cur = new int[t.Length + 1];
        for (int j = 0; j <= t.Length; j++) prev[j] = j;

        for (int i = 1; i <= s.Length; i++)
        {
            cur[0] = i;
            for (int j = 1; j <= t.Length; j++)
            {
                int cost = s[i - 1] == t[j - 1] ? 0 : 1;
                cur[j] = Math.Min(Math.Min(cur[j - 1] + 1, prev[j] + 1), prev[j - 1] + cost);
            }
            (prev, cur) = (cur, prev);
        }

        return prev[t.Length];
    }
}
