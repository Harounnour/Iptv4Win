using System.Security.Cryptography;
using System.Text;
using System.Windows;

namespace Iptv4Win;

public partial class ParentControlWindow : Window
{
    enum DialogMode { Authorize, Setup, Change }
    readonly DialogMode _mode;
    static string PinFile => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "IPTV4Win", "parental.pin");

    ParentControlWindow(DialogMode mode, Window owner)
    {
        InitializeComponent();
        _mode = mode;
        Owner = owner;
        switch (mode)
        {
            case DialogMode.Authorize:
                TitleText.Text = "Chaîne protégée";
                OldLabel.Text = "Code PIN parental";
                OldPanel.Visibility = Visibility.Visible;
                NewPanel.Visibility = ConfirmPanel.Visibility = Visibility.Collapsed;
                MessageText.Text = "Saisissez le code parental pour autoriser cette chaîne.";
                break;
            case DialogMode.Setup:
                TitleText.Text = "Créer le code parental";
                OldPanel.Visibility = Visibility.Collapsed;
                MessageText.Text = "Les chaînes identifiées comme adultes seront bloquées sans ce code.";
                break;
            case DialogMode.Change:
                TitleText.Text = "Modifier le code parental";
                OldPanel.Visibility = Visibility.Visible;
                OldLabel.Text = "Ancien code PIN";
                MessageText.Text = "Confirmez l'ancien code puis choisissez un nouveau code.";
                break;
        }
    }

    static bool HasPin() => File.Exists(PinFile);

    static bool Verify(string pin)
    {
        try
        {
            var protectedBytes = File.ReadAllBytes(PinFile);
            var saved = ProtectedData.Unprotect(protectedBytes, null, DataProtectionScope.CurrentUser);
            var actual = Encoding.UTF8.GetBytes(pin);
            return CryptographicOperations.FixedTimeEquals(saved, actual);
        }
        catch { return false; }
    }

    static void SavePin(string pin)
    {
        var folder = Path.GetDirectoryName(PinFile)!;
        Directory.CreateDirectory(folder);
        var protectedBytes = ProtectedData.Protect(
            Encoding.UTF8.GetBytes(pin), null, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(PinFile, protectedBytes);
    }

    public static bool Authorize(Window owner)
    {
        if (!HasPin())
        {
            MessageBox.Show(owner,
                "Le contrôle parental n'est pas encore configuré. Créez un code PIN pour autoriser temporairement cette chaîne.",
                "Contrôle parental", MessageBoxButton.OK, MessageBoxImage.Information);
            var setup = new ParentControlWindow(DialogMode.Setup, owner);
            return setup.ShowDialog() == true;
        }
        var dialog = new ParentControlWindow(DialogMode.Authorize, owner);
        return dialog.ShowDialog() == true;
    }

    public static bool Configure(Window owner)
    {
        var mode = HasPin() ? DialogMode.Change : DialogMode.Setup;
        var dialog = new ParentControlWindow(mode, owner);
        return dialog.ShowDialog() == true;
    }

    void Validate_Click(object sender, RoutedEventArgs e)
    {
        if (_mode == DialogMode.Authorize)
        {
            if (Verify(OldPin.Password)) DialogResult = true;
            else MessageText.Text = "Code PIN incorrect.";
            return;
        }

        if (_mode == DialogMode.Change && !Verify(OldPin.Password))
        {
            MessageText.Text = "Ancien code PIN incorrect.";
            return;
        }

        var pin = NewPin.Password;
        if (pin.Length < 4 || pin.Length > 12 || pin.Any(c => !char.IsDigit(c)))
        {
            MessageText.Text = "Le code doit contenir de 4 à 12 chiffres.";
            return;
        }
        if (pin != ConfirmPin.Password)
        {
            MessageText.Text = "Les deux codes PIN ne correspondent pas.";
            return;
        }

        try
        {
            SavePin(pin);
            DialogResult = true;
        }
        catch (Exception ex)
        {
            MessageText.Text = "Impossible d'enregistrer le code : " + ex.Message;
        }
    }
}