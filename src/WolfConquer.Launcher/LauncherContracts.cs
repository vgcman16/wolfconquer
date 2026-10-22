namespace WolfConquer.Launcher;

public enum LauncherFlow { Client, Source }
public enum LauncherAction { CheckAndUpdateClient, VerifyClient, LaunchClient, UpdateSource }

public sealed record LauncherInputs(string ConfigurationPath, string ClientRoot,
    string SourceRoot, bool AllowFreshInstall);

// Percentages are reported observations in [0,100]. Null means unknown, never guessed.
// IsReady is explicit controller authority, not an inference from completion or a path.
public sealed record LauncherUiUpdate(string? Status = null, string? Detail = null,
    string? Stage = null, double? StagePercent = null, double? BytePercent = null,
    string? ByteText = null, string? Activity = null, bool? IsReady = null);

public sealed record LauncherActivity(DateTimeOffset Timestamp, string Message)
{
    public string Time => Timestamp.ToLocalTime().ToString("HH:mm:ss");
}
