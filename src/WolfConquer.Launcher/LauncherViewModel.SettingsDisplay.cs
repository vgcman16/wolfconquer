using System.IO;
using System.Text.Json;
using WolfConquer.Updater;

namespace WolfConquer.Launcher;

public sealed partial class LauncherViewModel
{
    private const string WorkingDirectoryUnavailable = "Working directory: Unavailable — select a valid local configuration.";
    private string _launchWorkingDirectoryText = WorkingDirectoryUnavailable;
    public string LaunchWorkingDirectoryText => _launchWorkingDirectoryText;

    // Display-only snapshot. This does not grant readiness, change paths, or configure a launch.
    internal void DisplayClientSettings(ClientSettingsSummary settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (!LocalPath(ConfigurationPath) || !string.Equals(Path.GetFullPath(ConfigurationPath),
            settings.ConfigurationPath, StringComparison.OrdinalIgnoreCase)) return;
        _launchWorkingDirectoryText = settings.LaunchWorkingDirectoryRelativePath is { } relative
            ? $"Working directory: {relative} (relative to client folder)."
            : "Working directory: Client folder root (default).";
        Changed(nameof(LaunchWorkingDirectoryText));
    }

    internal void RefreshClientSettingsDisplay()
    {
        // Called only by explicit configuration selection/focus and client-action entry, never progress reports.
        InvalidateClientSettingsDisplay();
        if (IsVisualPreview || !LocalPath(ConfigurationPath)) return;
        try { DisplayClientSettings(LauncherWorkflow.ReadClientSettings(ConfigurationPath)); }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or
            JsonException or NotSupportedException)
        { /* Missing/invalid local settings remain unavailable, not an operational failure or readiness claim. */ }
    }

    private void InvalidateClientSettingsDisplay()
    {
        _launchWorkingDirectoryText = WorkingDirectoryUnavailable;
        Changed(nameof(LaunchWorkingDirectoryText));
    }
}
