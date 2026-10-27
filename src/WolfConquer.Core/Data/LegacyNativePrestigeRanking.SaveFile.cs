using WolfConquer.Core.World;

namespace WolfConquer.Core.Data;

internal partial class LegacyNativePrestigeRanking
{
    // Save holds SynRoot across validated image preparation and this writer.
    // An ordinary uncertain attempt fences this absolute path for this process.
    // It retains remaining sidecars; it is not durable journal/startup recovery.
    // Baseline comparison is not a cross-process compare-and-swap. Reparse checks
    // do not prove safety against concurrent hostile namespace replacement.
    // A consumed stage cannot be retained; cleanup failures may leave sidecars
    // even after a verified commit. No directory flush/crash-durability is proved.
    private static readonly Dictionary<string, PrestigeSavePaths> PrestigeSaveRecoveryRequired = new(
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

    private static void WritePrestigeSaveImage(string path, byte[] image)
    {
        ArgumentNullException.ThrowIfNull(image);
        // Validate the original supplied spelling before absolute capture can
        // normalize it; then fence the exact captured destination as well.
        LegacyMailboxPackageMutationFence.RequireFileMutation(path);
        var destination = Path.GetFullPath(path);
        LegacyMailboxPackageMutationFence.RequireFileMutation(destination);
        if (PrestigeSaveRecoveryRequired.TryGetValue(destination, out var recovery))
        {
            throw PrestigeSaveRecoveryFailure(recovery);
        }

        var paths = NewPrestigeSavePaths(destination);
        FileStream? before = null;
        var ownsStage = false;
        var ownsBackup = false;
        var attempted = false;
        var operationReturned = false;
        var retainArtifacts = false;
        try
        {
            before = OpenPrestigeSaveRead(destination);
            RequirePrestigeSaveRegularPath(destination);
            CreatePrestigeSaveStage(paths.Stage, image, out ownsStage);
            if (before is not null)
            {
                ReservePrestigeSaveBackup(paths.Backup, out ownsBackup);
            }

            LegacyMailboxPackageMutationFence.RequireFileMutation(path);
            LegacyMailboxPackageMutationFence.RequireFileMutation(destination);
            RequirePrestigeSaveRegularPath(destination);
            RequirePrestigeSaveRegularPath(paths.Stage);
            if (!PrestigeSaveMatchesImage(paths.Stage, image) || !PrestigeSaveMatchesPrior(destination, before))
            {
                throw new IOException("Prestige save stage or destination changed before commitment.");
            }

            // From this point even an interrupted observation must retain all
            // still-owned artifacts. Replace/Move can consume the stage name.
            attempted = true;
            retainArtifacts = true;
            if (before is null)
            {
                File.Move(paths.Stage, destination, overwrite: false);
            }
            else
            {
                File.Replace(paths.Stage, destination, paths.Backup, ignoreMetadataErrors: false);
            }

            operationReturned = true;
            ownsStage = false;
            if (!PrestigeSaveMatchesImage(destination, image))
            {
                throw new IOException("Prestige save replacement returned without a verified destination image.");
            }

            retainArtifacts = false;
        }
        catch (Exception error) when (IsOrdinaryPrestigeSaveFailure(error))
        {
            if (!attempted)
            {
                throw;
            }

            var outcome = ReconcilePrestigeSave(paths, before, image, operationReturned, out var observationError);
            if (outcome == PrestigeSaveOutcome.Committed)
            {
                ownsStage = false;
                retainArtifacts = false;
                return;
            }

            if (outcome == PrestigeSaveOutcome.Refused)
            {
                retainArtifacts = false;
                throw;
            }

            PrestigeSaveRecoveryRequired.TryAdd(destination, paths);
            throw PrestigeSaveRecoveryFailure(paths, observationError is null ? error :
                new AggregateException("Prestige save and outcome observation failed.", error, observationError));
        }
        finally
        {
            ClosePrestigeSaveRead(before);
            if (!retainArtifacts)
            {
                DeleteOwnedPrestigeSaveFile(paths.Stage, ownsStage);
                DeleteOwnedPrestigeSaveFile(paths.Backup, ownsBackup);
            }
        }
    }

    private static void CreatePrestigeSaveStage(string path, byte[] image, out bool ownsFile)
    {
        ownsFile = false;
        LegacyMailboxPackageMutationFence.RequireFileMutation(path);
        RequirePrestigeSaveRegularPath(path);
        using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        ownsFile = true;
        file.Write(image);
        file.Flush(flushToDisk: true);
    }

    private static void ReservePrestigeSaveBackup(string path, out bool ownsFile)
    {
        ownsFile = false;
        LegacyMailboxPackageMutationFence.RequireFileMutation(path);
        RequirePrestigeSaveRegularPath(path);
        using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        ownsFile = true;
        file.Flush(flushToDisk: true);
    }

    private enum PrestigeSaveOutcome { Refused, Committed, Indeterminate }
    private sealed record PrestigeSavePaths(string Destination, string Stage, string Backup);
}
