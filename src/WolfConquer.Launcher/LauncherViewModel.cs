using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace WolfConquer.Launcher;

public sealed partial class LauncherViewModel : INotifyPropertyChanged
{
    private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;
    private readonly LauncherCommand _clientTab, _sourceTab, _cancel;
    private readonly LauncherAsyncCommand _primary, _verify, _launch;
    private Func<LauncherAction, LauncherInputs, IProgress<LauncherUiUpdate>, CancellationToken, Task>? _execute;
    private CancellationTokenSource? _cancellation;
    private long _operationGeneration;
    private LauncherFlow _selectedFlow;
    private string _configurationPath = "", _clientRoot = "", _sourceRoot = "";
    private bool _allowFreshInstall, _isBusy, _isReady, _cancelRequested, _hasStatusReport, _isVisualPreview;
    private string _status = "Not configured", _detail = "Choose a configuration file and an explicit client folder to begin.";
    private string _stage = "No check has run", _byteText = "Download totals are not known yet.";
    private double? _stagePercent, _bytePercent;
    private ImageSource? _heroImage;

    public LauncherViewModel()
    {
        _clientTab = new(() => SelectedFlow = LauncherFlow.Client, () => !IsBusy && !IsVisualPreview);
        _sourceTab = new(() => SelectedFlow = LauncherFlow.Source, () => !IsBusy && !IsVisualPreview);
        _primary = new(() => RunAsync(IsClientTab ? LauncherAction.CheckAndUpdateClient : LauncherAction.UpdateSource), CanRun, ReportFailure);
        _verify = new(() => RunAsync(LauncherAction.VerifyClient), () => CanRun() && IsClientTab, ReportFailure);
        _launch = new(() => RunAsync(LauncherAction.LaunchClient), () => CanRun() && IsClientTab && IsReady, ReportFailure);
        _cancel = new(RequestCancellation, () => IsBusy && !_cancelRequested && _cancellation is not null);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public ObservableCollection<LauncherActivity> Activity { get; } = [];
    public ICommand SelectClientCommand => _clientTab;
    public ICommand SelectSourceCommand => _sourceTab;
    public ICommand PrimaryCommand => _primary;
    public ICommand VerifyCommand => _verify;
    public ICommand LaunchCommand => _launch;
    public ICommand CancelCommand => _cancel;
    public Func<LauncherAction, LauncherInputs, IProgress<LauncherUiUpdate>, CancellationToken, Task>? ExecuteAsync
    {
        get => _execute;
        set { _execute = value; RefreshCommands(); }
    }
    public ImageSource? HeroImage { get => _heroImage; set { _heroImage = value; Changed(); } }

    private void Changed([CallerMemberName] string? name = null)
    {
        if (PropertyChanged is not { } handlers) return;
        foreach (PropertyChangedEventHandler handler in handlers.GetInvocationList())
            try { handler(this, new(name)); } catch (Exception error) { LauncherNotifications.Observe(error); }
    }
    private void Changed(params string[] names) { foreach (var name in names) Changed(name); }
    private void RefreshCommands()
    {
        _clientTab.Refresh(); _sourceTab.Refresh(); _primary.Refresh(); _verify.Refresh(); _launch.Refresh(); _cancel.Refresh();
    }
    private static bool LocalPath(string path) => !string.IsNullOrWhiteSpace(path) &&
        System.IO.Path.IsPathFullyQualified(path) && !path.StartsWith("\\\\", StringComparison.Ordinal);
    private bool CanRun() => !IsBusy && !IsVisualPreview && _execute is not null &&
        (IsClientTab ? LocalPath(ConfigurationPath) && LocalPath(ClientRoot) : LocalPath(SourceRoot));
    private void AddActivity(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        try { Activity.Add(new(DateTimeOffset.Now, Bound(message, 2000))); }
        catch (Exception error) { LauncherNotifications.Observe(error); }
        while (Activity.Count > 200)
        {
            var count = Activity.Count;
            try { Activity.RemoveAt(0); } catch (Exception error) { LauncherNotifications.Observe(error); }
            if (Activity.Count == count) break;
        }
        Changed(nameof(HasActivity));
    }
    public void ReportShellError(string message)
    {
        ArgumentNullException.ThrowIfNull(message);
        DispatchSafely(() =>
        {
            _status = "Action unavailable"; _detail = Bound(message, 500); _isReady = false;
            AddActivity(Bound(message, 1000)); Changed(nameof(StatusText), nameof(StatusDetail), nameof(IsReady)); RefreshCommands();
        });
    }
}
