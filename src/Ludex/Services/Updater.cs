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

public sealed class UpdateInfo
{
	public Version Version { get; init; } = new Version(0, 0, 0, 0);


	public string Tag { get; init; } = "";


	public string AssetName { get; init; } = "";


	public string AssetUrl { get; init; } = "";


	public long AssetSize { get; init; }

	public string? Sha256 { get; init; }

	public string PageUrl { get; init; } = "";


	public string? Notes { get; init; }

	public string SizeText
	{
		get
		{
			if (AssetSize <= 0)
			{
				return "";
			}
			return ((double)AssetSize / 1048576.0).ToString("0.#", CultureInfo.InvariantCulture) + " МБ";
		}
	}
}

public sealed class UpdateException : Exception
{
	public UpdateException(string message)
		: base(message)
	{
	}
}

public static class Updater
{
	public const string Repo = "dosherrk/ludex";

	public static string FeedUrl
	{
		get
		{
			string environmentVariable = Environment.GetEnvironmentVariable("LUDEX_UPDATE_FEED");
			if (!string.IsNullOrWhiteSpace(environmentVariable))
			{
				return environmentVariable.Trim();
			}
			return "https://api.github.com/repos/dosherrk/ludex/releases/latest";
		}
	}

	public static Version CurrentVersion
	{
		get
		{
			Assembly assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
			string text = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
			if (!string.IsNullOrWhiteSpace(text))
			{
				int num = text.IndexOf('+');
				if (num > 0)
				{
					text = text.Substring(0, num);
				}
				if (Version.TryParse(text, out Version result))
				{
					return Normalize(result);
				}
			}
			return Normalize(assembly.GetName().Version ?? new Version(1, 0, 0, 0));
		}
	}

	public static string CurrentVersionText => CurrentVersion.ToString(3);

	private static Version Normalize(Version v)
	{
		return new Version(v.Major, (v.Minor > 0) ? v.Minor : 0, (v.Build > 0) ? v.Build : 0, (v.Revision > 0) ? v.Revision : 0);
	}

	private static HttpClient NewClient()
	{
		return new HttpClient
		{
			Timeout = TimeSpan.FromMinutes(10.0),
			DefaultRequestHeaders = 
			{
				UserAgent = 
				{
					new ProductInfoHeaderValue("Ludex-Updater", CurrentVersionText)
				},
				Accept = 
				{
					new MediaTypeWithQualityHeaderValue("application/vnd.github+json")
				}
			}
		};
	}

	public static async Task<UpdateInfo?> CheckAsync(CancellationToken ct = default(CancellationToken))
	{
		using HttpClient http = NewClient();
		string json;
		try
		{
			json = await http.GetStringAsync(FeedUrl, ct).ConfigureAwait(continueOnCapturedContext: false);
		}
		catch (Exception ex) when (((ex is HttpRequestException || ex is TaskCanceledException) ? 1 : 0) != 0)
		{
			throw new UpdateException("Не удалось связаться с GitHub: " + ex.Message);
		}
		using JsonDocument jsonDocument = JsonDocument.Parse(json);
		JsonElement rootElement = jsonDocument.RootElement;
		if (rootElement.TryGetProperty("draft", out var value) && value.ValueKind == JsonValueKind.True)
		{
			return null;
		}
		if (rootElement.TryGetProperty("prerelease", out var value2) && value2.ValueKind == JsonValueKind.True)
		{
			return null;
		}
		JsonElement value3;
		string text = (rootElement.TryGetProperty("tag_name", out value3) ? (value3.GetString() ?? "") : "");
		Version version = ParseVersion(text);
		if (version == null)
		{
			throw new UpdateException("Не удалось разобрать версию в теге релиза: «" + text + "»");
		}
		if (version <= CurrentVersion)
		{
			return null;
		}
		JsonElement value4;
		string pageUrl = (rootElement.TryGetProperty("html_url", out value4) ? (value4.GetString() ?? "") : "");
		JsonElement value5;
		string notes = (rootElement.TryGetProperty("body", out value5) ? value5.GetString() : null);
		if (!rootElement.TryGetProperty("assets", out var value6) || value6.ValueKind != JsonValueKind.Array)
		{
			throw new UpdateException("В релизе нет файлов для скачивания.");
		}
		string[] array = AssetCandidates(version);
		JsonElement? jsonElement = null;
		string assetName = "";
		string[] array2 = array;
		foreach (string value7 in array2)
		{
			foreach (JsonElement item in value6.EnumerateArray())
			{
				if (item.TryGetProperty("name", out var value8))
				{
					string text2 = value8.GetString() ?? "";
					if (text2.Equals(value7, StringComparison.OrdinalIgnoreCase))
					{
						jsonElement = item;
						assetName = text2;
						break;
					}
				}
			}
			if (jsonElement.HasValue)
			{
				break;
			}
		}
		if (!jsonElement.HasValue)
		{
			throw new UpdateException($"В релизе {text} нет файла для этой системы (искали: {string.Join(", ", array)}).");
		}
		JsonElement value9 = jsonElement.Value;
		JsonElement value10;
		string assetUrl = (value9.TryGetProperty("browser_download_url", out value10) ? (value10.GetString() ?? "") : "");
		JsonElement value11;
		long value12;
		long assetSize = ((value9.TryGetProperty("size", out value11) && value11.TryGetInt64(out value12)) ? value12 : 0);
		string sha = null;
		if (value9.TryGetProperty("digest", out var value13) && value13.ValueKind == JsonValueKind.String)
		{
			string text3 = value13.GetString() ?? "";
			if (text3.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
			{
				string text4 = text3;
				sha = text4.Substring(7, text4.Length - 7);
			}
		}
		return new UpdateInfo
		{
			Version = version,
			Tag = text,
			AssetName = assetName,
			AssetUrl = assetUrl,
			AssetSize = assetSize,
			Sha256 = sha,
			PageUrl = pageUrl,
			Notes = notes
		};
	}

	public static string[] AssetCandidates(Version version)
	{
		string text = version.ToString(3);
		_ = RuntimeInformation.OSArchitecture;
		if (OperatingSystem.IsWindows())
		{
			string text2 = "Ludex-Setup-" + text + ".exe";
			string text3 = "Ludex-" + text + "-windows.zip";
			string text4 = "Ludex-" + text + "-portable.exe";
			if (!IsInstalledCopy())
			{
				return new string[3] { text3, text4, text2 };
			}
			return new string[3] { text2, text3, text4 };
		}
		return new string[2]
		{
			"Ludex-" + text + "-linux-x64.tar.gz",
			"Ludex-" + text + "-linux-x64.deb"
		};
	}

	private static Version? ParseVersion(string tag)
	{
		string text = tag.Trim();
		if (text.StartsWith("v", StringComparison.OrdinalIgnoreCase))
		{
			string text2 = text;
			text = text2.Substring(1, text2.Length - 1);
		}
		int num = text.IndexOfAny(new char[2] { '-', '+' });
		if (num > 0)
		{
			text = text.Substring(0, num);
		}
		if (!Version.TryParse(text, out Version result))
		{
			return null;
		}
		return Normalize(result);
	}

	public static async Task<string> DownloadAsync(UpdateInfo info, IProgress<double>? progress = null, CancellationToken ct = default(CancellationToken))
	{
		string text = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ludex-update-" + info.Version.ToString(3));
		Directory.CreateDirectory(text);
		string file = System.IO.Path.Combine(text, info.AssetName);
		try
		{
			if (File.Exists(file) && new FileInfo(file).Length == info.AssetSize && info.AssetSize > 0)
			{
				progress?.Report(1.0);
				return file;
			}
			using HttpClient http = NewClient();
			using HttpResponseMessage resp = await http.GetAsync(info.AssetUrl, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(continueOnCapturedContext: false);
			resp.EnsureSuccessStatusCode();
			long total = resp.Content.Headers.ContentLength ?? info.AssetSize;
			Stream src = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(continueOnCapturedContext: false);
			try
			{
				FileStream dst = File.Create(file);
				try
				{
					byte[] buffer = new byte[81920];
					long done = 0L;
					while (true)
					{
						int num;
						int read = (num = await src.ReadAsync(buffer, ct).ConfigureAwait(continueOnCapturedContext: false));
						if (num <= 0)
						{
							break;
						}
						await dst.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(continueOnCapturedContext: false);
						done += read;
						if (total > 0)
						{
							progress?.Report((double)done / (double)total);
						}
					}
				}
				finally
				{
					if (dst != null)
					{
						await dst.DisposeAsync();
					}
				}
			}
			finally
			{
				if (src != null)
				{
					await src.DisposeAsync();
				}
			}
			progress?.Report(1.0);
			bool flag = !string.IsNullOrEmpty(info.Sha256);
			if (flag)
			{
				flag = !(await Sha256Async(file, ct).ConfigureAwait(continueOnCapturedContext: false)).Equals(info.Sha256, StringComparison.OrdinalIgnoreCase);
			}
			if (flag)
			{
				try
				{
					File.Delete(file);
				}
				catch
				{
				}
				throw new UpdateException("Файл обновления повреждён (не совпала контрольная сумма). Попробуйте ещё раз.");
			}
			return file;
		}
		catch (Exception ex) when (((ex is HttpRequestException || ex is IOException || ex is TaskCanceledException) ? 1 : 0) != 0)
		{
			try
			{
				if (File.Exists(file))
				{
					File.Delete(file);
				}
			}
			catch
			{
			}
			throw new UpdateException("Не удалось скачать обновление: " + ex.Message);
		}
	}

	private static async Task<string> Sha256Async(string path, CancellationToken ct)
	{
		FileStream stream = File.OpenRead(path);
		string result;
		try
		{
			result = Convert.ToHexString(await SHA256.HashDataAsync(stream, ct).ConfigureAwait(continueOnCapturedContext: false));
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

	public static void ApplyAndRestart(string packagePath, UpdateInfo info)
	{
		int processId = Environment.ProcessId;
		string text = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ludex-update-" + info.Version.ToString(3));
		Directory.CreateDirectory(text);
		AppPaths.Log("Update: applying " + info.Tag + " from " + packagePath);
		string exe = Environment.ProcessPath ?? throw new UpdateException("Не удалось определить путь к приложению.");
		if (OperatingSystem.IsWindows())
		{
			ApplyWindows(packagePath, info, text, processId, exe);
		}
		else
		{
			ApplyUnix(packagePath, info, text, processId, exe);
		}
	}

	private static void ApplyWindows(string packagePath, UpdateInfo info, string work, int pid, string exe)
	{
		string text = System.IO.Path.GetDirectoryName(exe) ?? work;
		bool num = info.AssetName.Contains("Setup", StringComparison.OrdinalIgnoreCase);
		bool flag = info.AssetName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase);
		bool flag2 = IsWritable(text);
		string text2 = System.IO.Path.Combine(work, "apply.ps1");
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.AppendLine("$ErrorActionPreference = 'Stop'");
		StringBuilder stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder3 = stringBuilder2;
		StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(88, 1, stringBuilder2);
		handler.AppendLiteral("while (Get-Process -Id ");
		handler.AppendFormatted(pid);
		handler.AppendLiteral(" -ErrorAction SilentlyContinue) { Start-Sleep -Milliseconds 300 }");
		stringBuilder3.AppendLine(ref handler);
		stringBuilder.AppendLine("Start-Sleep -Milliseconds 500");
		if (num)
		{
			stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder4 = stringBuilder2;
			handler = new StringBuilder.AppendInterpolatedStringHandler(51, 1, stringBuilder2);
			handler.AppendLiteral("Start-Process -FilePath '");
			handler.AppendFormatted(Escape(packagePath));
			handler.AppendLiteral("' -ArgumentList '/S' -Wait");
			stringBuilder4.AppendLine(ref handler);
			stringBuilder.AppendLine("$inst = (Get-ItemProperty -Path 'HKCU:\\Software\\Ludex' -Name InstallDir -ErrorAction SilentlyContinue).InstallDir");
			stringBuilder.AppendLine("if ($inst -and (Test-Path -LiteralPath (Join-Path $inst 'Ludex.exe'))) { Start-Process -FilePath (Join-Path $inst 'Ludex.exe') }");
			stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder5 = stringBuilder2;
			handler = new StringBuilder.AppendInterpolatedStringHandler(35, 1, stringBuilder2);
			handler.AppendLiteral("else { Start-Process -FilePath '");
			handler.AppendFormatted(Escape(exe));
			handler.AppendLiteral("' }");
			stringBuilder5.AppendLine(ref handler);
		}
		else if (flag2 && !flag)
		{
			stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder6 = stringBuilder2;
			handler = new StringBuilder.AppendInterpolatedStringHandler(9, 1, stringBuilder2);
			handler.AppendLiteral("$zip = '");
			handler.AppendFormatted(Escape(packagePath));
			handler.AppendLiteral("'");
			stringBuilder6.AppendLine(ref handler);
			stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder7 = stringBuilder2;
			handler = new StringBuilder.AppendInterpolatedStringHandler(9, 1, stringBuilder2);
			handler.AppendLiteral("$dst = '");
			handler.AppendFormatted(Escape(text));
			handler.AppendLiteral("'");
			stringBuilder7.AppendLine(ref handler);
			stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder8 = stringBuilder2;
			handler = new StringBuilder.AppendInterpolatedStringHandler(9, 1, stringBuilder2);
			handler.AppendLiteral("$tmp = '");
			handler.AppendFormatted(Escape(System.IO.Path.Combine(work, "unpack")));
			handler.AppendLiteral("'");
			stringBuilder8.AppendLine(ref handler);
			stringBuilder.AppendLine("Remove-Item -LiteralPath $tmp -Recurse -Force -ErrorAction SilentlyContinue");
			stringBuilder.AppendLine("New-Item -ItemType Directory -Path $tmp | Out-Null");
			stringBuilder.AppendLine("Expand-Archive -LiteralPath $zip -DestinationPath $tmp -Force");
			stringBuilder.AppendLine("$src = Get-ChildItem -LiteralPath $tmp -Directory | Select-Object -First 1");
			stringBuilder.AppendLine("if (-not $src) { throw 'архив обновления пуст' }");
			stringBuilder.AppendLine("Copy-Item -Path (Join-Path $src.FullName '*') -Destination $dst -Recurse -Force");
			stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder9 = stringBuilder2;
			handler = new StringBuilder.AppendInterpolatedStringHandler(26, 1, stringBuilder2);
			handler.AppendLiteral("Start-Process -FilePath '");
			handler.AppendFormatted(Escape(exe));
			handler.AppendLiteral("'");
			stringBuilder9.AppendLine(ref handler);
		}
		else if (flag2)
		{
			stringBuilder.AppendLine("$target = '" + Escape(exe) + "'");
			stringBuilder.AppendLine("$fresh = $target + '.new'");
			stringBuilder.AppendLine("$backup = $target + '.old'");
			stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder10 = stringBuilder2;
			handler = new StringBuilder.AppendInterpolatedStringHandler(52, 1, stringBuilder2);
			handler.AppendLiteral("Copy-Item -LiteralPath '");
			handler.AppendFormatted(Escape(packagePath));
			handler.AppendLiteral("' -Destination $fresh -Force");
			stringBuilder10.AppendLine(ref handler);
			stringBuilder.AppendLine("if (Test-Path -LiteralPath $backup) { Remove-Item -LiteralPath $backup -Force -ErrorAction SilentlyContinue }");
			stringBuilder.AppendLine("Move-Item -LiteralPath $target -Destination $backup -Force");
			stringBuilder.AppendLine("try {");
			stringBuilder.AppendLine("  Move-Item -LiteralPath $fresh -Destination $target -Force");
			stringBuilder.AppendLine("} catch {");
			stringBuilder.AppendLine("  Move-Item -LiteralPath $backup -Destination $target -Force; throw");
			stringBuilder.AppendLine("}");
			stringBuilder.AppendLine("Remove-Item -LiteralPath $backup -Force -ErrorAction SilentlyContinue");
			stringBuilder.AppendLine("Start-Process -FilePath $target");
		}
		else
		{
			string path = System.IO.Path.Combine(work, "apply-elevated.ps1");
			StringBuilder stringBuilder11 = new StringBuilder();
			stringBuilder11.AppendLine("$ErrorActionPreference = 'Stop'");
			stringBuilder2 = stringBuilder11;
			StringBuilder stringBuilder12 = stringBuilder2;
			handler = new StringBuilder.AppendInterpolatedStringHandler(88, 1, stringBuilder2);
			handler.AppendLiteral("while (Get-Process -Id ");
			handler.AppendFormatted(pid);
			handler.AppendLiteral(" -ErrorAction SilentlyContinue) { Start-Sleep -Milliseconds 300 }");
			stringBuilder12.AppendLine(ref handler);
			if (flag)
			{
				stringBuilder2 = stringBuilder11;
				StringBuilder stringBuilder13 = stringBuilder2;
				handler = new StringBuilder.AppendInterpolatedStringHandler(52, 2, stringBuilder2);
				handler.AppendLiteral("Copy-Item -LiteralPath '");
				handler.AppendFormatted(Escape(packagePath));
				handler.AppendLiteral("' -Destination '");
				handler.AppendFormatted(Escape(exe));
				handler.AppendLiteral(".new' -Force");
				stringBuilder13.AppendLine(ref handler);
				stringBuilder2 = stringBuilder11;
				StringBuilder stringBuilder14 = stringBuilder2;
				handler = new StringBuilder.AppendInterpolatedStringHandler(52, 2, stringBuilder2);
				handler.AppendLiteral("Move-Item -LiteralPath '");
				handler.AppendFormatted(Escape(exe));
				handler.AppendLiteral(".new' -Destination '");
				handler.AppendFormatted(Escape(exe));
				handler.AppendLiteral("' -Force");
				stringBuilder14.AppendLine(ref handler);
			}
			else
			{
				stringBuilder2 = stringBuilder11;
				StringBuilder stringBuilder15 = stringBuilder2;
				handler = new StringBuilder.AppendInterpolatedStringHandler(9, 1, stringBuilder2);
				handler.AppendLiteral("$zip = '");
				handler.AppendFormatted(Escape(packagePath));
				handler.AppendLiteral("'");
				stringBuilder15.AppendLine(ref handler);
				stringBuilder2 = stringBuilder11;
				StringBuilder stringBuilder16 = stringBuilder2;
				handler = new StringBuilder.AppendInterpolatedStringHandler(9, 1, stringBuilder2);
				handler.AppendLiteral("$dst = '");
				handler.AppendFormatted(Escape(text));
				handler.AppendLiteral("'");
				stringBuilder16.AppendLine(ref handler);
				stringBuilder2 = stringBuilder11;
				StringBuilder stringBuilder17 = stringBuilder2;
				handler = new StringBuilder.AppendInterpolatedStringHandler(9, 1, stringBuilder2);
				handler.AppendLiteral("$tmp = '");
				handler.AppendFormatted(Escape(System.IO.Path.Combine(work, "unpack")));
				handler.AppendLiteral("'");
				stringBuilder17.AppendLine(ref handler);
				stringBuilder11.AppendLine("Remove-Item -LiteralPath $tmp -Recurse -Force -ErrorAction SilentlyContinue");
				stringBuilder11.AppendLine("New-Item -ItemType Directory -Path $tmp | Out-Null");
				stringBuilder11.AppendLine("Expand-Archive -LiteralPath $zip -DestinationPath $tmp -Force");
				stringBuilder11.AppendLine("$src = Get-ChildItem -LiteralPath $tmp -Directory | Select-Object -First 1");
				stringBuilder11.AppendLine("Copy-Item -Path (Join-Path $src.FullName '*') -Destination $dst -Recurse -Force");
			}
			stringBuilder2 = stringBuilder11;
			StringBuilder stringBuilder18 = stringBuilder2;
			handler = new StringBuilder.AppendInterpolatedStringHandler(26, 1, stringBuilder2);
			handler.AppendLiteral("Start-Process -FilePath '");
			handler.AppendFormatted(Escape(exe));
			handler.AppendLiteral("'");
			stringBuilder18.AppendLine(ref handler);
			File.WriteAllText(path, stringBuilder11.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
			stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder19 = stringBuilder2;
			handler = new StringBuilder.AppendInterpolatedStringHandler(120, 1, stringBuilder2);
			handler.AppendLiteral("Start-Process -FilePath 'powershell' -ArgumentList '-NoProfile','-ExecutionPolicy','Bypass','-File','");
			handler.AppendFormatted(Escape(path));
			handler.AppendLiteral("' -Verb RunAs -Wait");
			stringBuilder19.AppendLine(ref handler);
		}
		File.WriteAllText(text2, stringBuilder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
		LaunchDetached("powershell", "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"" + text2 + "\"");
	}

	private static void ApplyUnix(string packagePath, UpdateInfo info, string work, int pid, string exe)
	{
		string text = System.IO.Path.Combine(work, "apply.sh");
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.AppendLine("#!/bin/sh");
		StringBuilder stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder3 = stringBuilder2;
		StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(46, 1, stringBuilder2);
		handler.AppendLiteral("while kill -0 ");
		handler.AppendFormatted(pid);
		handler.AppendLiteral(" 2>/dev/null; do sleep 0.3; done");
		stringBuilder3.AppendLine(ref handler);
		stringBuilder.AppendLine("sleep 0.4");
		string value = System.IO.Path.GetDirectoryName(exe) ?? work;
		stringBuilder.AppendLine("set -e");
		stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder4 = stringBuilder2;
		handler = new StringBuilder.AppendInterpolatedStringHandler(5, 1, stringBuilder2);
		handler.AppendLiteral("cd '");
		handler.AppendFormatted(work);
		handler.AppendLiteral("'");
		stringBuilder4.AppendLine(ref handler);
		stringBuilder.AppendLine("rm -rf unpack && mkdir -p unpack");
		stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder5 = stringBuilder2;
		handler = new StringBuilder.AppendInterpolatedStringHandler(21, 1, stringBuilder2);
		handler.AppendLiteral("tar -xzf '");
		handler.AppendFormatted(Escape(packagePath));
		handler.AppendLiteral("' -C unpack");
		stringBuilder5.AppendLine(ref handler);
		stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder6 = stringBuilder2;
		handler = new StringBuilder.AppendInterpolatedStringHandler(6, 1, stringBuilder2);
		handler.AppendLiteral("DST='");
		handler.AppendFormatted(value);
		handler.AppendLiteral("'");
		stringBuilder6.AppendLine(ref handler);
		stringBuilder.AppendLine("install_here() {");
		stringBuilder.AppendLine("  if [ -d unpack/Ludex ]; then");
		stringBuilder.AppendLine("    cp -a unpack/Ludex/. \"$DST/\"");
		stringBuilder.AppendLine("  else");
		stringBuilder.AppendLine("    cp -f unpack/Ludex \"$DST/Ludex.new\"");
		stringBuilder.AppendLine("    chmod +x \"$DST/Ludex.new\"");
		stringBuilder.AppendLine("    mv -f \"$DST/Ludex.new\" \"$DST/Ludex\"");
		stringBuilder.AppendLine("  fi");
		stringBuilder.AppendLine("  chmod +x \"$DST/Ludex\" 2>/dev/null || true");
		stringBuilder.AppendLine("  [ -f unpack/ludex.png ] && cp -f unpack/ludex.png \"$DST/ludex.png\" || true");
		stringBuilder.AppendLine("  [ -f unpack/ludex.desktop ] && cp -f unpack/ludex.desktop \"$DST/ludex.desktop\" || true");
		stringBuilder.AppendLine("  [ -f unpack/Ludex/ludex.png ] && cp -f unpack/Ludex/ludex.png \"$DST/ludex.png\" || true");
		stringBuilder.AppendLine("  return 0");
		stringBuilder.AppendLine("}");
		stringBuilder.AppendLine("if [ -w \"$DST\" ]; then");
		stringBuilder.AppendLine("  install_here");
		stringBuilder.AppendLine("elif command -v pkexec >/dev/null 2>&1; then");
		stringBuilder.AppendLine("  pkexec sh -c \"if [ -d '$work/unpack/Ludex' ]; then cp -a '$work/unpack/Ludex/.' '$dir/'; else cp -f '$work/unpack/Ludex' '$dir/Ludex'; fi; chmod +x '$dir/Ludex'\"");
		stringBuilder.AppendLine("else");
		stringBuilder.AppendLine("  exit 2");
		stringBuilder.AppendLine("fi");
		stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder7 = stringBuilder2;
		handler = new StringBuilder.AppendInterpolatedStringHandler(20, 1, stringBuilder2);
		handler.AppendLiteral("'");
		handler.AppendFormatted(exe);
		handler.AppendLiteral("' >/dev/null 2>&1 &");
		stringBuilder7.AppendLine(ref handler);
		File.WriteAllText(text, stringBuilder.ToString().Replace("\r\n", "\n"), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
		LaunchDetached("/bin/sh", "\"" + text + "\"");
	}

	private static bool IsInstalledCopy()
	{
		if (!OperatingSystem.IsWindows())
		{
			return false;
		}
		string processPath = Environment.ProcessPath;
		if (string.IsNullOrWhiteSpace(processPath))
		{
			return false;
		}
		try
		{
			using RegistryKey registryKey = Registry.CurrentUser.OpenSubKey("Software\\Ludex");
			if (!(registryKey?.GetValue("InstallDir") is string text) || string.IsNullOrWhiteSpace(text))
			{
				return false;
			}
			string fullPath = System.IO.Path.GetFullPath(processPath);
			ReadOnlySpan<char> readOnlySpan = System.IO.Path.GetFullPath(text).TrimEnd(System.IO.Path.DirectorySeparatorChar);
			char reference = System.IO.Path.DirectorySeparatorChar;
			string value = string.Concat(readOnlySpan, new ReadOnlySpan<char>(ref reference));
			return fullPath.StartsWith(value, StringComparison.OrdinalIgnoreCase);
		}
		catch
		{
			return false;
		}
	}

	private static void LaunchDetached(string fileName, string arguments)
	{
		Process.Start(new ProcessStartInfo(fileName, arguments)
		{
			UseShellExecute = false,
			CreateNoWindow = true,
			WorkingDirectory = System.IO.Path.GetTempPath()
		});
	}

	public static bool CanSelfUpdate(out string? reason)
	{
		reason = null;
		string processPath = Environment.ProcessPath;
		if (string.IsNullOrWhiteSpace(processPath))
		{
			return true;
		}
		if (OperatingSystem.IsWindows())
		{
			return true;
		}
		string text = System.IO.Path.GetDirectoryName(processPath) ?? "";
		if (IsWritable(text))
		{
			return true;
		}
		if (HasCommand("pkexec"))
		{
			return true;
		}
		reason = "Папка приложения (" + text + ") защищена от записи, а pkexec не найден. Обновитесь вручную: скачайте .deb из релиза и поставьте через apt.";
		return false;
	}

	private static bool HasCommand(string name)
	{
		string[] array = (Environment.GetEnvironmentVariable("PATH") ?? "").Split(System.IO.Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
		foreach (string path in array)
		{
			try
			{
				if (File.Exists(System.IO.Path.Combine(path, name)))
				{
					return true;
				}
			}
			catch
			{
			}
		}
		return false;
	}

	public static bool IsWritable(string dir)
	{
		try
		{
			string path = System.IO.Path.Combine(dir, ".ludex-write-" + Guid.NewGuid().ToString("N").Substring(0, 6));
			File.WriteAllText(path, "1");
			File.Delete(path);
			return true;
		}
		catch
		{
			return false;
		}
	}

	private static string Escape(string path)
	{
		return path.Replace("'", "''");
	}
}
