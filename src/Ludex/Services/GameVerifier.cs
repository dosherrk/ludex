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

public static class GameVerifier
{
	private sealed class CacheEntry
	{
		public bool Exists { get; set; }

		public DateTime CheckedUtc { get; set; }
	}

	public sealed class Result
	{
		public List<string> Confirmed { get; } = new List<string>();


		public List<string> Rejected { get; } = new List<string>();


		public bool Offline { get; set; }
	}

	private const string SearchUrl = "https://store.steampowered.com/search/results/?term={0}&start=0&count=20&infinite=1&json=1&cc=us&l=english&category1=998";

	private static readonly HttpClient Http = new HttpClient
	{
		Timeout = TimeSpan.FromSeconds(15.0)
	};

	private static readonly SemaphoreSlim Gate = new SemaphoreSlim(2, 2);

	private static readonly object RateLock = new object();

	private static DateTime _nextSlot = DateTime.UtcNow;

	private const int GapMs = 400;

	private static readonly TimeSpan HitTtl = TimeSpan.FromDays(60.0);

	private static readonly TimeSpan MissTtl = TimeSpan.FromDays(14.0);

	private static Dictionary<string, CacheEntry>? _cache;

	private static readonly object CacheLock = new object();

	private static string CacheFile => System.IO.Path.Combine(AppPaths.Root, "verify.json");

	public static async Task<Result> VerifyAsync(IEnumerable<string> titles, Action<string>? onProgress = null, Func<bool>? isCancelled = null, CancellationToken ct = default(CancellationToken))
	{
		Result result = new Result();
		List<string> list = titles.Where((string t) => !string.IsNullOrWhiteSpace(t)).Distinct<string>(StringComparer.OrdinalIgnoreCase).ToList();
		if (list.Count == 0)
		{
			return result;
		}
		LoadCache();
		int done = 0;
		foreach (string title in list)
		{
			if (isCancelled?.Invoke() ?? false)
			{
				break;
			}
			done++;
			onProgress?.Invoke(Loc.F("auto.verify.progress", done, list.Count, title));
			bool? flag = await ExistsAsync(title, ct);
			if (!flag.HasValue)
			{
				result.Offline = true;
				foreach (string item in list.Skip(done - 1))
				{
					result.Confirmed.Add(item);
				}
				break;
			}
			if (flag.Value)
			{
				result.Confirmed.Add(title);
			}
			else
			{
				result.Rejected.Add(title);
			}
		}
		foreach (string item2 in result.Rejected)
		{
			AppPaths.Log("не игра: " + item2);
		}
		return result;
	}

	private static async Task<bool?> ExistsAsync(string title, CancellationToken ct)
	{
		string key = Normalize(title);
		if (key.Length == 0)
		{
			return null;
		}
		lock (CacheLock)
		{
			if (_cache.TryGetValue(key, out CacheEntry value))
			{
				TimeSpan timeSpan = (value.Exists ? HitTtl : MissTtl);
				if (DateTime.UtcNow - value.CheckedUtc < timeSpan)
				{
					return value.Exists;
				}
			}
		}
		if (StoreCatalog.MatchesTitle(title))
		{
			return Remember(key, exists: true);
		}
		await Gate.WaitAsync(ct);
		try
		{
			await WaitSlot();
			string requestUri = "https://store.steampowered.com/search/results/?term=" + Uri.EscapeDataString(title) + "&start=0&count=20&infinite=1&json=1&cc=us&l=english&category1=998";
			using HttpResponseMessage response = await Http.GetAsync(requestUri, ct);
			if (!response.IsSuccessStatusCode)
			{
				return null;
			}
			using JsonDocument jsonDocument = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
			JsonElement value2;
			bool exists = (from m in Regex.Matches(jsonDocument.RootElement.TryGetProperty("results_html", out value2) ? (value2.GetString() ?? "") : "", "class=\"title\">(.*?)</span>", RegexOptions.Singleline)
				select WebUtility.HtmlDecode(Regex.Replace(m.Groups[1].Value, "<.*?>", "")).Trim()).ToList().Any((string name) => Similar(Normalize(name), key));
			return Remember(key, exists);
		}
		catch (Exception ex) when (!(ex is OperationCanceledException))
		{
			AppPaths.Log("GameVerifier: " + ex.Message);
			return null;
		}
		finally
		{
			Gate.Release();
		}
	}

	private static async Task WaitSlot()
	{
		DateTime dateTime;
		lock (RateLock)
		{
			dateTime = ((_nextSlot < DateTime.UtcNow) ? DateTime.UtcNow : _nextSlot);
			_nextSlot = dateTime.AddMilliseconds(400.0);
		}
		TimeSpan timeSpan = dateTime - DateTime.UtcNow;
		if (timeSpan > TimeSpan.Zero)
		{
			await Task.Delay(timeSpan);
		}
	}

	private static bool? Remember(string key, bool exists)
	{
		lock (CacheLock)
		{
			_cache[key] = new CacheEntry
			{
				Exists = exists,
				CheckedUtc = DateTime.UtcNow
			};
		}
		SaveCache();
		return exists;
	}

	private static bool Similar(string candidate, string key)
	{
		if (candidate.Length > 0)
		{
			if (!candidate.Contains(key, StringComparison.Ordinal))
			{
				return key.Contains(candidate, StringComparison.Ordinal);
			}
			return true;
		}
		return false;
	}

	private static string Normalize(string text)
	{
		return Regex.Replace((text ?? "").ToLowerInvariant(), "[^a-z0-9а-яё]+", "");
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
				_cache = (File.Exists(CacheFile) ? (JsonSerializer.Deserialize<Dictionary<string, CacheEntry>>(File.ReadAllText(CacheFile)) ?? new Dictionary<string, CacheEntry>(StringComparer.OrdinalIgnoreCase)) : new Dictionary<string, CacheEntry>(StringComparer.OrdinalIgnoreCase));
			}
			catch
			{
				_cache = new Dictionary<string, CacheEntry>(StringComparer.OrdinalIgnoreCase);
			}
		}
	}

	private static void SaveCache()
	{
		try
		{
			lock (CacheLock)
			{
				string contents = JsonSerializer.Serialize(_cache);
				string text = CacheFile + ".tmp";
				File.WriteAllText(text, contents);
				File.Move(text, CacheFile, overwrite: true);
			}
		}
		catch (Exception ex)
		{
			AppPaths.Log("GameVerifier cache: " + ex.Message);
		}
	}
}
