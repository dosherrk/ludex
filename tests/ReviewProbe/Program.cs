using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Ludex.Services;
using Ludex.UI;
using System.Text.Json;

AppBuilder.Configure<ReviewApp>().UsePlatformDetect().StartWithClassicDesktopLifetime(args);

public sealed class ReviewApp : Application
{
    public override void Initialize() { RequestedThemeVariant = ThemeVariant.Dark; Styles.Add(new FluentTheme()); }
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = new Window { Title = "Review test owner", Width = 800, Height = 600 };
            desktop.MainWindow = window;
            window.Opened += async (_, _) => {
                var choices = new List<ScanHit> {
                    new() {Title="Unknown One", FolderPath="/fake/disk-one/game", ExePath="/fake/disk-one/game/play.exe"},
                    new() {Title="Unknown One", FolderPath="/fake/disk-two/game", ExePath="/fake/disk-two/game/play.exe"}
                };
                var result = await Dialogs.ReviewCandidatesAsync(window, choices);
                File.WriteAllText(Environment.GetEnvironmentVariable("REVIEW_RESULT")!, JsonSerializer.Serialize(result.Select(x => x.FolderPath)));
                window.Close();
            };
        }
        base.OnFrameworkInitializationCompleted();
    }
}
