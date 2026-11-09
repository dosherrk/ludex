using System;
using System.Collections.Generic;
using System.Linq;

namespace Ludex.Services;

/// <summary>
/// Игра из встроенного каталога рекомендаций. Каталог локальный: ни сети, ни внешних
/// ссылок — только название, разработчик, год, жанры и короткое описание на двух языках.
/// </summary>
public sealed class RecGame
{
    public string Title { get; init; } = "";
    public string Developer { get; init; } = "";
    public int Year { get; init; }
    /// <summary>Ключи жанров: action, rpg, strategy, indie, horror, sim, adventure, survival.</summary>
    public string[] Genres { get; init; } = Array.Empty<string>();
    public string Ru { get; init; } = "";
    public string En { get; init; } = "";

    public bool Has(string genre) => genre == "all" || Array.IndexOf(Genres, genre) >= 0;
    public string Description => Loc.IsRu ? Ru : En;

    /// <summary>Детерминированная пара цветов постера — от названия, поэтому не «скачет».</summary>
    public (string A, string B) Poster() => PosterColors(Title);

    /// <summary>Пара цветов для заливки-заглушки по названию игры.</summary>
    public static (string A, string B) PosterColors(string title)
    {
        unchecked
        {
            uint h = 2166136261;
            foreach (var ch in title) h = (h ^ ch) * 16777619;

            var hue = (h % 360) / 360.0;
            var hue2 = ((h / 360) % 80 + 20) / 360.0 + hue;   // сдвиг соседнего оттенка
            if (hue2 > 1) hue2 -= 1;

            var a = Hsv(hue, 0.55, 0.30);
            var b = Hsv(hue2, 0.72, 0.78);
            return (a, b);
        }
    }

    private static string Hsv(double h, double s, double v)
    {
        var i = (int)Math.Floor(h * 6);
        var f = h * 6 - i;
        var p = v * (1 - s);
        var q = v * (1 - f * s);
        var t = v * (1 - (1 - f) * s);

        double r, g, b;
        switch (i % 6)
        {
            case 0: r = v; g = t; b = p; break;
            case 1: r = q; g = v; b = p; break;
            case 2: r = p; g = v; b = t; break;
            case 3: r = p; g = q; b = v; break;
            case 4: r = t; g = p; b = v; break;
            default: r = v; g = p; b = q; break;
        }

        return $"#{To(r)}{To(g)}{To(b)}";
        static string To(double x) => ((int)Math.Round(Math.Clamp(x, 0, 1) * 255)).ToString("x2");
    }
}

/// <summary>Встроенный каталог рекомендаций и его фильтры (поиск, жанр, годы, сортировка).</summary>
public static class Recommendations
{
    /// <summary>Год, с которого игра считается новинкой.</summary>
    public const int NewSinceYear = 2024;

    public static readonly string[] Genres =
    {
        "action", "rpg", "strategy", "indie", "horror", "sim", "adventure", "survival",
    };

    public static readonly IReadOnlyList<RecGame> Games = new List<RecGame>
    {
        new() { Title = "Baldur's Gate 3", Developer = "Larian Studios", Year = 2023, Genres = new[] { "rpg", "strategy" },
            Ru = "Партийная RPG по правилам настольной D&D: три акта, десятки спутников и почти любой выбор имеет последствия.",
            En = "A party-based RPG on D&D rules: three acts, dozens of companions and consequences for almost every choice." },
        new() { Title = "Divinity: Original Sin 2", Developer = "Larian Studios", Year = 2017, Genres = new[] { "rpg" },
            Ru = "Пошаговые бои, взаимодействие стихий и кооператив до четырёх игроков в мире Ривеллона.",
            En = "Turn-based combat, elemental interactions and four-player co-op across Rivellon." },
        new() { Title = "Disco Elysium", Developer = "ZA/UM", Year = 2019, Genres = new[] { "rpg", "adventure" },
            Ru = "Детектив без боевой системы: спор с собственными навыками, диалоги и расследование в Мартинезе.",
            En = "A detective story without combat: arguments with your own skills, dialogue and a murder in Martinaise." },
        new() { Title = "The Witcher 3: Wild Hunt", Developer = "CD Projekt RED", Year = 2015, Genres = new[] { "rpg", "action" },
            Ru = "Геральт в открытом мире: контракты на чудовищ, побочные истории и два больших дополнения.",
            En = "Geralt in an open world: monster contracts, side stories and two large expansions." },
        new() { Title = "Cyberpunk 2077", Developer = "CD Projekt RED", Year = 2020, Genres = new[] { "rpg", "action" },
            Ru = "Найт-Сити, импланты и три пути развития героя; после обновлений — с полноценной полицейской системой.",
            En = "Night City, implants and three character builds, polished up by years of free updates." },
        new() { Title = "Kingdom Come: Deliverance II", Developer = "Warhorse Studios", Year = 2025, Genres = new[] { "rpg", "adventure" },
            Ru = "Историческая RPG без магии: средневековая Богемия, мечи, репутация и чешская деревня в деталях.",
            En = "A history-grounded RPG without magic: medieval Bohemia, swords, reputation and a very detailed countryside." },
        new() { Title = "Clair Obscur: Expedition 33", Developer = "Sandfall Interactive", Year = 2025, Genres = new[] { "rpg" },
            Ru = "Пошаговая RPG с парированием в реальном времени и рисованным миром в духе belle époque.",
            En = "A turn-based RPG with real-time parries and a painterly belle époque world." },
        new() { Title = "Elden Ring", Developer = "FromSoftware", Year = 2022, Genres = new[] { "action", "rpg" },
            Ru = "Открытый мир Междуземья: свободное исследование, сложные боссы и билды под любой стиль.",
            En = "The open world of the Lands Between: free exploration, hard bosses and builds for any playstyle." },
        new() { Title = "Sekiro: Shadows Die Twice", Developer = "FromSoftware", Year = 2019, Genres = new[] { "action" },
            Ru = "Дуэли на клинках с упором на парирование, крюк-кошка и феодальная Япония.",
            En = "Blade duels built around parrying, a grappling hook and feudal Japan." },
        new() { Title = "Lies of P", Developer = "Round8 Studio", Year = 2023, Genres = new[] { "action", "rpg" },
            Ru = "Мрачная вариация на тему Пиноккио: сменные лезвия, кукольный город и непростые боссы.",
            En = "A dark take on Pinocchio: swappable blades, a puppet city and demanding bosses." },
        new() { Title = "Hades", Developer = "Supergiant Games", Year = 2020, Genres = new[] { "indie", "action" },
            Ru = "Роглайт-побег из подземного царства, где каждая смерть двигает историю вперёд.",
            En = "A roguelite escape from the underworld where every death moves the story forward." },
        new() { Title = "Hades II", Developer = "Supergiant Games", Year = 2025, Genres = new[] { "indie", "action" },
            Ru = "Продолжение с новой героиней, колдовством и заметно более широким набором маршрутов.",
            En = "A sequel with a new heroine, witchcraft and a much wider set of routes." },
        new() { Title = "Hollow Knight", Developer = "Team Cherry", Year = 2017, Genres = new[] { "indie", "adventure" },
            Ru = "Метроидвания о подземном королевстве: ручная карта, скрытые комнаты и очень точная боёвка.",
            En = "A metroidvania through an underground kingdom: hand-drawn map, hidden rooms and precise combat." },
        new() { Title = "Hollow Knight: Silksong", Developer = "Team Cherry", Year = 2025, Genres = new[] { "indie", "adventure" },
            Ru = "Хорнет в новом королевстве: быстрее, выше и с системой инструментов вместо амулетов.",
            En = "Hornet in a new kingdom: faster, higher and built around tools instead of charms." },
        new() { Title = "Celeste", Developer = "Extremely OK Games", Year = 2018, Genres = new[] { "indie", "adventure" },
            Ru = "Точный платформер о восхождении на гору — с честными комнатами и ассист-режимом.",
            En = "A precise platformer about climbing a mountain, with fair rooms and a full assist mode." },
        new() { Title = "Ori and the Will of the Wisps", Developer = "Moon Studios", Year = 2020, Genres = new[] { "indie", "adventure" },
            Ru = "Красивая метроидвания с плавным движением и оркестровым саундтреком.",
            En = "A beautiful metroidvania with fluid movement and an orchestral score." },
        new() { Title = "Vampire Survivors", Developer = "poncle", Year = 2022, Genres = new[] { "indie", "action" },
            Ru = "Автоатака, тысячи врагов на экране и сборка билда из случайных улучшений.",
            En = "Auto-attacks, thousands of enemies on screen and builds assembled from random upgrades." },
        new() { Title = "Balatro", Developer = "LocalThunk", Year = 2024, Genres = new[] { "indie", "strategy" },
            Ru = "Покерный роглайт-пасьянс: джокеры, множители и очень затягивающие забеги.",
            En = "A poker roguelite: jokers, multipliers and runs that are hard to stop." },
        new() { Title = "Dave the Diver", Developer = "Mintrocket", Year = 2023, Genres = new[] { "indie", "adventure" },
            Ru = "Днём — подводная охота и исследование рифа, вечером — управление суши-рестораном.",
            En = "By day, diving and spearfishing; by night, running a sushi restaurant." },
        new() { Title = "Factorio", Developer = "Wube Software", Year = 2020, Genres = new[] { "strategy", "sim" },
            Ru = "Фабрика на чужой планете: конвейеры, поезда и автоматизация до полного порядка в голове.",
            En = "A factory on an alien planet: belts, trains and automation taken as far as you like." },
        new() { Title = "Factorio: Space Age", Developer = "Wube Software", Year = 2024, Genres = new[] { "strategy", "sim" },
            Ru = "Большое дополнение: несколько планет, космические платформы и логистика между мирами.",
            En = "A large expansion: several planets, space platforms and logistics between worlds." },
        new() { Title = "RimWorld", Developer = "Ludeon Studios", Year = 2018, Genres = new[] { "strategy", "sim" },
            Ru = "Симулятор колонии с ИИ-рассказчиком: отношения, болезни, рейды и неожиданные истории.",
            En = "A colony sim with an AI storyteller: relationships, illness, raids and unexpected stories." },
        new() { Title = "Crusader Kings III", Developer = "Paradox Development Studio", Year = 2020, Genres = new[] { "strategy" },
            Ru = "Династическая стратегия: интриги, браки и наследование вместо прямого захвата карты.",
            En = "A dynasty strategy game where intrigue, marriages and inheritance matter more than conquest." },
        new() { Title = "Stellaris", Developer = "Paradox Development Studio", Year = 2016, Genres = new[] { "strategy" },
            Ru = "Космическая 4X-стратегия с исследованием галактики и очень гибкой настройкой империи.",
            En = "A space 4X with galaxy exploration and a very flexible empire builder." },
        new() { Title = "Total War: Warhammer III", Developer = "Creative Assembly", Year = 2022, Genres = new[] { "strategy" },
            Ru = "Пошаговые сражения и масштабные битвы в фэнтезийном мире, плюс общая кампания по всем частям.",
            En = "Turn-based strategy and huge real-time battles, plus a campaign spanning all three games." },
        new() { Title = "Age of Empires IV", Developer = "Relic Entertainment", Year = 2021, Genres = new[] { "strategy" },
            Ru = "Классическая RTS с восемью цивилизациями, кампанией-документалкой и понятным входом.",
            En = "A classic RTS with eight civilisations, a documentary-style campaign and a gentle learning curve." },
        new() { Title = "Against the Storm", Developer = "Eremite Games", Year = 2023, Genres = new[] { "strategy", "sim" },
            Ru = "Городская стратегия в цикле забегов: короткие поселения под вечным дождём и метапрогресс.",
            En = "A city builder built on runs: short settlements under endless rain, plus meta progression." },
        new() { Title = "Frostpunk 2", Developer = "11 bit studios", Year = 2024, Genres = new[] { "strategy", "survival" },
            Ru = "Город под снегом вырос в мегаполис: фракции, законы и борьба за тепло и топливо.",
            En = "The frozen city has grown into a metropolis: factions, laws and the fight for heat and fuel." },
        new() { Title = "This War of Mine", Developer = "11 bit studios", Year = 2014, Genres = new[] { "strategy", "survival" },
            Ru = "Выживание гражданских в осаждённом городе — про моральные компромиссы, а не про победу.",
            En = "Civilians surviving a siege — a game about moral trade-offs rather than victory." },
        new() { Title = "Cities: Skylines II", Developer = "Colossal Order", Year = 2023, Genres = new[] { "sim", "strategy" },
            Ru = "Градостроительный симулятор с честной экономикой, трафиком и симуляцией жителей.",
            En = "A city builder with a deep economy, traffic simulation and simulated residents." },
        new() { Title = "Stardew Valley", Developer = "ConcernedApe", Year = 2016, Genres = new[] { "sim", "indie" },
            Ru = "Ферма, рудники, дружба с соседями и медитативный сезонный цикл — одним разработчиком.",
            En = "Farming, mines, neighbours and a meditative seasonal loop, made by one developer." },
        new() { Title = "Subnautica", Developer = "Unknown Worlds", Year = 2018, Genres = new[] { "survival", "adventure" },
            Ru = "Выживание на океанской планете: строительство баз, крафт и очень много неизведанной глубины.",
            En = "Survival on an ocean planet: base building, crafting and a lot of unexplored depth." },
        new() { Title = "Valheim", Developer = "Iron Gate Studio", Year = 2021, Genres = new[] { "survival", "adventure" },
            Ru = "Выживание в мире викингов: постройки, боссы по биомам и кооператив на десятерых.",
            En = "Viking survival: building, biome bosses and co-op for up to ten players." },
        new() { Title = "The Long Dark", Developer = "Hinterland Studio", Year = 2017, Genres = new[] { "survival", "sim" },
            Ru = "Холод как главный противник: погода, тепло и осторожное планирование каждого перехода.",
            En = "Cold as the main enemy: weather, warmth and careful planning of every trip." },
        new() { Title = "No Man's Sky", Developer = "Hello Games", Year = 2016, Genres = new[] { "adventure", "sim" },
            Ru = "Бесшовные планеты, корабли и базы; за годы бесплатных обновлений вырос в отдельный жанр.",
            En = "Seamless planets, ships and bases — grown into its own genre after years of free updates." },
        new() { Title = "Death Stranding", Developer = "Kojima Productions", Year = 2019, Genres = new[] { "adventure" },
            Ru = "Доставка грузов через опустевшие ландшафты: маршруты, снаряжение и общая инфраструктура игроков.",
            En = "Courier runs across empty landscapes: routes, gear and shared player infrastructure." },
        new() { Title = "Red Dead Redemption 2", Developer = "Rockstar Games", Year = 2018, Genres = new[] { "action", "adventure" },
            Ru = "Вестерн с очень подробным открытым миром, лагерем банды и медленным, внимательным темпом.",
            En = "A western with a deeply detailed open world, a gang camp and a slow, deliberate pace." },
        new() { Title = "God of War", Developer = "Santa Monica Studio", Year = 2018, Genres = new[] { "action", "adventure" },
            Ru = "Однорукий топор, северная мифология и путешествие отца с сыном без склеек камеры.",
            En = "A returning axe, Norse mythology and a father-son journey told in a single shot." },
        new() { Title = "Ghost of Tsushima", Developer = "Sucker Punch Productions", Year = 2020, Genres = new[] { "action", "adventure" },
            Ru = "Самурай на Цусиме: стойки, дуэли и открытый мир, который ведёт ветром, а не стрелкой.",
            En = "A samurai on Tsushima: stances, duels and an open world navigated by wind instead of arrows." },
        new() { Title = "Horizon Zero Dawn", Developer = "Guerrilla Games", Year = 2017, Genres = new[] { "action", "adventure" },
            Ru = "Охота на машины-животных луком и ловушками, плюс история о том, что случилось с миром.",
            En = "Hunting machine animals with bow and traps, wrapped around the story of what happened to the world." },
        new() { Title = "Control", Developer = "Remedy Entertainment", Year = 2019, Genres = new[] { "action" },
            Ru = "Телекинез, «Федеральное бюро контроля» и здание, которое меняет планировку само.",
            En = "Telekinesis, a paranormal bureau and a building that keeps rearranging itself." },
        new() { Title = "DOOM Eternal", Developer = "id Software", Year = 2020, Genres = new[] { "action" },
            Ru = "Очень быстрый шутер: арсенал на все случаи, добивания и постоянное движение.",
            En = "A very fast shooter: a full arsenal, glory kills and constant movement." },
        new() { Title = "Metro Exodus", Developer = "4A Games", Year = 2019, Genres = new[] { "action", "horror" },
            Ru = "Путешествие по постсоветской России в «Паровозе»: полуоткрытые локации и дефицит патронов.",
            En = "A journey across post-apocalyptic Russia by train: semi-open levels and scarce ammo." },
        new() { Title = "S.T.A.L.K.E.R. 2: Heart of Chornobyl", Developer = "GSC Game World", Year = 2024, Genres = new[] { "horror", "survival", "action" },
            Ru = "Зона, аномалии и мутанты: шутер от первого лица с выживанием и очень плотной атмосферой.",
            En = "The Zone, anomalies and mutants: a first-person shooter with survival and thick atmosphere." },
        new() { Title = "Lethal Company", Developer = "Zeekerss", Year = 2023, Genres = new[] { "horror", "indie" },
            Ru = "Кооперативный сбор металлолома на заброшенных базах, где монстры смешнее, чем страшнее.",
            En = "Co-op scrap runs in abandoned facilities where the monsters are as funny as they are scary." },
        new() { Title = "Phasmophobia", Developer = "Kinetic Games", Year = 2020, Genres = new[] { "horror", "indie" },
            Ru = "Команда охотников за призраками с приборами и голосовым чатом, который слышат призраки.",
            En = "A ghost-hunting crew with instruments and voice chat that the ghost can actually hear." },
        new() { Title = "Resident Evil 4", Developer = "Capcom", Year = 2023, Genres = new[] { "horror", "action" },
            Ru = "Ремейк классики: деревня, торговец, чемодан с перестановкой предметов и точная стрельба.",
            En = "A remake of the classic: the village, the merchant, attache-case inventory and precise shooting." },
        new() { Title = "Dead Space", Developer = "Motive Studio", Year = 2023, Genres = new[] { "horror", "action" },
            Ru = "Ремейк космического хоррора: отрезание конечностей, «Ишимура» и тишина между скрипами.",
            En = "A sci-fi horror remake: limb dismemberment, the Ishimura and silence between creaks." },
        new() { Title = "Alien: Isolation", Developer = "Creative Assembly", Year = 2014, Genres = new[] { "horror" },
            Ru = "Один чужой, который учится на ваших укрытиях; лучший комплимент — играть приходится медленно.",
            En = "A single xenomorph that learns from your hiding spots; the best compliment is how slowly you play." },
        new() { Title = "Deep Rock Galactic", Developer = "Ghost Ship Games", Year = 2020, Genres = new[] { "action", "indie" },
            Ru = "Кооператив про гномов-шахтёров: полностью разрушаемые пещеры и четыре разных класса.",
            En = "Co-op dwarf mining: fully destructible caves and four distinct classes." },
        new() { Title = "It Takes Two", Developer = "Hazelight Studios", Year = 2021, Genres = new[] { "adventure" },
            Ru = "Кооператив строго на двоих, где механики меняются каждые полчаса, а проходить игру в одиночку нельзя.",
            En = "Two-player-only co-op where the mechanics change every half hour." },
        new() { Title = "Split Fiction", Developer = "Hazelight Studios", Year = 2025, Genres = new[] { "adventure" },
            Ru = "Ещё один кооператив на двоих: писательницы внутри собственных выдуманных миров.",
            En = "Another two-player co-op: two writers travelling through each other's invented worlds." },
        new() { Title = "Portal 2", Developer = "Valve", Year = 2011, Genres = new[] { "adventure", "indie" },
            Ru = "Головоломки с порталами, отличные шутки и отдельная кооперативная кампания.",
            En = "Portal puzzles, great jokes and a separate co-op campaign." },
        new() { Title = "Half-Life: Alyx", Developer = "Valve", Year = 2020, Genres = new[] { "action", "adventure" },
            Ru = "Полноценная VR-игра серии: перезарядка руками, хедкрабы и физика на каждом шагу.",
            En = "A full VR entry in the series: manual reloads, headcrabs and physics everywhere." },
        new() { Title = "Indiana Jones and the Great Circle", Developer = "MachineGames", Year = 2024, Genres = new[] { "adventure", "action" },
            Ru = "Приключение от первого лица: хлыст, раскопки, головоломки и много стелса.",
            En = "A first-person adventure: whip, digs, puzzles and a lot of stealth." },
        new() { Title = "Mount & Blade II: Bannerlord", Developer = "TaleWorlds Entertainment", Year = 2022, Genres = new[] { "rpg", "strategy" },
            Ru = "Средневековая песочница: отряд, торговля, осады и личная армия, которую вы собираете сами.",
            En = "A medieval sandbox: a warband, trade, sieges and an army you assemble yourself." },
    };

    /// <summary>Отфильтровать и отсортировать каталог.</summary>
    public static List<RecGame> Filter(string? query, string genre, string period, string sort)
    {
        IEnumerable<RecGame> q = Games;

        if (!string.IsNullOrWhiteSpace(query))
        {
            var s = query.Trim();
            q = q.Where(g =>
                g.Title.Contains(s, StringComparison.CurrentCultureIgnoreCase) ||
                g.Developer.Contains(s, StringComparison.CurrentCultureIgnoreCase) ||
                g.Genres.Any(x => Loc.T("genre." + x).Contains(s, StringComparison.CurrentCultureIgnoreCase)));
        }

        if (!string.IsNullOrEmpty(genre) && genre != "all")
            q = q.Where(g => g.Has(genre));

        q = period switch
        {
            "new" => q.Where(g => g.Year >= NewSinceYear),
            "2020" => q.Where(g => g.Year is >= 2020 and < NewSinceYear),
            "old" => q.Where(g => g.Year < 2020),
            _ => q,
        };

        q = sort switch
        {
            "old" => q.OrderBy(g => g.Year).ThenBy(g => g.Title, StringComparer.CurrentCultureIgnoreCase),
            "az" => q.OrderBy(g => g.Title, StringComparer.CurrentCultureIgnoreCase),
            "dev" => q.OrderBy(g => g.Developer, StringComparer.CurrentCultureIgnoreCase).ThenBy(g => g.Year),
            _ => q.OrderByDescending(g => g.Year).ThenBy(g => g.Title, StringComparer.CurrentCultureIgnoreCase),
        };

        return q.ToList();
    }
}
