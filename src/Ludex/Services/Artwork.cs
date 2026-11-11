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

public sealed class ArtCandidate
{
	public string Url { get; set; } = "";


	public string Source { get; set; } = "";


	public string Label { get; set; } = "";


	public bool Portrait { get; set; }

	public int? AppId { get; set; }

	public int Width { get; set; }

	public int Height { get; set; }
}

public static class Artwork
{
	private const string SteamCdn = "https://cdn.cloudflare.steamstatic.com/steam/apps/";

	private const string SgdbApi = "https://www.steamgriddb.com/api/v2/";

	public static readonly HttpClient Http = CreateClient();

	private static HttpClient CreateClient()
	{
		HttpClient httpClient = new HttpClient(new HttpClientHandler
		{
			AutomaticDecompression = DecompressionMethods.All
		});
		httpClient.Timeout = TimeSpan.FromSeconds(20.0);
		httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) Ludex/1.4");
		httpClient.DefaultRequestHeaders.Accept.ParseAdd("*/*");
		return httpClient;
	}

	public static async Task<List<(int appId, string name)>> SearchSteamAsync(string term, CancellationToken ct)
	{
		List<(int appId, string name)> result = new List<(int appId, string name)>();
		if (string.IsNullOrWhiteSpace(term))
		{
			return result;
		}
		string requestUri = "https://store.steampowered.com/api/storesearch/?term=" + Uri.EscapeDataString(term) + "&l=english&cc=us";
		try
		{
			using JsonDocument jsonDocument = JsonDocument.Parse(await Http.GetStringAsync(requestUri, ct).ConfigureAwait(continueOnCapturedContext: false));
			if (!jsonDocument.RootElement.TryGetProperty("items", out var value))
			{
				return result;
			}
			foreach (JsonElement item in value.EnumerateArray())
			{
				if ((!item.TryGetProperty("type", out var value2) || !(value2.GetString() != "app")) && item.TryGetProperty("id", out var value3) && item.TryGetProperty("name", out var value4))
				{
					result.Add((value3.GetInt32(), value4.GetString() ?? ""));
				}
			}
		}
		catch (Exception ex)
		{
			AppPaths.Log("SearchSteam failed: " + ex.Message);
		}
		if (result.Count == 0)
		{
			List<(int appId, string name)> list = result;
			list.AddRange(await SearchCommunityAsync(term, ct).ConfigureAwait(continueOnCapturedContext: false));
		}
		return result;
	}

	private static async Task<List<(int appId, string name)>> SearchCommunityAsync(string term, CancellationToken ct)
	{
		List<(int appId, string name)> result = new List<(int appId, string name)>();
		if (string.IsNullOrWhiteSpace(term))
		{
			return result;
		}
		string requestUri = "https://steamcommunity.com/actions/SearchApps/" + Uri.EscapeDataString(term);
		try
		{
			using JsonDocument jsonDocument = JsonDocument.Parse(await Http.GetStringAsync(requestUri, ct).ConfigureAwait(continueOnCapturedContext: false));
			if (jsonDocument.RootElement.ValueKind != JsonValueKind.Array)
			{
				return result;
			}
			foreach (JsonElement item in jsonDocument.RootElement.EnumerateArray())
			{
				if (item.TryGetProperty("appid", out var value) && item.TryGetProperty("name", out var value2) && int.TryParse((value.ValueKind == JsonValueKind.String) ? value.GetString() : value.GetRawText(), out var result2))
				{
					result.Add((result2, value2.GetString() ?? ""));
				}
			}
		}
		catch (Exception ex)
		{
			AppPaths.Log("SearchCommunity failed: " + ex.Message);
		}
		return result;
	}

	public static async Task<(int appId, string name, double score)?> IdentifyAsync(string title, CancellationToken ct)
	{
		string cleaned = Scanner.CleanTitle(title);
		if (cleaned.Length < 2)
		{
			return null;
		}
		List<string> list = new List<string> { cleaned };
		string text = string.Join(" ", cleaned.Split(' ').Take(5));
		if (text != cleaned)
		{
			list.Add(text);
		}
		foreach (string item in list)
		{
			List<(int appId, string name)> list2 = await SearchSteamAsync(item, ct).ConfigureAwait(continueOnCapturedContext: false);
			if (list2.Count != 0)
			{
				(int, string, double) value = (from r in list2
					select (appId: r.appId, name: r.name, score: Similarity(cleaned, r.name)) into x
					orderby x.score descending
					select x).First();
				if (value.Item3 >= 0.45)
				{
					return value;
				}
			}
		}
		return null;
	}

	public static async Task<List<ArtCandidate>> SteamCandidatesAsync(int appId, string name, CancellationToken ct)
	{
		List<ArtCandidate> list = new List<ArtCandidate>
		{
			new ArtCandidate
			{
				Url = $"{"https://cdn.cloudflare.steamstatic.com/steam/apps/"}{appId}/library_600x900.jpg",
				Source = "steam",
				Label = name + " · poster",
				Portrait = true,
				AppId = appId,
				Width = 600,
				Height = 900
			},
			new ArtCandidate
			{
				Url = $"{"https://cdn.cloudflare.steamstatic.com/steam/apps/"}{appId}/library_600x900_2x.jpg",
				Source = "steam",
				Label = name + " · poster 2x",
				Portrait = true,
				AppId = appId,
				Width = 600,
				Height = 900
			},
			new ArtCandidate
			{
				Url = $"{"https://cdn.cloudflare.steamstatic.com/steam/apps/"}{appId}/header.jpg",
				Source = "steam",
				Label = name + " · header",
				Portrait = false,
				AppId = appId,
				Width = 460,
				Height = 215
			},
			new ArtCandidate
			{
				Url = $"{"https://cdn.cloudflare.steamstatic.com/steam/apps/"}{appId}/capsule_616x353.jpg",
				Source = "steam",
				Label = name + " · capsule",
				Portrait = false,
				AppId = appId,
				Width = 616,
				Height = 353
			}
		};
		await Task.CompletedTask;
		return list;
	}

	public static async Task<List<ArtCandidate>> SteamGridDbCandidatesAsync(string query, string apiKey, CancellationToken ct)
	{
		List<ArtCandidate> list = new List<ArtCandidate>();
		if (string.IsNullOrWhiteSpace(apiKey))
		{
			return list;
		}
		try
		{
			using HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Get, "https://www.steamgriddb.com/api/v2/search/autocomplete/" + Uri.EscapeDataString(query));
			req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey.Trim());
			using HttpResponseMessage resp = await Http.SendAsync(req, ct).ConfigureAwait(continueOnCapturedContext: false);
			if (!resp.IsSuccessStatusCode)
			{
				return list;
			}
			using JsonDocument doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(continueOnCapturedContext: false));
			if (!doc.RootElement.TryGetProperty("data", out var value) || value.ValueKind != JsonValueKind.Array)
			{
				return list;
			}
			foreach (JsonElement item in value.EnumerateArray().Take(1))
			{
				if (!item.TryGetProperty("id", out var value2))
				{
					continue;
				}
				JsonElement value3;
				string gameName = (item.TryGetProperty("name", out value3) ? (value3.GetString() ?? query) : query);
				using HttpRequestMessage gridReq = new HttpRequestMessage(HttpMethod.Get, "https://www.steamgriddb.com/api/v2/" + $"grids/game/{value2.GetInt32()}?dimensions=600x900,342x482,460x215&types=static&nsfw=false&humor=false");
				gridReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey.Trim());
				using HttpResponseMessage gridResp = await Http.SendAsync(gridReq, ct).ConfigureAwait(continueOnCapturedContext: false);
				if (!gridResp.IsSuccessStatusCode)
				{
					goto end_IL_0385;
				}
				using (JsonDocument jsonDocument = JsonDocument.Parse(await gridResp.Content.ReadAsStringAsync(ct).ConfigureAwait(continueOnCapturedContext: false)))
				{
					if (!jsonDocument.RootElement.TryGetProperty("data", out var value4) || value4.ValueKind != JsonValueKind.Array)
					{
						continue;
					}
					foreach (JsonElement item2 in value4.EnumerateArray())
					{
						if (item2.TryGetProperty("url", out var value5))
						{
							JsonElement value6;
							int num = (item2.TryGetProperty("width", out value6) ? value6.GetInt32() : 0);
							JsonElement value7;
							int num2 = (item2.TryGetProperty("height", out value7) ? value7.GetInt32() : 0);
							list.Add(new ArtCandidate
							{
								Url = (value5.GetString() ?? ""),
								Source = "steamgriddb",
								Label = gameName,
								Portrait = (num2 > num),
								Width = num,
								Height = num2
							});
							if (list.Count >= 20)
							{
								break;
							}
						}
					}
					continue;
				}
				end_IL_0385:;
			}
		}
		catch (Exception ex)
		{
			AppPaths.Log("SteamGridDB failed: " + ex.Message);
		}
		return list;
	}

	public static async Task<List<ArtCandidate>> SearchAsync(string query, string? sgdbKey, CancellationToken ct)
	{
		List<ArtCandidate> result = new List<ArtCandidate>();
		if (!string.IsNullOrWhiteSpace(sgdbKey))
		{
			string query2 = Scanner.CleanTitle(query);
			List<ArtCandidate> list = result;
			list.AddRange(await SteamGridDbCandidatesAsync(query2, sgdbKey, ct).ConfigureAwait(continueOnCapturedContext: false));
		}
		foreach (var item3 in (await SearchSteamAsync(Scanner.CleanTitle(query), ct).ConfigureAwait(continueOnCapturedContext: false)).Take(3))
		{
			int item = item3.Item1;
			string item2 = item3.Item2;
			List<ArtCandidate> list = result;
			list.AddRange(await SteamCandidatesAsync(item, item2, ct).ConfigureAwait(continueOnCapturedContext: false));
		}
		return (from c in result
			where !string.IsNullOrWhiteSpace(c.Url)
			group c by c.Url into g
			select g.First() into c
			orderby c.Portrait descending, (!(c.Source == "steamgriddb")) ? 1 : 0
			select c).ToList();
	}

	public static async Task<(string? path, bool portrait)> DirectAsync(int appId, string gameId, CancellationToken ct)
	{
		foreach (ArtCandidate candidate in await SteamCandidatesAsync(appId, "", ct).ConfigureAwait(continueOnCapturedContext: false))
		{
			if (ct.IsCancellationRequested)
			{
				return (path: null, portrait: false);
			}
			byte[] array = await FetchBytesAsync(candidate.Url, ct).ConfigureAwait(continueOnCapturedContext: false);
			if (array != null)
			{
				string text = SaveCover(gameId, array, candidate.Portrait);
				if (text != null)
				{
					return (path: text, portrait: candidate.Portrait);
				}
			}
		}
		return (path: null, portrait: false);
	}

	public static async Task<byte[]?> FetchBytesAsync(string url, CancellationToken ct = default(CancellationToken))
	{
		_ = 1;
		try
		{
			using HttpResponseMessage resp = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(continueOnCapturedContext: false);
			if (!resp.IsSuccessStatusCode)
			{
				return null;
			}
			byte[] array = await resp.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(continueOnCapturedContext: false);
			return (array.Length < 1200) ? null : array;
		}
		catch
		{
			return null;
		}
	}

	public static async Task<(string? path, bool portrait, int? appId, string? matchedName)> AutoArtworkAsync(string title, string? sgdbKey, string gameId, CancellationToken ct)
	{
		if (!string.IsNullOrWhiteSpace(sgdbKey))
		{
			List<ArtCandidate> source = await SteamGridDbCandidatesAsync(Scanner.CleanTitle(title), sgdbKey, ct).ConfigureAwait(continueOnCapturedContext: false);
			ArtCandidate portrait3 = source.FirstOrDefault((ArtCandidate c) => c.Portrait) ?? source.FirstOrDefault();
			if (portrait3 != null)
			{
				byte[] array = await FetchBytesAsync(portrait3.Url, ct).ConfigureAwait(continueOnCapturedContext: false);
				if (array != null)
				{
					string text = SaveCover(gameId, array, portrait3.Portrait);
					if (text != null)
					{
						return (path: text, portrait: portrait3.Portrait, appId: null, matchedName: null);
					}
				}
			}
		}
		(int appId, string name, double score)? match = await IdentifyAsync(title, ct).ConfigureAwait(continueOnCapturedContext: false);
		if (!match.HasValue)
		{
			return (path: null, portrait: false, appId: null, matchedName: null);
		}
		foreach (ArtCandidate portrait3 in await SteamCandidatesAsync(match.Value.appId, match.Value.name, ct).ConfigureAwait(continueOnCapturedContext: false))
		{
			byte[] array2 = await FetchBytesAsync(portrait3.Url, ct).ConfigureAwait(continueOnCapturedContext: false);
			if (array2 != null)
			{
				string text2 = SaveCover(gameId, array2, portrait3.Portrait);
				if (text2 != null)
				{
					return (path: text2, portrait: portrait3.Portrait, appId: match.Value.appId, matchedName: match.Value.name);
				}
			}
		}
		return (path: null, portrait: false, appId: match.Value.appId, matchedName: match.Value.name);
	}

	public static string? SaveCover(string gameId, byte[] data, bool portrait)
	{
		try
		{
			foreach (string item in Directory.EnumerateFiles(AppPaths.CoversDir, gameId + "_*"))
			{
				try
				{
					File.Delete(item);
				}
				catch
				{
				}
			}
			string text = System.IO.Path.Combine(AppPaths.CoversDir, gameId + "_" + (portrait ? "p" : "w") + ".img");
			File.WriteAllBytes(text, data);
			return text;
		}
		catch (Exception ex)
		{
			AppPaths.Log("SaveCover failed: " + ex.Message);
			return null;
		}
	}

	public static string? SaveLocalCover(string gameId, string sourceFile, bool portrait)
	{
		try
		{
			byte[] data = File.ReadAllBytes(sourceFile);
			return SaveCover(gameId, data, portrait);
		}
		catch (Exception ex)
		{
			AppPaths.Log("SaveLocalCover failed: " + ex.Message);
			return null;
		}
	}

	public static double Similarity(string a, string b)
	{
		string text = Scanner.Normalize(Scanner.CleanTitle(a));
		string text2 = Scanner.Normalize(b);
		if (text.Length == 0 || text2.Length == 0)
		{
			return 0.0;
		}
		if (text == text2)
		{
			return 1.0;
		}
		string[] collection = Scanner.CleanTitle(a).ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
		string[] collection2 = b.ToLowerInvariant().Split(new char[7] { ' ', ':', '-', '’', '\'', ',', '.' }, StringSplitOptions.RemoveEmptyEntries);
		HashSet<string> first = new HashSet<string>(collection);
		HashSet<string> second = new HashSet<string>(collection2);
		int num = first.Intersect(second).Count();
		int num2 = first.Union(second).Count();
		double num3 = ((num2 == 0) ? 0.0 : ((double)num / (double)num2));
		double val = 1.0 - (double)Levenshtein(text, text2) / (double)Math.Max(text.Length, text2.Length);
		double num4 = 0.55 * num3 + 0.45 * Math.Max(0.0, val);
		if (text2.StartsWith(text, StringComparison.Ordinal) || text.StartsWith(text2, StringComparison.Ordinal))
		{
			num4 += 0.12;
		}
		return Math.Clamp(num4, 0.0, 1.0);
	}

	private static int Levenshtein(string s, string t)
	{
		if (s.Length == 0)
		{
			return t.Length;
		}
		if (t.Length == 0)
		{
			return s.Length;
		}
		int[] array = new int[t.Length + 1];
		int[] array2 = new int[t.Length + 1];
		for (int i = 0; i <= t.Length; i++)
		{
			array[i] = i;
		}
		for (int j = 1; j <= s.Length; j++)
		{
			array2[0] = j;
			for (int k = 1; k <= t.Length; k++)
			{
				int num = ((s[j - 1] != t[k - 1]) ? 1 : 0);
				array2[k] = Math.Min(Math.Min(array2[k - 1] + 1, array[k] + 1), array[k - 1] + num);
			}
			int[] array3 = array2;
			array2 = array;
			array = array3;
		}
		return array[t.Length];
	}
}
