using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Ludex.Services;

/// <summary>Игра из сетевого каталога (данные Steam Store).</summary>
public sealed class StoreGame
{
    public int AppId { get; init; }
    public string Name { get; init; } = "";
    public string Developer { get; set; } = "";
    public string Publisher { get; set; } = "";
    public string CoverUrl { get; init; } = "";
    public string HeaderUrl { get; init; } = "";
    public int DiscountPercent { get; init; }
    /// <summary>Цена в минимальных единицах валюты (копейки/центы), 0 — бесплатно.</summary>
    public long FinalPrice { get; init; }
    public long OriginalPrice { get; init; }
    public string Currency { get; init; } = "";
    public bool Free { get; init; }
    public bool Windows { get; init; }
    public bool Mac { get; init; }
    public bool Linux { get; init; }

    /// <summary>Оценка числа владельцев (нижняя граница) и текущий онлайн — из открытой статистики.</summary>
    public long OwnersLow { get; init; }
    public int Ccu { get; init; }

    /// <summary>Разделы, в которые игра попала: today, new, discount, top, soon, all, hits, free.</summary>
    public HashSet<string> Sections { get; } = new();

    // ---- добирается из appdetails
    public string? ReleaseDate { get; set; }
    public string[] Genres { get; set; } = Array.Empty<string>();
    public string? Description { get; set; }
    public bool ComingSoon { get; set; }

    // ---- локальные файлы кэша
    public string? CoverPath { get; set; }
    public string? HeaderPath { get; set; }

    public string SteamUrl => $"https://store.steampowered.com/app/{AppId}/";
}

public sealed class StoreFeedResult
{
    public bool Ok { get; init; }
    public string? Error { get; init; }
    public List<StoreGame> Games { get; } = new();
    public DateTime FetchedUtc { get; init; }
    public bool FromCache { get; init; }
}

/// <summary>
/// Сетевой каталог рекомендаций: публичные витрины Steam Store (лидеры продаж, новинки,
/// скидки, «скоро»), описания из appdetails и обложки с CDN. Всё кэшируется на диск,
/// поэтому повторный запуск работает без сети.
/// </summary>
public static class StoreFeed
{
    private const string FeedUrl = "https://store.steampowered.com/api/featuredcategories";
    private const string DetailsUrl = "https://store.steampowered.com/api/appdetails";

    private static readonly TimeSpan FeedTtl = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan MetaTtl = TimeSpan.FromDays(7);

    /// <summary>Порядок разделов: как на витрине Steam.</summary>
    public static readonly (string Key, string Json)[] Sections =
    {
        ("top", "top_sellers"),
        ("discount", "specials"),
        ("new", "new_releases"),
        ("soon", "coming_soon"),
    };

    public static string CacheDir
    {
        get
        {
            var dir = Path.Combine(AppPaths.Root, "store");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    public static string CoversDir
    {
        get
        {
            var dir = Path.Combine(CacheDir, "covers");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    private static string FeedFile => Path.Combine(CacheDir, $"feed-{LangCode}.json");
    private static string MetaFile => Path.Combine(CacheDir, $"meta-{LangCode}.json");

    private static string LangCode => Loc.IsRu ? "ru" : "en";
    private static string SteamLang => Loc.IsRu ? "russian" : "english";
    private static string Country => Loc.IsRu ? "ru" : "us";

    private static HttpClient NewClient()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(25) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Ludex/" + Updater.CurrentVersionText);
        return http;
    }

    // ------------------------------------------------------------------ витрины

    /// <summary>Загрузить витрины: сначала кэш (если свежий), иначе сеть.</summary>
    public static async Task<StoreFeedResult> LoadAsync(bool force, CancellationToken ct)
    {
        var cached = ReadFeedCache();
        if (!force && cached != null && DateTime.UtcNow - cached.Value.FetchedUtc < FeedTtl)
            return cached.Value.Result;

        try
        {
            using var http = NewClient();
            var url = $"{FeedUrl}?cc={Country}&l={SteamLang}";
            var json = await http.GetStringAsync(url, ct);

            var games = ParseFeed(json, out var fetchedUtc);
            var result = new StoreFeedResult { Ok = true, FetchedUtc = fetchedUtc };
            result.Games.AddRange(games);

            ApplyCachedMeta(result.Games);
            ApplyCachedCovers(result.Games);
            WriteFeedCache(json);

            AppPaths.Log($"StoreFeed: загружено {games.Count} игр");
            return result;
        }
        catch (Exception ex)
        {
            AppPaths.Log("StoreFeed failed: " + ex.Message);

            // сеть недоступна — отдаём даже устаревший кэш
            if (cached != null)
            {
                var stale = new StoreFeedResult { Ok = true, FetchedUtc = cached.Value.FetchedUtc, FromCache = true };
                stale.Games.AddRange(cached.Value.Result.Games);
                return stale;
            }

            return new StoreFeedResult { Ok = false, Error = ex.Message };
        }
    }

    private static List<StoreGame> ParseFeed(string json, out DateTime fetchedUtc)
    {
        fetchedUtc = DateTime.UtcNow;

        var map = new Dictionary<int, StoreGame>();
        var order = new List<int>();

        using var doc = JsonDocument.Parse(json);

        foreach (var (key, section) in Sections)
        {
            if (!doc.RootElement.TryGetProperty(section, out var block)) continue;
            if (!block.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array) continue;

            foreach (var item in items.EnumerateArray())
            {
                if (!item.TryGetProperty("id", out var idEl) || !idEl.TryGetInt32(out var id) || id <= 0) continue;

                if (!map.TryGetValue(id, out var game))
                {
                    var name = item.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                    if (string.IsNullOrWhiteSpace(name)) continue;

                    var finalPrice = Long(item, "final_price", 0);
                    var originalPrice = Long(item, "original_price", finalPrice);

                    game = new StoreGame
                    {
                        AppId = id,
                        Name = name,
                        CoverUrl = Str(item, "large_capsule_image"),
                        HeaderUrl = Str(item, "header_image"),
                        DiscountPercent = (int)Long(item, "discount_percent", 0),
                        FinalPrice = finalPrice,
                        OriginalPrice = originalPrice,
                        Currency = Str(item, "currency"),
                        Free = finalPrice == 0 && originalPrice == 0 && section != "coming_soon",
                        Windows = Bool(item, "windows_available"),
                        Mac = Bool(item, "mac_available"),
                        Linux = Bool(item, "linux_available"),
                    };

                    map[id] = game;
                    order.Add(id);
                }

                game.Sections.Add(section switch
                {
                    "top_sellers" => "top",
                    "specials" => "discount",
                    "new_releases" => "new",
                    _ => "soon",
                });
            }
        }

        // «Сегодня» — лидеры продаж и скидки, как их показывает главная страница магазина
        foreach (var id in order)
        {
            var g = map[id];
            if (g.Sections.Contains("top") || g.Sections.Contains("discount")) g.Sections.Add("today");
        }

        return order.Select(id => map[id]).ToList();

        // «original_price» у бесплатных и будущих игр приходит как null — читаем только числа
        static long Long(JsonElement el, string name, long fallback) =>
            el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out var value)
                ? value
                : fallback;

        static string Str(JsonElement el, string name) =>
            el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";

        static bool Bool(JsonElement el, string name) =>
            el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.True;
    }

    // ------------------------------------------------------------------ описания

    /// <summary>Догрузить жанры, дату выхода и описание через appdetails (с кэшем на неделю).</summary>
    public static async Task EnrichAsync(IReadOnlyList<StoreGame> games, Action? onUpdated, CancellationToken ct)
    {
        var meta = ReadMetaCache();
        var need = games.Where(g => !meta.ContainsKey(g.AppId) || DateTime.UtcNow - meta[g.AppId].UpdatedUtc > MetaTtl)
                        .Select(g => g.AppId)
                        .ToList();

        // уже известное применяем сразу
        foreach (var g in games) ApplyMeta(g, meta);

        if (need.Count == 0) return;

        using var http = NewClient();
        using var gate = new SemaphoreSlim(6);
        var done = 0;

        var tasks = need.Select(async id =>
        {
            await gate.WaitAsync(ct);
            try
            {
                var url = $"{DetailsUrl}?appids={id}&l={SteamLang}&cc={Country}";
                var json = await http.GetStringAsync(url, ct);

                using var doc = JsonDocument.Parse(json);
                if (!doc.RootElement.TryGetProperty(id.ToString(CultureInfo.InvariantCulture), out var entry)) return;
                if (!entry.TryGetProperty("success", out var ok) || ok.ValueKind != JsonValueKind.True) return;
                if (!entry.TryGetProperty("data", out var data)) return;

                var item = new MetaEntry { UpdatedUtc = DateTime.UtcNow };

                if (data.TryGetProperty("genres", out var genres) && genres.ValueKind == JsonValueKind.Array)
                    item.Genres = genres.EnumerateArray()
                        .Select(g => g.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "")
                        .Where(s => s.Length > 0)
                        .Distinct()
                        .ToArray();

                if (data.TryGetProperty("release_date", out var rd))
                {
                    item.ReleaseDate = rd.TryGetProperty("date", out var date) ? date.GetString() : null;
                    item.ComingSoon = rd.TryGetProperty("coming_soon", out var cs) && cs.ValueKind == JsonValueKind.True;
                }

                if (data.TryGetProperty("short_description", out var sd))
                    item.Description = sd.GetString();

                if (data.TryGetProperty("header_image", out var hi) && hi.ValueKind == JsonValueKind.String)
                    item.HeaderImage = hi.GetString();

                item.Developer = FirstOf(data, "developers");
                item.Publisher = FirstOf(data, "publishers");

                lock (meta) meta[id] = item;
            }
            catch (Exception ex)
            {
                AppPaths.Log($"StoreFeed details {id}: {ex.Message}");
            }
            finally
            {
                gate.Release();
                if (Interlocked.Increment(ref done) % 10 == 0) onUpdated?.Invoke();
            }
        });

        await Task.WhenAll(tasks);

        foreach (var g in games) ApplyMeta(g, meta);
        WriteMetaCache(meta);
        onUpdated?.Invoke();

        static void ApplyMeta(StoreGame g, Dictionary<int, MetaEntry> meta)
        {
            if (!meta.TryGetValue(g.AppId, out var m)) return;
            if (m.Genres.Length > 0) g.Genres = m.Genres;
            g.ReleaseDate ??= m.ReleaseDate;
            g.ComingSoon = g.ComingSoon || m.ComingSoon;
            if (!string.IsNullOrWhiteSpace(m.Description)) g.Description ??= m.Description;
            if (string.IsNullOrEmpty(g.Developer) && !string.IsNullOrWhiteSpace(m.Developer)) g.Developer = m.Developer!;
            if (string.IsNullOrEmpty(g.Publisher) && !string.IsNullOrWhiteSpace(m.Publisher)) g.Publisher = m.Publisher!;
        }
    }

    // ------------------------------------------------------------------ обложки

    /// <summary>Скачать обложки в кэш (уже скачанные пропускаются).</summary>
    public static async Task DownloadCoversAsync(IReadOnlyList<StoreGame> games, Action? onUpdated, CancellationToken ct)
    {
        using var http = NewClient();
        using var gate = new SemaphoreSlim(6);
        var done = 0;

        var tasks = games.Select(async game =>
        {
            var cover = Path.Combine(CoversDir, $"{game.AppId}.jpg");
            var header = Path.Combine(CoversDir, $"{game.AppId}-h.jpg");

            if (File.Exists(cover) && new FileInfo(cover).Length > 1000) game.CoverPath = cover;
            if (File.Exists(header) && new FileInfo(header).Length > 1000) game.HeaderPath = header;

            var needCover = game.CoverPath == null && game.CoverUrl.Length > 0;
            var needHeader = game.HeaderPath == null && game.HeaderUrl.Length > 0;
            if (!needCover && !needHeader)
            {
                if (Interlocked.Increment(ref done) % 8 == 0) onUpdated?.Invoke();
                return;
            }

            await gate.WaitAsync(ct);
            try
            {
                if (needCover)
                {
                    var bytes = await http.GetByteArrayAsync(game.CoverUrl, ct);
                    await File.WriteAllBytesAsync(cover, bytes, ct);
                    game.CoverPath = cover;
                }

                if (needHeader)
                {
                    var bytes = await http.GetByteArrayAsync(game.HeaderUrl, ct);
                    await File.WriteAllBytesAsync(header, bytes, ct);
                    game.HeaderPath = header;
                }
            }
            catch (Exception ex)
            {
                AppPaths.Log($"StoreFeed cover {game.AppId}: {ex.Message}");
            }
            finally
            {
                gate.Release();
                if (Interlocked.Increment(ref done) % 8 == 0) onUpdated?.Invoke();
            }
        });

        await Task.WhenAll(tasks);
        onUpdated?.Invoke();
    }

    // ------------------------------------------------------------------ цены

    /// <summary>«1 499 ₽», «Бесплатно», «—» для будущих игр без цены.</summary>
    public static string PriceText(StoreGame game)
    {
        if (game.Free) return Loc.T("store.free");
        if (game.FinalPrice <= 0) return "—";
        return FormatPrice(game.FinalPrice, game.Currency);
    }

    /// <summary>Старая цена до скидки (пустая строка, если скидки нет).</summary>
    public static string OldPriceText(StoreGame game)
    {
        if (game.DiscountPercent <= 0 || game.OriginalPrice <= game.FinalPrice) return "";
        return FormatPrice(game.OriginalPrice, game.Currency);
    }

    private static string FormatPrice(long minorUnits, string currency)
    {
        var value = minorUnits / 100.0;
        var number = value % 1 == 0
            ? value.ToString("N0", CultureInfo.CurrentCulture)
            : value.ToString("N2", CultureInfo.CurrentCulture);

        return currency switch
        {
            "RUB" => $"{number} ₽",
            "USD" => $"${number}",
            "EUR" => $"€{number}",
            "UAH" => $"{number} ₴",
            "KZT" => $"{number} ₸",
            "BYN" => $"{number} Br",
            "" => number,
            _ => $"{number} {currency}",
        };
    }

    // ------------------------------------------------------------------ кэш

    private sealed class MetaEntry
    {
        public string[] Genres { get; set; } = Array.Empty<string>();
        public string? HeaderImage { get; set; }
        public string? Developer { get; set; }
        public string? Publisher { get; set; }
        public string? ReleaseDate { get; set; }
        public string? Description { get; set; }
        public bool ComingSoon { get; set; }
        public DateTime UpdatedUtc { get; set; }
    }

    private static (StoreFeedResult Result, DateTime FetchedUtc)? ReadFeedCache()
    {
        try
        {
            if (!File.Exists(FeedFile)) return null;

            var fetched = File.GetLastWriteTimeUtc(FeedFile);
            var json = File.ReadAllText(FeedFile);
            var games = ParseFeed(json, out _);
            if (games.Count == 0) return null;

            var result = new StoreFeedResult { Ok = true, FetchedUtc = fetched, FromCache = true };
            result.Games.AddRange(games);

            ApplyCachedMeta(result.Games);
            ApplyCachedCovers(result.Games);
            return (result, fetched);
        }
        catch (Exception ex)
        {
            AppPaths.Log("StoreFeed cache read: " + ex.Message);
            return null;
        }
    }

    private static void WriteFeedCache(string json)
    {
        try { File.WriteAllText(FeedFile, json); }
        catch (Exception ex) { AppPaths.Log("StoreFeed cache write: " + ex.Message); }
    }

    /// <summary>
    /// Ссылка на обложку из карточки магазина. Нужна, когда прямой адрес CDN
    /// (<c>apps/{id}/header.jpg</c>) у новой или снятой с продажи игры отдаёт 404:
    /// в ответе магазина у обложки собственный хеш в пути.
    /// </summary>
    public static async Task<string?> HeaderImageAsync(int appId, CancellationToken ct)
    {
        var meta = ReadMetaCache();
        if (meta.TryGetValue(appId, out var known) && !string.IsNullOrWhiteSpace(known.HeaderImage))
            return known.HeaderImage;

        try
        {
            using var http = NewClient();
            var url = $"{DetailsUrl}?appids={appId}&l={SteamLang}&cc={Country}";
            var json = await http.GetStringAsync(url, ct);

            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty(appId.ToString(CultureInfo.InvariantCulture), out var entry)) return null;
            if (!entry.TryGetProperty("data", out var data)) return null;
            if (!data.TryGetProperty("header_image", out var hi) || hi.ValueKind != JsonValueKind.String) return null;

            var header = hi.GetString();
            if (string.IsNullOrWhiteSpace(header)) return null;

            known ??= new MetaEntry { UpdatedUtc = DateTime.UtcNow };
            known.HeaderImage = header;
            lock (meta) meta[appId] = known;
            WriteMetaCache(meta);

            return header;
        }
        catch (Exception ex)
        {
            AppPaths.Log($"StoreFeed header {appId}: {ex.Message}");
            return null;
        }
    }

    /// <summary>Первый элемент массива строк из ответа магазина (разработчик, издатель).</summary>
    private static string? FirstOf(JsonElement data, string name)
    {
        if (!data.TryGetProperty(name, out var arr) || arr.ValueKind != JsonValueKind.Array) return null;

        foreach (var item in arr.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String) continue;
            var text = item.GetString();
            if (!string.IsNullOrWhiteSpace(text)) return text;
        }

        return null;
    }

    private static Dictionary<int, MetaEntry> ReadMetaCache()
    {
        try
        {
            if (!File.Exists(MetaFile)) return new Dictionary<int, MetaEntry>();
            var json = File.ReadAllText(MetaFile);
            return JsonSerializer.Deserialize<Dictionary<int, MetaEntry>>(json) ?? new Dictionary<int, MetaEntry>();
        }
        catch (Exception ex)
        {
            AppPaths.Log("StoreFeed meta read: " + ex.Message);
            return new Dictionary<int, MetaEntry>();
        }
    }

    private static void WriteMetaCache(Dictionary<int, MetaEntry> meta)
    {
        try
        {
            lock (meta)
                File.WriteAllText(MetaFile, JsonSerializer.Serialize(meta));
        }
        catch (Exception ex)
        {
            AppPaths.Log("StoreFeed meta write: " + ex.Message);
        }
    }

    private static void ApplyCachedMeta(List<StoreGame> games)
    {
        var meta = ReadMetaCache();
        if (meta.Count == 0) return;

        foreach (var g in games)
        {
            if (!meta.TryGetValue(g.AppId, out var m)) continue;
            if (m.Genres.Length > 0) g.Genres = m.Genres;
            g.ReleaseDate = m.ReleaseDate;
            g.ComingSoon = m.ComingSoon;
            g.Description = m.Description;
        }
    }

    private static void ApplyCachedCovers(List<StoreGame> games)
    {
        foreach (var g in games)
        {
            var cover = Path.Combine(CoversDir, $"{g.AppId}.jpg");
            var header = Path.Combine(CoversDir, $"{g.AppId}-h.jpg");
            if (File.Exists(cover) && new FileInfo(cover).Length > 1000) g.CoverPath = cover;
            if (File.Exists(header) && new FileInfo(header).Length > 1000) g.HeaderPath = header;
        }
    }
}
