using System.Reflection;
using System.Runtime.CompilerServices;
using Ludex;
using Ludex.Services;
using Ludex.UI;

// Exercise library operations without displaying a window or touching a real profile.
string temp = Path.Combine(Path.GetTempPath(), "ludex-library-probe-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(temp);
Environment.SetEnvironmentVariable("XDG_DATA_HOME", Path.Combine(temp, "data"));
int count = 0;
void Check(bool condition, string message) { count++; if (!condition) throw new Exception("FAIL " + message); Console.WriteLine("PASS " + message); }
try
{
    var window = (MainWindow)RuntimeHelpers.GetUninitializedObject(typeof(MainWindow));
    var games = new List<GameEntry>();
    const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
    typeof(MainWindow).GetField("_games", flags)!.SetValue(window, games);
    typeof(MainWindow).GetField("_settings", flags)!.SetValue(window, new AppSettings());
    object Call(string method, params object[] args) => typeof(MainWindow).GetMethod(method, flags)!.Invoke(window, args)!;
    var offline = new GameEntry { Title = "Offline", FolderPath = Path.Combine(temp, "offline"), ExePath = Path.Combine(temp, "offline", "game.exe"), Source = "steam", FromLauncher = true, Favorite = true, PlayCount = 42 };
    games.Add(offline);
    Call("MergeHits", new List<ScanHit>());
    Check(games.Contains(offline) && offline.Favorite && offline.PlayCount == 42, "empty scan preserves inaccessible game and metadata");
    Call("MergeImported", new List<LauncherGame>());
    Check(games.Contains(offline) && offline.Source == "steam" && offline.FromLauncher, "empty launcher import preserves inaccessible game and source");
    string first = Path.Combine(temp, "disk1", "Portal 2"), second = Path.Combine(temp, "disk2", "Portal 2");
    Directory.CreateDirectory(first); Directory.CreateDirectory(second);
    var one = new GameEntry { Title = "Portal 2", FolderPath = first, ExePath = Path.Combine(first, "game.exe") };
    var two = new GameEntry { Title = "Portal 2", FolderPath = second, ExePath = Path.Combine(second, "game.exe") };
    var duplicate = new GameEntry { Title = "Other name", FolderPath = first + Path.DirectorySeparatorChar, ExePath = one.ExePath, Favorite = true, PlayCount = 12 };
    var sameFolderDifferentExe = new GameEntry { Title = "Another game", FolderPath = first, ExePath = Path.Combine(first, "another.exe") };
    var missingExe = new GameEntry { Title = "Unknown executable", FolderPath = first, ExePath = "" };
    games.AddRange(new[] { one, two, duplicate, sameFolderDifferentExe, missingExe });
    Call("RemoveDuplicates");
    Check(games.Contains(one) && games.Contains(two), "same title on separate disks remains separate");
    Check(games.Contains(sameFolderDifferentExe) && games.Contains(missingExe), "different or unknown executables in same folder are not merged");
    Check(!games.Contains(duplicate) && one.Favorite && one.PlayCount == 12, "same folder and executable deduplicate and preserve metadata");
    var third = Path.Combine(temp, "disk3", "Portal 2");
    Call("MergeImported", new List<LauncherGame> { new() { Title = "Portal 2", Folder = third, Exe = Path.Combine(third, "game.exe"), Launcher = "steam", AppId = 620 } });
    Check(games.Any(g => g.FolderPath == third && g.Source == "steam") && games.Contains(one) && games.Contains(two), "import of same title on another disk creates separate entry");
    Check(games.Contains(offline), "partial import does not remove earlier launcher entries");
    Console.WriteLine($"{count} checks passed");
}
finally { try { Directory.Delete(temp, true); } catch { } }
