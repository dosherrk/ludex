using Ludex.Services;
if (!OperatingSystem.IsLinux()) throw new Exception("Run this isolated test on Linux with a temporary HOME");
string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
if (!home.Contains("ludex-refresh-test-")) throw new Exception("Use a dedicated temporary HOME");
Environment.SetEnvironmentVariable("XDG_DATA_HOME", Path.Combine(home, "test-data"));
string apps = Path.Combine(home, ".local/share/Steam/steamapps"); Directory.CreateDirectory(apps);
void Install(int id, string name)
{
    string folder = Path.Combine(apps, "common", name); Directory.CreateDirectory(folder);
    File.WriteAllText(Path.Combine(folder, "game.exe"), "test");
    File.WriteAllText(Path.Combine(apps, $"appmanifest_{id}.acf"), $"\"AppState\" {{ \"appid\" \"{id}\" \"name\" \"{name}\" \"installdir\" \"{name}\" \"StateFlags\" \"4\" }}");
}
void Check(bool ok, string message) { if (!ok) throw new Exception("FAIL " + message); Console.WriteLine("PASS " + message); }
Install(1111, "AlphaVoyage");
Check(Launchers.Import(new[] { "steam" }, refresh: true).Count == 1, "initial refresh finds installed game");
Install(2222, "BetaVoyage");
Check(Launchers.Import(new[] { "steam" }).Count == 1, "non-refresh import may use session cache");
var refreshed = Launchers.Import(new[] { "steam" }, refresh: true);
Check(refreshed.Count == 2 && refreshed.Any(x => x.AppId == 2222), "explicit rescan picks up newly installed Steam game");
File.Delete(Path.Combine(apps, "appmanifest_2222.acf"));
Check(Launchers.Import(new[] { "steam" }, refresh: true).All(x => x.AppId != 2222), "refresh no longer reports deleted manifest as installed");
Console.WriteLine("4 launcher refresh checks passed");
