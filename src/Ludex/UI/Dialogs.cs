using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Ludex.Services;

namespace Ludex.UI;

public static class Dialogs
{
    private static Window CreateShell(string title, double width, Control content)
    {
        var shell = new Window
        {
            Title = title,
            Width = width,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = Skin.Panel,
            ShowInTaskbar = false,
            Content = content,
        };

        // мягкое появление: лёгкий подъём + проявление
        Ui.FadeSlideIn(content, 0, 180, 8);
        shell.Opened += (_, _) => Ui.FadeSlideIn(content, 0, 170, 8);
        return shell;
    }

    public static async Task<bool> ConfirmAsync(Window owner, string title, string text, string okText, string cancelText)
    {
        bool result = false;
        Window window = null!;

        var message = Ui.T(text, 13, Skin.TextMuted, wrap: TextWrapping.Wrap);
        message.MaxWidth = 420;

        var panel = new StackPanel { Margin = new Thickness(24), Spacing = 20 };
        panel.Children.Add(Ui.T(title, 18, Skin.Text, FontWeight.SemiBold));
        panel.Children.Add(message);

        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 10,
        };
        row.Children.Add(Ui.GhostButton(cancelText, () => window?.Close()));
        row.Children.Add(Ui.PrimaryButton(okText, () =>
        {
            result = true;
            window?.Close();
        }));
        panel.Children.Add(row);

        window = CreateShell(title, 480, panel);
        await window.ShowDialog(owner);
        return result;
    }

    public static async Task<string?> PromptAsync(Window owner, string title, string initial)
    {
        string? result = null;
        Window window = null!;

        var box = new TextBox
        {
            Text = initial,
            FontSize = 14,
            Height = 38,
            Background = Skin.PanelAlt,
            Foreground = Skin.Text,
            BorderBrush = Skin.Border,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10, 0, 10, 0),
            VerticalContentAlignment = VerticalAlignment.Center,
        };
        Ui.AnimateTextBox(box);

        void Accept()
        {
            result = box.Text?.Trim();
            window?.Close();
        }

        var panel = new StackPanel { Margin = new Thickness(24), Spacing = 16 };
        panel.Children.Add(Ui.T(title, 18, Skin.Text, FontWeight.SemiBold));
        panel.Children.Add(box);

        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 10,
        };
        row.Children.Add(Ui.GhostButton(Loc.T("btn.cancel"), () => window?.Close()));
        row.Children.Add(Ui.PrimaryButton(Loc.T("btn.save"), Accept));
        panel.Children.Add(row);

        window = CreateShell(title, 460, panel);

        box.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter) Accept();
            else if (e.Key == Key.Escape) window.Close();
        };

        window.Opened += (_, _) =>
        {
            box.Focus();
            box.SelectAll();
        };

        await window.ShowDialog(owner);
        return string.IsNullOrWhiteSpace(result) ? null : result;
    }

    public static void ShowError(Window owner, string text)
    {
        _ = ShowMessageAsync(owner, "Ludex", text);
    }

    public static async Task ShowMessageAsync(Window owner, string title, string text)
    {
        Window window = null!;

        var panel = new StackPanel { Margin = new Thickness(24), Spacing = 18 };
        panel.Children.Add(Ui.T(title, 18, Skin.Text, FontWeight.SemiBold));

        var msg = Ui.T(text, 13, Skin.TextMuted, wrap: TextWrapping.Wrap);
        msg.MaxWidth = 420;
        panel.Children.Add(msg);

        var ok = Ui.PrimaryButton("OK", () => window?.Close());
        ok.HorizontalAlignment = HorizontalAlignment.Right;
        panel.Children.Add(ok);

        window = CreateShell(title, 470, panel);
        await window.ShowDialog(owner);
    }

    // ------------------------------------------------------------ автообновление

    /// <summary>«Доступна версия X» — вернёт true, если пользователь согласился обновиться.</summary>
    public static async Task<bool> UpdateAvailableAsync(Window owner, UpdateInfo info)
    {
        bool result = false;
        Window window = null!;

        var panel = new StackPanel { Margin = new Thickness(24), Spacing = 18 };
        panel.Children.Add(Ui.T(Loc.T("update.title"), 18, Skin.Text, FontWeight.SemiBold));

        var text = Loc.F("update.available", info.Version.ToString(3), Updater.CurrentVersionText);
        if (!string.IsNullOrEmpty(info.AssetName))
            text += "\n\n" + info.AssetName + (info.SizeText.Length > 0 ? " · " + info.SizeText : "");

        var msg = Ui.T(text, 13, Skin.TextMuted, wrap: TextWrapping.Wrap);
        msg.MaxWidth = 440;
        panel.Children.Add(msg);

        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 10,
        };
        row.Children.Add(Ui.GhostButton(Loc.T("update.later"), () => window?.Close()));
        row.Children.Add(Ui.PrimaryButton(Loc.T("update.now"), () =>
        {
            result = true;
            window?.Close();
        }));
        panel.Children.Add(row);

        window = CreateShell(Loc.T("update.title"), 480, panel);
        await window.ShowDialog(owner);
        return result;
    }

    /// <summary>Модальное окно с полосой прогресса: work выполняется, окно закрывается само.</summary>
    public static async Task ShowProgressAsync(Window owner, string title, string hint,
        Func<IProgress<double>, Task> work)
    {
        Exception? error = null;
        Window window = null!;

        var bar = new ProgressBar
        {
            Height = 6,
            Minimum = 0,
            Maximum = 1,
            Value = 0,
            Background = Skin.PanelAlt,
            Foreground = Skin.Accent,
            CornerRadius = new CornerRadius(3),
        };

        var panel = new StackPanel { Margin = new Thickness(24), Spacing = 16 };
        panel.Children.Add(Ui.T(title, 18, Skin.Text, FontWeight.SemiBold));
        var hintLabel = Ui.T(hint, 12.5, Skin.TextMuted, wrap: TextWrapping.Wrap);
        hintLabel.MaxWidth = 420;
        panel.Children.Add(hintLabel);
        panel.Children.Add(bar);

        window = CreateShell(title, 460, panel);
        window.CanResize = false;

        var progress = new Progress<double>(v => bar.Value = Math.Clamp(double.IsNaN(v) ? 0 : v, 0, 1));

        window.Opened += async (_, _) =>
        {
            try
            {
                await work(progress);
            }
            catch (Exception ex)
            {
                error = ex;
            }

            Dispatcher.UIThread.Post(() => window.Close());
        };

        await window.ShowDialog(owner);
        if (error != null) throw error;
    }
}
