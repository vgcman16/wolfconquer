namespace WolfConquer.Launcher;

public sealed partial class LauncherViewModel
{
    public LauncherFlow SelectedFlow
    {
        get => _selectedFlow;
        set
        {
            if (IsBusy || _selectedFlow == value || value is not (LauncherFlow.Client or LauncherFlow.Source)) return;
            _selectedFlow = value;
            if (!IsVisualPreview) ResetInputStatus();
            Changed(nameof(SelectedFlow), nameof(IsClientTab), nameof(IsSourceTab), nameof(Headline), nameof(Description),
                nameof(FlowEyebrow), nameof(PrimaryLabel));
        }
    }
    public bool IsClientTab => SelectedFlow == LauncherFlow.Client;
    public bool IsSourceTab => !IsClientTab;
    public string Headline => IsClientTab ? "The hunt continues." : "Build the next chapter.";
    public string FlowEyebrow => IsClientTab ? "YOUR CLIENT · YOUR NEXT ADVENTURE" : "YOUR SOURCE · YOUR WORK PRESERVED";
    public string Description => IsClientTab ? "Signed updates, explicit paths, and a clear view of every step." :
        "Update a clean checkout without mixing source code and game-client files.";
    public string PrimaryLabel => IsClientTab ? "Check & update" : "Update source";
    public string ConfigurationPath { get => _configurationPath; set => SetInput(ref _configurationPath, value, nameof(ConfigurationPath)); }
    public string ClientRoot { get => _clientRoot; set => SetInput(ref _clientRoot, value, nameof(ClientRoot)); }
    public string SourceRoot { get => _sourceRoot; set => SetInput(ref _sourceRoot, value, nameof(SourceRoot)); }
    public bool AllowFreshInstall
    {
        get => _allowFreshInstall;
        set { if (IsBusy || _allowFreshInstall == value) return; _allowFreshInstall = value; Changed(); if (!IsVisualPreview) ResetInputStatus(); }
    }
    public bool IsBusy => _isBusy;
    public bool IsReady => _isReady;
    public bool CanEditPaths => !IsBusy && !IsVisualPreview;
    public bool IsVisualPreview => _isVisualPreview;
    public bool IsAnimating => IsBusy || IsVisualPreview;
    public bool HasActivity => Activity.Count != 0;
    public string StatusText => _status;
    public string StatusDetail => _detail;
    public string StageLabel => _stage;
    public double StageProgress => _stagePercent ?? 0;
    public double ByteProgress => _bytePercent ?? 0;
    public bool StageIndeterminate => IsBusy && _stagePercent is null;
    public bool ByteIndeterminate => IsBusy && _bytePercent is null;
    public string StageProgressText => _stagePercent is { } value ? value.ToString("0") + "%" : IsBusy ? "In progress" : "Not reported";
    public string ByteProgressText => _bytePercent is { } value ? value.ToString("0") + "%" : "—";
    public string ByteText => _byteText;

    private void SetInput(ref string field, string? value, string name)
    {
        value ??= "";
        if (IsBusy || field == value) return;
        field = value; Changed(name);
        if (name == nameof(ConfigurationPath)) InvalidateClientSettingsDisplay();
        if (!IsVisualPreview) ResetInputStatus();
    }
    private void ResetInputStatus()
    {
        if (IsBusy) return;
        _operationGeneration++;
        _isReady = false;
        _status = (IsClientTab ? LocalPath(ConfigurationPath) && LocalPath(ClientRoot) : LocalPath(SourceRoot)) ? "Not checked" : "Not configured";
        _detail = IsClientTab ? "Choose a configuration file and client folder, then check for signed updates." :
            "Choose a clean local source checkout. Local edits must be preserved; no client configuration is needed.";
        _stage = "No check has run"; _stagePercent = null; _bytePercent = null; _byteText = "Download totals are not known yet.";
        Changed(nameof(IsReady), nameof(StatusText), nameof(StatusDetail)); ProgressChanged(); RefreshCommands();
    }
    private void ProgressChanged() => Changed(nameof(StageLabel), nameof(StageProgress), nameof(ByteProgress), nameof(StageIndeterminate),
        nameof(ByteIndeterminate), nameof(StageProgressText), nameof(ByteProgressText), nameof(ByteText));
    private void BusyChanged()
    {
        Changed(nameof(IsBusy), nameof(CanEditPaths), nameof(IsAnimating), nameof(IsReady)); ProgressChanged(); RefreshCommands();
    }
}
