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

namespace Ludex.Services;

public sealed class StoreGame
{
	public int AppId { get; init; }

	public string Name { get; init; } = "";


	public string Developer { get; set; } = "";


	public string Publisher { get; set; } = "";


	public string CoverUrl { get; init; } = "";


	public string HeaderUrl { get; init; } = "";


	public int DiscountPercent { get; init; }

	public long FinalPrice { get; init; }

	public long OriginalPrice { get; init; }

	public string Currency { get; init; } = "";


	public bool Free { get; init; }

	public bool Windows { get; init; }

	public bool Mac { get; init; }

	public bool Linux { get; init; }

	public long OwnersLow { get; init; }

	public int Ccu { get; init; }

	public HashSet<string> Sections { get; } = new HashSet<string>();


	public string? ReleaseDate { get; set; }

	public string[] Genres { get; set; } = Array.Empty<string>();


	public string[] Themes { get; set; } = Array.Empty<string>();


	public string? Description { get; set; }

	public bool ComingSoon { get; set; }

	public string? CoverPath { get; set; }

	public string? HeaderPath { get; set; }

	public string SteamUrl => $"https://store.steampowered.com/app/{AppId}/";
}

public sealed class StoreFeedResult
{
	public bool Ok { get; set; }

	public string? Error { get; init; }

	public List<StoreGame> Games { get; } = new List<StoreGame>();


	public DateTime FetchedUtc { get; set; }

	public bool FromCache { get; set; }
}

public static class StoreFeed
{
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

	private const string FeedUrl = "https://store.steampowered.com/api/featuredcategories";

	private const string DetailsUrl = "https://store.steampowered.com/api/appdetails";

	private static readonly TimeSpan FeedTtl = TimeSpan.FromMinutes(30.0);

	private static readonly TimeSpan MetaTtl = TimeSpan.FromDays(7.0);

	public static readonly (string Key, string Json)[] Sections = new(string, string)[4]
	{
		("top", "top_sellers"),
		("discount", "specials"),
		("new", "new_releases"),
		("soon", "coming_soon")
	};

	public static string CacheDir
	{
		get
		{
			string text = System.IO.Path.Combine(AppPaths.Root, "store");
			Directory.CreateDirectory(text);
			return text;
		}
	}

	public static string CoversDir
	{
		get
		{
			string text = System.IO.Path.Combine(CacheDir, "covers");
			Directory.CreateDirectory(text);
			return text;
		}
	}

	private static string FeedFile => System.IO.Path.Combine(CacheDir, "feed-" + LangCode + ".json");

	private static string MetaFile => System.IO.Path.Combine(CacheDir, "meta-" + LangCode + ".json");

	private static string LangCode
	{
		get
		{
			if (!Loc.IsRu)
			{
				return "en";
			}
			return "ru";
		}
	}

	private static string SteamLang
	{
		get
		{
			if (!Loc.IsRu)
			{
				return "english";
			}
			return "russian";
		}
	}

	private static string Country
	{
		get
		{
			if (!Loc.IsRu)
			{
				return "us";
			}
			return "ru";
		}
	}

	private static HttpClient NewClient()
	{
		HttpClient httpClient = new HttpClient();
		httpClient.Timeout = TimeSpan.FromSeconds(25.0);
		httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Ludex/" + Updater.CurrentVersionText);
		return httpClient;
	}

	public static async Task<StoreFeedResult> LoadAsync(bool force, CancellationToken ct)
	{
		(StoreFeedResult Result, DateTime FetchedUtc)? cached = ReadFeedCache();
		if (!force && cached.HasValue && DateTime.UtcNow - cached.Value.FetchedUtc < FeedTtl)
		{
			return cached.Value.Result;
		}
		try
		{
			using HttpClient http = NewClient();
			string requestUri = $"{"https://store.steampowered.com/api/featuredcategories"}?cc={Country}&l={SteamLang}";
			string json = await http.GetStringAsync(requestUri, ct);
			DateTime fetchedUtc;
			List<StoreGame> list = ParseFeed(json, out fetchedUtc);
			StoreFeedResult storeFeedResult = new StoreFeedResult();
			storeFeedResult.Ok = true;
			storeFeedResult.FetchedUtc = fetchedUtc;
			storeFeedResult.Games.AddRange(list);
			ApplyCachedMeta(storeFeedResult.Games);
			ApplyCachedCovers(storeFeedResult.Games);
			WriteFeedCache(json);
			AppPaths.Log($"StoreFeed: загружено {list.Count} игр");
			return storeFeedResult;
		}
		catch (Exception ex)
		{
			AppPaths.Log("StoreFeed failed: " + ex.Message);
			if (cached.HasValue)
			{
				StoreFeedResult storeFeedResult2 = new StoreFeedResult();
				storeFeedResult2.Ok = true;
				storeFeedResult2.FetchedUtc = cached.Value.FetchedUtc;
				storeFeedResult2.FromCache = true;
				storeFeedResult2.Games.AddRange(cached.Value.Result.Games);
				return storeFeedResult2;
			}
			return new StoreFeedResult
			{
				Ok = false,
				Error = ex.Message
			};
		}
	}

	private static List<StoreGame> ParseFeed(string json, out DateTime fetchedUtc)
	{
		fetchedUtc = DateTime.UtcNow;
		Dictionary<int, StoreGame> map = new Dictionary<int, StoreGame>();
		List<int> list = new List<int>();
		using (JsonDocument jsonDocument = JsonDocument.Parse(json))
		{
			(string, string)[] sections = Sections;
			for (int i = 0; i < sections.Length; i++)
			{
				string item = sections[i].Item2;
				if (!jsonDocument.RootElement.TryGetProperty(item, out var value) || !value.TryGetProperty("items", out var value2) || value2.ValueKind != JsonValueKind.Array)
				{
					continue;
				}
				foreach (JsonElement item2 in value2.EnumerateArray())
				{
					if (!item2.TryGetProperty("id", out var value3) || !value3.TryGetInt32(out var value4) || value4 <= 0)
					{
						continue;
					}
					if (!map.TryGetValue(value4, out StoreGame value5))
					{
						JsonElement value6;
						string text = (item2.TryGetProperty("name", out value6) ? (value6.GetString() ?? "") : "");
						if (string.IsNullOrWhiteSpace(text))
						{
							continue;
						}
						long num = Long(item2, "final_price", 0L);
						long num2 = Long(item2, "original_price", num);
						value5 = new StoreGame
						{
							AppId = value4,
							Name = text,
							CoverUrl = Str(item2, "large_capsule_image"),
							HeaderUrl = Str(item2, "header_image"),
							DiscountPercent = (int)Long(item2, "discount_percent", 0L),
							FinalPrice = num,
							OriginalPrice = num2,
							Currency = Str(item2, "currency"),
							Free = (num == 0L && num2 == 0L && item != "coming_soon"),
							Windows = Bool(item2, "windows_available"),
							Mac = Bool(item2, "mac_available"),
							Linux = Bool(item2, "linux_available")
						};
						map[value4] = value5;
						list.Add(value4);
					}
					HashSet<string> sections2 = value5.Sections;
					sections2.Add(item switch
					{
						"top_sellers" => "top", 
						"specials" => "discount", 
						"new_releases" => "new", 
						_ => "soon", 
					});
				}
			}
			foreach (int item3 in list)
			{
				StoreGame storeGame = map[item3];
				if (storeGame.Sections.Contains("top") || storeGame.Sections.Contains("discount"))
				{
					storeGame.Sections.Add("today");
				}
			}
			return list.Select((int id) => map[id]).ToList();
		}
		static bool Bool(JsonElement el, string name)
		{
			if (el.TryGetProperty(name, out var value7))
			{
				return value7.ValueKind == JsonValueKind.True;
			}
			return false;
		}
		static long Long(JsonElement el, string name, long fallback)
		{
			if (!el.TryGetProperty(name, out var value9) || value9.ValueKind != JsonValueKind.Number || !value9.TryGetInt64(out var value10))
			{
				return fallback;
			}
			return value10;
		}
		static string Str(JsonElement el, string name)
		{
			if (!el.TryGetProperty(name, out var value8) || value8.ValueKind != JsonValueKind.String)
			{
				return "";
			}
			return value8.GetString() ?? "";
		}
	}

	public static async Task EnrichAsync(IReadOnlyList<StoreGame> games, Action? onUpdated, CancellationToken ct)
	{
		Action onUpdated2 = onUpdated;
		Dictionary<int, MetaEntry> meta2 = ReadMetaCache();
		List<int> list = (from g in games
			where !meta2.ContainsKey(g.AppId) || DateTime.UtcNow - meta2[g.AppId].UpdatedUtc > MetaTtl
			select g.AppId).ToList();
		foreach (StoreGame game in games)
		{
			ApplyMeta(game, meta2);
		}
		if (list.Count == 0)
		{
			return;
		}
		HttpClient http = NewClient();
		try
		{
			SemaphoreSlim gate = new SemaphoreSlim(6);
			try
			{
				int done = 0;
				await Task.WhenAll(((IEnumerable<int>)list).Select((Func<int, Task>)async delegate(int id)
				{
					await gate.WaitAsync(ct);
					try
					{
						string requestUri = $"{"https://store.steampowered.com/api/appdetails"}?appids={id}&l={SteamLang}&cc={Country}";
						using JsonDocument jsonDocument = JsonDocument.Parse(await http.GetStringAsync(requestUri, ct));
						if (!jsonDocument.RootElement.TryGetProperty(id.ToString(CultureInfo.InvariantCulture), out var value2) || !value2.TryGetProperty("success", out var value3) || value3.ValueKind != JsonValueKind.True || !value2.TryGetProperty("data", out var value4))
						{
							return;
						}
						MetaEntry metaEntry = new MetaEntry
						{
							UpdatedUtc = DateTime.UtcNow
						};
						if (value4.TryGetProperty("genres", out var value5) && value5.ValueKind == JsonValueKind.Array)
						{
							metaEntry.Genres = (from s in value5.EnumerateArray().Select(delegate(JsonElement g)
								{
									JsonElement jsonElement = g;
									object obj;
									if (jsonElement.TryGetProperty("description", out var value11))
									{
										obj = value11.GetString();
										if (obj == null)
										{
											return "";
										}
									}
									else
									{
										obj = "";
									}
									return (string)obj;
								})
								where s.Length > 0
								select s).Distinct().ToArray();
						}
						if (value4.TryGetProperty("release_date", out var value6))
						{
							metaEntry.ReleaseDate = (value6.TryGetProperty("date", out var value7) ? value7.GetString() : null);
							metaEntry.ComingSoon = value6.TryGetProperty("coming_soon", out var value8) && value8.ValueKind == JsonValueKind.True;
						}
						if (value4.TryGetProperty("short_description", out var value9))
						{
							metaEntry.Description = value9.GetString();
						}
						if (value4.TryGetProperty("header_image", out var value10) && value10.ValueKind == JsonValueKind.String)
						{
							metaEntry.HeaderImage = value10.GetString();
						}
						metaEntry.Developer = FirstOf(value4, "developers");
						metaEntry.Publisher = FirstOf(value4, "publishers");
						lock (meta2)
						{
							meta2[id] = metaEntry;
						}
					}
					catch (Exception ex)
					{
						AppPaths.Log($"StoreFeed details {id}: {ex.Message}");
					}
					finally
					{
						gate.Release();
						if (Interlocked.Increment(ref done) % 10 == 0)
						{
							onUpdated2?.Invoke();
						}
					}
				}));
				foreach (StoreGame game2 in games)
				{
					ApplyMeta(game2, meta2);
				}
				WriteMetaCache(meta2);
				onUpdated2?.Invoke();
			}
			finally
			{
				if (gate != null)
				{
					((IDisposable)gate).Dispose();
				}
			}
		}
		finally
		{
			if (http != null)
			{
				((IDisposable)http).Dispose();
			}
		}
		static void ApplyMeta(StoreGame g, Dictionary<int, MetaEntry> meta)
		{
			if (meta.TryGetValue(g.AppId, out MetaEntry value))
			{
				if (value.Genres.Length != 0)
				{
					g.Genres = value.Genres;
				}
				StoreGame storeGame = g;
				if (storeGame.ReleaseDate == null)
				{
					string text = (storeGame.ReleaseDate = value.ReleaseDate);
				}
				g.ComingSoon = g.ComingSoon || value.ComingSoon;
				if (!string.IsNullOrWhiteSpace(value.Description))
				{
					storeGame = g;
					if (storeGame.Description == null)
					{
						string text = (storeGame.Description = value.Description);
					}
				}
				if (string.IsNullOrEmpty(g.Developer) && !string.IsNullOrWhiteSpace(value.Developer))
				{
					g.Developer = value.Developer;
				}
				if (string.IsNullOrEmpty(g.Publisher) && !string.IsNullOrWhiteSpace(value.Publisher))
				{
					g.Publisher = value.Publisher;
				}
			}
		}
	}

	private static string FormatPrice(long minorUnits, string currency)
	{
		double num = (double)minorUnits / 100.0;
		string text = ((num % 1.0 == 0.0) ? num.ToString("N0", CultureInfo.CurrentCulture) : num.ToString("N2", CultureInfo.CurrentCulture));
		return currency switch
		{
			"RUB" => text + " ₽", 
			"USD" => "$" + text, 
			"EUR" => "€" + text, 
			"UAH" => text + " ₴", 
			"KZT" => text + " ₸", 
			"BYN" => text + " Br", 
			"" => text, 
			_ => text + " " + currency, 
		};
	}

	private static (StoreFeedResult Result, DateTime FetchedUtc)? ReadFeedCache()
	{
		try
		{
			if (!File.Exists(FeedFile))
			{
				return null;
			}
			DateTime lastWriteTimeUtc = File.GetLastWriteTimeUtc(FeedFile);
			DateTime fetchedUtc;
			List<StoreGame> list = ParseFeed(File.ReadAllText(FeedFile), out fetchedUtc);
			if (list.Count == 0)
			{
				return null;
			}
			StoreFeedResult storeFeedResult = new StoreFeedResult();
			storeFeedResult.Ok = true;
			storeFeedResult.FetchedUtc = lastWriteTimeUtc;
			storeFeedResult.FromCache = true;
			storeFeedResult.Games.AddRange(list);
			ApplyCachedMeta(storeFeedResult.Games);
			ApplyCachedCovers(storeFeedResult.Games);
			return (storeFeedResult, lastWriteTimeUtc);
		}
		catch (Exception ex)
		{
			AppPaths.Log("StoreFeed cache read: " + ex.Message);
			return null;
		}
	}

	private static void WriteFeedCache(string json)
	{
		try
		{
			File.WriteAllText(FeedFile, json);
		}
		catch (Exception ex)
		{
			AppPaths.Log("StoreFeed cache write: " + ex.Message);
		}
	}

	public static async Task<string?> HeaderImageAsync(int appId, CancellationToken ct)
	{
		Dictionary<int, MetaEntry> meta = ReadMetaCache();
		if (meta.TryGetValue(appId, out MetaEntry known) && !string.IsNullOrWhiteSpace(known.HeaderImage))
		{
			return known.HeaderImage;
		}
		try
		{
			using HttpClient http = NewClient();
			string requestUri = $"{"https://store.steampowered.com/api/appdetails"}?appids={appId}&l={SteamLang}&cc={Country}";
			using JsonDocument jsonDocument = JsonDocument.Parse(await http.GetStringAsync(requestUri, ct));
			if (!jsonDocument.RootElement.TryGetProperty(appId.ToString(CultureInfo.InvariantCulture), out var value))
			{
				return null;
			}
			if (!value.TryGetProperty("data", out var value2))
			{
				return null;
			}
			if (!value2.TryGetProperty("header_image", out var value3) || value3.ValueKind != JsonValueKind.String)
			{
				return null;
			}
			string @string = value3.GetString();
			if (string.IsNullOrWhiteSpace(@string))
			{
				return null;
			}
			if (known == null)
			{
				known = new MetaEntry
				{
					UpdatedUtc = DateTime.UtcNow
				};
			}
			known.HeaderImage = @string;
			lock (meta)
			{
				meta[appId] = known;
			}
			WriteMetaCache(meta);
			return @string;
		}
		catch (Exception ex)
		{
			AppPaths.Log($"StoreFeed header {appId}: {ex.Message}");
			return null;
		}
	}

	private static string? FirstOf(JsonElement data, string name)
	{
		if (!data.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Array)
		{
			return null;
		}
		foreach (JsonElement item in value.EnumerateArray())
		{
			if (item.ValueKind == JsonValueKind.String)
			{
				string @string = item.GetString();
				if (!string.IsNullOrWhiteSpace(@string))
				{
					return @string;
				}
			}
		}
		return null;
	}

	private static Dictionary<int, MetaEntry> ReadMetaCache()
	{
		try
		{
			if (!File.Exists(MetaFile))
			{
				return new Dictionary<int, MetaEntry>();
			}
			return JsonSerializer.Deserialize<Dictionary<int, MetaEntry>>(File.ReadAllText(MetaFile)) ?? new Dictionary<int, MetaEntry>();
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
			{
				File.WriteAllText(MetaFile, JsonSerializer.Serialize(meta));
			}
		}
		catch (Exception ex)
		{
			AppPaths.Log("StoreFeed meta write: " + ex.Message);
		}
	}

	private static void ApplyCachedMeta(List<StoreGame> games)
	{
		Dictionary<int, MetaEntry> dictionary = ReadMetaCache();
		if (dictionary.Count == 0)
		{
			return;
		}
		foreach (StoreGame game in games)
		{
			if (dictionary.TryGetValue(game.AppId, out var value))
			{
				if (value.Genres.Length != 0)
				{
					game.Genres = value.Genres;
				}
				game.ReleaseDate = value.ReleaseDate;
				game.ComingSoon = value.ComingSoon;
				game.Description = value.Description;
			}
		}
	}

	private static void ApplyCachedCovers(List<StoreGame> games)
	{
		foreach (StoreGame game in games)
		{
			string text = System.IO.Path.Combine(CoversDir, $"{game.AppId}.jpg");
			string text2 = System.IO.Path.Combine(CoversDir, $"{game.AppId}-h.jpg");
			if (File.Exists(text) && new FileInfo(text).Length > 1000)
			{
				game.CoverPath = text;
			}
			if (File.Exists(text2) && new FileInfo(text2).Length > 1000)
			{
				game.HeaderPath = text2;
			}
		}
	}
}
