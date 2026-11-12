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

public static class StoreCatalog
{
	private sealed class ExcludeFile
	{
		public DateTime FetchedUtc { get; set; }

		public int[] Free { get; set; } = Array.Empty<int>();


		public int[] Mmo { get; set; } = Array.Empty<int>();

	}

	private sealed class CacheFileModel
	{
		public DateTime FetchedUtc { get; set; }

		public List<CacheEntry> Games { get; set; } = new List<CacheEntry>();

	}

	private sealed class CacheEntry
	{
		public int Id { get; set; }

		public string Name { get; set; } = "";


		public string[] G { get; set; } = Array.Empty<string>();


		public string[]? T { get; set; }

		public string? Dev { get; set; }

		public string? Pub { get; set; }

		public string? Year { get; set; }

		public string? About { get; set; }
	}

	private const string SearchUrl = "https://store.steampowered.com/search/results/?query&count=100&infinite=1&json=1&cc=ru&l=russian&category1=998&category2=2";

	private const int PagesPerGenre = 3;

	private const int FreePages = 6;

	private const int MmoPages = 4;

	private static int _minGapMs = 600;

	private static readonly SemaphoreSlim Gate = new SemaphoreSlim(2, 2);

	private static readonly object RateLock = new object();

	private static DateTime _nextSlot = DateTime.UtcNow;

	private static readonly TimeSpan ExcludeTtl = TimeSpan.FromDays(14.0);

	private static readonly TimeSpan Ttl = TimeSpan.FromDays(1.0);

	private static DateTime _remoteStamp;

	private static readonly TimeSpan FailTtl = TimeSpan.FromMinutes(10.0);

	public static readonly (string Genre, string Tag)[] SteamGenres = new(string, string)[10]
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
		("Early Access", "493")
	};

	private static readonly string[] OnlineOnly = new string[114]
	{
		"dota", "counter-strike", "counter strike", "cs2", "cs:go", "cs go", "pubg", "apex legends", "valorant", "overwatch",
		"warframe", "war thunder", "world of tanks", "world of warships", "destiny 2", "rainbow six", "tarkov", "hunt: showdown", "dead by daylight", "rust",
		"dayz", "garry's mod", "team fortress", "fallout 76", "elder scrolls online", "final fantasy xiv", "guild wars", "path of exile", "lost ark", "black desert",
		"new world", "throne and liberty", "albion online", "eve online", "star citizen", "marvel rivals", "deadlock", "the finals", "delta force", "naraka",
		"crossout", "enlisted", "genshin", "honkai", "zenless", "wuthering waves", "tower of fantasy", "torchlight: infinite", "limbus", "where winds meet",
		"once human", "r.e.p.o", "peak", "phasmophobia", "backrooms", "lethal company", "content warning", "among us", "fall guys", "rocket league",
		"brawlhalla", "paladins", "smite", "warface", "crossfire", "arma reforger", "squad", "hell let loose", "insurgency", "battlefield",
		"call of duty", "halo infinite", "the isle", "scum", "miscreated", "realm royale", "super people", "vampire: the masquerade - bloodhunt", "bloodhunt", "the cycle",
		"spellbreak", "crucible", "artifact", "gwent", "hearthstone", "magic: the gathering arena", "yu-gi-oh", "master duel", "mtg arena", "payday",
		"казино", "casino", "rv there yet", "how to fish", "gtfo", "deep rock galactic", "sea of thieves", "star wars: the old republic", "dc universe online", "neverwinter",
		"blade & soul", "archeage", "ragnarok online", "runescape", "tibia", "mabinogi", "second life", "vrchat", "rec room", "roblox",
		"the elder scrolls online", "eso:", "deadside", "escape from backrooms"
	};

	private static readonly string[] Noise = new string[10] { "soundtrack", "dedicated server", " sdk", "demo", "beta test", "playtest", "wallpaper engine", "crosshair", "benchmark", "editor" };

	private static Task<List<StoreGame>>? _task;

	private static List<StoreGame>? _loaded;

	private static bool _blocked;

	private static readonly Regex RowRegex = new Regex("data-ds-appid=\"(\\d+)[,\"](.*?)</a>", RegexOptions.Singleline);

	private static readonly Regex TitleRegex = new Regex("<span class=\"title\">(.*?)</span>", RegexOptions.Singleline);

	public static string[] RemoteCatalogUrls
	{
		get
		{
			string environmentVariable = Environment.GetEnvironmentVariable("LUDEX_CATALOG_URL");
			if (!string.IsNullOrWhiteSpace(environmentVariable))
			{
				return new string[1] { environmentVariable };
			}
			return new string[2]
			{
				"https://dosherrk.github.io/ludex/catalog." + (Loc.IsRu ? "ru" : "en") + ".json.gz",
				"https://dosherrk.github.io/ludex/catalog.json.gz"
			};
		}
	}

	/// <summary>
	/// Каталоги для индекса названий: берём оба языка. Русский каталог локализован
	/// («Хогвартс: Наследие» вместо «Hogwarts Legacy»), поэтому по одному языку
	/// половина библиотеки не находилась бы.
	/// </summary>
	public static string[] IndexCatalogUrls
	{
		get
		{
			string environmentVariable = Environment.GetEnvironmentVariable("LUDEX_CATALOG_URL");
			if (!string.IsNullOrWhiteSpace(environmentVariable))
			{
				return new string[1] { environmentVariable };
			}
			return new string[2]
			{
				"https://dosherrk.github.io/ludex/catalog.ru.json.gz",
				"https://dosherrk.github.io/ludex/catalog.en.json.gz"
			};
		}
	}

	public static DateTime FetchedUtc { get; private set; }

	public static string? LastError { get; private set; }

	public static string? ProgressText { get; private set; }

	public static double ProgressFraction { get; private set; }

	public static bool IsReady => _task?.IsCompletedSuccessfully ?? false;

	private static string CacheDir
	{
		get
		{
			string cacheDir = StoreFeed.CacheDir;
			Directory.CreateDirectory(cacheDir);
			return cacheDir;
		}
	}

	private static string CacheFile => System.IO.Path.Combine(CacheDir, "catalog.json");

	/// <summary>Компактный индекс названий: ~1,5 МБ вместо 13–43 МБ полного каталога.</summary>
	private static string IndexFile => System.IO.Path.Combine(CacheDir, "catalog.index.gz");

	private static string FailFile => System.IO.Path.Combine(CacheDir, "catalog-fail.txt");

	private static string ExcludePath => System.IO.Path.Combine(CacheDir, "exclude.json");

	// Индекс «нормализованное название» для быстрой проверки «это игра?».
	// Строится один раз и стоит копейки по сравнению с полным списком из 130 тысяч объектов.
	private static HashSet<string>? _titleKeys;

	/// <summary>
	/// Предзагрузка для старта приложения: НЕ читает 43-мегабайтный каталог целиком,
	/// а только разбирает его в индекс названий, чтобы проверка «это игра?» работала
	/// сразу. Полный список подгружается позже, при открытии раздела рекомендаций.
	/// Это снимает ~3,5 секунды и ~300 МБ памяти с каждого запуска.
	/// </summary>
	public static async Task PreloadIndexAsync(CancellationToken ct = default(CancellationToken))
	{
		if (_titleKeys != null)
		{
			return;
		}
		if (_task != null && _task.IsCompletedSuccessfully)
		{
			// полный список уже в памяти — индекс не нужен
			return;
		}
		await Task.Run(delegate
		{
			try
			{
				EnsureIndex();
			}
			catch (Exception ex)
			{
				AppPaths.Log("StoreCatalog index: " + ex.Message);
			}
		}, ct).ConfigureAwait(continueOnCapturedContext: false);
	}

	private static void EnsureIndex()
	{
		if (_titleKeys != null)
		{
			return;
		}
		HashSet<string> keys = ReadIndexFile();
		if (keys == null)
		{
			// индекса ещё нет (первый запуск или обновление с прежней версии):
			// если есть полный кэш — берём из него, иначе качаем компактный индекс
			keys = ReadIndexFromFullCache();
			if (keys == null)
			{
				keys = DownloadIndex();
			}
			if (keys != null && keys.Count > 0)
			{
				WriteIndexFile(keys);
			}
		}
		_titleKeys = keys ?? new HashSet<string>(StringComparer.Ordinal);
		AppPaths.Log($"StoreCatalog: индекс названий {_titleKeys.Count} записей (без загрузки полного списка)");
	}

	private static HashSet<string>? ReadIndexFile()
	{
		try
		{
			if (!File.Exists(IndexFile))
			{
				return null;
			}
			using FileStream stream = File.OpenRead(IndexFile);
			using GZipStream gzip = new GZipStream(stream, CompressionMode.Decompress);
			using StreamReader reader = new StreamReader(gzip, Encoding.UTF8);
			HashSet<string> keys = new HashSet<string>(StringComparer.Ordinal);
			string? line;
			while ((line = reader.ReadLine()) != null)
			{
				if (line.Length > 0)
				{
					keys.Add(line);
				}
			}
			return keys.Count > 0 ? keys : null;
		}
		catch (Exception ex)
		{
			AppPaths.Log("StoreCatalog index read: " + ex.Message);
			return null;
		}
	}

	private static void WriteIndexFile(HashSet<string> keys)
	{
		try
		{
			string temp = IndexFile + ".tmp";
			using (FileStream stream = File.Create(temp))
			using (GZipStream gzip = new GZipStream(stream, CompressionLevel.Fastest))
			using (StreamWriter writer = new StreamWriter(gzip, new UTF8Encoding(false)))
			{
				foreach (string key in keys)
				{
					writer.WriteLine(key);
				}
			}
			File.Move(temp, IndexFile, overwrite: true);
			AppPaths.Log($"StoreCatalog: индекс сохранён ({new FileInfo(IndexFile).Length / 1024} КБ)");
		}
		catch (Exception ex)
		{
			AppPaths.Log("StoreCatalog index write: " + ex.Message);
		}
	}

	private static HashSet<string>? ReadIndexFromFullCache()
	{
		try
		{
			if (!File.Exists(CacheFile))
			{
				return null;
			}
			CacheFileModel model = JsonSerializer.Deserialize<CacheFileModel>(File.ReadAllText(CacheFile));
			if (model?.Games == null || model.Games.Count == 0)
			{
				return null;
			}
			HashSet<string> keys = new HashSet<string>(StringComparer.Ordinal);
			foreach (CacheEntry entry in model.Games)
			{
				string key = Normalize(entry.Name);
				if (key.Length > 0)
				{
					keys.Add(key);
				}
			}
			return keys;
		}
		catch (Exception ex)
		{
			AppPaths.Log("StoreCatalog index from cache: " + ex.Message);
			return null;
		}
	}

	/// <summary>
	/// Скачивает каталог (5 МБ в gzip), оставляет только названия и сохраняет компактный
	/// индекс. Полный список в память не попадает — это и есть экономия ~300 МБ.
	/// </summary>
	private static HashSet<string>? DownloadIndex()
	{
		try
		{
			using HttpClient http = MakeClient();
			HashSet<string>? merged = null;
			foreach (string url in IndexCatalogUrls)
			{
				try
				{
					using HttpResponseMessage response = http.GetAsync(url).GetAwaiter().GetResult();
					if (!response.IsSuccessStatusCode)
					{
						continue;
					}
					using Stream stream = response.Content.ReadAsStreamAsync().GetAwaiter().GetResult();
					using GZipStream gzip = new GZipStream(stream, CompressionMode.Decompress);
					using JsonDocument document = JsonDocument.Parse(gzip);
					if (!document.RootElement.TryGetProperty("games", out JsonElement games) || games.ValueKind != JsonValueKind.Array)
					{
						continue;
					}
					HashSet<string> keys = new HashSet<string>(StringComparer.Ordinal);
					foreach (JsonElement game in games.EnumerateArray())
					{
						if (game.ValueKind == JsonValueKind.Array)
						{
							// компактный формат: [id, "название", ...]
							foreach (JsonElement field in game.EnumerateArray())
							{
								if (field.ValueKind == JsonValueKind.String)
								{
									string key = Normalize(field.GetString() ?? "");
									if (key.Length > 0)
									{
										keys.Add(key);
									}
									break;
								}
							}
						}
						else if (game.ValueKind == JsonValueKind.Object && game.TryGetProperty("name", out JsonElement name))
						{
							string key = Normalize(name.GetString() ?? "");
							if (key.Length > 0)
							{
								keys.Add(key);
							}
						}
					}
					if (keys.Count > 0)
					{
						AppPaths.Log($"StoreCatalog: индекс из {url.Split('/').Last()} — {keys.Count} названий");
						merged ??= new HashSet<string>(StringComparer.Ordinal);
						merged.UnionWith(keys);
					}
				}
				catch (Exception ex2)
				{
					AppPaths.Log("StoreCatalog index download: " + ex2.Message);
				}
			}
			if (merged != null && merged.Count > 0)
			{
				return merged;
			}
		}
		catch (Exception ex)
		{
			AppPaths.Log("StoreCatalog index download failed: " + ex.Message);
		}
		return null;
	}

	private static string Normalize(string text)
	{
		return new string((text ?? "").ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
	}

	public static Task<List<StoreGame>> LoadAsync(bool force, Action<string>? progress, CancellationToken ct)
	{
		if (!force)
		{
			Task<List<StoreGame>> task = _task;
			if (task != null && task.IsCompletedSuccessfully)
			{
				return _task;
			}
		}
		if (!force)
		{
			Task<List<StoreGame>> task2 = _task;
			if (task2 != null && !task2.IsCompleted)
			{
				return _task;
			}
		}
		Task<List<StoreGame>>? task3 = (_task = LoadCoreAsync(force, progress, ct));
		task3.ContinueWith(delegate(Task<List<StoreGame>> t)
		{
			if (t.Status == TaskStatus.RanToCompletion)
			{
				_loaded = t.Result;
			}
		}, TaskScheduler.Default);
		return task3;
	}

	public static bool MatchesTitle(string title)
	{
		if (_titleKeys == null)
		{
			EnsureIndex();
		}
		if (_titleKeys == null || _titleKeys.Count == 0)
		{
			return false;
		}
		string key = Normalize(title);
		if (key.Length < 3)
		{
			return false;
		}
		if (_titleKeys.Contains(key))
		{
			return true;
		}
		// Частичное совпадение допускаем только для близких по длине названий.
		// Раньше сравнение было «любая строка содержит любую другую», и при 130 тысячах
		// записей почти всё признавалось игрой (например «TqWsProxy» или «Downloads»):
		// чистка библиотеки после этого не убирала ничего.
		if (key.Length < 8)
		{
			return false;
		}
		foreach (string candidate in _titleKeys)
		{
			if (candidate.Length < 8 || Math.Abs(candidate.Length - key.Length) * 100 > Math.Max(candidate.Length, key.Length) * 25)
			{
				continue;
			}
			if (candidate.Contains(key, StringComparison.Ordinal) || key.Contains(candidate, StringComparison.Ordinal))
			{
				return true;
			}
		}
		return false;
	}

	private static void Report(Action<string>? progress, string text, double fraction)
	{
		ProgressText = text;
		ProgressFraction = fraction;
		progress?.Invoke(text);
	}

	private static async Task<List<StoreGame>> LoadCoreAsync(bool force, Action<string>? progress, CancellationToken ct)
	{
		Action<string> progress2 = progress;
		List<StoreGame> cached = ReadCache();
		DateTime dateTime = ReadCacheStamp();
		bool flag = dateTime != default(DateTime) && DateTime.UtcNow - dateTime <= Ttl;
		if (!force && cached != null)
		{
			FetchedUtc = dateTime;
			if (!flag)
			{
				AppPaths.Log("StoreCatalog: показываем кэш, обновляем в фоне");
				Task.Run(async delegate
				{
					try
					{
						await LoadCoreAsync(force: true, null, CancellationToken.None);
					}
					catch (Exception ex3)
					{
						AppPaths.Log("StoreCatalog background: " + ex3.Message);
					}
				}, CancellationToken.None);
			}
			Report(progress2, Loc.T("splash.catalogReady"), 1.0);
			return cached;
		}
		if (!force && Throttled())
		{
			LastError = Loc.T("catalog.throttled");
			if (cached != null)
			{
				return cached;
			}
		}
		try
		{
			List<StoreGame> list = await TryRemoteAsync(progress2, ct);
			if (list != null)
			{
				FetchedUtc = ((_remoteStamp == default(DateTime)) ? DateTime.UtcNow : _remoteStamp);
				LastError = null;
				Report(progress2, Loc.F("recs.loaded", list.Count), 1.0);
				AppPaths.Log($"StoreCatalog: готовый каталог {list.Count} игр");
				WriteCache(list, FetchedUtc, withGenreSweep: true);
				TryDelete(FailFile);
				return list;
			}
			HttpClient http = MakeClient();
			try
			{
				DateTime deadline = DateTime.UtcNow + TimeSpan.FromSeconds(90.0);
				_blocked = false;
				Dictionary<int, StoreGame> merged = new Dictionary<int, StoreGame>();
				List<int> order = new List<int>();
				Report(progress2, Loc.T("catalog.loadingFree"), 0.05);
				(HashSet<int>, HashSet<int>) tuple = await LoadExclusionsAsync(http, ct);
				HashSet<int> free = tuple.Item1;
				HashSet<int> mmo = tuple.Item2;
				List<(string Genre, string Tag, int Start)> jobs = new List<(string, string, int)>();
				(string, string)[] steamGenres = SteamGenres;
				for (int i = 0; i < steamGenres.Length; i++)
				{
					(string, string) tuple2 = steamGenres[i];
					string item = tuple2.Item1;
					string item2 = tuple2.Item2;
					for (int j = 0; j < 3; j++)
					{
						jobs.Add((item, item2, j * 100));
					}
				}
				ConcurrentDictionary<(string Genre, int Start), List<(int AppId, string Name)>> fetched = new ConcurrentDictionary<(string, int), List<(int appId, string name)>>();
				int donePages = 0;
				await Task.WhenAll(((IEnumerable<(string, string, int)>)jobs).Select((Func<(string, string, int), Task>)async delegate((string Genre, string Tag, int Start) job)
				{
					ct.ThrowIfCancellationRequested();
					if (!_blocked && !(DateTime.UtcNow > deadline))
					{
						List<(int appId, string name)> value3 = await FetchPageAsync(http, "tags=" + job.Tag, job.Start, ct);
						fetched[(job.Genre, job.Start)] = value3;
						int num2 = Interlocked.Increment(ref donePages);
						Report(progress2, Loc.F("catalog.loading", Math.Min(SteamGenres.Length, num2 * SteamGenres.Length / jobs.Count + 1), SteamGenres.Length, DisplayGenre(job.Genre)), 0.15 + 0.8 * (double)num2 / (double)jobs.Count);
					}
				}));
				steamGenres = SteamGenres;
				for (int k = 0; k < steamGenres.Length; k++)
				{
					string item3 = steamGenres[k].Item1;
					for (int l = 0; l < 3; l++)
					{
						if (!fetched.TryGetValue((item3, l * 100), out List<(int appId, string name)> value))
						{
							continue;
						}
						foreach (var (num, name) in value)
						{
							if (free.Contains(num) || mmo.Contains(num))
							{
								continue;
							}
							if (!merged.TryGetValue(num, out StoreGame value2))
							{
								if (Rejected(name))
								{
									continue;
								}
								value2 = new StoreGame
								{
									AppId = num,
									Name = name,
									CoverUrl = $"https://cdn.cloudflare.steamstatic.com/steam/apps/{num}/header.jpg",
									HeaderUrl = $"https://cdn.cloudflare.steamstatic.com/steam/apps/{num}/header.jpg"
								};
								value2.Sections.Add("all");
								merged[num] = value2;
								order.Add(num);
							}
							string text = DisplayGenre(item3);
							if (Array.IndexOf(value2.Genres, text) < 0)
							{
								value2.Genres = value2.Genres.Append(text).ToArray();
							}
						}
					}
				}
				List<StoreGame> list2 = order.Select((int id) => merged[id]).ToList();
				if (list2.Count < 200)
				{
					LastError = (_blocked ? Loc.T("catalog.blocked") : Loc.T("catalog.empty"));
					try
					{
						File.WriteAllText(FailFile, DateTime.UtcNow.ToString("O") + " " + LastError);
					}
					catch
					{
					}
					AppPaths.Log("StoreCatalog: пустой результат поиска магазина");
					return cached ?? new List<StoreGame>();
				}
				FetchedUtc = DateTime.UtcNow;
				LastError = null;
				Report(progress2, Loc.F("recs.loaded", list2.Count), 1.0);
				AppPaths.Log($"StoreCatalog: собрано {list2.Count} игр");
				WriteCache(list2);
				TryDelete(FailFile);
				return list2;
			}
			finally
			{
				if (http != null)
				{
					((IDisposable)http).Dispose();
				}
			}
		}
		catch (OperationCanceledException)
		{
			return cached ?? new List<StoreGame>();
		}
		catch (Exception ex2)
		{
			LastError = ex2.Message;
			AppPaths.Log("StoreCatalog failed: " + ex2.Message);
			try
			{
				File.WriteAllText(FailFile, DateTime.UtcNow.ToString("O") + " " + ex2.Message);
			}
			catch
			{
			}
			return cached ?? new List<StoreGame>();
		}
	}

	private static async Task<List<StoreGame>?> TryRemoteAsync(Action<string>? progress, CancellationToken ct)
	{
		string[] remoteCatalogUrls = RemoteCatalogUrls;
		for (int i = 0; i < remoteCatalogUrls.Length; i++)
		{
			List<StoreGame> list = await TryRemoteFromAsync(remoteCatalogUrls[i], progress, ct);
			if (list != null)
			{
				return list;
			}
			ct.ThrowIfCancellationRequested();
		}
		return null;
	}

	private static async Task<List<StoreGame>?> TryRemoteFromAsync(string url, Action<string>? progress, CancellationToken ct)
	{
		_ = 4;
		try
		{
			Report(progress, Loc.T("catalog.download"), 0.12);
			using HttpClient http = MakeClient();
			http.Timeout = TimeSpan.FromSeconds(90.0);
			using HttpResponseMessage response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
			if (!response.IsSuccessStatusCode)
			{
				AppPaths.Log($"StoreCatalog remote {url}: HTTP {response.StatusCode}");
				return null;
			}
			Stream raw = await response.Content.ReadAsStreamAsync(ct);
			List<StoreGame> result;
			try
			{
				GZipStream gz = new GZipStream(raw, CompressionMode.Decompress);
				List<StoreGame> list;
				try
				{
					using JsonDocument jsonDocument = await JsonDocument.ParseAsync(gz, default(JsonDocumentOptions), ct);
					JsonElement rootElement = jsonDocument.RootElement;
					if (!rootElement.TryGetProperty("games", out var value) || value.ValueKind != JsonValueKind.Array)
					{
						AppPaths.Log("StoreCatalog remote: в файле нет списка игр");
						list = null;
					}
					else
					{
						string[] array = Array.Empty<string>();
						if (rootElement.TryGetProperty("genres", out var value2) && value2.ValueKind == JsonValueKind.Array)
						{
							array = value2.EnumerateArray().Select(delegate(JsonElement x)
							{
								JsonElement jsonElement2 = x;
								return jsonElement2.GetString() ?? "";
							}).ToArray();
						}
						string[] array2 = Array.Empty<string>();
						if (rootElement.TryGetProperty("themes", out var value3) && value3.ValueKind == JsonValueKind.Array)
						{
							array2 = value3.EnumerateArray().Select(delegate(JsonElement x)
							{
								JsonElement jsonElement = x;
								return jsonElement.GetString() ?? "";
							}).ToArray();
						}
						_remoteStamp = DateTime.UtcNow;
						if (rootElement.TryGetProperty("utc", out var value4) && value4.ValueKind == JsonValueKind.String && DateTime.TryParse(value4.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var result2))
						{
							_remoteStamp = result2;
						}
						List<StoreGame> list2 = new List<StoreGame>(value.GetArrayLength());
						foreach (JsonElement item in value.EnumerateArray())
						{
							if (item.ValueKind != JsonValueKind.Array || item.GetArrayLength() < 2)
							{
								continue;
							}
							int num = ((item[0].ValueKind == JsonValueKind.Number) ? item[0].GetInt32() : 0);
							string text = (item[1].GetString() ?? "").Trim();
							if (num <= 0 || text.Length == 0 || Rejected(text))
							{
								continue;
							}
							StoreGame storeGame = new StoreGame
							{
								AppId = num,
								Name = text,
								CoverUrl = $"https://cdn.cloudflare.steamstatic.com/steam/apps/{num}/header.jpg",
								HeaderUrl = $"https://cdn.cloudflare.steamstatic.com/steam/apps/{num}/header.jpg"
							};
							int arrayLength = item.GetArrayLength();
							if (array2.Length != 0 && arrayLength >= 4 && item[3].ValueKind == JsonValueKind.String)
							{
								string text2 = (item[3].GetString() ?? "").Trim();
								if (text2.Length > 0 && long.TryParse(text2, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var result3) && result3 != 0L)
								{
									List<string> list3 = new List<string>(4);
									for (int i = 0; i < array2.Length && i < 62; i++)
									{
										if ((result3 & (1L << i)) != 0L && array2[i].Length > 0)
										{
											list3.Add(array2[i]);
										}
									}
									storeGame.Themes = list3.ToArray();
								}
							}
							int num2 = ((arrayLength >= 8) ? 4 : ((arrayLength >= 7) ? 3 : (-1)));
							if (num2 > 0)
							{
								if (item[num2].ValueKind == JsonValueKind.String)
								{
									storeGame.Developer = (item[num2].GetString() ?? "").Trim();
								}
								if (item[num2 + 1].ValueKind == JsonValueKind.String)
								{
									storeGame.Publisher = (item[num2 + 1].GetString() ?? "").Trim();
								}
								if (item[num2 + 2].ValueKind == JsonValueKind.Number && item[num2 + 2].GetInt32() > 1900)
								{
									storeGame.ReleaseDate = item[num2 + 2].GetInt32().ToString(CultureInfo.InvariantCulture);
								}
								if (item[num2 + 3].ValueKind == JsonValueKind.String)
								{
									string text3 = (item[num2 + 3].GetString() ?? "").Trim();
									if (text3.Length > 0)
									{
										storeGame.Description = text3;
									}
								}
							}
							if (arrayLength >= 3 && array.Length != 0)
							{
								string? obj = item[2].GetString() ?? "";
								List<string> list4 = new List<string>(3);
								string[] array3 = obj.Split(' ', StringSplitOptions.RemoveEmptyEntries);
								for (int j = 0; j < array3.Length; j++)
								{
									if (int.TryParse(array3[j], out var result4) && result4 >= 0 && result4 < array.Length)
									{
										string text4 = array[result4];
										if (text4.Length > 0 && !list4.Contains(text4))
										{
											list4.Add(text4);
										}
									}
								}
								storeGame.Genres = list4.ToArray();
							}
							storeGame.Sections.Add("all");
							list2.Add(storeGame);
						}
						if (list2.Count < 500)
						{
							AppPaths.Log($"StoreCatalog remote: всего {list2.Count} игр — файл не используем");
							list = null;
						}
						else
						{
							list = list2;
						}
					}
				}
				finally
				{
					if (gz != null)
					{
						await gz.DisposeAsync();
					}
				}
				result = list;
			}
			finally
			{
				if (raw != null)
				{
					await raw.DisposeAsync();
				}
			}
			return result;
		}
		catch (OperationCanceledException)
		{
			throw;
		}
		catch (Exception ex2)
		{
			AppPaths.Log("StoreCatalog remote " + url + ": " + ex2.Message);
			return null;
		}
	}

	private static async Task<(HashSet<int> Free, HashSet<int> Mmo)> LoadExclusionsAsync(HttpClient http, CancellationToken ct)
	{
		try
		{
			if (File.Exists(ExcludePath))
			{
				ExcludeFile excludeFile = JsonSerializer.Deserialize<ExcludeFile>(File.ReadAllText(ExcludePath));
				if (excludeFile != null && DateTime.UtcNow - excludeFile.FetchedUtc < ExcludeTtl && excludeFile.Free.Length != 0)
				{
					return (Free: excludeFile.Free.ToHashSet(), Mmo: excludeFile.Mmo.ToHashSet());
				}
			}
		}
		catch (Exception ex)
		{
			AppPaths.Log("StoreCatalog exclude cache: " + ex.Message);
		}
		HashSet<int> free = (await CollectAsync(http, "maxprice=free", 6, ct)).Select<(int, string), int>(((int AppId, string Name) r) => r.AppId).ToHashSet();
		HashSet<int> hashSet = (await CollectAsync(http, "tags=128", 4, ct)).Select<(int, string), int>(((int AppId, string Name) r) => r.AppId).ToHashSet();
		try
		{
			File.WriteAllText(ExcludePath, JsonSerializer.Serialize(new ExcludeFile
			{
				FetchedUtc = DateTime.UtcNow,
				Free = free.ToArray(),
				Mmo = hashSet.ToArray()
			}));
		}
		catch (Exception ex2)
		{
			AppPaths.Log("StoreCatalog exclude write: " + ex2.Message);
		}
		return (Free: free, Mmo: hashSet);
	}

	private static async Task<List<(int AppId, string Name)>> CollectAsync(HttpClient http, string query, int pages, CancellationToken ct)
	{
		List<(int AppId, string Name)> rows = new List<(int appId, string name)>();
		for (int page = 0; page < pages; page++)
		{
			ct.ThrowIfCancellationRequested();
			if (_blocked)
			{
				break;
			}
			List<(int AppId, string Name)> list = rows;
			list.AddRange(await FetchPageAsync(http, query, page * 100, ct));
		}
		return rows;
	}

	private static async Task WaitTurnAsync(CancellationToken ct)
	{
		int num;
		lock (RateLock)
		{
			DateTime utcNow = DateTime.UtcNow;
			if (_nextSlot < utcNow)
			{
				_nextSlot = utcNow;
			}
			DateTime nextSlot = _nextSlot;
			_nextSlot = nextSlot.AddMilliseconds(_minGapMs);
			num = (int)Math.Max(0.0, (nextSlot - utcNow).TotalMilliseconds);
		}
		if (num > 0)
		{
			await Task.Delay(num, ct);
		}
	}

	private static async Task<List<(int AppId, string Name)>> FetchPageAsync(HttpClient http, string query, int start, CancellationToken ct)
	{
		string url = $"{"https://store.steampowered.com/search/results/?query&count=100&infinite=1&json=1&cc=ru&l=russian&category1=998&category2=2"}&start={start}&{query}";
		for (int attempt = 0; attempt < 3; attempt++)
		{
			ct.ThrowIfCancellationRequested();
			await Gate.WaitAsync(ct);
			try
			{
				await WaitTurnAsync(ct);
				using HttpResponseMessage response = await http.GetAsync(url, ct);
				if (response.StatusCode == HttpStatusCode.Forbidden && attempt == 0)
				{
					_blocked = true;
					AppPaths.Log("StoreCatalog: магазин ограничил запросы (" + query + ")");
					return new List<(int appId, string name)>();
				}
				if (response.StatusCode == HttpStatusCode.TooManyRequests)
				{
					_minGapMs = Math.Min(_minGapMs * 3 / 2, 2500);
					continue;
				}
				response.EnsureSuccessStatusCode();
				string json = await response.Content.ReadAsStringAsync(ct);
				if (_minGapMs > 600)
				{
					_minGapMs -= 40;
				}
				return ParsePage(json);
			}
			catch (OperationCanceledException)
			{
				throw;
			}
			catch (Exception ex2) when (attempt < 2)
			{
				AppPaths.Log($"StoreCatalog page {start} ({query}): {ex2.Message}");
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
		return new List<(int appId, string name)>();
	}

	private static List<(int AppId, string Name)> ParsePage(string json)
	{
		List<(int appId, string name)> list = new List<(int appId, string name)>();
		using JsonDocument jsonDocument = JsonDocument.Parse(json);
		if (!jsonDocument.RootElement.TryGetProperty("results_html", out var value) || value.ValueKind != JsonValueKind.String)
		{
			return list;
		}
		string input = value.GetString() ?? "";
		foreach (Match item in RowRegex.Matches(input))
		{
			if (!int.TryParse(item.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
			{
				continue;
			}
			Match match2 = TitleRegex.Match(item.Groups[2].Value);
			if (match2.Success)
			{
				string text = WebUtility.HtmlDecode(Regex.Replace(match2.Groups[1].Value, "<.*?>", "")).Trim();
				if (text.Length != 0)
				{
					list.Add((result, text));
				}
			}
		}
		return list;
	}

	private static HttpClient MakeClient()
	{
		HttpClient httpClient = new HttpClient();
		httpClient.Timeout = TimeSpan.FromSeconds(45.0);
		httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) Ludex/" + Updater.CurrentVersionText);
		httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language", "ru,en;q=0.8");
		return httpClient;
	}

	public static string DisplayGenre(string genre)
	{
		return Loc.T("cgenre." + genre);
	}

	private static bool Rejected(string name)
	{
		string text = name.ToLowerInvariant();
		string[] noise = Noise;
		foreach (string value in noise)
		{
			if (text.Contains(value, StringComparison.Ordinal))
			{
				return true;
			}
		}
		noise = OnlineOnly;
		foreach (string value2 in noise)
		{
			if (text.Contains(value2, StringComparison.Ordinal))
			{
				return true;
			}
		}
		return false;
	}

	private static bool Throttled()
	{
		try
		{
			if (!File.Exists(FailFile))
			{
				return false;
			}
			DateTime lastWriteTimeUtc = File.GetLastWriteTimeUtc(FailFile);
			return DateTime.UtcNow - lastWriteTimeUtc < FailTtl;
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
			if (File.Exists(path))
			{
				File.Delete(path);
			}
		}
		catch
		{
		}
	}

	private static void CarryGenres(List<StoreGame> games)
	{
		try
		{
			if (!File.Exists(CacheFile))
			{
				return;
			}
			CacheFileModel cacheFileModel = JsonSerializer.Deserialize<CacheFileModel>(File.ReadAllText(CacheFile));
			if (cacheFileModel == null || cacheFileModel.Games.Count == 0)
			{
				return;
			}
			Dictionary<int, CacheEntry> dictionary = new Dictionary<int, CacheEntry>(cacheFileModel.Games.Count);
			foreach (CacheEntry game in cacheFileModel.Games)
			{
				if (game.G.Length == 0)
				{
					string[] t = game.T;
					if (((t != null && t.Length != 0) ? 1 : 0) <= 0)
					{
						continue;
					}
				}
				dictionary[game.Id] = game;
			}
			int num = 0;
			foreach (StoreGame game2 in games)
			{
				if (!dictionary.TryGetValue(game2.AppId, out var value))
				{
					continue;
				}
				if (game2.Genres.Length == 0 && value.G.Length != 0)
				{
					game2.Genres = value.G;
				}
				if (game2.Themes.Length == 0)
				{
					string[] t2 = value.T;
					if (t2 != null && t2.Length != 0)
					{
						game2.Themes = value.T;
					}
				}
				num++;
			}
			if (num > 0)
			{
				AppPaths.Log($"StoreCatalog: жанры восстановлены для {num} игр из прошлого кэша");
			}
		}
		catch (Exception ex)
		{
			AppPaths.Log("StoreCatalog carry: " + ex.Message);
		}
	}

	private static DateTime ReadCacheStamp()
	{
		try
		{
			if (!File.Exists(CacheFile))
			{
				return default(DateTime);
			}
			return JsonSerializer.Deserialize<CacheFileModel>(File.ReadAllText(CacheFile))?.FetchedUtc ?? default(DateTime);
		}
		catch
		{
			return default(DateTime);
		}
	}

	private static List<StoreGame>? ReadCache()
	{
		try
		{
			if (!File.Exists(CacheFile))
			{
				return null;
			}
			CacheFileModel cacheFileModel = JsonSerializer.Deserialize<CacheFileModel>(File.ReadAllText(CacheFile));
			if (cacheFileModel == null || cacheFileModel.Games.Count == 0)
			{
				return null;
			}
			if (DateTime.UtcNow - cacheFileModel.FetchedUtc > Ttl)
			{
				return null;
			}
			List<StoreGame> list = new List<StoreGame>(cacheFileModel.Games.Count);
			foreach (CacheEntry game in cacheFileModel.Games)
			{
				StoreGame storeGame = new StoreGame
				{
					AppId = game.Id,
					Name = game.Name,
					CoverUrl = $"https://cdn.cloudflare.steamstatic.com/steam/apps/{game.Id}/header.jpg",
					HeaderUrl = $"https://cdn.cloudflare.steamstatic.com/steam/apps/{game.Id}/header.jpg"
				};
				storeGame.Genres = game.G;
				string[] t = game.T;
				if (t != null && t.Length != 0)
				{
					storeGame.Themes = game.T;
				}
				storeGame.Developer = game.Dev ?? "";
				storeGame.Publisher = game.Pub ?? "";
				storeGame.ReleaseDate = game.Year;
				storeGame.Description = game.About;
				storeGame.Sections.Add("all");
				string text = System.IO.Path.Combine(StoreFeed.CoversDir, $"{game.Id}.jpg");
				if (File.Exists(text))
				{
					storeGame.CoverPath = text;
				}
				list.Add(storeGame);
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

	private static void WriteCache(List<StoreGame> games, DateTime? stamp = null, bool withGenreSweep = false)
	{
		try
		{
			if (!withGenreSweep)
			{
				CarryGenres(games);
			}
			CacheFileModel cacheFileModel = new CacheFileModel
			{
				FetchedUtc = (stamp ?? DateTime.UtcNow)
			};
			foreach (StoreGame game in games)
			{
				cacheFileModel.Games.Add(new CacheEntry
				{
					Id = game.AppId,
					Name = game.Name,
					G = game.Genres,
					T = ((game.Themes.Length != 0) ? game.Themes : null),
					Dev = (string.IsNullOrEmpty(game.Developer) ? null : game.Developer),
					Pub = (string.IsNullOrEmpty(game.Publisher) ? null : game.Publisher),
					Year = game.ReleaseDate,
					About = (string.IsNullOrEmpty(game.Description) ? null : game.Description)
				});
			}
			File.WriteAllText(CacheFile, JsonSerializer.Serialize(cacheFileModel));
		}
		catch (Exception ex)
		{
			AppPaths.Log("StoreCatalog cache write: " + ex.Message);
		}
	}
}
