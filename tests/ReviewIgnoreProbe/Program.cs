using Ludex;
using Ludex.Services;
using System.Text.Json;

string temp = Environment.GetEnvironmentVariable("LUDEX_REVIEW_TEST_ROOT") ?? Path.Combine(Path.GetTempPath(), "ludex-review-ignore-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(Path.Combine(temp, "data"));
Environment.SetEnvironmentVariable("XDG_DATA_HOME", Path.Combine(temp, "data"));
Directory.SetCurrentDirectory(temp);
int count = 0;
void Check(bool ok, string description) { count++; if (!ok) throw new Exception("FAIL " + description); Console.WriteLine("PASS " + description); }
try
{
    Check(Path.GetFullPath(AppPaths.Root).StartsWith(Path.GetFullPath(temp) + Path.DirectorySeparatorChar, StringComparison.Ordinal), "test data is isolated");
    var oldSettings = JsonSerializer.Deserialize<AppSettings>("{\"Language\":\"en\"}")!;
    Check(oldSettings.IgnoredReviewPaths.Count == 0 && oldSettings.Language == "en", "older settings keep defaults");
    string one = Path.Combine(temp, "Disk1", "Game"), two = Path.Combine(temp, "Disk2", "Game");
    var s = new AppSettings { Language = "en", Folders = new List<string> { Path.Combine(temp, "games") }, HiddenGames = new List<string> { "previously-hidden" } };
    Check(ReviewIgnorePaths.Add(s, new[] { one }), "saving selected path succeeds");
    Check(ReviewIgnorePaths.Contains(s.IgnoredReviewPaths, one + Path.DirectorySeparatorChar), "normalized trailing separator matches");
    Check(!ReviewIgnorePaths.Contains(s.IgnoredReviewPaths, two), "same title at another path is not ignored");
    Check(!ReviewIgnorePaths.Contains(s.IgnoredReviewPaths, one + "-other"), "prefix paths are not ignored");
    if (!OperatingSystem.IsWindows()) Check(!ReviewIgnorePaths.Contains(s.IgnoredReviewPaths, one.ToLowerInvariant()), "Linux paths remain case-sensitive");
    Check(ReviewIgnorePaths.Add(s, new[] { one, two }) && s.IgnoredReviewPaths.Count == 2, "duplicates are not recorded and multiple paths work");
    var loaded = Storage.LoadSettings();
    Check(ReviewIgnorePaths.Contains(loaded.IgnoredReviewPaths, one) && ReviewIgnorePaths.Contains(loaded.IgnoredReviewPaths, two), "ignore survives settings reload");
    Check(loaded.Language == "en" && loaded.Folders.SequenceEqual(s.Folders) && loaded.HiddenGames.SequenceEqual(s.HiddenGames), "unrelated settings preserved");
    var before = File.ReadAllText(AppPaths.SettingsFile);
    Check(!ReviewIgnorePaths.Add(loaded, new[] { "relative/path" }) && loaded.IgnoredReviewPaths.Count == 2, "invalid path fails without changes");
    Check(File.ReadAllText(AppPaths.SettingsFile) == before, "invalid path does not rewrite settings");
    string backup = AppPaths.SettingsFile + ".probe-backup";
    File.Move(AppPaths.SettingsFile, backup);
    Directory.CreateDirectory(AppPaths.SettingsFile);
    try
    {
        Check(!ReviewIgnorePaths.Add(loaded, new[] { Path.Combine(temp, "failure") }) && loaded.IgnoredReviewPaths.Count == 2, "save failure rolls back ignored paths");
        Check(!ReviewIgnorePaths.Clear(loaded) && loaded.IgnoredReviewPaths.Count == 2, "reset failure rolls back ignored paths");
        Check(File.ReadAllText(backup) == before, "save failure leaves prior settings intact");
    }
    finally { Directory.Delete(AppPaths.SettingsFile); File.Move(backup, AppPaths.SettingsFile); }
    Check(ReviewIgnorePaths.Clear(loaded) && Storage.LoadSettings().IgnoredReviewPaths.Count == 0, "reset survives restart");
    Check(Storage.LoadSettings().HiddenGames.SequenceEqual(s.HiddenGames), "reset leaves hidden games alone");
    Console.WriteLine($"{count}/{count} passed");
}
finally { try { Directory.Delete(temp, true); } catch { } }
