using System;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;

namespace Ludex;

public class App : Application
{
    public override void Initialize()
    {
        RequestedThemeVariant = ThemeVariant.Dark;
        Styles.Add(new FluentTheme());

        // Полосы прокрутки: тонкие, полупрозрачные, плавно «проявляются» при наведении.
        var bar = new Style(x => x.OfType<Avalonia.Controls.Primitives.ScrollBar>())
        {
            Setters =
            {
                new Setter(Avalonia.Layout.Layoutable.WidthProperty, 8.0),
                new Setter(Avalonia.Layout.Layoutable.HeightProperty, 8.0),
                new Setter(Visual.OpacityProperty, 0.45),
                new Setter(Avalonia.Controls.Primitives.TemplatedControl.BackgroundProperty, Avalonia.Media.Brushes.Transparent),
                new Setter(Visual.TransitionsProperty, new Transitions
                {
                    new DoubleTransition
                    {
                        Property = Visual.OpacityProperty,
                        Duration = TimeSpan.FromMilliseconds(190),
                    },
                }),
            },
        };

        var barHover = new Style(x => x.OfType<Avalonia.Controls.Primitives.ScrollBar>().Class(":pointerover"))
        {
            Setters = { new Setter(Visual.OpacityProperty, 1.0) },
        };

        Styles.Add(bar);
        Styles.Add(barHover);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // сначала отдельное маленькое окно загрузки, как у Steam;
            // главное окно создаётся сразу, но показывается уже собранным
            var splash = new Ludex.UI.SplashWindow();
            splash.Show();

            // окно создаётся сразу, но показывается только после прогрева —
            // иначе платформа показывает его само, до окончания загрузки
            var main = new Ludex.UI.MainWindow();

            Dispatcher.UIThread.Post(async () =>
            {
                try
                {
                    await main.WarmUpAsync();
                }
                catch (Exception ex)
                {
                    AppPaths.Log("WarmUp: " + ex);
                }

                main.Show();
                desktop.MainWindow = main;
                splash.Finish();
            });
        }

        base.OnFrameworkInitializationCompleted();
    }
}
