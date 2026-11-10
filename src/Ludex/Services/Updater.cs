using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Ludex.Services;

/// <summary>Сведения о найденном обновлении.</summary>
public sealed class UpdateInfo
{
    public Version Version { get; init; } = new(0, 0, 0, 0);
    public string Tag { get; init; } = "";
    public string AssetName { get; init; } = "";
    public string AssetUrl { get; init; } = "";
    public long AssetSize { get; init; }
    public string? Sha256 { get; init; }
    public string PageUrl { get; init; } = "";
    public string? Notes { get; init; }

    /// <summary>Сколько это в мегабайтах — для подписи в диалоге.</summary>
    public string SizeText => AssetSize > 0
        ? (AssetSize / 1048576.0).ToString("0.#", CultureInfo.InvariantCulture) + " МБ"
        : "";
}

public sealed class UpdateException : Exception
{
    public UpdateException(string message) : base(message) { }
}

/// <summary>
/// Автообновление через GitHub Releases: проверка последнего релиза, скачивание
/// подходящего под систему файла и замена приложения с перезапуском.
///
/// Замена делается отдельным скриптом-помощником: он ждёт, пока Ludex закроется,
/// подменяет файлы и запускает новую версию. Иначе Windows/Linux не дадут
/// перезаписать работающий исполняемый файл.
/// </summary>
public static class Updater
{
    public const string Repo = "dosherrk/ludex";

    /// <summary>Сюда смотрит проверка обновлений. Переопределяется переменной LUDEX_UPDATE_FEED (для тестов и зеркал).</summary>
    public static string FeedUrl
    {
        get
        {
            var custom = Environment.GetEnvironmentVariable("LUDEX_UPDATE_FEED");
            return string.IsNullOrWhiteSpace(custom)
                ? $"https://api.github.com/repos/{Repo}/releases/latest"
                : custom.Trim();
        }
    }

    public static Version CurrentVersion
    {
        get
        {
            var asm = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();

            var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            if (!string.IsNullOrWhiteSpace(info))
            {
                var cut = info.IndexOf('+');
                if (cut > 0) info = info[..cut];
                if (Version.TryParse(info, out var v)) return Normalize(v);
            }

            return Normalize(asm.GetName().Version ?? new Version(1, 0, 0, 0));
        }
    }

    public static string CurrentVersionText => CurrentVersion.ToString(3);

    private static Version Normalize(Version v) =>
        new(v.Major,
            v.Minor > 0 ? v.Minor : 0,
            v.Build > 0 ? v.Build : 0,
            v.Revision > 0 ? v.Revision : 0);

    private static HttpClient NewClient()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Ludex-Updater", CurrentVersionText));
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return http;
    }

    // ---------------------------------------------------------------- проверка

    /// <summary>Возвращает описание новой версии или null, если обновляться некуда.</summary>
    public static async Task<UpdateInfo?> CheckAsync(CancellationToken ct = default)
    {
        using var http = NewClient();
        string json;
        try
        {
            json = await http.GetStringAsync(FeedUrl, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new UpdateException("Не удалось связаться с GitHub: " + ex.Message);
        }

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (root.TryGetProperty("draft", out var draft) && draft.ValueKind == JsonValueKind.True) return null;
        if (root.TryGetProperty("prerelease", out var pre) && pre.ValueKind == JsonValueKind.True) return null;

        var tag = root.TryGetProperty("tag_name", out var t) ? t.GetString() ?? "" : "";
        var version = ParseVersion(tag);
        if (version == null) throw new UpdateException($"Не удалось разобрать версию в теге релиза: «{tag}»");
        if (version <= CurrentVersion) return null;

        var page = root.TryGetProperty("html_url", out var h) ? h.GetString() ?? "" : "";
        var notes = root.TryGetProperty("body", out var b) ? b.GetString() : null;

        if (!root.TryGetProperty("assets", out var assetsEl) || assetsEl.ValueKind != JsonValueKind.Array)
            throw new UpdateException("В релизе нет файлов для скачивания.");

        var wanted = AssetCandidates(version);
        JsonElement? found = null;
        string assetName = "";

        // идём по приоритету: первый существующий файл выигрывает
        foreach (var candidate in wanted)
        {
            foreach (var asset in assetsEl.EnumerateArray())
            {
                if (!asset.TryGetProperty("name", out var nameEl)) continue;
                var name = nameEl.GetString() ?? "";
                if (!name.Equals(candidate, StringComparison.OrdinalIgnoreCase)) continue;
                found = asset;
                assetName = name;
                break;
            }
            if (found != null) break;
        }

        if (found == null)
            throw new UpdateException(
                $"В релизе {tag} нет файла для этой системы (искали: {string.Join(", ", wanted)}).");

        var asset2 = found.Value;
        var url = asset2.TryGetProperty("browser_download_url", out var u) ? u.GetString() ?? "" : "";
        var size = asset2.TryGetProperty("size", out var s) && s.TryGetInt64(out var sz) ? sz : 0;
        string? digest = null;
        if (asset2.TryGetProperty("digest", out var dg) && dg.ValueKind == JsonValueKind.String)
        {
            var raw = dg.GetString() ?? "";
            if (raw.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase)) digest = raw[7..];
        }

        return new UpdateInfo
        {
            Version = version,
            Tag = tag,
            AssetName = assetName,
            AssetUrl = url,
            AssetSize = size,
            Sha256 = digest,
            PageUrl = page,
            Notes = notes,
        };
    }

    /// <summary>Имена файлов релиза, подходящие текущей системе, в порядке приоритета.</summary>
    public static string[] AssetCandidates(Version version)
    {
        var v = version.ToString(3);
        var arch = RuntimeInformation.OSArchitecture;

        if (OperatingSystem.IsWindows())
        {
            var setup = $"Ludex-Setup-{v}.exe";

            // с 1.17.0 приложение ставится набором файлов, поэтому портативная версия
            // обновляется архивом целиком (раньше хватало подмены одного .exe)
            var zip = $"Ludex-{v}-windows.zip";
            var portable = $"Ludex-{v}-portable.exe";   // запасной вариант для старых релизов

            // у установленной копии гоняем установщик: иначе «тихая» установка обновит
            // копию в AppData, а перезапустится старая
            return IsInstalledCopy()
                ? new[] { setup, zip, portable }
                : new[] { zip, portable, setup };
        }

        if (OperatingSystem.IsMacOS())
        {
            var apple = arch is Architecture.Arm64;
            return apple
                ? new[] { $"Ludex-{v}-macos-arm64.zip", $"Ludex-{v}-macos-arm64.dmg" }
                : new[] { $"Ludex-{v}-macos-x64.zip", $"Ludex-{v}-macos-x64.dmg" };
        }

        // Linux: tar.gz обновляется на месте без root, .deb — запасной вариант
        return new[] { $"Ludex-{v}-linux-x64.tar.gz", $"Ludex-{v}-linux-x64.deb" };
    }

    private static Version? ParseVersion(string tag)
    {
        var s = tag.Trim();
        if (s.StartsWith("v", StringComparison.OrdinalIgnoreCase)) s = s[1..];
        var cut = s.IndexOfAny(new[] { '-', '+' });
        if (cut > 0) s = s[..cut];
        return Version.TryParse(s, out var v) ? Normalize(v) : null;
    }

    // -------------------------------------------------------------- скачивание

    /// <summary>Качает файл обновления в temp и проверяет sha256, если GitHub его отдал.</summary>
    public static async Task<string> DownloadAsync(UpdateInfo info, IProgress<double>? progress = null,
        CancellationToken ct = default)
    {
        var dir = Path.Combine(Path.GetTempPath(), "ludex-update-" + info.Version.ToString(3));
        Directory.CreateDirectory(dir);
        var file = Path.Combine(dir, info.AssetName);

        try
        {
            if (File.Exists(file) && new FileInfo(file).Length == info.AssetSize && info.AssetSize > 0)
            {
                progress?.Report(1);
                return file;
            }

            using var http = NewClient();
            using var resp = await http
                .GetAsync(info.AssetUrl, HttpCompletionOption.ResponseHeadersRead, ct)
                .ConfigureAwait(false);
            resp.EnsureSuccessStatusCode();

            var total = resp.Content.Headers.ContentLength ?? info.AssetSize;

            await using (var src = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false))
            await using (var dst = File.Create(file))
            {
                var buffer = new byte[81920];
                long done = 0;
                int read;
                while ((read = await src.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
                {
                    await dst.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
                    done += read;
                    if (total > 0) progress?.Report((double)done / total);
                }
            }

            progress?.Report(1);

            if (!string.IsNullOrEmpty(info.Sha256))
            {
                var actual = await Sha256Async(file, ct).ConfigureAwait(false);
                if (!actual.Equals(info.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    try { File.Delete(file); } catch { }
                    throw new UpdateException("Файл обновления повреждён (не совпала контрольная сумма). Попробуйте ещё раз.");
                }
            }

            return file;
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or TaskCanceledException)
        {
            try { if (File.Exists(file)) File.Delete(file); } catch { }
            throw new UpdateException("Не удалось скачать обновление: " + ex.Message);
        }
    }

    private static async Task<string> Sha256Async(string path, CancellationToken ct)
    {
        await using var stream = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(stream, ct).ConfigureAwait(false);
        return Convert.ToHexString(hash);
    }

    // ---------------------------------------------------------------- установка

    /// <summary>
    /// Готовит замену файлов и запускает скрипт-помощник. Вызывающий код должен
    /// сразу закрыть приложение: помощник ждёт завершения процесса.
    /// </summary>
    public static void ApplyAndRestart(string packagePath, UpdateInfo info)
    {
        var pid = Environment.ProcessId;
        var work = Path.Combine(Path.GetTempPath(), "ludex-update-" + info.Version.ToString(3));
        Directory.CreateDirectory(work);

        AppPaths.Log($"Update: applying {info.Tag} from {packagePath}");

        var exe = Environment.ProcessPath
                  ?? throw new UpdateException("Не удалось определить путь к приложению.");

        if (OperatingSystem.IsWindows()) ApplyWindows(packagePath, info, work, pid, exe);
        else ApplyUnix(packagePath, info, work, pid, exe, OperatingSystem.IsMacOS());
    }

    private static void ApplyWindows(string packagePath, UpdateInfo info, string work, int pid, string exe)
    {
        var dir = Path.GetDirectoryName(exe) ?? work;
        var isInstaller = info.AssetName.Contains("Setup", StringComparison.OrdinalIgnoreCase);
        var isExe = info.AssetName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase);
        var writable = IsWritable(dir);

        var script = Path.Combine(work, "apply.ps1");
        var sb = new StringBuilder();
        sb.AppendLine("$ErrorActionPreference = 'Stop'");
        sb.AppendLine($"while (Get-Process -Id {pid} -ErrorAction SilentlyContinue) {{ Start-Sleep -Milliseconds 300 }}");
        sb.AppendLine("Start-Sleep -Milliseconds 500");

        if (isInstaller)
        {
            // NSIS-установщик умеет тихую установку и сам обновит Program Files
            sb.AppendLine($"Start-Process -FilePath '{Escape(packagePath)}' -ArgumentList '/S' -Wait");
            sb.AppendLine("$inst = (Get-ItemProperty -Path 'HKCU:\\Software\\Ludex' -Name InstallDir -ErrorAction SilentlyContinue).InstallDir");
            sb.AppendLine("if ($inst -and (Test-Path -LiteralPath (Join-Path $inst 'Ludex.exe'))) { Start-Process -FilePath (Join-Path $inst 'Ludex.exe') }");
            sb.AppendLine($"else {{ Start-Process -FilePath '{Escape(exe)}' }}");
        }
        else if (writable && !isExe)
        {
            // портативная версия: распаковываем архив и накрываем папку приложения файлами
            sb.AppendLine($"$zip = '{Escape(packagePath)}'");
            sb.AppendLine($"$dst = '{Escape(dir)}'");
            sb.AppendLine($"$tmp = '{Escape(Path.Combine(work, "unpack"))}'");
            sb.AppendLine("Remove-Item -LiteralPath $tmp -Recurse -Force -ErrorAction SilentlyContinue");
            sb.AppendLine("New-Item -ItemType Directory -Path $tmp | Out-Null");
            sb.AppendLine("Expand-Archive -LiteralPath $zip -DestinationPath $tmp -Force");
            sb.AppendLine("$src = Get-ChildItem -LiteralPath $tmp -Directory | Select-Object -First 1");
            sb.AppendLine("if (-not $src) { throw 'архив обновления пуст' }");
            sb.AppendLine("Copy-Item -Path (Join-Path $src.FullName '*') -Destination $dst -Recurse -Force");
            sb.AppendLine($"Start-Process -FilePath '{Escape(exe)}'");
        }
        else if (writable)
        {
            // старый формат обновления: подменяем exe рядом с собой
            sb.AppendLine("$target = '" + Escape(exe) + "'");
            sb.AppendLine("$fresh = $target + '.new'");
            sb.AppendLine("$backup = $target + '.old'");
            sb.AppendLine($"Copy-Item -LiteralPath '{Escape(packagePath)}' -Destination $fresh -Force");
            sb.AppendLine("if (Test-Path -LiteralPath $backup) { Remove-Item -LiteralPath $backup -Force -ErrorAction SilentlyContinue }");
            sb.AppendLine("Move-Item -LiteralPath $target -Destination $backup -Force");
            sb.AppendLine("try {");
            sb.AppendLine("  Move-Item -LiteralPath $fresh -Destination $target -Force");
            sb.AppendLine("} catch {");
            // если подмена не удалась — возвращаем рабочий файл на место
            sb.AppendLine("  Move-Item -LiteralPath $backup -Destination $target -Force; throw");
            sb.AppendLine("}");
            sb.AppendLine("Remove-Item -LiteralPath $backup -Force -ErrorAction SilentlyContinue");
            sb.AppendLine("Start-Process -FilePath $target");
        }
        else
        {
            // Program Files без прав: просим UAC и копируем с повышением
            var elevated = Path.Combine(work, "apply-elevated.ps1");
            var inner = new StringBuilder();
            inner.AppendLine("$ErrorActionPreference = 'Stop'");
            inner.AppendLine($"while (Get-Process -Id {pid} -ErrorAction SilentlyContinue) {{ Start-Sleep -Milliseconds 300 }}");
            if (isExe)
            {
                inner.AppendLine($"Copy-Item -LiteralPath '{Escape(packagePath)}' -Destination '{Escape(exe)}.new' -Force");
                inner.AppendLine($"Move-Item -LiteralPath '{Escape(exe)}.new' -Destination '{Escape(exe)}' -Force");
            }
            else
            {
                inner.AppendLine($"$zip = '{Escape(packagePath)}'");
                inner.AppendLine($"$dst = '{Escape(dir)}'");
                inner.AppendLine($"$tmp = '{Escape(Path.Combine(work, "unpack"))}'");
                inner.AppendLine("Remove-Item -LiteralPath $tmp -Recurse -Force -ErrorAction SilentlyContinue");
                inner.AppendLine("New-Item -ItemType Directory -Path $tmp | Out-Null");
                inner.AppendLine("Expand-Archive -LiteralPath $zip -DestinationPath $tmp -Force");
                inner.AppendLine("$src = Get-ChildItem -LiteralPath $tmp -Directory | Select-Object -First 1");
                inner.AppendLine("Copy-Item -Path (Join-Path $src.FullName '*') -Destination $dst -Recurse -Force");
            }
            inner.AppendLine($"Start-Process -FilePath '{Escape(exe)}'");
            File.WriteAllText(elevated, inner.ToString(), new UTF8Encoding(true));

            sb.AppendLine($"Start-Process -FilePath 'powershell' -ArgumentList '-NoProfile','-ExecutionPolicy','Bypass','-File','{Escape(elevated)}' -Verb RunAs -Wait");
        }

        File.WriteAllText(script, sb.ToString(), new UTF8Encoding(true));
        LaunchDetached("powershell", $"-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"{script}\"");
    }

    private static void ApplyUnix(string packagePath, UpdateInfo info, string work, int pid, string exe, bool macos)
    {
        var script = Path.Combine(work, "apply.sh");
        var sb = new StringBuilder();
        sb.AppendLine("#!/bin/sh");
        sb.AppendLine($"while kill -0 {pid} 2>/dev/null; do sleep 0.3; done");
        sb.AppendLine("sleep 0.4");

        if (macos)
        {
            var bundle = MacBundleRoot(exe) ?? throw new UpdateException(
                "Не удалось найти Ludex.app вокруг приложения — обновите вручную из релиза.");

            sb.AppendLine("set -e");
            sb.AppendLine($"WORK='{work}'");
            sb.AppendLine("cd \"$WORK\"");
            sb.AppendLine("rm -rf unpack && mkdir -p unpack");
            sb.AppendLine($"ditto -x -k '{Escape(packagePath)}' unpack");
            sb.AppendLine("NEW=\"\"");
            sb.AppendLine("for a in unpack/*.app; do NEW=\"$a\"; break; done");
            sb.AppendLine("[ -n \"$NEW\" ] || exit 1");
            sb.AppendLine($"TARGET='{bundle}'");
            sb.AppendLine("xattr -dr com.apple.quarantine \"$NEW\" 2>/dev/null || true");
            sb.AppendLine("if [ -w \"$(dirname \"$TARGET\")\" ]; then");
            sb.AppendLine("  rm -rf \"$TARGET\"");
            sb.AppendLine("  cp -R \"$NEW\" \"$TARGET\"");
            sb.AppendLine("else");
            sb.AppendLine("  osascript -e \"do shell script \\\"rm -rf '$TARGET' && cp -R '$WORK/$NEW' '$TARGET'\\\" with administrator privileges\"");
            sb.AppendLine("fi");
            sb.AppendLine("open \"$TARGET\"");
        }
        else
        {
            var dir = Path.GetDirectoryName(exe) ?? work;
            sb.AppendLine("set -e");
            sb.AppendLine($"cd '{work}'");
            sb.AppendLine("rm -rf unpack && mkdir -p unpack");
            sb.AppendLine($"tar -xzf '{Escape(packagePath)}' -C unpack");
            sb.AppendLine($"DST='{dir}'");
            sb.AppendLine("install_here() {");
            // 1.17.0+: в архиве папка Ludex со всем набором файлов; старые релизы — один бинарник
            sb.AppendLine("  if [ -d unpack/Ludex ]; then");
            sb.AppendLine("    cp -a unpack/Ludex/. \"$DST/\"");
            sb.AppendLine("  else");
            sb.AppendLine("    cp -f unpack/Ludex \"$DST/Ludex.new\"");
            sb.AppendLine("    chmod +x \"$DST/Ludex.new\"");
            sb.AppendLine("    mv -f \"$DST/Ludex.new\" \"$DST/Ludex\"");
            sb.AppendLine("  fi");
            sb.AppendLine("  chmod +x \"$DST/Ludex\" 2>/dev/null || true");
            sb.AppendLine("  [ -f unpack/ludex.png ] && cp -f unpack/ludex.png \"$DST/ludex.png\" || true");
            sb.AppendLine("  [ -f unpack/ludex.desktop ] && cp -f unpack/ludex.desktop \"$DST/ludex.desktop\" || true");
            sb.AppendLine("  [ -f unpack/Ludex/ludex.png ] && cp -f unpack/Ludex/ludex.png \"$DST/ludex.png\" || true");
            sb.AppendLine("  return 0");
            sb.AppendLine("}");
            sb.AppendLine("if [ -w \"$DST\" ]; then");
            sb.AppendLine("  install_here");
            sb.AppendLine("elif command -v pkexec >/dev/null 2>&1; then");
            // системная установка в /opt — просим пароль через pkexec
            sb.AppendLine("  pkexec sh -c \"if [ -d '$work/unpack/Ludex' ]; then cp -a '$work/unpack/Ludex/.' '$dir/'; else cp -f '$work/unpack/Ludex' '$dir/Ludex'; fi; chmod +x '$dir/Ludex'\"");
            sb.AppendLine("else");
            sb.AppendLine("  exit 2");
            sb.AppendLine("fi");
            sb.AppendLine($"'{exe}' >/dev/null 2>&1 &");
        }

        File.WriteAllText(script, sb.ToString().Replace("\r\n", "\n"), new UTF8Encoding(false));
        LaunchDetached("/bin/sh", $"\"{script}\"");
    }

    /// <summary>
    /// Это копия, поставленная установщиком (лежит в его InstallDir), — тогда обновляем через Setup.
    /// Портативная копия обновляется подменой файла рядом с собой.
    /// </summary>
    private static bool IsInstalledCopy()
    {
        if (!OperatingSystem.IsWindows()) return false;

        var exe = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(exe)) return false;

        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Ludex");
            if (key?.GetValue("InstallDir") is not string dir || string.IsNullOrWhiteSpace(dir)) return false;

            var full = Path.GetFullPath(exe);
            var root = Path.GetFullPath(dir).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return full.StartsWith(root, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Путь до .app, внутри которого лежит исполняемый файл (macOS).</summary>
    private static string? MacBundleRoot(string exePath)
    {
        var dir = new DirectoryInfo(Path.GetDirectoryName(exePath) ?? "");
        while (dir != null)
        {
            if (dir.Name.EndsWith(".app", StringComparison.OrdinalIgnoreCase)) return dir.FullName;
            dir = dir.Parent;
        }
        return null;
    }

    private static void LaunchDetached(string fileName, string arguments)
    {
        var psi = new ProcessStartInfo(fileName, arguments)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetTempPath(),
        };
        Process.Start(psi);
    }

    /// <summary>
    /// Может ли приложение обновить себя на месте. Если нет — честно говорим
    /// «обновитесь вручную», а не делаем вид, что обновились.
    /// </summary>
    public static bool CanSelfUpdate(out string? reason)
    {
        reason = null;

        var exe = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(exe)) return true; // не смогли определить — пробуем как обычно

        // Windows: портативная копия обновляется рядом, установленная — через NSIS/UAC
        if (OperatingSystem.IsWindows()) return true;

        var dir = Path.GetDirectoryName(exe) ?? "";

        if (OperatingSystem.IsMacOS())
        {
            var bundle = MacBundleRoot(exe);
            var target = bundle != null ? Path.GetDirectoryName(bundle) ?? dir : dir;
            // без прав на папку попросим администратора через osascript
            return IsWritable(target) || HasCommand("osascript");
        }

        if (IsWritable(dir)) return true;
        if (HasCommand("pkexec")) return true;

        reason = $"Папка приложения ({dir}) защищена от записи, а pkexec не найден. " +
                 "Обновитесь вручную: скачайте .deb из релиза и поставьте через apt.";
        return false;
    }

    private static bool HasCommand(string name)
    {
        var path = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                if (File.Exists(Path.Combine(dir, name))) return true;
            }
            catch
            {
                // путь может быть кривым — не наша проблема
            }
        }
        return false;
    }

    /// <summary>Можно ли писать в папку (для выбора способа обновления).</summary>
    public static bool IsWritable(string dir)
    {
        try
        {
            var probe = Path.Combine(dir, ".ludex-write-" + Guid.NewGuid().ToString("N")[..6]);
            File.WriteAllText(probe, "1");
            File.Delete(probe);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string Escape(string path) => path.Replace("'", "''");
}
