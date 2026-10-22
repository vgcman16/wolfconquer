using WolfConquer.Updates.Install;

namespace WolfConquer.Updater;

// Display-only projection; executing a workflow always reloads the actual local trust configuration.
public sealed record ClientSettingsSummary(string ConfigurationPath, string ProductId, string Channel,
    string InstallDirectory, string LaunchRelativePath, bool AllowLoopbackHttp,
    string? LaunchWorkingDirectoryRelativePath = null, string? ClientAssetLayoutPreset = null);

public sealed record ClientWorkflowResult(string Status, string ProductId, string Channel, long? Sequence,
    int RecoveredTransactions, bool MetadataOnly, UpdateInstallResult? Installation, int? LaunchedProcessId,
    // Exact signed path exists; this is not a current-file, process or login guarantee.
    bool LaunchCandidateAvailable = false);
