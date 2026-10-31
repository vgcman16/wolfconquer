using WolfConquer.Core.World;

namespace WolfConquer.Core.Data;

internal sealed partial class LegacyItemUidRegistry
{
    private sealed record RegistryPaths(string Users, string Items, string Mailbox, string Redeem,
        string Claim, string Directory, string Lease, string State, string Pending,
        string History, string ArchivePending);
    private sealed record RegistryState(int Version, string Domain, string Users, string Items,
        string Mailbox, string Redeem, string Claim, uint HistoricalFloor,
        string HistoricalEvidence, ulong Generation, uint HighWater, OperationReservation[]? Operations = null,
        ProducerIntent[]? ProducerIntents = null, string? ArchiveHead = null,
        string? ArchiveHeadSha256 = null, ulong? ArchiveCount = null);

    internal static bool TryBootstrap(LegacyItemUidRegistryOptions options,
        out LegacyItemUidRegistry? registry, out string reason) =>
        TryAcquire(options, bootstrap: true, LegacyItemUidInterruption.None, out registry, out reason);

    internal static bool TryBootstrap(LegacyItemUidRegistryOptions options, LegacyItemUidInterruption interruption,
        out LegacyItemUidRegistry? registry, out string reason) =>
        TryAcquire(options, bootstrap: true, interruption, out registry, out reason);

    internal static bool TryOpen(LegacyItemUidRegistryOptions options,
        out LegacyItemUidRegistry? registry, out string reason) =>
        TryAcquire(options, bootstrap: false, LegacyItemUidInterruption.None, out registry, out reason);

    internal static bool TryOpen(LegacyItemUidRegistryOptions options, Guid expectedDomain,
        out LegacyItemUidRegistry? registry, out string reason)
    {
        if (expectedDomain != Guid.Empty)
            return TryAcquire(options, false, LegacyItemUidInterruption.None, out registry, out reason, expectedDomain);
        registry = null; reason = "itemUidExpectedDomainInvalid"; return false;
    }

    private static bool TryAcquire(LegacyItemUidRegistryOptions options, bool bootstrap,
        LegacyItemUidInterruption interruption, out LegacyItemUidRegistry? registry, out string reason,
        Guid? expectedDomain = null, LegacyOwnedAdmissionOptions? configuredStartup = null)
    {
        registry = null;
        reason = "itemUidConfigurationInvalid";
        FileStream? lease = null;
        try
        {
            if (!Enum.IsDefined(interruption) || !TryPaths(options, out var paths, out reason)) return false;
            if (bootstrap)
            {
                // Never silently recreate a deleted/corrupt ledger in an old domain.
                if (File.Exists(paths.Directory) || Directory.Exists(paths.Directory))
                { reason = "itemUidRegistryAlreadyExists"; return false; }
                Directory.CreateDirectory(paths.Directory);
                EnsureOrdinaryPath(paths.Directory);
                lease = new(paths.Lease, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
            }
            else
            {
                if (!Directory.Exists(paths.Directory)) { reason = "itemUidRegistryMissing"; return false; }
                EnsureOrdinaryPath(paths.Lease);
                lease = new(paths.Lease, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            }
            if (!TryValidateDirectory(paths, out reason) || LegacyTradeJournal.HasPending(paths.Users))
            { if (reason.Length == 0) reason = RecoveryRequired + ":snapshotJournalPending"; return false; }
            if (expectedDomain is { } expected && !RequireExpectedDomain(paths, options, expected, out reason)) return false;
            if (!LegacyItemUidRawCatalog.TryScan(paths.Items, paths.Mailbox, paths.Redeem, paths.Claim,
                out var observed, out reason)) return false;
            RegistryState current;
            if (bootstrap)
            {
                var historicalFloor = options.HistoricalHighWater!.Value; // TryPaths required this value.
                current = new(1, Guid.NewGuid().ToString("N"), paths.Users, paths.Items, paths.Mailbox,
                    paths.Redeem, paths.Claim, historicalFloor, options.HistoricalEvidence,
                    0, Math.Max(historicalFloor, observed.Maximum));
                if (!TryCreate(paths, current, interruption, out reason)) return false;
            }
            else if (!TryRecoverAndRead(paths, options, observed.Maximum, out current, out reason,
                configuredStartup, lease)) return false;
            // Bootstrap/open is an explicitly quiescent operation. This second
            // journal check does not fabricate a global "no raw writers" proof.
            if (LegacyTradeJournal.HasPending(paths.Users))
            { reason = RecoveryRequired + ":snapshotJournalAppeared"; return false; }
            var acquired = new LegacyItemUidRegistry(paths, lease, current);
            acquired.RememberArchiveIndex(current, ReadArchiveCatalog(paths, current, null, allowNext: false));
            registry = acquired;
            lease = null;
            reason = string.Empty;
            return true;
        }
        catch (Exception exception) when (IsFileFailure(exception))
        { reason = RecoveryRequired + ":open:" + exception.GetType().Name; return false; }
        finally
        {
            if (lease is not null)
            {
                try { lease.Dispose(); }
                catch (Exception closeError) when (IsFileFailure(closeError))
                { reason = RecoveryRequired + ":leaseClose:" + closeError.GetType().Name; }
            }
        }
    }

    private static bool TryPaths(LegacyItemUidRegistryOptions options, out RegistryPaths paths, out string reason)
    {
        paths = null!;
        reason = "itemUidConfigurationInvalid";
        if (options is null || !options.HistoricalHighWater.HasValue ||
            string.IsNullOrWhiteSpace(options.HistoricalEvidence) || options.HistoricalEvidence.Length > 4096 ||
            options.HistoricalEvidence.Contains('\r') || options.HistoricalEvidence.Contains('\n')) return false;
        var roots = new[] { options.UsersRoot, options.ItemsRoot, options.MailboxRoot, options.RedeemFile, options.ClaimFile };
        if (roots.Any(value => string.IsNullOrWhiteSpace(value) || !Path.IsPathFullyQualified(value))) return false;
        var normalized = roots.Select(value => Path.TrimEndingDirectorySeparator(Path.GetFullPath(value))).ToArray();
        if (normalized.Distinct(PathComparer).Count() != normalized.Length) return false;
        foreach (var path in normalized) EnsureOrdinaryPath(path);
        if (!Directory.Exists(normalized[0]) || !Directory.Exists(normalized[1]) || File.Exists(normalized[2]) ||
            Directory.Exists(normalized[3]) || Directory.Exists(normalized[4])) return false;
        var directory = Path.Combine(normalized[1], ".managed-item-uids");
        EnsureOrdinaryPath(directory);
        if (normalized.Any(value => PathComparer.Equals(value, directory) ||
            value.StartsWith(directory + Path.DirectorySeparatorChar, PathComparison))) return false;
        paths = new(normalized[0], normalized[1], normalized[2], normalized[3], normalized[4], directory,
            Path.Combine(directory, "owner.lock"), Path.Combine(directory, "state.json"), Path.Combine(directory, "pending.json"),
            Path.Combine(directory, "history"), Path.Combine(directory, "archive.pending.json"));
        reason = string.Empty;
        return true;
    }

    private static bool TryValidateDirectory(RegistryPaths paths, out string reason)
    {
        reason = RecoveryRequired + ":unexpectedRegistryEntry";
        var count = 0;
        foreach (var entry in Directory.EnumerateFileSystemEntries(paths.Directory))
        {
            if (++count > 5) return false;
            EnsureOrdinaryPath(entry);
            if (PathComparer.Equals(entry, paths.History))
            {
                if (!Directory.Exists(entry)) return false;
                continue;
            }
            if (Directory.Exists(entry) || (!PathComparer.Equals(entry, paths.Lease) &&
                !PathComparer.Equals(entry, paths.State) && !PathComparer.Equals(entry, paths.Pending) &&
                !PathComparer.Equals(entry, paths.ArchivePending))) return false;
        }
        reason = string.Empty;
        return true;
    }

    private static readonly StringComparer PathComparer = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
    private static readonly StringComparison PathComparison = OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private static void EnsureOrdinaryPath(string path)
    {
        var current = Path.GetFullPath(path);
        while (!string.IsNullOrEmpty(current))
        {
            try
            {
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("UID registry rejects reparse paths.");
            }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }
            var parent = Path.GetDirectoryName(current);
            if (parent == current) break;
            current = parent;
        }
    }

    private static bool IsFileFailure(Exception exception) => exception is IOException or UnauthorizedAccessException or
        ArgumentException or NotSupportedException or OverflowException or FormatException or
        System.Text.Json.JsonException or System.Security.SecurityException;
}
