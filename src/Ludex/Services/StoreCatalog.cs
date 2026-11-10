using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Ludex.Services;

/// <summary>
/// Большой каталог игр: собирается из поиска магазина Steam по жанровым тегам,
/// с фильтром «есть одиночная игра» и без программ, бесплатных сетевых поделок
/// и MMO. Данные кэшируются на три дня, ключи API не нужны.
/// </summary>
public static class StoreCatalog
{
    private const string SearchUrl =
        "https://store.steampowered.com/search/results/?query&count=100&infinite=1&json=1" +
        "&cc=ru&l=russian&category1=998&category2=2";

    /// <summary>Сколько страниц по 100 игр берём с каждого жанра.</summary>
    private const int PagesPerGenre = 3;

    /// <summary>Глубина списков «бесплатные» и «MMO» — их вычитаем из каталога.</summary>
    private const int FreePages = 12;
    private const int MmoPages = 6;

    private static readonly TimeSpan Ttl = TimeSpan.FromDays(3);
    private static readonly TimeSpan FailTtl = TimeSpan.FromMinutes(10);

    /// <summary>Жанры магазина: подпись и тег поиска.</summary>
    public static readonly (string Genre, string Tag)[] SteamGenres =
    {
        ("Action", "19"),
        ("Adventure", "21"),
        ("Indie", "492"),
        ("RPG", "122"),
        ("Strategy", "9"),
        ("Simulation", "599"),
        ("Casual", "597"),
        ("Racing", "699"),
        ("Sports", "701"),
        ("Early Access", "493"),
    };

    /// <summary>
    /// Игры, которые без онлайна бессмысленны: сетевые шутеры, лайв-сервисы и
    /// кооперативы «только с друзьями». Сверяем по названию, без учёта регистра.
    /// </summary>
    private static readonly string[] OnlineOnly =
    {
        "dota", "counter-strike", "counter strike", "cs2", "cs:go", "cs go", "pubg", "apex legends",
        "valorant", "overwatch", "warframe", "war thunder", "world of tanks", "world of warships",
        "destiny 2", "rainbow six", "tarkov", "hunt: showdown", "dead by daylight", "rust",
        "dayz", "garry's mod", "team fortress", "fallout 76", "elder scrolls online",
        "final fantasy xiv", "guild wars", "path of exile", "lost ark", "black desert",
        "new world", "throne and liberty", "albion online", "eve online", "star citizen",
        "marvel rivals", "deadlock", "the finals", "delta force", "naraka", "crossout",
        "enlisted", "genshin", "honkai", "zenless", "wuthering waves", "tower of fantasy",
        "torchlight: infinite", "limbus", "where winds meet", "once human", "r.e.p.o",
        "peak", "phasmophobia", "backrooms", "lethal company", "content warning",
        "among us", "fall guys", "rocket league", "brawlhalla", "paladins", "smite",
        "warface", "crossfire", "arma reforger", "squad", "hell let loose", "insurgency",
        "battlefield", "call of duty", "halo infinite", "the isle", "scum", "miscreated",
        "realm royale", "super people", "vampire: the masquerade - bloodhunt", "bloodhunt",
        "the cycle", "spellbreak", "crucible", "artifact", "gwent", "hearthstone",
        "magic: the gathering arena", "yu-gi-oh", "master duel", "mtg arena",
        "payday", "казино", "casino", "rv there yet", "how to fish", "gtfo",
        "deep rock galactic", "sea of thieves", "star wars: the old republic",
        "dc universe online", "neverwinter", "blade & soul", "archeage", "ragnarok online",
        "runescape", "tibia", "mabinogi", "second life", "vrchat", "rec room", "roblox",
        "the elder scrolls online", "eso:", "deadside", "escape from backrooms",
    };

    private static readonly string[] Noise =
    {
        "soundtrack", "dedicated server", " sdk", "demo", "beta test", "playtest",
        "wallpaper engine", "crosshair", "benchmark", "editor",
    };

    /// <summary>Когда каталог собран (UTC): кэш или свежий обход жанров.</summary>
    public static DateTime FetchedUtc { get; private set; }

    /// <summary>Последняя ошибка загрузки — её показываем в интерфейсе, если каталог не собрался.</summary>
    public static string? LastError { get; private set; }

    private static string CacheDir
    {
        get
        {
            var dir = StoreFeed.CacheDir;
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    private static string CacheFile => Path.Combine(CacheDir, "catalog.json");
    private static string FailFile => Path.Combine(CacheDir, "catalog-fail.txt");

    /// <summary>Собирает каталог: сначала кэш, если он свежий, иначе обход жанров.</summary>
    public static async Task<List<StoreGame>> LoadAsync(bool force, Action<string>? progress, CancellationToken ct)
    {
        var cached = ReadCache();
        if (!force && cached != null)
        {
            FetchedUtc = ReadCacheStamp();
            return cached;
        }

        if (!force && Throttled())
        {
            LastError = Loc.T("catalog.throttled");
            if (cached != null) return cached;
        }

        try
        {
            using var http = MakeClient();
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(90);

            var merged = new Dictionary<int, StoreGame>();
            var order = new List<int>();

            // 1. списки, которые из каталога вычитаем
            progress?.Invoke(Loc.T("catalog.loadingFree"));
            var free = (await CollectAsync(http, "maxprice=free", FreePages, ct))
                .Select(r => r.AppId).ToHashSet();
            progress?.Invoke(Loc.T("catalog.loadingMmo"));
            var mmo = (await CollectAsync(http, "tags=128", MmoPages, ct))
                .Select(r => r.AppId).ToHashSet();

            // 2. жанровые подборки
            var genreIndex = 0;
            foreach (var (genre, tag) in SteamGenres)
            {
                ct.ThrowIfCancellationRequested();
                progress?.Invoke(Loc.F("catalog.loading", ++genreIndex, SteamGenres.Length, DisplayGenre(genre)));

                if (DateTime.UtcNow > deadline) break;

                var rows = await CollectAsync(http, $"tags={tag}", PagesPerGenre, ct);

                foreach (var (appId, name) in rows)
                {
                    if (free.Contains(appId) || mmo.Contains(appId)) continue;

                    if (!merged.TryGetValue(appId, out var game))
                    {
                        if (Rejected(name)) continue;

                        game = new StoreGame
                        {
                            AppId = appId,
                            Name = name,
                            CoverUrl = $"https://cdn.cloudflare.steamstatic.com/steam/apps/{appId}/header.jpg",
                            HeaderUrl = $"https://cdn.cloudflare.steamstatic.com/steam/apps/{appId}/header.jpg",
                        };

                        game.Sections.Add("all");
                        merged[appId] = game;
                        order.Add(appId);
                    }

                    var label = DisplayGenre(genre);
                    if (Array.IndexOf(game.Genres, label) < 0)
                        game.Genres = game.Genres.Append(label).ToArray();
                }
            }

            var games = order.Select(id => merged[id]).ToList();
            if (games.Count == 0)
            {
                LastError = Loc.T("catalog.empty");
                AppPaths.Log("StoreCatalog: пустой результат поиска магазина");
                return cached ?? new List<StoreGame>();
            }

            FetchedUtc = DateTime.UtcNow;
            LastError = null;
            AppPaths.Log($"StoreCatalog: собрано {games.Count} игр");

            WriteCache(games);
            TryDelete(FailFile);
            return games;
        }
        catch (OperationCanceledException)
        {
            return cached ?? new List<StoreGame>();
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            AppPaths.Log("StoreCatalog failed: " + ex.Message);
            try { File.WriteAllText(FailFile, DateTime.UtcNow.ToString("O") + " " + ex.Message); } catch { /* ignore */ }
            return cached ?? new List<StoreGame>();
        }
    }

    // ------------------------------------------------------------------ сеть

    private static async Task<List<(int AppId, string Name)>> CollectAsync(HttpClient http,
        string query, int pages, CancellationToken ct)
    {
        var rows = new List<(int AppId, string Name)>();
        for (var page = 0; page < pages; page++)
        {
            ct.ThrowIfCancellationRequested();
            rows.AddRange(await FetchPageAsync(http, query, page * 100, ct));
            await Task.Delay(450, ct);
        }

        return rows;
    }

    private static async Task<List<(int AppId, string Name)>> FetchPageAsync(HttpClient http,
        string query, int start, CancellationToken ct)
    {
        var url = $"{SearchUrl}&start={start}&{query}";

        for (var attempt = 0; attempt < 3; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                using var response = await http.GetAsync(url, ct);
                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    await Task.Delay(TimeSpan.FromSeconds(8 * (attempt + 1)), ct);
                    continue;
                }

                response.EnsureSuccessStatusCode();
                var json = await response.Content.ReadAsStringAsync(ct);
                return ParsePage(json);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (attempt < 2)
            {
                AppPaths.Log($"StoreCatalog page {start} ({query}): {ex.Message}");
                await Task.Delay(TimeSpan.FromSeconds(2 + attempt * 2), ct);
            }
            catch
            {
                break;
            }
        }

        return new List<(int AppId, string Name)>();
    }

    private static readonly Regex RowRegex = new("data-ds-appid=\"(\\d+)[,\"](.*?)</a>", RegexOptions.Singleline);
    private static readonly Regex TitleRegex = new("<span class=\"title\">(.*?)</span>", RegexOptions.Singleline);

    /// <summary>Разбирает HTML-кусок ответа поиска: пары «идентификатор — название».</summary>
    private static List<(int AppId, string Name)> ParsePage(string json)
    {
        var result = new List<(int AppId, string Name)>();

        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("results_html", out var html) ||
            html.ValueKind != JsonValueKind.String)
            return result;

        var text = html.GetString() ?? "";

        foreach (Match row in RowRegex.Matches(text))
        {
            if (!int.TryParse(row.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var appId))
                continue;

            var title = TitleRegex.Match(row.Groups[2].Value);
            if (!title.Success) continue;

            var name = WebUtility.HtmlDecode(Regex.Replace(title.Groups[1].Value, "<.*?>", "")).Trim();
            if (name.Length == 0) continue;

            result.Add((appId, name));
        }

        return result;
    }

    private static HttpClient MakeClient()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(45) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) Ludex/" + Updater.CurrentVersionText);
        http.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language", "ru,en;q=0.8");
        return http;
    }

    // ------------------------------------------------------------------ фильтры

    /// <summary>Название игры на языке интерфейса.</summary>
    public static string DisplayGenre(string genre) => Loc.T("cgenre." + genre);

    private static bool Rejected(string name)
    {
        var lower = name.ToLowerInvariant();

        foreach (var word in Noise)
            if (lower.Contains(word, StringComparison.Ordinal)) return true;

        foreach (var word in OnlineOnly)
            if (lower.Contains(word, StringComparison.Ordinal)) return true;

        return false;
    }

    private static bool Throttled()
    {
        try
        {
            if (!File.Exists(FailFile)) return false;
            var stamp = File.GetLastWriteTimeUtc(FailFile);
            return DateTime.UtcNow - stamp < FailTtl;
        }
        catch
        {
            return false;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch
        {
            // не критично
        }
    }

    // ------------------------------------------------------------------ кэш

    private sealed class CacheFileModel
    {
        public DateTime FetchedUtc { get; set; }
        public List<CacheEntry> Games { get; set; } = new();
    }

    private sealed class CacheEntry
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string[] G { get; set; } = Array.Empty<string>();
    }

    private static DateTime ReadCacheStamp()
    {
        try
        {
            if (!File.Exists(CacheFile)) return default;
            var model = JsonSerializer.Deserialize<CacheFileModel>(File.ReadAllText(CacheFile));
            return model?.FetchedUtc ?? default;
        }
        catch
        {
            return default;
        }
    }

    private static List<StoreGame>? ReadCache()
    {
        try
        {
            if (!File.Exists(CacheFile)) return null;

            var model = JsonSerializer.Deserialize<CacheFileModel>(File.ReadAllText(CacheFile));
            if (model == null || model.Games.Count == 0) return null;
            if (DateTime.UtcNow - model.FetchedUtc > Ttl) return null;

            var list = new List<StoreGame>(model.Games.Count);
            foreach (var e in model.Games)
            {
                var game = new StoreGame
                {
                    AppId = e.Id,
                    Name = e.Name,
                    CoverUrl = $"https://cdn.cloudflare.steamstatic.com/steam/apps/{e.Id}/header.jpg",
                    HeaderUrl = $"https://cdn.cloudflare.steamstatic.com/steam/apps/{e.Id}/header.jpg",
                };

                game.Genres = e.G;
                game.Sections.Add("all");

                var cover = Path.Combine(StoreFeed.CoversDir, $"{e.Id}.jpg");
                if (File.Exists(cover)) game.CoverPath = cover;

                list.Add(game);
            }

            AppPaths.Log($"StoreCatalog: из кэша {list.Count} игр");
            return list;
        }
        catch (Exception ex)
        {
            AppPaths.Log("StoreCatalog cache: " + ex.Message);
            return null;
        }
    }

    private static void WriteCache(List<StoreGame> games)
    {
        try
        {
            var model = new CacheFileModel { FetchedUtc = DateTime.UtcNow };
            foreach (var g in games)
            {
                model.Games.Add(new CacheEntry
                {
                    Id = g.AppId,
                    Name = g.Name,
                    G = g.Genres,
                });
            }

            File.WriteAllText(CacheFile, JsonSerializer.Serialize(model));
        }
        catch (Exception ex)
        {
            AppPaths.Log("StoreCatalog cache write: " + ex.Message);
        }
    }
}
