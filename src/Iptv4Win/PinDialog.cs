using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Iptv4Win;

/// <summary>Small modal PIN dialog built in code to keep the project dependency-free.</summary>
internal static class PinDialog
{
    public static string? Show(Window owner, string title, string message, bool confirm = false)
    {
        var window = new Window
        {
            Title = title,
            Owner = owner,
            Width = 380,
            Height = confirm ? 270 : 220,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ResizeMode = ResizeMode.NoResize,
            Background = new SolidColorBrush(Color.FromRgb(24, 30, 40)),
            Foreground = Brushes.White
        };

        var panel = new StackPanel { Margin = new Thickness(22) };
        panel.Children.Add(new TextBlock
        {
            Text = message, TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 14), FontSize = 14
        });

        var pin = new PasswordBox { MaxLength = 6, Margin = new Thickness(0, 0, 0, 10), Padding = new Thickness(8) };
        panel.Children.Add(pin);

        PasswordBox? repeated = null;
        if (confirm)
        {
            panel.Children.Add(new TextBlock { Text = "Confirmez le code PIN", Margin = new Thickness(0, 0, 0, 5) });
            repeated = new PasswordBox { MaxLength = 6, Padding = new Thickness(8) };
            panel.Children.Add(repeated);
        }

        var error = new TextBlock { Foreground = Brushes.OrangeRed, Margin = new Thickness(0, 7, 0, 0) };
        panel.Children.Add(error);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 14, 0, 0) };
        var ok = new Button { Content = "Valider", MinWidth = 90, Padding = new Thickness(12, 6, 12, 6), IsDefault = true };
        var cancel = new Button { Content = "Annuler", MinWidth = 90, Padding = new Thickness(12, 6, 12, 6), Margin = new Thickness(8, 0, 0, 0), IsCancel = true };
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);
        panel.Children.Add(buttons);
        window.Content = panel;

        string? result = null;
        ok.Click += (_, _) =>
        {
            if (pin.Password.Length is < 4 or > 6 || pin.Password.Any(c => c < '0' || c > '9'))
            {
                error.Text = "Saisissez un code de 4 à 6 chiffres.";
                return;
            }
            if (repeated != null && pin.Password != repeated.Password)
            {
                error.Text = "Les deux codes ne correspondent pas.";
                repeated.Clear();
                return;
            }
            result = pin.Password;
            window.DialogResult = true;
        };

        window.Loaded += (_, _) => pin.Focus();
        window.ShowDialog();
        return result;
    }
}
