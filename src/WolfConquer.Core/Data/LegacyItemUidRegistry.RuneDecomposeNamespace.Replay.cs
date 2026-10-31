using WolfConquer.Core.World;

namespace WolfConquer.Core.Data;

internal sealed partial class LegacyItemUidRegistry
{
    // Only the configured acquisition path can supply the actual newly held
    // exclusive lease. This never joins a UID transition to a pending game WAL.
    private sealed class RuneDecomposeNamespaceReplayCustody
    {
        private readonly RegistryPaths paths;
        private readonly FileStream lease;
        private readonly LegacyOwnedAdmissionOptions configured;
        private readonly byte[] beforeBytes, afterBytes;
        private readonly RegistryState before, after;

        private RuneDecomposeNamespaceReplayCustody(RegistryPaths paths, FileStream lease,
            LegacyOwnedAdmissionOptions configured, byte[] beforeBytes, byte[] afterBytes,
            RegistryState before, RegistryState after)
        {
            this.paths = paths; this.lease = lease; this.configured = configured;
            this.beforeBytes = beforeBytes; this.afterBytes = afterBytes; this.before = before; this.after = after;
        }

        internal static RuneDecomposeNamespaceReplayCustody Capture(RegistryPaths paths, FileStream lease,
            LegacyOwnedAdmissionOptions configured, byte[] beforeBytes, byte[] afterBytes,
            RegistryState before, RegistryState after)
        {
            if (!configured.TryNormalize(out var normalized, out _) || normalized != configured ||
                !IsNext(before, after) || !(IsRuneDecomposeNamespaceRetirement(before, after,
                    before with { Generation = after.Generation, HighWater = after.HighWater }) ||
                    IsRuneBagNamespaceRetirement(before, after,
                    before with { Generation = after.Generation, HighWater = after.HighWater })))
                throw new InvalidDataException("Decompose namespace replay requires its exact configured transition.");
            var custody = new RuneDecomposeNamespaceReplayCustody(paths, lease, configured,
                beforeBytes, afterBytes, before, after);
            custody.RequireBefore();
            return custody;
        }

        internal void RequireBefore()
        {
            RequireLeaseAndClearance(before);
            EnsureOrdinaryPath(paths.Pending);
            if (!ReadBounded(paths.State).AsSpan().SequenceEqual(beforeBytes) ||
                !ReadBounded(paths.Pending).AsSpan().SequenceEqual(afterBytes))
                throw new InvalidDataException("Decompose namespace replay state or pending identity changed.");
            LegacyTradeJournal.RequireClearRuneDecomposeNamespace(configured);
        }

        internal void RequireAfter()
        {
            RequireLeaseAndClearance(after);
            if (File.Exists(paths.Pending) || Directory.Exists(paths.Pending) ||
                !ReadBounded(paths.State).AsSpan().SequenceEqual(afterBytes))
                throw new InvalidDataException("Decompose namespace replay poststate is unproven.");
            LegacyTradeJournal.RequireClearRuneDecomposeNamespace(configured);
        }

        private void RequireLeaseAndClearance(RegistryState current)
        {
            if (lease.SafeFileHandle.IsClosed || lease.SafeFileHandle.IsInvalid || !lease.CanRead || !lease.CanWrite ||
                !PathComparer.Equals(Path.GetFullPath(lease.Name), paths.Lease))
                throw new IOException("Decompose namespace replay lost its actual exclusive lease.");
            _ = lease.Length;
            EnsureOrdinaryPath(paths.Lease); EnsureOrdinaryPath(paths.ArchivePending);
            if (!File.Exists(paths.Lease) || !TryValidateDirectory(paths, out _) ||
                File.Exists(paths.ArchivePending) || Directory.Exists(paths.ArchivePending) ||
                configured.ExpectedDomain != Guid.ParseExact(current.Domain, "N") ||
                !MatchesConfiguration(current, paths, configured.RegistryOptions))
                throw new InvalidDataException("Decompose namespace replay configuration or staging changed.");
            var archives = ReadArchiveCatalog(paths, current, null, allowNext: false);
            _ = RequireCompletedOrdinaryHistory(paths, current, configured, archives);
            LegacyTradeJournal.RequireClearRuneDecomposeNamespace(configured);
            if (!LegacyItemUidRawCatalog.TryScan(paths.Items, paths.Mailbox, paths.Redeem, paths.Claim,
                out var observed, out var reason) || observed.Maximum > current.HighWater)
                throw new InvalidDataException("Decompose namespace replay raw catalog is unproven: " + reason);
        }
    }
}
