using System;

namespace Ludex.Services;

/// <summary>
/// Удаление игры средствами её лаунчера. Вместо того чтобы стирать папку руками,
/// Ludex открывает родной деинсталлятор лаунчера (Steam, Epic) — тот сам уберёт файлы,
/// манифесты и записи в своём списке, поэтому игра не вернётся «фантомом».
/// </summary>
public static class LauncherUninstall
{
    /// <summary>Ссылка на удаление в лаунчере или null, если такой возможности нет.</summary>
    public static string? UrlFor(GameEntry game)
    {
        switch (game.Source)
        {
            case "steam" when game.SteamAppId.HasValue:
                return $"steam://uninstall/{game.SteamAppId.Value}";

            case "steam" when int.TryParse(game.ExternalId, out var steamId):
                return $"steam://uninstall/{steamId}";

            case "epic" when !string.IsNullOrWhiteSpace(game.ExternalId):
                return $"com.epicgames.launcher://apps/{Uri.EscapeDataString(game.ExternalId)}?action=uninstall";

            // GOG Galaxy на команду удаления не реагирует — открываем карточку игры
            case "gog" when !string.IsNullOrWhiteSpace(game.ExternalId):
                return $"goggalaxy://openGameView/{Uri.EscapeDataString(game.ExternalId)}";

            default:
                return null;
        }
    }

    /// <summary>Есть ли для игры удаление через лаунчер.</summary>
    public static bool Available(GameEntry game) => UrlFor(game) != null;

    /// <summary>Название лаунчера для текстов интерфейса.</summary>
    public static string LauncherName(string? source) => source switch
    {
        "steam" => "Steam",
        "epic" => "Epic Games",
        "gog" => "GOG Galaxy",
        _ => "лаунчер",
    };
}
