using Ludex;
using Ludex.Services;

string temp = Path.Combine(Path.GetTempPath(), "ludex-uninstall-test-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(temp);
int tests = 0;
void Check(bool condition, string message) { tests++; if (!condition) throw new Exception("FAIL: " + message); Console.WriteLine("PASS " + message); }
try
{
    string games = Path.Combine(temp, "Games");
    string game = Path.Combine(games, "Borderlands 3");
    string win64 = Path.Combine(game, "OakGame", "Binaries", "Win64");
    string data = Path.Combine(game, "OakGame", "Content");
    string other = Path.Combine(games, "Other Game");
    Directory.CreateDirectory(win64); Directory.CreateDirectory(data); Directory.CreateDirectory(other);
    string exe = Path.Combine(win64, "Borderlands3.exe");
    File.WriteAllText(exe, "fake exe"); File.WriteAllText(Path.Combine(data, "fake.pak"), "fake game content");
    string neighbor = Path.Combine(other, "neighbor.exe"); File.WriteAllText(neighbor, "do not delete");
    var entry = new GameEntry { Title = "Borderlands 3", FolderPath = win64, ExePath = exe };
    var otherEntry = new GameEntry { Title = "Neighbor", FolderPath = other, ExePath = neighbor };
    string? Suggest(GameEntry g, IEnumerable<GameEntry>? others = null, IEnumerable<string>? roots = null) =>
        Uninstaller.SuggestGameFolder(g, others ?? new[] { g }, roots ?? Array.Empty<string>());
    Check(Suggest(entry, new[] { entry, otherEntry }) == game, "suggest complete game root from title for old Win64 entry");
    Check(Suggest(entry, new[] { entry, otherEntry }, new[] { games }) == game, "game below configured scan root can be suggested");
    Check(Suggest(entry, new[] { entry, otherEntry }, new[] { game }) == null, "configured scan root itself is not auto-selected");
    var renamed = new GameEntry { Title = "Renamed game", FolderPath = win64, ExePath = exe };
    Check(Suggest(renamed) == null, "unmatched title falls back to manual choice");
    var missing = new GameEntry { Title = "Borderlands 3", FolderPath = win64, ExePath = Path.Combine(win64, "missing.exe") };
    Check(Suggest(missing) == null, "missing executable falls back to manual choice");
    var nestedOther = new GameEntry { Title = "Other", FolderPath = data, ExePath = Path.Combine(data, "other.exe") };
    Check(Suggest(entry, new[] { entry, nestedOther }) == null, "suggestion blocked when another registered game is inside");
    var unrealProject = new GameEntry { Title = "OakGame", FolderPath = win64, ExePath = exe };
    Check(Suggest(unrealProject) == null, "Unreal project subfolder is not treated as complete game root");
    string doubled = Path.Combine(temp, "Same Game", "Same Game");
    Directory.CreateDirectory(doubled); string doubledExe = Path.Combine(doubled, "a.exe"); File.WriteAllText(doubledExe, "fake");
    var ambiguous = new GameEntry { Title = "Same Game", FolderPath = doubled, ExePath = doubledExe };
    Check(Suggest(ambiguous) == null, "multiple title matches require manual choice");
    string decorated = Path.Combine(games, "Portal.2 [Repack]"); Directory.CreateDirectory(decorated);
    string decoratedExe = Path.Combine(decorated, "portal2.exe"); File.WriteAllText(decoratedExe, "fake");
    var cleanTitle = new GameEntry { Title = "Portal 2", FolderPath = decorated, ExePath = decoratedExe };
    Check(Suggest(cleanTitle) == decorated, "cleaned folder title is recognized");
    Check(Uninstaller.IsValidGameFolder(game, entry, new[] { entry, otherEntry }, out _), "complete game folder accepted");
    Check(!Uninstaller.IsValidGameFolder(win64, entry, new[] { entry, otherEntry }, out _), "Win64 subfolder blocked");
    Check(!Uninstaller.IsValidGameFolder(games, entry, new[] { entry, otherEntry }, out _), "shared Games folder blocked");
    Check(!Uninstaller.IsValidGameFolder(other, entry, new[] { entry, otherEntry }, out _), "folder without game executable blocked");
    Check(!Uninstaller.IsValidGameFolder(temp, entry, new[] { entry, otherEntry }, out _), "folder containing another library game blocked");
    Check(!Uninstaller.ContainsPath(game, game + "-other/file.exe"), "path prefix cannot impersonate descendant");
    Check(!Uninstaller.IsSafeToDelete(Path.GetPathRoot(game), out _), "drive root blocked");
    Check(!Uninstaller.IsSafeToDelete(AppContext.BaseDirectory, out _), "application binary folder blocked");
    if (!OperatingSystem.IsWindows())
    {
        string link = Path.Combine(temp, "link"); Directory.CreateSymbolicLink(link, game);
        Check(!Uninstaller.IsSafeToDelete(link, out _), "symlink folder blocked");
        Directory.Delete(link);
        string blockedGame = Path.Combine(temp, "NoPermission");
        string lockedDir = Path.Combine(blockedGame, "locked"); Directory.CreateDirectory(lockedDir);
        File.WriteAllText(Path.Combine(lockedDir, "file"), "locked");
        File.SetUnixFileMode(lockedDir, UnixFileMode.UserRead | UnixFileMode.UserExecute);
        var fail = await Uninstaller.RemoveFolderAsync(blockedGame);
        Check(!fail.ok && Directory.Exists(blockedGame), "permission error reports failure and folder remains");
        File.SetUnixFileMode(lockedDir, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }
    var result = await Uninstaller.RemoveFolderAsync(game);
    Check(result.ok && !Directory.Exists(game), "whole selected game folder deleted");
    Check(File.Exists(neighbor), "neighboring game preserved");
    Console.WriteLine($"{tests} checks passed");
}
finally { if (Directory.Exists(temp)) Directory.Delete(temp, recursive: true); }
