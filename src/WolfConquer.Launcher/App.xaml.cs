using System.Windows;

namespace WolfConquer.Launcher;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var window = new MainWindow();
        MainWindow = window;
        var showWindow = true;
        ConfigureStartup(window, e.Args, ref showWindow);
        if (showWindow) window.Show();
    }

    // Composition and the explicitly labelled render-preview path belong to the host.
    partial void ConfigureStartup(MainWindow window, string[] arguments, ref bool showWindow);
}
