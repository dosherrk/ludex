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

public static class StoreSearch
{
	private sealed class CacheEntry
	{
		public DateTime AtUtc { get; set; }

		public List<StoreGame> Games { get; set; } = new List<StoreGame>();

	}

	private const string SearchUrl = "https://store.steampowered.com/search/results/?term={0}&start=0&count={1}&infinite=1&json=1&cc=us&l={2}&category1=998";

	private const string FallbackUrl = "https://store.steampowered.com/api/storesearch/?term={0}&l={1}&cc=us";

	private static readonly HttpClient Http = CreateClient();

	public const int MaxResults = 25;

	private static Dictionary<string, CacheEntry>? _cache;

	private static readonly object CacheLock = new object();

	private static readonly TimeSpan Ttl = TimeSpan.FromDays(3.0);

	private static readonly Regex RowPattern = new Regex("data-ds-appid=\"(\\d+)\".*?</a>", RegexOptions.Compiled | RegexOptions.Singleline);

	private static readonly Regex TitlePattern = new Regex("class=\"title\">(.*?)</span>", RegexOptions.Compiled | RegexOptions.Singleline);

	private static readonly Regex ImagePattern = new Regex("<img[^>]+src=\"(https://[^\"]+)\"", RegexOptions.Compiled | RegexOptions.Singleline);

	private static readonly Regex ReleasedPattern = new Regex("search_released[^>]*>\\s*(.*?)\\s*<", RegexOptions.Compiled | RegexOptions.Singleline);

	private static readonly string[] AddonWords = new string[14]
	{
		"soundtrack", "саундтрек", "season pass", "сезонный пропуск", "supporter pack", "artbook", "артбук", "cosmetic", "expansion pass", "upgrade pack",
		"starter pack", "deluxe upgrade", "skin pack", "digital artbook"
	};

	private static string CacheFile => System.IO.Path.Combine(StoreFeed.CacheDir, "search.json");

	public static bool Busy { get; private set; }

	private static HttpClient CreateClient()
	{
		HttpClient httpClient = new HttpClient();
		httpClient.Timeout = TimeSpan.FromSeconds(12.0);
		httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Ludex/" + Updater.CurrentVersionText);
		httpClient.DefaultRequestHeaders.AcceptLanguage.ParseAdd("ru,en;q=0.8");
		return httpClient;
	}

	public static async Task<List<StoreGame>> SearchAsync(string term, CancellationToken ct = default(CancellationToken))
	{
		string query = Normalize(term);
		if (query.Length < 2)
		{
			return new List<StoreGame>();
		}
		LoadCache();
		CacheEntry cached;
		lock (CacheLock)
		{
			_cache.TryGetValue(query, out cached);
		}
		if (cached != null && DateTime.UtcNow - cached.AtUtc < Ttl)
		{
			return cached.Games;
		}
		Busy = true;
		try
		{
			List<StoreGame> list = await FetchAsync(term.Trim(), ct);
			if (list.Count == 0 && cached != null)
			{
				return cached.Games;
			}
			Save(query, list);
			return list;
		}
		catch (Exception ex)
		{
			AppPaths.Log("StoreSearch: " + ex.Message);
			return cached?.Games ?? new List<StoreGame>();
		}
		finally
		{
			Busy = false;
		}
	}

	private static async Task<List<StoreGame>> FetchAsync(string term, CancellationToken ct)
	{
		string lang = (Loc.IsRu ? "russian" : "english");
		List<StoreGame> list = await SearchPageAsync(term, lang, ct);
		if (list.Count > 0)
		{
			return list;
		}
		return await FallbackAsync(term, lang, ct);
	}

	private static async Task<List<StoreGame>> SearchPageAsync(string term, string lang, CancellationToken ct)
	{
		string requestUri = $"https://store.steampowered.com/search/results/?term={Uri.EscapeDataString(term)}&start=0&count={50}&infinite=1&json=1&cc=us&l={lang}&category1=998";
		using HttpResponseMessage response = await Http.GetAsync(requestUri, ct);
		if (!response.IsSuccessStatusCode)
		{
			AppPaths.Log($"StoreSearch: поиск «{term}» — ответ {response.StatusCode}");
			return new List<StoreGame>();
		}
		string text = await response.Content.ReadAsStringAsync(ct);
		AppPaths.Log($"StoreSearch: поиск «{term}» — {text.Length} символов");
		string text2;
		try
		{
			using JsonDocument jsonDocument = JsonDocument.Parse(text);
			text2 = (jsonDocument.RootElement.TryGetProperty("results_html", out var value) ? (value.GetString() ?? "") : "");
		}
		catch (JsonException)
		{
			AppPaths.Log("StoreSearch: ответ не JSON — " + text.Substring(0, Math.Min(200, text.Length)));
			return new List<StoreGame>();
		}
		if (text2.Length == 0)
		{
			return new List<StoreGame>();
		}
		List<StoreGame> list = new List<StoreGame>();
		HashSet<int> hashSet = new HashSet<int>();
		foreach (Match item in RowPattern.Matches(text2))
		{
			if (!int.TryParse(item.Groups[1].Value, out var result) || !hashSet.Add(result))
			{
				continue;
			}
			string value2 = item.Value;
			if (value2.Contains("data-ds-bundleid", StringComparison.Ordinal))
			{
				continue;
			}
			Match match2 = TitlePattern.Match(value2);
			if (!match2.Success)
			{
				continue;
			}
			string text3 = WebUtility.HtmlDecode(Regex.Replace(match2.Groups[1].Value, "<.*?>", "")).Trim();
			if (text3.Length == 0)
			{
				continue;
			}
			Match match3 = ImagePattern.Match(value2);
			Match match4 = ReleasedPattern.Match(value2);
			string text4 = "";
			if (match4.Success)
			{
				Match match5 = Regex.Match(match4.Groups[1].Value, "(19|20)\\d{2}");
				if (match5.Success)
				{
					text4 = match5.Value;
				}
			}
			list.Add(new StoreGame
			{
				AppId = result,
				Name = text3,
				CoverUrl = (match3.Success ? match3.Groups[1].Value : ""),
				HeaderUrl = $"https://cdn.cloudflare.steamstatic.com/steam/apps/{result}/header.jpg",
				ReleaseDate = ((text4.Length > 0) ? text4 : null)
			});
		}
		return RemoveAddons(list).Take(25).ToList();
	}

	private static List<StoreGame> RemoveAddons(List<StoreGame> games)
	{
		List<string> list = games.Select((StoreGame g) => g.Name).ToList();
		List<StoreGame> list2 = new List<StoreGame>();
		foreach (StoreGame game in games)
		{
			string name = game.Name;
			string lower = name.ToLowerInvariant();
			if (AddonWords.Any((string w) => lower.Contains(w, StringComparison.Ordinal)))
			{
				continue;
			}
			bool flag = false;
			foreach (string item in list)
			{
				if ((object)item == name || item.Length >= name.Length || !name.StartsWith(item, StringComparison.CurrentCultureIgnoreCase))
				{
					continue;
				}
				string text = name;
				int length = item.Length;
				string text2 = text.Substring(length, text.Length - length).TrimStart();
				if (text2.Length != 0)
				{
					bool flag2;
					switch (text2[0])
					{
					case '-':
					case ':':
					case '–':
					case '—':
						flag2 = true;
						break;
					default:
						flag2 = false;
						break;
					}
					if (flag2 && !char.IsDigit((text2.Length > 1) ? text2[1] : ' '))
					{
						flag = true;
						break;
					}
				}
			}
			if (!flag)
			{
				list2.Add(game);
			}
		}
		return list2;
	}

	private static async Task<List<StoreGame>> FallbackAsync(string term, string lang, CancellationToken ct)
	{
		_ = 3;
		try
		{
			string requestUri = $"https://store.steampowered.com/api/storesearch/?term={Uri.EscapeDataString(term)}&l={lang}&cc=us";
			using HttpResponseMessage response = await Http.GetAsync(requestUri, ct);
			if (!response.IsSuccessStatusCode)
			{
				return new List<StoreGame>();
			}
			Stream stream = await response.Content.ReadAsStreamAsync(ct);
			List<StoreGame> result;
			try
			{
				using JsonDocument jsonDocument = await JsonDocument.ParseAsync(stream, default(JsonDocumentOptions), ct);
				if (!jsonDocument.RootElement.TryGetProperty("items", out var value) || value.ValueKind != JsonValueKind.Array)
				{
					result = new List<StoreGame>();
				}
				else
				{
					List<StoreGame> list = new List<StoreGame>();
					foreach (JsonElement item in value.EnumerateArray())
					{
						if (item.ValueKind != JsonValueKind.Object || (item.TryGetProperty("type", out var value2) && !string.Equals(value2.GetString(), "app", StringComparison.OrdinalIgnoreCase)) || !item.TryGetProperty("id", out var value3) || !value3.TryGetInt32(out var value4))
						{
							continue;
						}
						JsonElement value5;
						string text = (item.TryGetProperty("name", out value5) ? (value5.GetString() ?? "").Trim() : "");
						if (text.Length != 0)
						{
							JsonElement value6;
							string coverUrl = ((item.TryGetProperty("tiny_image", out value6) && value6.ValueKind == JsonValueKind.String) ? (value6.GetString() ?? "") : "");
							list.Add(new StoreGame
							{
								AppId = value4,
								Name = text,
								CoverUrl = coverUrl,
								HeaderUrl = $"https://cdn.cloudflare.steamstatic.com/steam/apps/{value4}/header.jpg"
							});
							if (list.Count >= 25)
							{
								break;
							}
						}
					}
					result = list;
				}
			}
			finally
			{
				if (stream != null)
				{
					await stream.DisposeAsync();
				}
			}
			return result;
		}
		catch (Exception ex)
		{
			AppPaths.Log("StoreSearch fallback: " + ex.Message);
			return new List<StoreGame>();
		}
	}

	public static List<StoreGame> Cached(string term)
	{
		string text = Normalize(term);
		if (text.Length < 2)
		{
			return new List<StoreGame>();
		}
		LoadCache();
		lock (CacheLock)
		{
			CacheEntry value;
			return _cache.TryGetValue(text, out value) ? value.Games : new List<StoreGame>();
		}
	}

	private static string Normalize(string term)
	{
		return Regex.Replace((term ?? "").Trim().ToLowerInvariant(), "\\s+", " ");
	}

	private static void LoadCache()
	{
		if (_cache != null)
		{
			return;
		}
		lock (CacheLock)
		{
			if (_cache != null)
			{
				return;
			}
			try
			{
				if (!File.Exists(CacheFile))
				{
					_cache = new Dictionary<string, CacheEntry>(StringComparer.OrdinalIgnoreCase);
				}
				else
				{
					_cache = JsonSerializer.Deserialize<Dictionary<string, CacheEntry>>(File.ReadAllText(CacheFile)) ?? new Dictionary<string, CacheEntry>(StringComparer.OrdinalIgnoreCase);
				}
			}
			catch
			{
				_cache = new Dictionary<string, CacheEntry>(StringComparer.OrdinalIgnoreCase);
			}
		}
	}

	private static void Save(string query, List<StoreGame> games)
	{
		lock (CacheLock)
		{
			if (_cache == null)
			{
				_cache = new Dictionary<string, CacheEntry>(StringComparer.OrdinalIgnoreCase);
			}
			_cache[query] = new CacheEntry
			{
				AtUtc = DateTime.UtcNow,
				Games = games
			};
			if (_cache.Count > 300)
			{
				foreach (KeyValuePair<string, CacheEntry> item in _cache.OrderBy((KeyValuePair<string, CacheEntry> kv) => kv.Value.AtUtc).Take(_cache.Count - 300).ToList())
				{
					_cache.Remove(item.Key);
				}
			}
			try
			{
				string contents = JsonSerializer.Serialize(_cache);
				string text = CacheFile + ".tmp";
				File.WriteAllText(text, contents);
				File.Move(text, CacheFile, overwrite: true);
			}
			catch (Exception ex)
			{
				AppPaths.Log("StoreSearch cache: " + ex.Message);
			}
		}
	}
}
