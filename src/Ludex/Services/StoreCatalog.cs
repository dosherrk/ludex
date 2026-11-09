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

/// <summary>
/// Большой каталог игр: список жанров берётся из открытой статистики SteamSpy
/// (по одному запросу на жанр), поэтому в каталоге тысячи игр с жанрами, ценами,
/// скидками и оценкой популярности. Обложки качаются по мере показа карточек.
/// </summary>
public static class StoreCatalog
{
    private const string SpyApi = "https://steamspy.com/api.php";

    /// <summary>Жанры, которые забираем. «Massively Multiplayer» намеренно нет:
    /// в рекомендациях не нужны сетевые игры вроде Dota 2 и CS.</summary>
    private static readonly string[] SteamGenres =
    {
        "Action", "Adventure", "Indie", "RPG", "Strategy", "Simulation",
        "Casual", "Racing", "Sports", "Early Access",
    };

    /// <summary>Известные сетевые free-to-play — исключаем, даже если попали в жанр.</summary>
    private static readonly HashSet<int> Blacklist = new()
    {
        570,    // Dota 2
        730,    // Counter-Strike 2
        440,    // Team Fortress 2
        1172470,// Apex Legends
        230410, // Warframe
        1085660,// Destiny 2
        2357570,// Overwatch 2
        2767030,// Marvel Rivals
        1097150,// Fall Guys
        236390, // War Thunder
        386180, // Crossout
        238960, // Path of Exile
        761890, // Albion Online
        1599340,// Lost Ark
        291550, // Brawlhalla
        252950, // Rocket League
        1203220,// Naraka: Bladepoint
        2073850,// The Finals
        2507950,// Delta Force
        2139460,// Once Human
        578080, // PUBG: BATTLEGROUNDS
        3320700,// Deadlock
    };

    /// <summary>Порог владельцев: ниже — это уже шум из инди-свалки.</summary>
    private const int MinOwners = 100000;

    /// <summary>Не больше стольких игр с одного жанра (самые популярные) — держим память в узде.</summary>
    private const int PerGenre = 1200;

    private static readonly TimeSpan Ttl = TimeSpan.FromDays(3);
    private static readonly TimeSpan FailTtl = TimeSpan.FromMinutes(10);

    private static string CacheFile => Path.Combine(StoreFeed.CacheDir, "catalog.json");
    private static string FailFile => Path.Combine(StoreFeed.CacheDir, "catalog-fail.txt");

    public static string DisplayGenre(string steamGenre) => Loc.T("cgenre." + steamGenre);

    // ------------------------------------------------------------------ загрузка

    /// <summary>Когда каталог собран (UTC): кэш или свежий обход жанров.</summary>
    public static DateTime FetchedUtc { get; private set; }

    public static async Task<List<StoreGame>> LoadAsync(bool force, Action<string>? progress, CancellationToken ct)
    {
        var cached = ReadCache();
        if (!force && cached != null) return cached;

        // недавняя неудача — не долбим сеть каждым открытием вкладки
        try
        {
            if (File.Exists(FailFile) && DateTime.UtcNow - File.GetLastWriteTimeUtc(FailFile) < FailTtl)
            {
                AppPaths.Log("StoreCatalog: недавняя неудача, берём кэш");
                return cached ?? new List<StoreGame>();
            }
        }
        catch { /* не критично */ }

        try
        {
            using var http = MakeClient();

            var merged = new Dictionary<int, StoreGame>();
            var genreIndex = 0;

            foreach (var genre in SteamGenres)
            {
                ct.ThrowIfCancellationRequested();
                progress?.Invoke(Loc.F("catalog.loading", ++genreIndex, SteamGenres.Length, DisplayGenre(genre)));

                var url = $"{SpyApi}?request=genre&genre={Uri.EscapeDataString(genre)}";
                var json = await http.GetByteArrayAsync(url, ct);

                MergeGenre(merged, json, genre);

                // жанровые ответы большие — освобождаем память до следующего
                GC.Collect(2, GCCollectionMode.Forced, true, true);
            }

            var games = merged.Values.ToList();
            FetchedUtc = DateTime.UtcNow;
            AppPaths.Log($"StoreCatalog: собрано {games.Count} игр");

            WriteCache(games);
            TryDelete(FailFile);
            return games;
        }
        catch (Exception ex)
        {
            AppPaths.Log("StoreCatalog failed: " + ex.Message);
            try { File.WriteAllText(FailFile, ex.Message); } catch { /* ignore */ }
            return cached ?? new List<StoreGame>();
        }
    }

    private static HttpClient MakeClient()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Ludex/" + Updater.CurrentVersionText);
        return http;
    }

    private static void MergeGenre(Dictionary<int, StoreGame> merged, byte[] json, string genre)
    {
        // жанровые списки приходят целиком (до 60 тысяч игр), поэтому разбираем их
        // потоком: целый документ в память не поднимаем, объекты создаём только для
        // тех игр, что прошли фильтры, и держим не больше PerGenre штук на жанр
        var picked = new List<(int Id, string Name, string Dev, string Pub, int Price, int Discount, int Ccu, int Owners)>();
        var reader = new Utf8JsonReader(json);

        while (reader.Read())
        {
            if (reader.TokenType != JsonTokenType.PropertyName || reader.CurrentDepth != 1) continue;

            if (!int.TryParse(reader.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) || id <= 0)
                continue;

            if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject) continue;

            using var doc = JsonDocument.ParseValue(ref reader);
            var el = doc.RootElement;

            var name = GetString(el, "name");
            if (name.Length == 0) continue;

            var owners = OwnersLow(GetString(el, "owners"));
            if (owners < MinOwners) continue;

            var price = GetInt(el, "price", 0);
            var ccu = GetInt(el, "ccu", 0);

            // бесплатные сетевые поделки с огромным онлайном — мимо,
            // а «цена 0 и онлайн 0» — давно снятые с продажи дубли
            if (price == 0 && ccu == 0) continue;
            if (price == 0 && ccu > 20000) continue;

            picked.Add((id, name, GetString(el, "developer"), GetString(el, "publisher"),
                price, GetInt(el, "discount", 0), ccu, owners));
        }

        foreach (var item in picked.OrderByDescending(x => x.Owners).Take(PerGenre))
            MergeItem(merged, item.Id, item.Name, item.Dev, item.Pub, item.Price, item.Discount, item.Ccu, item.Owners, genre);
    }

    private static void MergeItem(Dictionary<int, StoreGame> merged, int appId, string name,
        string developer, string publisher, int price, int discount, int ccu, int owners, string genre)
    {
        if (Blacklist.Contains(appId)) return;
        if (LooksLikeNoise(name)) return;

        if (!merged.TryGetValue(appId, out var game))
        {
            game = new StoreGame
            {
                AppId = appId,
                Name = name,
                CoverUrl = $"https://cdn.cloudflare.steamstatic.com/steam/apps/{appId}/header.jpg",
                HeaderUrl = $"https://cdn.cloudflare.steamstatic.com/steam/apps/{appId}/header.jpg",
                Developer = developer,
                Publisher = publisher,
                FinalPrice = price,
                OriginalPrice = discount > 0 ? (int)Math.Round(price / (1 - discount / 100.0)) : price,
                Currency = "USD",
                DiscountPercent = discount,
                Free = price == 0,
                OwnersLow = owners,
                Ccu = ccu,
            };

            game.Sections.Add("all");
            if (owners >= 200000) game.Sections.Add("hits");
            if (discount > 0) game.Sections.Add("discount");
            if (price == 0) game.Sections.Add("free");

            merged[appId] = game;
        }

        var label = DisplayGenre(genre);
        if (Array.IndexOf(game.Genres, label) < 0)
            game.Genres = game.Genres.Append(label).ToArray();
    }

    private static bool LooksLikeNoise(string name)
    {
        string[] noise = { "Soundtrack", "Dedicated Server", " SDK", "Demo", "Beta Test", "Playtest" };
        foreach (var n in noise)
            if (name.Contains(n, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    private static int OwnersLow(string owners)
    {
        if (string.IsNullOrWhiteSpace(owners)) return 0;
        var head = owners.Split("..")[0].Trim().Replace(",", "").Replace(" ", "");
        return int.TryParse(head, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : 0;
    }

    private static string GetString(JsonElement el, string name) =>
        el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";

    private static int GetInt(JsonElement el, string name, int fallback)
    {
        if (!el.TryGetProperty(name, out var v)) return fallback;

        if (v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var number)) return number;
        if (v.ValueKind == JsonValueKind.String &&
            int.TryParse(v.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            return parsed;

        return fallback;
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
        public string Dev { get; set; } = "";
        public string Pub { get; set; } = "";
        public string[] G { get; set; } = Array.Empty<string>();
        public int Price { get; set; }
        public int Old { get; set; }
        public int Disc { get; set; }
        public int Owners { get; set; }
        public int Ccu { get; set; }
    }

    private static List<StoreGame>? ReadCache()
    {
        try
        {
            if (!File.Exists(CacheFile)) return null;

            var model = JsonSerializer.Deserialize<CacheFileModel>(File.ReadAllText(CacheFile));
            if (model == null || model.Games.Count == 0) return null;
            if (DateTime.UtcNow - model.FetchedUtc > Ttl && model.Games.Count < 100) return null;

            var list = new List<StoreGame>(model.Games.Count);
            foreach (var e in model.Games)
            {
                var game = new StoreGame
                {
                    AppId = e.Id,
                    Name = e.Name,
                    Developer = e.Dev,
                    Publisher = e.Pub,
                    CoverUrl = $"https://cdn.cloudflare.steamstatic.com/steam/apps/{e.Id}/header.jpg",
                    HeaderUrl = $"https://cdn.cloudflare.steamstatic.com/steam/apps/{e.Id}/header.jpg",
                    FinalPrice = e.Price,
                    OriginalPrice = e.Old,
                    DiscountPercent = e.Disc,
                    Currency = "USD",
                    Free = e.Price == 0,
                    OwnersLow = e.Owners,
                    Ccu = e.Ccu,
                };

                game.Genres = e.G;
                game.Sections.Add("all");
                if (e.Owners >= 200000) game.Sections.Add("hits");
                if (e.Disc > 0) game.Sections.Add("discount");
                if (e.Price == 0 && e.Ccu > 0) game.Sections.Add("free");

                var cover = Path.Combine(StoreFeed.CoversDir, $"{e.Id}.jpg");
                if (File.Exists(cover)) game.CoverPath = cover;

                list.Add(game);
            }

            FetchedUtc = model.FetchedUtc;
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
            foreach (var g in games.OrderByDescending(x => x.OwnersLow))
            {
                model.Games.Add(new CacheEntry
                {
                    Id = g.AppId,
                    Name = g.Name,
                    Dev = g.Developer,
                    Pub = g.Publisher,
                    G = g.Genres,
                    Price = (int)g.FinalPrice,
                    Old = (int)g.OriginalPrice,
                    Disc = g.DiscountPercent,
                    Owners = (int)Math.Min(g.OwnersLow, int.MaxValue),
                    Ccu = g.Ccu,
                });
            }

            File.WriteAllText(CacheFile, JsonSerializer.Serialize(model));
            AppPaths.Log($"StoreCatalog: кэш записан ({model.Games.Count})");
        }
        catch (Exception ex)
        {
            AppPaths.Log("StoreCatalog cache write: " + ex.Message);
        }
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { /* ignore */ }
    }
}
