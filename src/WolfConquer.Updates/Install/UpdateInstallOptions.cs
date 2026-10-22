using WolfConquer.Updates.Progress;

namespace WolfConquer.Updates.Install;

public sealed record UpdateInstallOptions(string InstallRoot, IReadOnlyList<string> ProtectedExecutablePaths)
{
    public long MaxFileBytes { get; init; } = 4L << 30;
    public long MaxTotalBytes { get; init; } = 32L << 30;
    public int MaxFiles { get; init; } = 200000;
    public TimeSpan DownloadTimeout { get; init; } = TimeSpan.FromMinutes(30);
    public bool AllowFreshInstall { get; init; }
    public IProgress<UpdateOperationProgress>? Progress { get; init; }
}

public sealed record UpdateInstallResult(string ProductId, string Channel, long Sequence,
    int InstalledFiles, int SkippedFiles, string? TransactionDirectory);

// Cached successful release identity only; this is not a payload integrity result.
public sealed record InstalledReleaseMetadata(string ProductId, string Channel, long Sequence,
    string EnvelopeSha256, long EnvelopeLength);

public interface IUpdateProcessGuard
{
    Task EnsureStoppedAsync(IReadOnlyList<string> absoluteExecutablePaths, CancellationToken cancellationToken);
}

public sealed class UpdateRollbackException : IOException
{
    public string TransactionDirectory { get; }
    internal UpdateRollbackException(string directory, Exception install, Exception rollback)
        : base("Update failed and rollback could not finish; recovery journal retained at " + directory,
            new AggregateException(install, rollback)) => TransactionDirectory = directory;
}
