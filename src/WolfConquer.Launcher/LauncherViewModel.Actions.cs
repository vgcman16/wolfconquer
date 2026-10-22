namespace WolfConquer.Launcher;

public sealed partial class LauncherViewModel
{
    public void Report(LauncherUiUpdate update)
    {
        ArgumentNullException.ThrowIfNull(update);
        DispatchSafely(() => ApplyReport(update));
    }

    private void DispatchSafely(Action apply)
    {
        if (_dispatcher.HasShutdownStarted || _dispatcher.HasShutdownFinished) return;
        void Guarded() { try { apply(); } catch (Exception error) { LauncherNotifications.Observe(error); } }
        if (!_dispatcher.CheckAccess())
        {
            try { _dispatcher.BeginInvoke(new Action(Guarded)); }
            catch (InvalidOperationException error) { LauncherNotifications.Observe(error); }
            return;
        }
        Guarded();
    }

    private void ApplyReport(LauncherUiUpdate update)
    {
        if (update.Status is not null) { _status = Bound(update.Status, 80); _hasStatusReport = true; }
        if (update.Detail is not null) _detail = Bound(update.Detail, 1000);
        if (update.Stage is not null) _stage = Bound(update.Stage, 160);
        if (update.ByteText is not null) _byteText = Bound(update.ByteText, 200);
        _stagePercent = ValidPercent(update.StagePercent); _bytePercent = ValidPercent(update.BytePercent);
        if (update.IsReady is { } ready) _isReady = ready && !_cancelRequested;
        if (update.Activity is not null) AddActivity(update.Activity);
        Changed(nameof(StatusText), nameof(StatusDetail), nameof(IsReady)); ProgressChanged(); RefreshCommands();
    }

    private async Task RunAsync(LauncherAction action)
    {
        if (!CanRun() || _execute is null) return;
        var execute = _execute;
        var inputs = new LauncherInputs(ConfigurationPath, ClientRoot, SourceRoot, AllowFreshInstall);
        using var cancellation = new CancellationTokenSource();
        var generation = ++_operationGeneration;
        _cancellation = cancellation; _cancelRequested = false; _isBusy = true; _isReady = false; _hasStatusReport = false;
        _stagePercent = null; _bytePercent = null; _stage = "Waiting for the update controller";
        _status = "Working"; _detail = "The requested operation is starting. No result has been reported yet.";
        _byteText = "Waiting for reported download totals.";
        AddActivity("Requested: " + action + ".");
        Changed(nameof(StatusText), nameof(StatusDetail)); BusyChanged();
        try
        {
            var progress = new Progress<LauncherUiUpdate>(update => DispatchSafely(() =>
            {
                if (_operationGeneration == generation && ReferenceEquals(_cancellation, cancellation)) ApplyReport(update);
            }));
            await execute(action, inputs, progress, cancellation.Token);
            await _dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.Background);
            if (!_hasStatusReport) { _status = "Completed"; _detail = "The operation finished. Launch readiness has not been inferred."; }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            _operationGeneration++;
            _status = "Cancelled"; _detail = "The controller stopped the operation. Check again before launching."; _isReady = false;
            AddActivity("Operation cancelled.");
        }
        catch (Exception error) { _operationGeneration++; ReportFailure(error); }
        finally
        {
            _cancellation = null; _isBusy = false;
            Changed(nameof(StatusText), nameof(StatusDetail)); BusyChanged();
        }
    }

    public void RequestCancellation()
    {
        if (_cancellation is null || _cancelRequested) return;
        _cancelRequested = true; _isReady = false;
        _status = "Cancellation requested"; _detail = "Waiting for the controller to stop safely; do not close the launcher yet.";
        Changed(nameof(StatusText), nameof(StatusDetail), nameof(IsReady)); RefreshCommands();
        try { _cancellation.Cancel(); }
        catch (AggregateException error) { ReportFailure(error); }
    }
    private void ReportFailure(Exception error)
    {
        _isReady = false; _status = "Could not complete";
        var expected = error is System.IO.InvalidDataException or System.IO.IOException or ArgumentException or System.Net.Http.HttpRequestException;
        var raw = error.Message;
        var message = System.Text.RegularExpressions.Regex.Replace(raw.Length > 8192 ? raw[..8192] : raw, @"\s+", " ").Trim();
        _detail = expected && message.Length != 0 ? Bound(message, 500) :
            "The operation failed. Review the configuration and the controller's local logs before retrying.";
        AddActivity(expected && message.Length != 0 ? Bound("Operation failed: " + message, 1000) : "Operation failed (" + error.GetType().Name + ").");
        Changed(nameof(StatusText), nameof(StatusDetail), nameof(IsReady)); RefreshCommands();
    }
    private static double? ValidPercent(double? value) => value is { } number && double.IsFinite(number) && number is >= 0 and <= 100 ? number : null;
    private static string Bound(string value, int maximum) => value.Length > maximum ? value[..(maximum - 1)] + "…" : value;

    public static LauncherViewModel CreateVisualPreview()
    {
        var model = new LauncherViewModel
        {
            _isVisualPreview = true, _status = "VISUAL PREVIEW", _detail = "No update is running. Artwork and layout preview only.",
            _stage = "Sample pipeline · visual preview only", _stagePercent = 64, _bytePercent = 38,
            _byteText = "Sample progress · no download is running."
        };
        model.AddActivity("VISUAL PREVIEW · no update operation has run.");
        return model;
    }
}
