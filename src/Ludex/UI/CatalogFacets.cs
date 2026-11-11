using System;
using System.Collections.Generic;
using System.Linq;

namespace Ludex.UI;

/// <summary>
/// Справочник фильтров каталога: жанры, темы, сортировки и готовые подборки.
/// Темы приходят из файла каталога (v3, битовая маска) — здесь только их порядок,
/// подписи и то, как из них собираются «плитки» и пресеты.
/// </summary>
public static class CatalogFacets
{
    /// <summary>Десять жанров магазина — те же подписи, что в файле каталога.</summary>
    public static readonly string[] Genres =
    {
        "Экшен", "Приключения", "Инди", "RPG", "Стратегия",
        "Симулятор", "Казуальные", "Гонки", "Спорт", "Ранний доступ",
    };

    /// <summary>Порядок тем для панели фильтров; остальные из файла добавляются в конец.</summary>
    private static readonly string[] ThemeOrder =
    {
        "Open World", "Story Rich", "Atmospheric", "Post-apocalyptic", "Survival",
        "Survival Horror", "Horror", "Zombies", "Sci-fi", "Fantasy", "Space",
        "Mystery", "Detective", "Roguelike", "Metroidvania", "Platformer", "Puzzle",
        "Shooter", "FPS", "Action-Adventure", "Stealth", "Combat", "Building",
        "Crafting", "Farming Sim", "Sandbox", "Tactical", "Turn-Based", "Difficult",
        "Exploration", "Funny", "Music", "Great Soundtrack", "Singleplayer", "Co-op",
        "VR", "Free to Play", "1990s", "Cats", "Mystery Dungeon",
    };

    /// <summary>Название темы совпадает с жанром («Action», «Racing», «Early Access»…).</summary>
    public static bool IsGenreTheme(string? name) =>
        !string.IsNullOrWhiteSpace(name) && GenreThemeNames.Contains(name.Trim().ToLowerInvariant());

    private static readonly HashSet<string> GenreThemeNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "action", "adventure", "indie", "rpg", "strategy", "simulation", "casual",
        "racing", "sports", "early access", "экшен", "приключения", "инди", "стратегия",
        "симулятор", "казуальные", "гонки", "спорт", "ранний доступ",
    };

    /// <summary>
    /// Темы, которые показываем в панели. В файле каталога темы идут вместе с жанрами
    /// (в списке есть «Action», «Adventure», «Indie»…), а служебные «Free to Play»
    /// и «Singleplayer» не полезны — их отсекаем, чтобы не дублировать жанры.
    /// </summary>
    public static string[] Tags => _tags ??= ThemeOrder
        .Where(t => !IsGenreTheme(t))
        .Where(t => t is not ("Free to Play" or "Singleplayer"))
        .ToArray();

    private static string[]? _tags;

    /// <summary>Варианты сортировки: значение и ключ локализации.</summary>
    public static readonly (string Value, string Key)[] Sorts =
    {
        ("catalog", "recs.sortStore"),
        ("az", "recs.sortAz"),
        ("za", "facet.sort.za"),
        ("new", "facet.sort.new"),
        ("old", "facet.sort.old"),
    };

    /// <summary>
    /// Готовая подпись категории. Ключа может не быть (тема пришла из файла и не описана
    /// в Loc) — тогда показываем само название, но никогда не показываем «facet.tag.…».
    /// </summary>
    public static string Label(string name)
    {
        var trimmed = name.Trim();
        if (trimmed.Length == 0) return trimmed;

        var genre = Genres.FirstOrDefault(g => string.Equals(g, trimmed, StringComparison.CurrentCultureIgnoreCase));
        var genreKey = "facet.genre." + Key(genre ?? trimmed);
        var genreText = Loc.T(genreKey);
        if (genreText != genreKey) return genreText;

        var tagKey = "facet.tag." + Key(trimmed);
        var tagText = Loc.T(tagKey);
        if (tagText != tagKey) return tagText;

        return trimmed;
    }

    /// <summary>Ключ локализации для жанра: «Экшен» → «action».</summary>
    public static string Key(string name) => name.Trim().ToLowerInvariant() switch
    {
        "экшен" or "action" => "action",
        "приключения" or "adventure" => "adventure",
        "инди" or "indie" => "indie",
        "rpg" => "rpg",
        "стратегия" or "strategy" => "strategy",
        "симулятор" or "simulation" => "simulation",
        "казуальные" or "casual" => "casual",
        "гонки" or "racing" => "racing",
        "спорт" or "sports" => "sports",
        "ранний доступ" or "early access" => "early",
        "open world" => "openworld",
        "story rich" => "story",
        "atmospheric" => "atmospheric",
        "post-apocalyptic" => "postapoc",
        "survival" => "survival",
        "survival horror" => "survivalhorror",
        "horror" => "horror",
        "zombies" => "zombies",
        "sci-fi" => "scifi",
        "fantasy" => "fantasy",
        "space" => "space",
        "mystery" => "mystery",
        "detective" => "detective",
        "roguelike" => "roguelike",
        "metroidvania" => "metroidvania",
        "platformer" => "platformer",
        "puzzle" => "puzzle",
        "shooter" => "shooter",
        "fps" => "fps",
        "action-adventure" => "actionadventure",
        "stealth" => "stealth",
        "combat" => "combat",
        "building" => "building",
        "crafting" => "crafting",
        "farming sim" => "farming",
        "sandbox" => "sandbox",
        "tactical" => "tactical",
        "turn-based" => "turnbased",
        "difficult" => "difficult",
        "exploration" => "exploration",
        "funny" => "funny",
        "music" => "music",
        "great soundtrack" => "soundtrack",
        "co-op" => "coop",
        "vr" => "vr",
        "cats" => "cats",
        "mystery dungeon" => "mysterydungeon",
        "1990s" => "y1990",
        _ => name.Trim().ToLowerInvariant().Replace(' ', '-'),
    };

    /// <summary>Готовые подборки для панели фильтров — по одному нажатию.</summary>
    public static List<(string Title, CatalogFilter Filter)> Presets()
    {
        CatalogFilter Make(string[] tags, string decade = "", string size = "")
        {
            var f = new CatalogFilter { Decade = decade, Size = size };
            foreach (var t in tags) f.Tags.Add(t);
            return f;
        }

        return new List<(string, CatalogFilter)>
        {
            ("Недавние хиты", Make(new[] { "Open World", "Story Rich" }, "2020")),
            ("Кооператив на вечер", Make(new[] { "Co-op", "Survival" })),
            ("Ужасы и зомби", Make(new[] { "Horror", "Zombies" })),
            ("Расслабиться", Make(new[] { "Casual", "Building", "Farming Sim" })),
            ("Постапокалипсис", Make(new[] { "Post-apocalyptic" })),
            ("Космос и sci-fi", Make(new[] { "Sci-fi", "Space" })),
            ("Классика 2000-х", Make(Array.Empty<string>(), "2000")),
            ("Рогалики", Make(new[] { "Roguelike" })),
        };
    }

    /// <summary>Совпадает ли игра с набором тем (любая из выбранных).</summary>
    public static bool HasTheme(Services.StoreGame game, IEnumerable<string> tags)
    {
        foreach (var tag in tags)
            if (game.Themes.Contains(tag, StringComparer.CurrentCultureIgnoreCase))
                return true;
        return false;
    }
}
