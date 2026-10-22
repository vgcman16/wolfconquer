using System.Windows.Threading;
using WolfConquer.Updater;

namespace WolfConquer.Launcher;

public partial class App
{
    partial void ConfigureStartup(MainWindow window, string[] arguments, ref bool showWindow)
    {
        var previewRequested = arguments.Contains("--render-preview", StringComparer.Ordinal);
        if (previewRequested) showWindow = false;
        try
        {
            var options = LauncherArguments.Parse(arguments);
            if (options.TryGetValue("--render-preview", out var output))
            {
                window.ViewModel = LauncherViewModel.CreateVisualPreview();
                if (options.ContainsKey("--source-root")) window.ViewModel.SelectedFlow = LauncherFlow.Source;
                Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, () =>
                {
                    try { LauncherPreviewRenderer.Render(window, output); Shutdown(0); }
                    catch (Exception error) { Console.Error.WriteLine("Launcher preview failed: " + error.Message); Shutdown(1); }
                });
                return;
            }
            var model = window.ViewModel;
            model.ExecuteAsync = LauncherController.ExecuteAsync;
            if (options.TryGetValue("--config", out var config))
            {
                model.ConfigurationPath = config;
                var settings = LauncherWorkflow.ReadClientSettings(config);
                model.ClientRoot = settings.InstallDirectory;
                model.DisplayClientSettings(settings);
            }
            if (options.TryGetValue("--client-root", out var client)) model.ClientRoot = client;
            if (options.TryGetValue("--source-root", out var source))
            { model.SourceRoot = source; model.SelectedFlow = LauncherFlow.Source; }
            model.AllowFreshInstall = options.ContainsKey("--fresh");
        }
        catch (Exception error)
        {
            if (previewRequested)
            {
                Console.Error.WriteLine("Launcher preview failed: " + error.Message);
                Shutdown(1);
                return;
            }
            // Startup configuration cannot trigger an unhandled WPF/modal crash.
            window.ViewModel.ReportShellError("Launcher configuration needs attention: " + error.Message);
        }
    }
}

internal static class LauncherArguments
{
    internal static Dictionary<string, string> Parse(string[] arguments)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int index = 0; index < arguments.Length; index++)
        {
            var name = arguments[index];
            string value;
            if (name == "--fresh") value = "true";
            else if (name is "--config" or "--client-root" or "--source-root" or "--render-preview")
            {
                if (++index == arguments.Length || arguments[index].StartsWith("--", StringComparison.Ordinal))
                    throw new ArgumentException("A launcher option needs a value.");
                value = arguments[index];
            }
            else throw new ArgumentException("Unknown launcher option.");
            if (!result.TryAdd(name, value)) throw new ArgumentException("Duplicate launcher option.");
        }
        return result;
    }
}
