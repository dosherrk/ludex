using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Ludex.Services;

namespace Ludex;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            AppPaths.Log("Unhandled: " + e.ExceptionObject);
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            AppPaths.Log("Unobserved task: " + e.Exception);
            e.SetObserved();
        };

        // консольные переключатели: диагностика обновлений без запуска интерфейса
        if (args.Any(a => a is "--update-check" or "--update-apply" or "--version"))
        {
            RunUpdaterCli(args);
            return;
        }

        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            AppPaths.Log("Fatal: " + ex);
            throw;
        }
    }

    /// <summary>
    /// Ludex --version            — версия
    /// Ludex --update-check       — есть ли новая версия на GitHub
    /// Ludex --update-apply       — скачать и обновиться (окно приложения не открывается)
    /// </summary>
    private static void RunUpdaterCli(string[] args)
    {
        try
        {
            if (args.Contains("--version"))
            {
                Console.WriteLine("Ludex " + Updater.CurrentVersionText);
                return;
            }

            Console.WriteLine($"Ludex {Updater.CurrentVersionText}");
            Console.WriteLine("Источник обновлений: " + Updater.FeedUrl);

            var info = Updater.CheckAsync().GetAwaiter().GetResult();
            if (info == null)
            {
                Console.WriteLine("Обновлений нет: установлена последняя версия.");
                return;
            }

            Console.WriteLine($"Доступна версия {info.Version.ToString(3)} — {info.AssetName} ({info.SizeText})");

            if (!Updater.CanSelfUpdate(out var why))
            {
                Console.WriteLine("Автообновление здесь невозможно: " + why);
                Console.WriteLine("Страница релиза: " + info.PageUrl);
                Environment.ExitCode = 2;
                return;
            }

            if (!args.Contains("--update-apply")) return;

            var progress = new Progress<double>(p => Console.Write($"\rСкачивание: {p * 100:0}%   "));
            var package = Updater.DownloadAsync(info, progress).GetAwaiter().GetResult();
            Console.WriteLine("\nСкачано: " + package);

            Updater.ApplyAndRestart(package, info);
            Console.WriteLine("Помощник обновления запущен: приложение закроется, файлы заменятся, новая версия стартует сама.");
        }
        catch (Exception ex)
        {
            AppPaths.Log("Updater CLI failed: " + ex);
            Console.Error.WriteLine("Ошибка: " + ex.Message);
            Environment.ExitCode = 1;
        }
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
