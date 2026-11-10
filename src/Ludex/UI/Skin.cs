using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;

namespace Ludex.UI;

/// <summary>Палитра приложения: тема (тёмная/светлая), акцентный цвет и режим полупрозрачности.</summary>
public static class Skin
{
    public static string ThemeName { get; private set; } = "dark";
    public static string AccentName { get; private set; } = "white";
    public static bool Transparent { get; private set; } = true;

    public static bool IsLight => ThemeName == "light";

    public static Color BgColor = Color.Parse("#121215");
    public static Color PanelColor = Color.Parse("#1A1A1F");
    public static Color CardColor = Color.Parse("#1A1A1F");
    public static Color AccentColor = Color.Parse("#3B9EFF");

    public static IBrush Bg = Brushes.Transparent;
    public static IBrush BgDeep = Brushes.Transparent;
    public static IBrush Panel = Brushes.Transparent;
    public static IBrush PanelAlt = Brushes.Transparent;
    public static IBrush Card = Brushes.Transparent;
    public static IBrush CardHover = Brushes.Transparent;
    /// <summary>Плашка с названием под обложкой — в стеклянном режиме полупрозрачная.</summary>
    public static IBrush CardFooter = Brushes.Transparent;

    /// <summary>Плашка под названием игры — непрозрачная, в цвет фона приложения.</summary>
    public static IBrush CardPlate = Brushes.Transparent;
    public static IBrush CardFooterEdge = Brushes.Transparent;
    public static IBrush Accent = Brushes.Transparent;
    public static IBrush AccentHover = Brushes.Transparent;
    public static IBrush AccentSoft = Brushes.Transparent;
    public static IBrush Text = Brushes.Transparent;
    public static IBrush TextMuted = Brushes.Transparent;
    public static IBrush TextDim = Brushes.Transparent;
    public static IBrush Danger = Brushes.Transparent;
    public static IBrush DangerSoft = Brushes.Transparent;
    public static IBrush Border = Brushes.Transparent;
    public static IBrush Overlay = Brushes.Transparent;
    public static IBrush Shadow = Brushes.Transparent;
    /// <summary>Светлая грань панелей/карточек — даёт ощущение матового стекла.</summary>
    public static IBrush GlassEdge = Brushes.Transparent;
    /// <summary>Мягкий блик по верхней кромке стеклянной панели.</summary>
    public static IBrush GlassSheen = Brushes.Transparent;
    public static IBrush OnAccent = Brushes.White;
    /// <summary>Цвет текста на акцентной кнопке (для белого акцента — тёмный).</summary>
    private static void UpdateOnAccent()
    {
        var lum = (0.2126 * AccentColor.R + 0.7152 * AccentColor.G + 0.0722 * AccentColor.B) / 255.0;
        OnAccent = new SolidColorBrush(lum > 0.6 ? Color.Parse("#0B0B0D") : Colors.White);
    }
    public static IBrush PlaceholderText = Brushes.Transparent;
    public static IBrush PlaceholderTextDim = Brushes.Transparent;
    public static IBrush PlaceholderGradientA = Brushes.Transparent;
    public static BoxShadows CardShadow = default;

    /// <summary>
    /// Быстрая смена только акцентных кистей — для живого предпросмотра при перетаскивании
    /// маркера в палитре. Полный <see cref="Apply"/> перекраивает всю тему и вызывает
    /// пересчёт разметки, из-за чего панель «дёргалась» под курсором.
    /// </summary>
    public static void ApplyAccent(string? accent)
    {
        if (string.IsNullOrWhiteSpace(accent)) return;

        var name = accent.Trim().ToLowerInvariant();
        Color color;

        if (name.StartsWith("#") && Color.TryParse(name, out var custom))
        {
            color = IsLight ? FitForLightTheme(custom) : EnsureReadable(custom);
        }
        else
        {
            var pair = System.Array.Find(Accents, a => a.Id == name);
            if (pair.Id == null) return;
            color = Color.Parse(IsLight ? pair.Light : pair.Dark);
        }

        AccentColor = color;
        Accent = new SolidColorBrush(color);
        AccentHover = new SolidColorBrush(Lighten(color, 0.14));
        AccentSoft = new SolidColorBrush(Alpha(color, 0x38));
    }

    /// <summary>Доступные акцентные палитры: имя -> (тёмная тема, светлая тема).</summary>
    public static readonly (string Id, string Ru, string En, string Dark, string Light)[] Accents =
    {
        ("white", "Белый", "White", "#FFFFFF", "#111114"),
        ("blue", "Синий", "Blue", "#3B9EFF", "#1F7FD6"),
        ("violet", "Фиолетовый", "Violet", "#9B6BFF", "#7C4DFF"),
        ("green", "Зелёный", "Green", "#35C48A", "#1FA472"),
        ("orange", "Оранжевый", "Orange", "#FF9A4D", "#E07B1F"),
        ("pink", "Розовый", "Pink", "#F06292", "#D64A7B"),
        ("red", "Красный", "Red", "#E2574C", "#C8443A"),
        ("turquoise", "Бирюзовый", "Teal", "#2ECFD3", "#12A0A6"),
    };

    static Skin()
    {
        Apply("dark", "white", true);
    }

    private static Color Alpha(Color color, byte alpha) => Color.FromArgb(alpha, color.R, color.G, color.B);

    public static void Apply(string? theme, string? accent = null, bool? transparent = null)
    {
        ThemeName = string.Equals(theme, "light", System.StringComparison.OrdinalIgnoreCase) ? "light" : "dark";
        AccentName = string.IsNullOrWhiteSpace(accent) ? "white" : accent!.Trim().ToLowerInvariant();
        if (transparent.HasValue) Transparent = transparent.Value;

        if (Application.Current is { } app)
        {
            var variant = IsLight ? ThemeVariant.Light : ThemeVariant.Dark;
            if (app.RequestedThemeVariant != variant) app.RequestedThemeVariant = variant;
        }

        // акцент: либо готовый пресет ("blue", "violet", …), либо свой цвет "#RRGGBB" из круга палитры
        if (AccentName.StartsWith("#") && Color.TryParse(AccentName, out var custom))
        {
            var dark = EnsureReadable(custom);
            // на светлой теме акцент-заливка должна быть достаточно тёмной,
            // иначе белая кнопка сливается с панелями
            var light = FitForLightTheme(custom);
            AccentColor = IsLight ? light : dark;
        }
        else
        {
            var accentPair = System.Array.Find(Accents, a => a.Id == AccentName);
            if (accentPair.Id == null)
            {
                AccentName = "white";
                accentPair = Accents[0];
            }

            AccentColor = Color.Parse(IsLight ? accentPair.Light : accentPair.Dark);
        }

        // «немного стеклянный» режим: фон и панели почти плотные,
        // сквозь них едва заметно проступает рабочий стол / обои
        byte bgA = Transparent ? (byte)0xEE : (byte)0xFF;
        byte barA = Transparent ? (byte)0xF2 : (byte)0xFF;
        byte panelA = Transparent ? (byte)0xF4 : (byte)0xFF;
        byte altA = Transparent ? (byte)0xEA : (byte)0xFF;
        // подложка под обложкой почти не видна (её закрывает картинка),
        // а плашка с названием снизу — полупрозрачное стекло
        byte cardA = Transparent ? (byte)0x1A : (byte)0xFF;
        byte footerA = Transparent ? (byte)0xD8 : (byte)0xFF;
        byte hoverA = Transparent ? (byte)0x5C : (byte)0xFF;

        if (IsLight)
        {
            BgColor = Color.Parse("#EDF1F6");
            PanelColor = Color.Parse("#FFFFFF");
            CardColor = Color.Parse("#FFFFFF");

            Bg = new SolidColorBrush(Alpha(BgColor, bgA));
            BgDeep = new SolidColorBrush(Alpha(Color.Parse("#FFFFFF"), barA));
            Panel = new SolidColorBrush(Alpha(PanelColor, panelA));
            PanelAlt = new SolidColorBrush(Alpha(Color.Parse("#E7EDF4"), altA));
            Card = new SolidColorBrush(Alpha(CardColor, cardA));
            CardHover = new SolidColorBrush(Alpha(Color.Parse("#F3F8FD"), Transparent ? (byte)0x99 : (byte)0xF4));
            // плашка-подпись: стекло — мягкий градиент полупрозрачного белого и светлая грань сверху
            CardFooter = Transparent
                ? new LinearGradientBrush
                {
                    StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                    GradientStops =
                    {
                        new GradientStop(Alpha(Color.Parse("#FFFFFF"), 0xE4), 0),
                        new GradientStop(Alpha(Color.Parse("#EDF3FA"), 0xC6), 1),
                    },
                }
                : new SolidColorBrush(Color.Parse("#FFFFFF"));
            CardFooterEdge = new SolidColorBrush(Transparent
                ? Color.Parse("#B3FFFFFF")
                : Color.Parse("#E7EDF4"));
            CardPlate = new SolidColorBrush(Color.Parse("#FFFFFF"));
            GlassEdge = new SolidColorBrush(Color.Parse("#8CFFFFFF"));
            GlassSheen = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(Color.Parse("#59FFFFFF"), 0),
                    new GradientStop(Color.Parse("#00FFFFFF"), 0.45),
                },
            };
            Accent = new SolidColorBrush(AccentColor);
            AccentHover = new SolidColorBrush(Lighten(AccentColor, 0.14));
            AccentSoft = new SolidColorBrush(Alpha(AccentColor, 0x38));
            Text = new SolidColorBrush(Color.Parse("#16202B"));
            TextMuted = new SolidColorBrush(Color.Parse("#4E6072"));
            TextDim = new SolidColorBrush(Color.Parse("#7C8C9C"));
            Danger = new SolidColorBrush(Color.Parse("#C8443A"));
            DangerSoft = new SolidColorBrush(Color.Parse("#2EC8443A"));
            Border = new SolidColorBrush(Color.Parse("#D5DEE8"));
            Overlay = new SolidColorBrush(Color.Parse("#CCF1F5FA"));
            Shadow = new SolidColorBrush(Color.Parse("#2E000000"));
            PlaceholderText = new SolidColorBrush(Color.Parse("#8FA6BC"));
            PlaceholderTextDim = new SolidColorBrush(Color.Parse("#7C8C9C"));
            PlaceholderGradientA = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(Color.Parse("#DCE7F2"), 0),
                    new GradientStop(Color.Parse("#C2D3E6"), 1),
                },
            };
            CardShadow = new BoxShadows(new BoxShadow
            {
                OffsetX = 0,
                OffsetY = 2,
                Blur = 10,
                Color = Color.Parse("#22000000"),
            });
        }
        else
        {
            // «чёрная» тема: почти чистый чёрный с еле заметным холодным оттенком,
            // чтобы совпадать с новой иконкой (чёрный квадрат + белый знак)
            BgColor = Color.Parse("#08090C");
            PanelColor = Color.Parse("#101319");
            CardColor = Color.Parse("#12151B");

            Bg = new SolidColorBrush(Alpha(BgColor, bgA));
            BgDeep = new SolidColorBrush(Alpha(Color.Parse("#0C0C0F"), barA));
            Panel = new SolidColorBrush(Alpha(PanelColor, panelA));
            PanelAlt = new SolidColorBrush(Alpha(Color.Parse("#232329"), altA));
            Card = new SolidColorBrush(Alpha(CardColor, cardA));
            CardHover = new SolidColorBrush(Alpha(Color.Parse("#2C2C34"), Transparent ? (byte)0x70 : (byte)0xF5));
            CardFooter = Transparent
                ? new LinearGradientBrush
                {
                    StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                    GradientStops =
                    {
                        new GradientStop(Alpha(Color.Parse("#1B1B21"), 0xC2), 0),
                        new GradientStop(Alpha(Color.Parse("#0F0F12"), 0xA0), 1),
                    },
                }
                : new SolidColorBrush(Color.Parse("#0C0E13"));
            CardFooterEdge = new SolidColorBrush(Transparent
                ? Color.Parse("#26FFFFFF")
                : Color.Parse("#1C2029"));
            CardPlate = new SolidColorBrush(Color.Parse("#121215"));
            GlassSheen = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(Color.Parse("#14FFFFFF"), 0),
                    new GradientStop(Color.Parse("#00FFFFFF"), 0.45),
                },
            };
            GlassEdge = new SolidColorBrush(Color.Parse("#17FFFFFF"));
            Accent = new SolidColorBrush(AccentColor);
            AccentHover = new SolidColorBrush(Lighten(AccentColor, 0.16));
            AccentSoft = new SolidColorBrush(Alpha(AccentColor, 0x40));
            Text = new SolidColorBrush(Color.Parse("#F7F7F8"));
            TextMuted = new SolidColorBrush(Color.Parse("#A9A9B2"));
            TextDim = new SolidColorBrush(Color.Parse("#76767F"));
            Danger = new SolidColorBrush(Color.Parse("#E2574C"));
            DangerSoft = new SolidColorBrush(Color.Parse("#28E2574C"));
            Border = new SolidColorBrush(Color.Parse("#26262C"));
            Overlay = new SolidColorBrush(Color.Parse("#D9030407"));
            Shadow = new SolidColorBrush(Color.Parse("#7A000000"));
            PlaceholderText = new SolidColorBrush(Color.Parse("#3E3E46"));
            PlaceholderTextDim = new SolidColorBrush(Color.Parse("#85858E"));
            PlaceholderGradientA = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(Color.Parse("#232329"), 0),
                    new GradientStop(Color.Parse("#121215"), 1),
                },
            };
            CardShadow = new BoxShadows(new BoxShadow
            {
                OffsetX = 0,
                OffsetY = 3,
                Blur = 12,
                Color = Color.Parse("#66000000"),
            });
        }

        UpdateOnAccent();
    }

    /// <summary>Притемняет акцент, пока он не станет читаемым на светлой теме.</summary>
    private static Color FitForLightTheme(Color color)
    {
        var current = Darken(color, 0.16);
        var k = 0.16;
        while (Lum(current) > 0.30 && k < 1.2)
        {
            k += 0.04;
            current = Darken(color, k);
        }

        return current;
    }

    private static double Lum(Color c) =>
        (0.2126 * c.R + 0.7152 * c.G + 0.0722 * c.B) / 255.0;

    private static Color Darken(Color color, double amount)
    {
        byte D(byte v) => (byte)System.Math.Clamp(v - 255 * amount, 0, 255);
        return Color.FromArgb(color.A, D(color.R), D(color.G), D(color.B));
    }

    /// <summary>Слишком тёмный свой цвет на тёмной теме не читается — осветляем до разумного минимума.</summary>
    private static Color EnsureReadable(Color color)
    {
        var lum = (0.2126 * color.R + 0.7152 * color.G + 0.0722 * color.B) / 255.0;
        return lum >= 0.42 ? color : Lighten(color, 0.42 - lum);
    }

    /// <summary>«#RRGGBB» для показа пользователю.</summary>
    public static string ColorHex(Color color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}".ToLowerInvariant();

    private static Color Lighten(Color color, double amount)
    {
        byte L(byte v) => (byte)System.Math.Clamp(v + 255 * amount, 0, 255);
        return Color.FromArgb(color.A, L(color.R), L(color.G), L(color.B));
    }

    public static string AccentLabel(string id)
    {
        if (id.StartsWith("#")) return id.ToUpperInvariant();

        var pair = System.Array.Find(Accents, a => a.Id == id);
        if (pair.Id == null) pair = Accents[0];
        return Loc.IsRu ? pair.Ru : pair.En;
    }
}
