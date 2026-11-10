using System;
using System.Collections.Generic;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace Ludex.UI;

/// <summary>
/// Значки лаунчеров для панели «Интеграции»: официальные логотипы (Steam, Epic Games,
/// GOG, Ubisoft, EA, Battle.net, Heroic, Lutris) на плитке в фирменном цвете.
/// Логотипы лежат в Assets/logos, вшиты в сборку и не зависят от сети.
/// </summary>
public static class LauncherLogo
{
    private static readonly Dictionary<string, Bitmap?> Cache = new();

    public static Control Create(string key, double size = 32)
    {
        var (from, to) = Palette(key);

        var tile = new Border
        {
            Width = size,
            Height = size,
            CornerRadius = new CornerRadius(size * 0.26),
            ClipToBounds = true,
            VerticalAlignment = VerticalAlignment.Center,
            Background = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(Color.Parse(from), 0),
                    new GradientStop(Color.Parse(to), 1),
                },
            },
        };

        var logo = Load(key);
        if (logo != null)
        {
            tile.Child = new Image
            {
                Source = logo,
                Stretch = Stretch.Uniform,
                Margin = new Thickness(size * 0.07),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
        }
        else
        {
            // запасной вариант, если картинки нет в сборке
            var mark = Ui.T(key.Length >= 2 ? key[..2].ToUpperInvariant() : key.ToUpperInvariant(),
                10, Brushes.White, FontWeight.Bold);
            mark.HorizontalAlignment = HorizontalAlignment.Center;
            mark.VerticalAlignment = VerticalAlignment.Center;
            tile.Child = mark;
        }

        return tile;
    }

    private static Bitmap? Load(string key)
    {
        if (Cache.TryGetValue(key, out var cached)) return cached;

        Bitmap? bitmap = null;
        try
        {
            var uri = new Uri($"avares://Ludex/Assets/logos/{key}.png");
            using var stream = AssetLoader.Open(uri);
            bitmap = new Bitmap(stream);
        }
        catch (Exception ex)
        {
            AppPaths.Log($"logo {key}: {ex.Message}");
        }

        Cache[key] = bitmap;
        return bitmap;
    }

    /// <summary>Фирменные цвета плитки: от светлого к тёмному.</summary>
    private static (string From, string To) Palette(string key) => key switch
    {
        "steam" => ("#2A475E", "#101C27"),
        "epic" => ("#2C2C2C", "#0A0A0A"),
        "gog" => ("#A13BA8", "#5A1B60"),
        "ubisoft" => ("#26262E", "#08080C"),
        "ea" => ("#2B2B2B", "#0B0B0D"),
        "battlenet" => ("#4E95DB", "#1B4C86"),
        "heroic" => ("#7B4BE0", "#3A1E78"),
        "lutris" => ("#FFB04A", "#D97706"),
        _ => ("#3A3A46", "#1A1A22"),
    };
}
