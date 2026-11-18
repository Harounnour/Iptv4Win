using System.Windows;
using LibVLCSharp.Shared;

namespace Iptv4Win;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        Core.Initialize();
        new MainWindow().Show();
    }
}
