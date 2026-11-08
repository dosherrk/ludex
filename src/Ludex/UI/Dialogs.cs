using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

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
}
