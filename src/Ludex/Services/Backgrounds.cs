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

public static class Backgrounds
{
	private static readonly Regex MetaImage = new Regex("<meta[^>]+(?:property|name)\\s*=\\s*[\"'](?:og:image(?::secure_url)?|twitter:image(?::src)?)[\"'][^>]*>", RegexOptions.IgnoreCase | RegexOptions.Compiled);

	private static readonly Regex ContentAttr = new Regex("content\\s*=\\s*[\"'](?<url>[^\"']+)[\"']", RegexOptions.IgnoreCase | RegexOptions.Compiled);

	private static readonly Regex JsonImage = new Regex("\"image_?url\"\\s*:\\s*\"(?<url>https?://[^\"]+)\"", RegexOptions.IgnoreCase | RegexOptions.Compiled);

	private static readonly Regex AnyImage = new Regex("https?://[^\"'\\s<>]+?\\.(?:jpe?g|png|gif|webp)(?:\\?[^\"'\\s<>]*)?", RegexOptions.IgnoreCase | RegexOptions.Compiled);

	private static readonly Regex BadImage = new Regex("favicon|/logo|logo_|sprite|/icon|icon_|badge|emoji|avatar|/webapp/|profile|placeholder", RegexOptions.IgnoreCase | RegexOptions.Compiled);

	private static readonly Regex PinContentImage = new Regex("^https?://i\\.pinimg\\.com/[^/]+/[0-9a-f]{2}/[0-9a-f]{2}/[0-9a-f]{2}/[0-9a-f]+\\.[a-z]+", RegexOptions.IgnoreCase | RegexOptions.Compiled);

	public static async Task<(string? path, string? error)> SetFromUrlAsync(string url, CancellationToken ct = default(CancellationToken))
	{
		if (string.IsNullOrWhiteSpace(url))
		{
			return (path: null, error: "empty");
		}
		string text = url.Trim();
		if (!Uri.TryCreate(text, UriKind.Absolute, out Uri result) || (result.Scheme != Uri.UriSchemeHttp && result.Scheme != Uri.UriSchemeHttps))
		{
			return (path: null, error: "url");
		}
		var (array, text2, text3) = await ResolveImageAsync(text, result, ct).ConfigureAwait(continueOnCapturedContext: false);
		if (array == null || text2 == null)
		{
			return (path: null, error: text3 ?? "download");
		}
		try
		{
			Clear();
			string path = System.IO.Path.Combine(AppPaths.Root, "background" + text2);
			await File.WriteAllBytesAsync(path, array, ct).ConfigureAwait(continueOnCapturedContext: false);
			return (path: path, error: null);
		}
		catch (Exception ex)
		{
			AppPaths.Log("Background save failed: " + ex.Message);
			return (path: null, error: "save");
		}
	}

	public static void Clear()
	{
		try
		{
			foreach (string item in Directory.EnumerateFiles(AppPaths.Root, "background.*"))
			{
				try
				{
					File.Delete(item);
				}
				catch
				{
				}
			}
		}
		catch
		{
		}
	}

	private static async Task<(byte[]? bytes, string? ext, string? error)> ResolveImageAsync(string url, Uri uri, CancellationToken ct)
	{
		var (bytes, finalUri, _) = await FetchAsync(url, ct).ConfigureAwait(continueOnCapturedContext: false);
		if (bytes != null)
		{
			string text = SniffExtension(bytes);
			if (text != null)
			{
				return (bytes: bytes, ext: text, error: null);
			}
		}
		string[] array2 = PinterestVariants(url);
		for (int j = 0; j < array2.Length; j++)
		{
			byte[] item = (await FetchAsync(array2[j], ct).ConfigureAwait(continueOnCapturedContext: false)).Item1;
			if (item != null)
			{
				string text2 = SniffExtension(item);
				if (text2 != null)
				{
					return (bytes: item, ext: text2, error: null);
				}
			}
		}
		if (bytes == null)
		{
			return (bytes: null, ext: null, error: "download");
		}
		if (!LooksLikeHtml(bytes))
		{
			return (bytes: null, ext: null, error: "format");
		}
		Uri pageUri = finalUri ?? uri;
		array2 = PinterestOEmbedUrls(pageUri);
		for (int j = 0; j < array2.Length; j++)
		{
			byte[] item2 = (await FetchAsync(array2[j], ct).ConfigureAwait(continueOnCapturedContext: false)).Item1;
			if (item2 == null)
			{
				continue;
			}
			string @string = Encoding.UTF8.GetString(item2);
			Match match = JsonImage.Match(@string) ?? AnyImage.Match(@string);
			if (match.Success)
			{
				string? text3 = WebUtility.HtmlDecode((match.Groups.Count > 0 && match.Groups["url"].Success) ? match.Groups["url"].Value : match.Value);
				var (array3, text4, _) = await ResolveImageAsync(text3, new Uri(text3), ct).ConfigureAwait(continueOnCapturedContext: false);
				if (array3 != null && text4 != null)
				{
					return (bytes: array3, ext: text4, error: null);
				}
			}
		}
		string text5 = ExtractImageUrl(bytes, pageUri);
		if (text5 == null)
		{
			return (bytes: null, ext: null, error: "page");
		}
		byte[] array4;
		string text6;
		string text7;
		if (!string.Equals(text5, pageUri.ToString(), StringComparison.OrdinalIgnoreCase))
		{
			(array4, text6, text7) = await ResolveImageAsync(text5, new Uri(text5), ct).ConfigureAwait(continueOnCapturedContext: false);
		}
		else
		{
			array4 = null;
			text6 = null;
			text7 = "page";
		}
		return ((byte[]? bytes, string? ext, string? error))((array4 != null && text6 != null) ? (bytes: array4, ext: text6, error: null) : (bytes: null, ext: null, error: text7 ?? "page"));
	}

	private static string[] PinterestVariants(string url)
	{
		if (!url.Contains("pinimg.com", StringComparison.OrdinalIgnoreCase))
		{
			return Array.Empty<string>();
		}
		Match match = Regex.Match(url, "^(?<head>https?://[^/]*pinimg\\.com/)(?<size>[^/]*)(?<tail>/.*)$", RegexOptions.IgnoreCase);
		if (!match.Success)
		{
			return Array.Empty<string>();
		}
		string value = match.Groups["head"].Value;
		_ = match.Groups["size"].Value;
		string value2 = match.Groups["tail"].Value;
		string text = Regex.Replace(value2, "\\.[A-Za-z0-9]{2,5}$", string.Empty);
		string text2 = ((value2.Length > text.Length) ? value2.Substring(text.Length) : ".jpg");
		string[] array = new string[6] { "originals", "1200x", "736x", "564x", "474x", "236x" };
		string[] array2 = new string[4] { text2, ".jpg", ".png", ".webp" };
		List<string> list = new List<string>();
		string[] array3 = array;
		foreach (string text3 in array3)
		{
			string[] array4 = array2;
			foreach (string text4 in array4)
			{
				string text5 = value + text3 + text + text4;
				if (!string.Equals(text5, url, StringComparison.OrdinalIgnoreCase))
				{
					list.Add(text5);
				}
			}
		}
		return list.ToArray();
	}

	private static string[] PinterestOEmbedUrls(Uri pageUri)
	{
		if (!pageUri.Host.Contains("pinterest.", StringComparison.OrdinalIgnoreCase))
		{
			return Array.Empty<string>();
		}
		Match match = Regex.Match(pageUri.AbsoluteUri, "https?://[^/]*pinterest\\.[a-z.]+/pin/(\\d+)", RegexOptions.IgnoreCase);
		List<string> list = new List<string> { "https://www.pinterest.com/oembed.json?url=" + Uri.EscapeDataString(pageUri.AbsoluteUri) };
		if (match.Success)
		{
			string stringToEscape = "https://www.pinterest.com/pin/" + match.Groups[1].Value + "/";
			list.Insert(0, "https://www.pinterest.com/oembed.json?url=" + Uri.EscapeDataString(stringToEscape));
		}
		return list.ToArray();
	}

	private static bool LooksLikeHtml(byte[] data)
	{
		if (data.Length < 16)
		{
			return false;
		}
		string text = Encoding.UTF8.GetString(data, 0, Math.Min(512, data.Length)).TrimStart();
		if (!text.StartsWith("<", StringComparison.Ordinal) && !text.Contains("<!doctype", StringComparison.OrdinalIgnoreCase))
		{
			return text.Contains("<html", StringComparison.OrdinalIgnoreCase);
		}
		return true;
	}

	private static async Task<(byte[]? bytes, Uri? finalUri, string? contentType)> FetchAsync(string url, CancellationToken ct)
	{
		_ = 1;
		try
		{
			using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url);
			request.Headers.TryAddWithoutValidation("Accept", "image/avif,image/webp,image/png,image/jpeg,image/*,*/*;q=0.8");
			request.Headers.TryAddWithoutValidation("Accept-Language", "ru-RU,ru;q=0.9,en;q=0.8");
			if (url.Contains("pinimg.com", StringComparison.OrdinalIgnoreCase))
			{
				request.Headers.TryAddWithoutValidation("Referer", "https://www.pinterest.com/");
			}
			using HttpResponseMessage resp = await Artwork.Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(continueOnCapturedContext: false);
			Uri finalUri = resp.RequestMessage?.RequestUri;
			if (!resp.IsSuccessStatusCode)
			{
				return (bytes: null, finalUri: finalUri, contentType: null);
			}
			byte[] array = await resp.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(continueOnCapturedContext: false);
			return (bytes: (array.Length < 1200) ? null : array, finalUri: finalUri, contentType: resp.Content.Headers.ContentType?.MediaType);
		}
		catch
		{
			return (bytes: null, finalUri: null, contentType: null);
		}
	}

	private static string? ExtractImageUrl(byte[] page, Uri baseUri)
	{
		string @string;
		try
		{
			@string = Encoding.UTF8.GetString(page);
		}
		catch
		{
			return null;
		}
		string text = null;
		foreach (Match item in MetaImage.Matches(@string))
		{
			Match match2 = ContentAttr.Match(item.Value);
			if (match2.Success)
			{
				text = WebUtility.HtmlDecode(match2.Groups["url"].Value);
				break;
			}
		}
		if (text == null)
		{
			text = FirstMatch(JsonImage.Matches(@string));
		}
		if (text == null || BadImage.IsMatch(text))
		{
			List<string> source = (from m in AnyImage.Matches(@string)
				select WebUtility.HtmlDecode(m.Value).TrimEnd('.', ',', ')', ';') into u
				where !BadImage.IsMatch(u)
				select u).ToList();
			text = source.FirstOrDefault((string u) => PinContentImage.IsMatch(u)) ?? source.FirstOrDefault((string u) => u.Contains("pinimg.com")) ?? source.FirstOrDefault((string u) => u.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) ?? source.FirstOrDefault();
		}
		if (string.IsNullOrWhiteSpace(text))
		{
			return null;
		}
		if (text.Contains("i.pinimg.com") && text.Contains("/736x/"))
		{
			text = text.Replace("/736x/", "/originals/");
		}
		if (Uri.TryCreate(text, UriKind.Absolute, out Uri result))
		{
			return result.ToString();
		}
		if (!Uri.TryCreate(baseUri, text, out Uri result2))
		{
			return null;
		}
		return result2.ToString();
	}

	private static string? FirstMatch(MatchCollection matches)
	{
		foreach (Match match in matches)
		{
			string text = WebUtility.HtmlDecode(match.Groups["url"].Value);
			if (text.StartsWith("http", StringComparison.OrdinalIgnoreCase))
			{
				return text;
			}
		}
		return null;
	}

	private static string? SniffExtension(byte[] data)
	{
		if (data.Length < 12)
		{
			return null;
		}
		if (data[0] == 137 && data[1] == 80 && data[2] == 78 && data[3] == 71)
		{
			return ".png";
		}
		if (data[0] == byte.MaxValue && data[1] == 216)
		{
			return ".jpg";
		}
		if (data[0] == 71 && data[1] == 73 && data[2] == 70)
		{
			return ".gif";
		}
		if (data[0] == 66 && data[1] == 77)
		{
			return ".bmp";
		}
		if (data[0] == 82 && data[1] == 73 && data[2] == 70 && data[3] == 70 && data[8] == 87 && data[9] == 69 && data[10] == 66 && data[11] == 80)
		{
			return ".webp";
		}
		return null;
	}
}
