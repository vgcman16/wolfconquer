using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace WolfConquer.Launcher;

public partial class MainWindow : Window
{
    public MainWindow() : this(new LauncherViewModel()) { }
    public MainWindow(LauncherViewModel model)
    {
        InitializeComponent();
        ViewModel = model;
    }
    public LauncherViewModel ViewModel
    {
        get => (LauncherViewModel)DataContext;
        set { ArgumentNullException.ThrowIfNull(value); DataContext = value; LoadHero(value); }
    }
    private static void LoadHero(LauncherViewModel model)
    {
        if (model.HeroImage is not null) return;
        try
        {
            var resource = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/wolfconquer-hero-v1.png"));
            if (resource is null) return;
            using var stream = resource.Stream;
            var image = new BitmapImage(); image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = stream; image.EndInit(); image.Freeze(); model.HeroImage = image;
        }
        catch (IOException) { } // Artwork is optional while the host prepares its original asset.
    }
    private void BrowseConfiguration_Click(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.CanEditPaths) return;
        try
        {
            var dialog = new OpenFileDialog { Title = "Choose local updater configuration", Filter = "JSON configuration (*.json)|*.json", CheckFileExists = true };
            if (dialog.ShowDialog(this) == true && Local(dialog.FileName))
            {
                ViewModel.ConfigurationPath = dialog.FileName;
                ViewModel.RefreshClientSettingsDisplay();
            }
        }
        catch (Exception error) when (error is IOException or InvalidOperationException or System.ComponentModel.Win32Exception)
        { ViewModel.ReportShellError("The local configuration picker could not be opened."); }
    }
    private void Configuration_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (ViewModel.CanEditPaths) ViewModel.RefreshClientSettingsDisplay();
    }
    private void BrowseClient_Click(object sender, RoutedEventArgs e) => BrowseFolder(false);
    private void BrowseSource_Click(object sender, RoutedEventArgs e) => BrowseFolder(true);
    private void BrowseFolder(bool source)
    {
        if (!ViewModel.CanEditPaths) return;
        try
        {
            var dialog = new OpenFolderDialog { Title = source ? "Choose a clean local source checkout" : "Choose the local client target folder", Multiselect = false };
            if (dialog.ShowDialog(this) != true || !Local(dialog.FolderName)) return;
            if (source) ViewModel.SourceRoot = dialog.FolderName; else ViewModel.ClientRoot = dialog.FolderName;
        }
        catch (Exception error) when (error is IOException or InvalidOperationException or System.ComponentModel.Win32Exception)
        { ViewModel.ReportShellError("The local folder picker could not be opened."); }
    }
    private bool Local(string path)
    {
        if (Path.IsPathFullyQualified(path) && !path.StartsWith("\\\\", StringComparison.Ordinal)) return true;
        ViewModel.ReportShellError("Choose an explicit local path. Network and device paths are not accepted here.");
        return false;
    }
    protected override void OnClosing(CancelEventArgs e)
    {
        if (ViewModel.IsBusy)
        {
            e.Cancel = true;
            if (MessageBox.Show(this, "An operation is still running. Request cancellation and wait for it to stop safely?",
                "WolfConquer", MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes && ViewModel.CancelCommand.CanExecute(null))
                ViewModel.CancelCommand.Execute(null);
        }
        base.OnClosing(e);
    }
}
