using System.Reflection;
using Ludex;
using Ludex.Services;

string testParent = Environment.GetEnvironmentVariable("LUDEX_TEST_TEMP_ROOT") ?? Path.GetTempPath();
string temp = Path.Combine(testParent, "ludex-autoscan-test-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(temp);
Environment.SetEnvironmentVariable("XDG_DATA_HOME", Path.Combine(temp, "data"));
string isolatedAppRoot = Path.Combine(temp, "data", "Ludex"); Directory.CreateDirectory(isolatedAppRoot);
typeof(AppPaths).GetField("_root", BindingFlags.NonPublic | BindingFlags.Static)!.SetValue(null, isolatedAppRoot);
string config = Path.Combine(temp, "config"), relocatedDownloads = Path.Combine(temp, "custom-inbox");
Directory.CreateDirectory(config);
Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", config);
if (OperatingSystem.IsLinux()) File.WriteAllText(Path.Combine(config, "user-dirs.dirs"), "XDG_DOWNLOAD_DIR=\"" + relocatedDownloads + "\"\n");
int count = 0;
void Check(bool ok, string message) { count++; if (!ok) throw new Exception("FAIL " + message); Console.WriteLine("PASS " + message); }
void Sparse(string p, long size) { Directory.CreateDirectory(Path.GetDirectoryName(p)!); using var f = File.Create(p); f.SetLength(size); }
string Game(string parent, string title, long bytes = 160L * 1024 * 1024)
{
    string p = Path.Combine(parent, title); Directory.CreateDirectory(p);
    Sparse(Path.Combine(p, title + ".exe"), 3L * 1024 * 1024);
    Sparse(Path.Combine(p, "content.pak"), bytes); return p;
}
const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Static;
var walk = typeof(AutoScanner).GetMethod("Walk", flags)!;
void Walk(string path, AutoScanner.Report report, HashSet<string> seen, int max = 6, IReadOnlyCollection<string>? ignore = null) => walk.Invoke(null, new object?[] { new DirectoryInfo(path), 0, max, "known", report, seen, (Action<string>)(_ => { }), (Func<bool>)(() => false), ignore });
try
{
    string first = Game(Path.Combine(temp, "disk-one"), "OrionJourney");
    string second = Game(Path.Combine(temp, "disk-two"), "OrionJourney");
    var report = new AutoScanner.Report(); var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    Walk(first, report, seen); Walk(second, report, seen);
    Check(report.Hits.Count == 2 && report.Hits.Any(x => x.Folder == first) && report.Hits.Any(x => x.Folder == second), "same title in different locations produces separate hits");
    Walk(first, report, seen);
    Check(report.Hits.Count == 2, "scanning the same folder twice does not create another hit");
    var ignoredReport = new AutoScanner.Report(); Walk(first, ignoredReport, new HashSet<string>(), ignore: new[] { "  ORIONjourney  " });
    Check(ignoredReport.Hits.Count == 0, "auto scan respects case-insensitive user executable exclusions");
    var unrelatedIgnore = new AutoScanner.Report(); Walk(first, unrelatedIgnore, new HashSet<string>(), ignore: new[] { "other-file" });
    Check(unrelatedIgnore.Hits.Count == 1, "unrelated exclusion does not hide a game");
    Check(Scanner.IsUserIgnoredExe(Path.Combine(first, "OrionJourney.exe"), new[] { "jourNEY" }) && !Scanner.IsUserIgnoredExe("game.exe", new[] { "   " }), "user exclusion matching handles names and ignores blank entries");
    string desktop = Path.Combine(temp, "desktop"), downloads = Game(Path.Combine(desktop, "Downloads"), "UnwantedVoyager");
    string neighbor = Game(Path.Combine(desktop, "DownloadsGame"), "WantedVoyager");
    var desktopReport = new AutoScanner.Report(); Walk(desktop, desktopReport, new HashSet<string>());
    Check(!desktopReport.Hits.Any(h => h.Folder == downloads) && desktopReport.Hits.Any(h => h.Folder == neighbor), "auto scan skips Downloads but keeps similarly named neighboring folder");
    Check(ScanPathGuard.IsDownloads(downloads) && !ScanPathGuard.IsDownloads(neighbor), "download protection matches folder boundaries");
    string? knownDownloads = Environment.GetEnvironmentVariable("LUDEX_TEST_KNOWN_DOWNLOADS");
    if (OperatingSystem.IsWindows() && !string.IsNullOrWhiteSpace(knownDownloads))
        Check(ScanPathGuard.IsDownloads(Path.Combine(knownDownloads, "game")), "Windows relocated Downloads known folder is blocked");
    if (OperatingSystem.IsLinux())
    {
        string moved = Game(relocatedDownloads, "MovedVoyager");
        Check(ScanPathGuard.IsDownloads(moved), "XDG redirected downloads path is blocked");
        string link = Path.Combine(temp, "game-link"); Directory.CreateSymbolicLink(link, relocatedDownloads);
        Check(ScanPathGuard.IsDownloads(link), "link to redirected Downloads is blocked");
        var movedReport = new AutoScanner.Report(); Walk(relocatedDownloads, movedReport, new HashSet<string>());
        Check(movedReport.Hits.Count == 0, "auto scan does not enter relocated Downloads root");
    }
    var downloadsReport = new AutoScanner.Report(); Walk(downloads, downloadsReport, new HashSet<string>());
    Check(downloadsReport.Hits.Count == 0, "auto scan rejects an explicitly supplied Downloads root");
    string rankedDir = Game(temp, "NebulaQuest");
    Sparse(Path.Combine(rankedDir, "NebulaConfig.exe"), 8L * 1024 * 1024);
    var detect = typeof(AutoScanner).GetMethod("DetectGame", flags)!;
    var rankedHit = (AutoScanner.Hit?)detect.Invoke(null, new object[] { new DirectoryInfo(rankedDir), "known" });
    Check(rankedHit != null && Path.GetFileName(rankedHit.Exe) == "NebulaQuest.exe" && !rankedHit.NeedsReview, "auto scan prefers matching game executable over larger config");
    string launcherDir = Path.Combine(temp, "space-launcher"); Directory.CreateDirectory(launcherDir);
    File.WriteAllText(Path.Combine(launcherDir, "Game.exe"), "test"); File.WriteAllText(Path.Combine(launcherDir, "GameServerTool.exe"), "test");
    var findExe = typeof(Launchers).GetMethod("FindExe", flags)!;
    string choice = (string)findExe.Invoke(null, new object[] { launcherDir })!;
    Check(Path.GetFileName(choice) == "Game.exe", "launcher import prefers game file over long server-tool name");
    File.Delete(Path.Combine(launcherDir, "Game.exe"));
    Check((string)findExe.Invoke(null, new object[] { launcherDir })! == "", "launcher import does not silently choose auxiliary file alone");
    File.WriteAllText(Path.Combine(launcherDir, "Alpha.exe"), "test"); File.WriteAllText(Path.Combine(launcherDir, "Beta.exe"), "test");
    Check((string)findExe.Invoke(null, new object[] { launcherDir })! == "", "ambiguous launcher import requires manual selection");
    if (OperatingSystem.IsLinux())
    {
        string nativeRoot = Path.Combine(temp, "native-games"), nativeDir = Path.Combine(nativeRoot, "NativeVoyager");
        Directory.CreateDirectory(nativeDir); string image = Path.Combine(nativeDir, "NativeVoyager.AppImage");
        Sparse(image, 160L * 1024 * 1024); File.SetUnixFileMode(image, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        var nativeHit = (AutoScanner.Hit?)detect.Invoke(null, new object[] { new DirectoryInfo(nativeDir), "known" });
        Check(nativeHit?.Exe == image && Scanner.Scan(new[] { nativeRoot }).Any(x => x.ExePath == image), "Linux AppImage is found by automatic and configured-folder scan");
        File.SetUnixFileMode(image, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        Check(detect.Invoke(null, new object[] { new DirectoryInfo(nativeDir), "known" }) == null, "non-executable AppImage is not treated as runnable");
    }
    string unityDir = Path.Combine(temp, "MiniLumen"); Directory.CreateDirectory(Path.Combine(unityDir, "MiniLumen_Data", "Managed"));
    Sparse(Path.Combine(unityDir, "MiniLumen.exe"), 128 * 1024); File.WriteAllText(Path.Combine(unityDir, "UnityPlayer.dll"), "test");
    var unityHit = (AutoScanner.Hit?)detect.Invoke(null, new object[] { new DirectoryInfo(unityDir), "known" });
    Check(unityHit != null && unityHit.NeedsReview, "small Unity fixture is proposed for review, not silently added");
    string godotDir = Path.Combine(temp, "MiniOrbit"); Directory.CreateDirectory(godotDir);
    Sparse(Path.Combine(godotDir, "MiniOrbit.exe"), 128 * 1024); Sparse(Path.Combine(godotDir, "MiniOrbit.pck"), 512 * 1024);
    var godotHit = (AutoScanner.Hit?)detect.Invoke(null, new object[] { new DirectoryInfo(godotDir), "known" });
    Check(godotHit != null && godotHit.NeedsReview, "small matched Godot fixture is proposed for review");
    string unmarked = Game(temp, "TinyUnmarked", 5L * 1024 * 1024);
    Check(detect.Invoke(null, new object[] { new DirectoryInfo(unmarked), "known" }) == null, "small unmarked executable is not automatically treated as a game");
    string depthRoot = Path.Combine(temp, "depth-fixture"), cursor = depthRoot;
    for (int i = 0; i < 8; i++) { cursor = Path.Combine(cursor, "level" + i); Directory.CreateDirectory(cursor); }
    var shortWalk = new AutoScanner.Report(); Walk(depthRoot, shortWalk, new HashSet<string>(), 3);
    var deepWalk = new AutoScanner.Report(); Walk(depthRoot, deepWalk, new HashSet<string>(), 8);
    Check(shortWalk.FoldersVisited == 4 && deepWalk.FoldersVisited == 9, "root-specific depth limits are honored");
    string desktopDeep = Path.Combine(temp, "desktop-deep");
    string unreal = Path.Combine(desktopDeep, "Deep Rock Galactic", "FSD", "Binaries", "Win64");
    Directory.CreateDirectory(Path.Combine(desktopDeep, "Deep Rock Galactic", "FSD", "Content"));
    Sparse(Path.Combine(unreal, "FSD-Win64-Shipping.exe"), 160L * 1024 * 1024);
    var unrealReport = new AutoScanner.Report(); Walk(desktopDeep, unrealReport, new HashSet<string>(), 4);
    Check(unrealReport.Hits.Any(h => h.Exe.EndsWith("FSD-Win64-Shipping.exe")), "depth four still finds desktop Unreal game in Win64");
    string cacheDir = Path.Combine(temp, "data", "Ludex"); Directory.CreateDirectory(cacheDir);
    File.WriteAllText(Path.Combine(cacheDir, "verify.json"), System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, object> {
        ["imaginaryunknownvoyager"] = new { Exists = false, CheckedUtc = DateTime.UtcNow },
        ["confirmedvoyager"] = new { Exists = true, CheckedUtc = DateTime.UtcNow }
    }));
    var verified = await GameVerifier.VerifyAsync(new[] { "ImaginaryUnknownVoyager", "ConfirmedVoyager" });
    Check(verified.Rejected.Contains("ImaginaryUnknownVoyager") && verified.Confirmed.Contains("ConfirmedVoyager"), "cached catalog miss remains distinguishable from confirmed title");
    Console.WriteLine($"{count} autoscan checks passed");
}
finally { try { Directory.Delete(temp, true); } catch { } }
