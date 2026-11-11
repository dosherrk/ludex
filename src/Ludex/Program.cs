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

namespace Ludex;

internal static class Program
{
	[STAThread]
	public static void Main(string[] args)
	{
		AppDomain.CurrentDomain.UnhandledException += delegate(object _, UnhandledExceptionEventArgs e)
		{
			AppPaths.Log("Unhandled: " + e.ExceptionObject);
		};
		TaskScheduler.UnobservedTaskException += delegate(object? _, UnobservedTaskExceptionEventArgs e)
		{
			AppPaths.Log("Unobserved task: " + e.Exception);
			e.SetObserved();
		};
		if (args.Any(delegate(string a)
		{
			switch (a)
			{
			case "--update-check":
			case "--update-apply":
			case "--version":
				return true;
			default:
				return false;
			}
		}))
		{
			RunUpdaterCli(args);
		}
		else
		{
			if (!SingleInstance.Claim())
			{
				return;
			}
			try
			{
				ClassicDesktopStyleApplicationLifetimeExtensions.StartWithClassicDesktopLifetime(BuildAvaloniaApp(), args, (Action<IClassicDesktopStyleApplicationLifetime>)null);
			}
			catch (Exception ex)
			{
				AppPaths.Log("Fatal: " + ex);
				if (args.All((string a) => !a.StartsWith("-psn", StringComparison.Ordinal)))
				{
					try
					{
						Console.Error.WriteLine("Ludex не запустился: " + ex.Message);
						Console.Error.WriteLine("Журнал: " + AppPaths.LogFile);
						if (!Console.IsErrorRedirected)
						{
							Console.Error.WriteLine("Нажмите Enter, чтобы закрыть окно.");
						}
						if (!Console.IsErrorRedirected)
						{
							Console.ReadLine();
						}
					}
					catch
					{
					}
				}
				throw;
			}
		}
	}

	private static void RunUpdaterCli(string[] args)
	{
		try
		{
			if (args.Contains("--version"))
			{
				Console.WriteLine("Ludex " + Updater.CurrentVersionText);
				return;
			}
			Console.WriteLine("Ludex " + Updater.CurrentVersionText);
			Console.WriteLine("Источник обновлений: " + Updater.FeedUrl);
			UpdateInfo result = Updater.CheckAsync().GetAwaiter().GetResult();
			if (result == null)
			{
				Console.WriteLine("Обновлений нет: установлена последняя версия.");
				return;
			}
			Console.WriteLine($"Доступна версия {result.Version.ToString(3)} — {result.AssetName} ({result.SizeText})");
			if (!Updater.CanSelfUpdate(out string reason))
			{
				Console.WriteLine("Автообновление здесь невозможно: " + reason);
				Console.WriteLine("Страница релиза: " + result.PageUrl);
				Environment.ExitCode = 2;
			}
			else if (args.Contains("--update-apply"))
			{
				Progress<double> progress = new Progress<double>(delegate(double p)
				{
					Console.Write($"\rСкачивание: {p * 100.0:0}%   ");
				});
				string result2 = Updater.DownloadAsync(result, progress).GetAwaiter().GetResult();
				Console.WriteLine("\nСкачано: " + result2);
				Updater.ApplyAndRestart(result2, result);
				Console.WriteLine("Помощник обновления запущен: приложение закроется, файлы заменятся, новая версия стартует сама.");
			}
		}
		catch (Exception ex)
		{
			AppPaths.Log("Updater CLI failed: " + ex);
			Console.Error.WriteLine("Ошибка: " + ex.Message);
			Environment.ExitCode = 1;
		}
	}

	public static AppBuilder BuildAvaloniaApp()
	{
		return LoggingExtensions.LogToTrace(AppBuilderExtension.WithInterFont(AppBuilderDesktopExtensions.UsePlatformDetect(AppBuilder.Configure<App>())), (LogEventLevel)3, Array.Empty<string>());
	}
}
