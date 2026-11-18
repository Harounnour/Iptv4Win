using System.Globalization;
using System.Windows;
using LibVLCSharp.Shared;

namespace Iptv4Win;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
        CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo("fr-FR");
        base.OnStartup(e);
        Core.Initialize();
        new MainWindow().Show();
    }
}
