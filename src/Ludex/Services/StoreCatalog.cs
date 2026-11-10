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
    private const int FreePages = 6;
    private const int MmoPages = 4;

    /// <summary>Минимальный шаг между запросами к поиску: магазин отвечает 429, если частить.</summary>
    private static int _minGapMs = 600;

    /// <summary>Не больше двух запросов одновременно — иначе магазин начинает отказывать.</summary>
    private static readonly SemaphoreSlim Gate = new(2, 2);
    private static readonly object RateLock = new();
    private static DateTime _nextSlot = DateTime.UtcNow;

    private static readonly TimeSpan ExcludeTtl = TimeSpan.FromDays(14);

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

    /// <summary>Что каталог делает прямо сейчас — заставка при запуске показывает эту строку.</summary>
    public static string? ProgressText { get; private set; }

    /// <summary>Доля готовности каталога: 0 — только начали, 1 — готов.</summary>
    public static double ProgressFraction { get; private set; }

    private static Task<List<StoreGame>>? _task;

    /// <summary>Магазин ответил 403 — дальше страницы не запрашиваем, заканчиваем быстро.</summary>
    private static bool _blocked;

    /// <summary>Каталог уже загружен (или грузится) — тогда повторный вызов ничего не начинает.</summary>
    public static bool IsReady => _task is { IsCompletedSuccessfully: true };

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
    public static Task<List<StoreGame>> LoadAsync(bool force, Action<string>? progress, CancellationToken ct)
    {
        // заставка и вкладка рекомендаций просят каталог одновременно — грузим один раз
        if (!force && _task is { IsCompletedSuccessfully: true }) return _task;
        if (!force && _task is { IsCompleted: false }) return _task;

        _task = LoadCoreAsync(force, progress, ct);
        return _task;
    }

    private static void Report(Action<string>? progress, string text, double fraction)
    {
        ProgressText = text;
        ProgressFraction = fraction;
        progress?.Invoke(text);
    }

    private static async Task<List<StoreGame>> LoadCoreAsync(bool force, Action<string>? progress, CancellationToken ct)
    {
        var cached = ReadCache();
        var stamp = ReadCacheStamp();
        var fresh = stamp != default && DateTime.UtcNow - stamp <= Ttl;

        if (!force && cached != null)
        {
            FetchedUtc = stamp;

            // устаревший кэш показываем сразу, а свежий собираем в фоне
            if (!fresh)
            {
                AppPaths.Log("StoreCatalog: показываем кэш, обновляем в фоне");
                _ = Task.Run(async () =>
                {
                    try { await LoadCoreAsync(true, null, CancellationToken.None); }
                    catch (Exception ex) { AppPaths.Log("StoreCatalog background: " + ex.Message); }
                }, CancellationToken.None);
            }

            Report(progress, Loc.T("splash.catalogReady"), 1);
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

            _blocked = false;
            var merged = new Dictionary<int, StoreGame>();
            var order = new List<int>();

            // 1. списки, которые из каталога вычитаем (меняются медленно — кэш на две недели)
            Report(progress, Loc.T("catalog.loadingFree"), 0.05);
            var (free, mmo) = await LoadExclusionsAsync(http, ct);

            // 2. жанровые подборки: страницы идут параллельно, но в общем темпе
            var jobs = new List<(string Genre, string Tag, int Start)>();
            foreach (var (genre, tag) in SteamGenres)
            {
                for (var page = 0; page < PagesPerGenre; page++)
                    jobs.Add((genre, tag, page * 100));
            }

            var fetched = new System.Collections.Concurrent.ConcurrentDictionary<(string Genre, int Start),
                List<(int AppId, string Name)>>();
            var donePages = 0;

            await Task.WhenAll(jobs.Select(async job =>
            {
                ct.ThrowIfCancellationRequested();
                if (_blocked || DateTime.UtcNow > deadline) return;

                var rows = await FetchPageAsync(http, $"tags={job.Tag}", job.Start, ct);
                fetched[(job.Genre, job.Start)] = rows;

                var processed = Interlocked.Increment(ref donePages);
                Report(progress,
                    Loc.F("catalog.loading", Math.Min(SteamGenres.Length, processed * SteamGenres.Length / jobs.Count + 1),
                        SteamGenres.Length, DisplayGenre(job.Genre)),
                    0.15 + 0.8 * processed / jobs.Count);
            }));

            // склейка в стабильном порядке: жанр за жанром, страница за страницей
            foreach (var (genre, tag) in SteamGenres)
            {
                for (var page = 0; page < PagesPerGenre; page++)
                {
                    if (!fetched.TryGetValue((genre, page * 100), out var rows)) continue;

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
            }

            var games = order.Select(id => merged[id]).ToList();
            if (games.Count < 200)
            {
                LastError = _blocked ? Loc.T("catalog.blocked") : Loc.T("catalog.empty");
                try { File.WriteAllText(FailFile, DateTime.UtcNow.ToString("O") + " " + LastError); } catch { /* ignore */ }
                AppPaths.Log("StoreCatalog: пустой результат поиска магазина");
                return cached ?? new List<StoreGame>();
            }

            FetchedUtc = DateTime.UtcNow;
            LastError = null;
            Report(progress, Loc.F("recs.loaded", games.Count), 1);
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

    // ------------------------------------------------------------------ исключения

    private sealed class ExcludeFile
    {
        public DateTime FetchedUtc { get; set; }
        public int[] Free { get; set; } = Array.Empty<int>();
        public int[] Mmo { get; set; } = Array.Empty<int>();
    }

    private static string ExcludePath => Path.Combine(CacheDir, "exclude.json");

    /// <summary>
    /// Бесплатные и MMO-игры, которые не должны попадать в подборку. Списки большие,
    /// меняются медленно, поэтому кэшируются отдельно на две недели.
    /// </summary>
    private static async Task<(HashSet<int> Free, HashSet<int> Mmo)> LoadExclusionsAsync(
        HttpClient http, CancellationToken ct)
    {
        try
        {
            if (File.Exists(ExcludePath))
            {
                var saved = JsonSerializer.Deserialize<ExcludeFile>(File.ReadAllText(ExcludePath));
                if (saved != null && DateTime.UtcNow - saved.FetchedUtc < ExcludeTtl && saved.Free.Length > 0)
                {
                    return (saved.Free.ToHashSet(), saved.Mmo.ToHashSet());
                }
            }
        }
        catch (Exception ex)
        {
            AppPaths.Log("StoreCatalog exclude cache: " + ex.Message);
        }

        var free = (await CollectAsync(http, "maxprice=free", FreePages, ct)).Select(r => r.AppId).ToHashSet();
        var mmo = (await CollectAsync(http, "tags=128", MmoPages, ct)).Select(r => r.AppId).ToHashSet();

        try
        {
            File.WriteAllText(ExcludePath, JsonSerializer.Serialize(new ExcludeFile
            {
                FetchedUtc = DateTime.UtcNow,
                Free = free.ToArray(),
                Mmo = mmo.ToArray(),
            }));
        }
        catch (Exception ex)
        {
            AppPaths.Log("StoreCatalog exclude write: " + ex.Message);
        }

        return (free, mmo);
    }

    // ------------------------------------------------------------------ сеть

    private static async Task<List<(int AppId, string Name)>> CollectAsync(HttpClient http,
        string query, int pages, CancellationToken ct)
    {
        var rows = new List<(int AppId, string Name)>();
        for (var page = 0; page < pages; page++)
        {
            ct.ThrowIfCancellationRequested();
            if (_blocked) break;

            rows.AddRange(await FetchPageAsync(http, query, page * 100, ct));
        }

        return rows;
    }

    /// <summary>Ждёт свою очередь по общему темпу запросов.</summary>
    private static async Task WaitTurnAsync(CancellationToken ct)
    {
        int gap;
        lock (RateLock)
        {
            var now = DateTime.UtcNow;
            if (_nextSlot < now) _nextSlot = now;

            var slot = _nextSlot;
            _nextSlot = slot.AddMilliseconds(_minGapMs);
            gap = (int)Math.Max(0, (slot - now).TotalMilliseconds);
        }

        if (gap > 0) await Task.Delay(gap, ct);
    }

    private static async Task<List<(int AppId, string Name)>> FetchPageAsync(HttpClient http,
        string query, int start, CancellationToken ct)
    {
        var url = $"{SearchUrl}&start={start}&{query}";

        for (var attempt = 0; attempt < 3; attempt++)
        {
            ct.ThrowIfCancellationRequested();

            await Gate.WaitAsync(ct);
            try
            {
                await WaitTurnAsync(ct);
                using var response = await http.GetAsync(url, ct);

                // 403 — это не «попробуй ещё раз», а «хватит»: прекращаем обход
                if (response.StatusCode == HttpStatusCode.Forbidden && attempt == 0)
                {
                    _blocked = true;
                    AppPaths.Log($"StoreCatalog: магазин ограничил запросы ({query})");
                    return new List<(int AppId, string Name)>();
                }

                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    // магазин просит притормозить: замедляемся и постепенно ускоряемся обратно
                    _minGapMs = Math.Min(_minGapMs * 3 / 2, 2500);
                    continue;
                }

                response.EnsureSuccessStatusCode();
                var json = await response.Content.ReadAsStringAsync(ct);
                if (_minGapMs > 600) _minGapMs -= 40;
                return ParsePage(json);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (attempt < 2)
            {
                AppPaths.Log($"StoreCatalog page {start} ({query}): {ex.Message}");
            }
            catch
            {
                break;
            }
            finally
            {
                Gate.Release();
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
