using Ludex;
using Ludex.Services;

string temp = Path.Combine(Path.GetTempPath(), "ludex-steam-probe-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(temp);
int count = 0;
void Check(bool condition, string message) { count++; if (!condition) throw new Exception("FAIL " + message); Console.WriteLine("PASS " + message); }
bool Uri(GameEntry game, string expected) => SteamGameLaunch.TryGetUri(game, out var actual) && actual == expected;
try
{
    var pubg = new GameEntry { Source="steam", FromLauncher=true, ExternalId="578080", SteamAppId=578080, ExePath="" };
    Check(Uri(pubg, "steam://run/578080"), "imported PUBG starts with correct Steam URI even without exe");
    var dota = new GameEntry { Source="Steam", FromLauncher=true, ExternalId="570", SteamAppId=570 };
    Check(Uri(dota, "steam://run/570"), "imported Dota 2 starts with correct Steam URI");
    pubg.SteamAppId=123;
    Check(Uri(pubg, "steam://run/578080"), "artwork id does not override imported id");
    Check(!SteamGameLaunch.TryGetUri(new GameEntry {Source="epic", FromLauncher=true, ExternalId="570", SteamAppId=570}, out _), "other launchers retain direct executable");
    Check(!SteamGameLaunch.TryGetUri(new GameEntry {Source="steam", SteamAppId=570}, out _), "artwork id alone is not trusted");
    Check(!SteamGameLaunch.TryGetUri(new GameEntry {Source="steam", FromLauncher=true, ExternalId="steam://run/570"}, out _), "malformed external id is rejected");
    Check(!SteamGameLaunch.TryGetUri(new GameEntry {Source="steam", FromLauncher=true, ExternalId="0"}, out _), "zero app id is rejected");
    Check(!SteamGameLaunch.TryGetUri(new GameEntry {Source="steam", ExternalId="570"}, out _), "unverified external id is not trusted");
    string apps = Path.Combine(temp, "SteamLibrary", "steamapps");
    string folder = Path.Combine(apps, "common", "dota 2 beta"); Directory.CreateDirectory(folder);
    string manifest = Path.Combine(apps, "appmanifest_570.acf");
    File.WriteAllText(manifest, "\"AppState\" { \"appid\" \"570\" \"StateFlags\" \"4\" \"installdir\" \"dota 2 beta\" }");
    var merged = new GameEntry { Source="steam", FolderPath=folder, SteamAppId=999, FromLauncher=false };
    Check(Uri(merged, "steam://run/570"), "real manifest identifies merged scan, ignoring artwork id");
    merged.Source="manual";
    Check(!SteamGameLaunch.TryGetUri(merged, out _), "manual entries are not redirected merely by folder location");
    merged.Source="steam"; merged.FolderPath=Path.Combine(apps, "common", "different"); Directory.CreateDirectory(merged.FolderPath);
    Check(!SteamGameLaunch.TryGetUri(merged, out _), "different folder is not attributed to manifest");
    merged.FolderPath=folder; File.WriteAllText(manifest, "\"appid\" \"570\" \"StateFlags\" \"2\" \"installdir\" \"dota 2 beta\"");
    Check(!SteamGameLaunch.TryGetUri(merged, out _), "manifest for not-installed game is rejected");
    File.WriteAllText(manifest, "\"appid\" \"570\" \"StateFlags\" \"4\" \"installdir\" \"dota 2 beta\"");
    File.WriteAllText(Path.Combine(apps,"appmanifest_571.acf"), "\"appid\" \"571\" \"StateFlags\" \"4\" \"installdir\" \"dota 2 beta\"");
    Check(!SteamGameLaunch.TryGetUri(merged, out _), "ambiguous manifests do not guess");
    Console.WriteLine($"{count} Steam launch checks passed");
}
finally { Directory.Delete(temp, true); }
