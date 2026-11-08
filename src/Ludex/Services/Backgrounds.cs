using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Ludex.Services;

/// <summary>Обои на фон приложения: скачивание по ссылке и хранение локально.</summary>
public static class Backgrounds
{
    public static async Task<(string? path, string? error)> SetFromUrlAsync(string url, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(url)) return (null, "empty");

        var trimmed = url.Trim();
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return (null, "url");
        }

        var (bytes, ext, error) = await ResolveImageAsync(trimmed, uri, ct).ConfigureAwait(false);
        if (bytes == null || ext == null) return (null, error ?? "download");

        try
        {
            Clear();
            var path = Path.Combine(AppPaths.Root, "background" + ext);
            await File.WriteAllBytesAsync(path, bytes, ct).ConfigureAwait(false);
            return (path, null);
        }
        catch (Exception ex)
        {
            AppPaths.Log("Background save failed: " + ex.Message);
            return (null, "save");
        }
    }

    public static void Clear()
    {
        try
        {
            foreach (var file in Directory.EnumerateFiles(AppPaths.Root, "background.*"))
            {
                try { File.Delete(file); } catch { }
            }
        }
        catch
        {
            // ignored
        }
    }

    /// <summary>
    /// Достаём картинку по ссылке: прямая ссылка, либо страница (Pinterest и т. п.),
    /// из которой вытаскиваем адрес изображения. Возвращает байты, расширение и код ошибки.
    /// </summary>
    private static async Task<(byte[]? bytes, string? ext, string? error)> ResolveImageAsync(
        string url, Uri uri, CancellationToken ct)
    {
        var (bytes, finalUri, _) = await FetchAsync(url, ct).ConfigureAwait(false);

        // 1) прямая картинка
        if (bytes != null && SniffExtension(bytes) is { } directExt) return (bytes, directExt, null);

        // 2) Pinterest: ссылки вида /736x/… иногда отдают 403 — пробуем другие размеры
        foreach (var variant in PinterestVariants(url))
        {
            var (variantBytes, _, _) = await FetchAsync(variant, ct).ConfigureAwait(false);
            if (variantBytes != null && SniffExtension(variantBytes) is { } variantExt)
                return (variantBytes, variantExt, null);
        }

        if (bytes == null) return (null, null, "download");

        // 3) по ссылке пришла страница — ищем в ней адрес картинки
        if (!LooksLikeHtml(bytes)) return (null, null, "format");

        var pageUri = finalUri ?? uri;

        // 3a) служебный oEmbed Pinterest (если отдаёт — берём готовый адрес)
        foreach (var oembed in PinterestOEmbedUrls(pageUri))
        {
            var (json, _, _) = await FetchAsync(oembed, ct).ConfigureAwait(false);
            if (json == null) continue;

            var text = Encoding.UTF8.GetString(json);
            var match = JsonImage.Match(text) ?? AnyImage.Match(text);
            if (!match.Success) continue;

            var candidate = System.Net.WebUtility.HtmlDecode(match.Groups.Count > 0 && match.Groups["url"].Success
                ? match.Groups["url"].Value
                : match.Value);

            var (imageBytes, imageExt, _) = await ResolveImageAsync(candidate, new Uri(candidate), ct).ConfigureAwait(false);
            if (imageBytes != null && imageExt != null) return (imageBytes, imageExt, null);
        }

        // 3b) og:image / twitter:image / JSON внутри самой страницы
        var directUrl = ExtractImageUrl(bytes, pageUri);
        if (directUrl == null) return (null, null, "page");

        var (resolvedBytes, resolvedExt, error) =
            string.Equals(directUrl, pageUri.ToString(), StringComparison.OrdinalIgnoreCase)
                ? (null, null, "page")
                : await ResolveImageAsync(directUrl, new Uri(directUrl), ct).ConfigureAwait(false);

        return resolvedBytes != null && resolvedExt != null
            ? (resolvedBytes, resolvedExt, null)
            : (null, null, error ?? "page");
    }

    /// <summary>
    /// Для Pinterest: пробуем типовые размеры (originals, 1200x, 736x, 564x, 474x, 236x)
    /// и разные расширения — у Pinterest превью бывают .jpg, а исходник .png.
    /// </summary>
    private static string[] PinterestVariants(string url)
    {
        if (!url.Contains("pinimg.com", StringComparison.OrdinalIgnoreCase)) return Array.Empty<string>();

        var match = Regex.Match(url, @"^(?<head>https?://[^/]*pinimg\.com/)(?<size>[^/]*)(?<tail>/.*)$",
            RegexOptions.IgnoreCase);
        if (!match.Success) return Array.Empty<string>();

        var head = match.Groups["head"].Value;
        var current = match.Groups["size"].Value;
        var tail = match.Groups["tail"].Value;

        var stem = Regex.Replace(tail, @"\.[A-Za-z0-9]{2,5}$", string.Empty);
        var currentExt = tail.Length > stem.Length ? tail.Substring(stem.Length) : ".jpg";

        var sizes = new[] { "originals", "1200x", "736x", "564x", "474x", "236x" };
        var extensions = new[] { currentExt, ".jpg", ".png", ".webp" };

        var result = new System.Collections.Generic.List<string>();
        foreach (var size in sizes)
        {
            foreach (var ext in extensions)
            {
                var candidate = head + size + stem + ext;
                if (!string.Equals(candidate, url, StringComparison.OrdinalIgnoreCase)) result.Add(candidate);
            }
        }

        return result.ToArray();
    }

    private static string[] PinterestOEmbedUrls(Uri pageUri)
    {
        if (!pageUri.Host.Contains("pinterest.", StringComparison.OrdinalIgnoreCase)) return Array.Empty<string>();

        var canonical = Regex.Match(pageUri.AbsoluteUri, @"https?://[^/]*pinterest\.[a-z.]+/pin/(\d+)", RegexOptions.IgnoreCase);
        var list = new System.Collections.Generic.List<string>
        {
            "https://www.pinterest.com/oembed.json?url=" + Uri.EscapeDataString(pageUri.AbsoluteUri),
        };

        if (canonical.Success)
        {
            var numeric = $"https://www.pinterest.com/pin/{canonical.Groups[1].Value}/";
            list.Insert(0, "https://www.pinterest.com/oembed.json?url=" + Uri.EscapeDataString(numeric));
        }

        return list.ToArray();
    }

    private static bool LooksLikeHtml(byte[] data)
    {
        if (data.Length < 16) return false;

        var head = Encoding.UTF8.GetString(data, 0, Math.Min(512, data.Length)).TrimStart();
        return head.StartsWith("<", StringComparison.Ordinal) ||
               head.Contains("<!doctype", StringComparison.OrdinalIgnoreCase) ||
               head.Contains("<html", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<(byte[]? bytes, Uri? finalUri, string? contentType)> FetchAsync(
        string url, CancellationToken ct)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.TryAddWithoutValidation(
                "Accept", "image/avif,image/webp,image/png,image/jpeg,image/*,*/*;q=0.8");
            request.Headers.TryAddWithoutValidation("Accept-Language", "ru-RU,ru;q=0.9,en;q=0.8");

            // Pinterest/пин-хостинги иногда не отдают картинку без Referer
            if (url.Contains("pinimg.com", StringComparison.OrdinalIgnoreCase))
                request.Headers.TryAddWithoutValidation("Referer", "https://www.pinterest.com/");

            using var resp = await Artwork.Http
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct)
                .ConfigureAwait(false);

            var finalUri = resp.RequestMessage?.RequestUri;
            if (!resp.IsSuccessStatusCode) return (null, finalUri, null);

            var bytes = await resp.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
            return (bytes.Length < 1200 ? null : bytes, finalUri,
                resp.Content.Headers.ContentType?.MediaType);
        }
        catch
        {
            return (null, null, null);
        }
    }

    private static readonly Regex MetaImage = new(
        @"<meta[^>]+(?:property|name)\s*=\s*[""'](?:og:image(?::secure_url)?|twitter:image(?::src)?)[""'][^>]*>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ContentAttr = new(
        @"content\s*=\s*[""'](?<url>[^""']+)[""']",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex JsonImage = new(
        @"""image_?url""\s*:\s*""(?<url>https?://[^""]+)""",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex AnyImage = new(
        @"https?://[^""'\s<>]+?\.(?:jpe?g|png|gif|webp)(?:\?[^""'\s<>]*)?",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>Служебная графика (логотипы, иконки, фавиконы) — обоями быть не может.</summary>
    private static readonly Regex BadImage = new(
        @"favicon|/logo|logo_|sprite|/icon|icon_|badge|emoji|avatar|/webapp/|profile|placeholder",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>Картинка контента Pinterest: i.pinimg.com/&lt;размер&gt;/ab/cd/ef/hash.ext</summary>
    private static readonly Regex PinContentImage = new(
        @"^https?://i\.pinimg\.com/[^/]+/[0-9a-f]{2}/[0-9a-f]{2}/[0-9a-f]{2}/[0-9a-f]+\.[a-z]+",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>Ищем в HTML-странице прямую ссылку на картинку (Pinterest хранит её в og:image).</summary>
    private static string? ExtractImageUrl(byte[] page, Uri baseUri)
    {
        string html;
        try
        {
            html = Encoding.UTF8.GetString(page);
        }
        catch
        {
            return null;
        }

        string? candidate = null;

        foreach (Match meta in MetaImage.Matches(html))
        {
            var content = ContentAttr.Match(meta.Value);
            if (!content.Success) continue;
            candidate = System.Net.WebUtility.HtmlDecode(content.Groups["url"].Value);
            break;
        }

        candidate ??= FirstMatch(JsonImage.Matches(html));

        if (candidate == null || BadImage.IsMatch(candidate))
        {
            // последняя попытка: любая «похожая на картинку» ссылка, без логотипов и иконок.
            // Для Pinterest в первую очередь берём картинку контента с i.pinimg.com.
            var all = AnyImage.Matches(html)
                .Select(m => System.Net.WebUtility.HtmlDecode(m.Value).TrimEnd('.', ',', ')', ';'))
                .Where(u => !BadImage.IsMatch(u))
                .ToList();

            candidate = all.FirstOrDefault(u => PinContentImage.IsMatch(u))
                        ?? all.FirstOrDefault(u => u.Contains("pinimg.com"))
                        ?? all.FirstOrDefault(u => u.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                        ?? all.FirstOrDefault();
        }

        if (string.IsNullOrWhiteSpace(candidate)) return null;

        // Pinterest: 736x — превью, originals — исходник (если он есть)
        if (candidate.Contains("i.pinimg.com") && candidate.Contains("/736x/"))
            candidate = candidate.Replace("/736x/", "/originals/");

        if (Uri.TryCreate(candidate, UriKind.Absolute, out var absolute)) return absolute.ToString();
        return Uri.TryCreate(baseUri, candidate, out var relative) ? relative.ToString() : null;
    }

    private static string? FirstMatch(MatchCollection matches)
    {
        foreach (Match match in matches)
        {
            var url = System.Net.WebUtility.HtmlDecode(match.Groups["url"].Value);
            if (url.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return url;
        }

        return null;
    }

    /// <summary>Определяем формат по «магическим» байтам — расширение в ссылке часто врёт.</summary>
    private static string? SniffExtension(byte[] data)
    {
        if (data.Length < 12) return null;

        if (data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47) return ".png";
        if (data[0] == 0xFF && data[1] == 0xD8) return ".jpg";
        if (data[0] == 'G' && data[1] == 'I' && data[2] == 'F') return ".gif";
        if (data[0] == 'B' && data[1] == 'M') return ".bmp";
        if (data[0] == 'R' && data[1] == 'I' && data[2] == 'F' && data[3] == 'F' &&
            data[8] == 'W' && data[9] == 'E' && data[10] == 'B' && data[11] == 'P') return ".webp";

        return null;
    }
}
