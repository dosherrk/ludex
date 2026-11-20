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
                string one = OperatingSystem.IsWindows() ? @"C:\fake\disk-one\game" : "/fake/disk-one/game";
                string two = OperatingSystem.IsWindows() ? @"C:\fake\disk-two\game" : "/fake/disk-two/game";
                var choices = new List<ScanHit> {
                    new() {Title="Unknown One", FolderPath=one, ExePath=Path.Combine(one, "play.exe")},
                    new() {Title="Unknown One", FolderPath=two, ExePath=Path.Combine(two, "play.exe")}
                };
                var settings = Storage.LoadSettings();
                var visible = choices.Where(x => !ReviewIgnorePaths.Contains(settings.IgnoredReviewPaths, x.FolderPath)).ToList();
                var result = await Dialogs.ReviewCandidatesAsync(window, visible, ignored =>
                    ReviewIgnorePaths.Add(settings, ignored.Select(x => x.FolderPath)));
                File.WriteAllText(Environment.GetEnvironmentVariable("REVIEW_RESULT")!, JsonSerializer.Serialize(new {
                    Visible = visible.Select(x => x.FolderPath), Added = result.Select(x => x.FolderPath), Ignored = settings.IgnoredReviewPaths
                }));
                window.Close();
            };
        }
        base.OnFrameworkInitializationCompleted();
    }
}
