using Avalonia.Animation.Easings;
using Avalonia.Animation;
using Avalonia.Collections;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Labs.Gif;
using Avalonia.Layout;
using Avalonia.Logging;
using Avalonia.Media.Imaging;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using Avalonia;
using Ludex.Services;
using Ludex.UI;
using Microsoft.Win32;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.IO;
using System.Linq;
using System.Net.Http.Headers;
using System.Net.Http;
using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System;

namespace Ludex.UI;

public static class CatalogFacets
{
	public static readonly string[] Genres = new string[10] { "Экшен", "Приключения", "Инди", "RPG", "Стратегия", "Симулятор", "Казуальные", "Гонки", "Спорт", "Ранний доступ" };

	private static readonly string[] ThemeOrder = new string[40]
	{
		"Open World", "Story Rich", "Atmospheric", "Post-apocalyptic", "Survival", "Survival Horror", "Horror", "Zombies", "Sci-fi", "Fantasy",
		"Space", "Mystery", "Detective", "Roguelike", "Metroidvania", "Platformer", "Puzzle", "Shooter", "FPS", "Action-Adventure",
		"Stealth", "Combat", "Building", "Crafting", "Farming Sim", "Sandbox", "Tactical", "Turn-Based", "Difficult", "Exploration",
		"Funny", "Music", "Great Soundtrack", "Singleplayer", "Co-op", "VR", "Free to Play", "1990s", "Cats", "Mystery Dungeon"
	};

	private static readonly HashSet<string> GenreThemeNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
	{
		"action", "adventure", "indie", "rpg", "strategy", "simulation", "casual", "racing", "sports", "early access",
		"экшен", "приключения", "инди", "стратегия", "симулятор", "казуальные", "гонки", "спорт", "ранний доступ"
	};

	private static string[]? _tags;

	public static readonly (string Value, string Key)[] Sorts = new(string, string)[5]
	{
		("catalog", "recs.sortStore"),
		("az", "recs.sortAz"),
		("za", "facet.sort.za"),
		("new", "facet.sort.new"),
		("old", "facet.sort.old")
	};

	public static string[] Tags => _tags ?? (_tags = (from t in ThemeOrder
		where !IsGenreTheme(t)
		where (!(t == "Free to Play") && !(t == "Singleplayer")) || 1 == 0
		select t).ToArray());

	public static bool IsGenreTheme(string? name)
	{
		if (!string.IsNullOrWhiteSpace(name))
		{
			return GenreThemeNames.Contains(name.Trim().ToLowerInvariant());
		}
		return false;
	}

	public static string Label(string name)
	{
		string trimmed = name.Trim();
		if (trimmed.Length == 0)
		{
			return trimmed;
		}
		string text = Genres.FirstOrDefault((string g) => string.Equals(g, trimmed, StringComparison.CurrentCultureIgnoreCase));
		string text2 = "facet.genre." + Key(text ?? trimmed);
		string text3 = Loc.T(text2);
		if (text3 != text2)
		{
			return text3;
		}
		string text4 = "facet.tag." + Key(trimmed);
		string text5 = Loc.T(text4);
		if (text5 != text4)
		{
			return text5;
		}
		return trimmed;
	}

	public static string Key(string name)
	{
		switch (name.Trim().ToLowerInvariant())
		{
		case "экшен":
		case "action":
			return "action";
		case "приключения":
		case "adventure":
			return "adventure";
		case "indie":
		case "инди":
			return "indie";
		case "rpg":
			return "rpg";
		case "стратегия":
		case "strategy":
			return "strategy";
		case "симулятор":
		case "simulation":
			return "simulation";
		case "casual":
		case "казуальные":
			return "casual";
		case "гонки":
		case "racing":
			return "racing";
		case "спорт":
		case "sports":
			return "sports";
		case "early access":
		case "ранний доступ":
			return "early";
		case "open world":
			return "openworld";
		case "story rich":
			return "story";
		case "atmospheric":
			return "atmospheric";
		case "post-apocalyptic":
			return "postapoc";
		case "survival":
			return "survival";
		case "survival horror":
			return "survivalhorror";
		case "horror":
			return "horror";
		case "zombies":
			return "zombies";
		case "sci-fi":
			return "scifi";
		case "fantasy":
			return "fantasy";
		case "space":
			return "space";
		case "mystery":
			return "mystery";
		case "detective":
			return "detective";
		case "roguelike":
			return "roguelike";
		case "metroidvania":
			return "metroidvania";
		case "platformer":
			return "platformer";
		case "puzzle":
			return "puzzle";
		case "shooter":
			return "shooter";
		case "fps":
			return "fps";
		case "action-adventure":
			return "actionadventure";
		case "stealth":
			return "stealth";
		case "combat":
			return "combat";
		case "building":
			return "building";
		case "crafting":
			return "crafting";
		case "farming sim":
			return "farming";
		case "sandbox":
			return "sandbox";
		case "tactical":
			return "tactical";
		case "turn-based":
			return "turnbased";
		case "difficult":
			return "difficult";
		case "exploration":
			return "exploration";
		case "funny":
			return "funny";
		case "music":
			return "music";
		case "great soundtrack":
			return "soundtrack";
		case "co-op":
			return "coop";
		case "vr":
			return "vr";
		case "cats":
			return "cats";
		case "mystery dungeon":
			return "mysterydungeon";
		case "1990s":
			return "y1990";
		default:
			return name.Trim().ToLowerInvariant().Replace(' ', '-');
		}
	}

	public static List<(string Title, CatalogFilter Filter)> Presets()
	{
		List<(string, CatalogFilter)> list = new List<(string, CatalogFilter)>();
		list.Add(("Недавние хиты", Make(new string[2] { "Open World", "Story Rich" }, "2020")));
		list.Add(("Кооператив на вечер", Make(new string[2] { "Co-op", "Survival" })));
		list.Add(("Ужасы и зомби", Make(new string[2] { "Horror", "Zombies" })));
		list.Add(("Расслабиться", Make(new string[3] { "Casual", "Building", "Farming Sim" })));
		list.Add(("Постапокалипсис", Make(new string[1] { "Post-apocalyptic" })));
		list.Add(("Космос и sci-fi", Make(new string[2] { "Sci-fi", "Space" })));
		list.Add(("Классика 2000-х", Make(Array.Empty<string>(), "2000")));
		list.Add(("Рогалики", Make(new string[1] { "Roguelike" })));
		return list;
		static CatalogFilter Make(string[] tags, string decade = "", string size = "")
		{
			CatalogFilter catalogFilter = new CatalogFilter
			{
				Decade = decade,
				Size = size
			};
			foreach (string item in tags)
			{
				catalogFilter.Tags.Add(item);
			}
			return catalogFilter;
		}
	}

	public static bool HasTheme(StoreGame game, IEnumerable<string> tags)
	{
		foreach (string tag in tags)
		{
			if (game.Themes.Contains<string>(tag, StringComparer.CurrentCultureIgnoreCase))
			{
				return true;
			}
		}
		return false;
	}
}

public sealed class CatalogFilter
{
	public HashSet<string> Genres { get; } = new HashSet<string>(StringComparer.CurrentCultureIgnoreCase);


	public HashSet<string> Tags { get; } = new HashSet<string>(StringComparer.CurrentCultureIgnoreCase);


	public string Decade { get; set; } = "";


	public string Size { get; set; } = "";


	public string Sort { get; set; } = "catalog";


	public bool OnlyDescribed { get; set; }

	public bool OnlyUnshelved { get; set; }

	public int Count => Genres.Count + Tags.Count + ((Decade.Length > 0) ? 1 : 0) + ((Size.Length > 0) ? 1 : 0) + (OnlyDescribed ? 1 : 0) + (OnlyUnshelved ? 1 : 0);

	public CatalogFilter Clone()
	{
		CatalogFilter catalogFilter = new CatalogFilter
		{
			Decade = Decade,
			Size = Size,
			Sort = Sort,
			OnlyDescribed = OnlyDescribed,
			OnlyUnshelved = OnlyUnshelved
		};
		foreach (string genre in Genres)
		{
			catalogFilter.Genres.Add(genre);
		}
		foreach (string tag in Tags)
		{
			catalogFilter.Tags.Add(tag);
		}
		return catalogFilter;
	}

	public void CopyFrom(CatalogFilter other)
	{
		Genres.Clear();
		Tags.Clear();
		foreach (string genre in other.Genres)
		{
			Genres.Add(genre);
		}
		foreach (string tag in other.Tags)
		{
			Tags.Add(tag);
		}
		Decade = other.Decade;
		Size = other.Size;
		Sort = other.Sort;
		OnlyDescribed = other.OnlyDescribed;
		OnlyUnshelved = other.OnlyUnshelved;
	}
}
