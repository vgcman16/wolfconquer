using WolfConquer.Core.World;

namespace WolfConquer.Core.Data;

internal sealed partial class LegacyItemUidRegistry
{
    // This entry point belongs to the actual configured admission startup. The
    // generic open/bootstrap APIs still refuse every pending game journal.
    internal static bool TryOpenOwnedAdmission(LegacyOwnedAdmissionOptions configuration,
        out LegacyItemUidRegistry? registry, out string reason)
    {
        registry = null;
        if (!configuration.TryNormalize(out var actual, out reason)) return false;
        return LegacyTradeJournal.HasPending(actual.UsersRoot)
            ? OwnedStartupCustody.TryOpen(actual, out registry, out reason)
            : TryAcquire(actual.RegistryOptions, false, LegacyItemUidInterruption.None,
                out registry, out reason, actual.ExpectedDomain, actual);
    }

    internal sealed partial class OwnedStartupCustody
    {
        internal static bool TryOpen(LegacyOwnedAdmissionOptions configuration,
            out LegacyItemUidRegistry? registry, out string reason)
        {
            registry = null;
            reason = RecoveryRequired + ":ownedStartupStateUnproven";
            FileStream? lease = null;
            try
            {
                if (!TryPaths(configuration.RegistryOptions, out var paths, out reason)) return false;
                if (!Directory.Exists(paths.Directory))
                { reason = "itemUidRegistryMissing"; return false; }
                EnsureOrdinaryPath(paths.Lease);
                lease = new(paths.Lease, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                if (!TryValidateDirectory(paths, out reason)) return false;
                RequireNoUidTransition(paths);
                if (!RequireExpectedDomain(paths, configuration.RegistryOptions,
                    configuration.ExpectedDomain, out reason)) return false;
                var stateBytes = ReadBounded(paths.State);
                var current = ParseState(stateBytes);
                var archives = ReadArchiveCatalog(paths, current, null, allowNext: false);
                var custody = new OwnedStartupCustody(configuration, paths, lease, stateBytes, current, archives);
                custody.RequireCurrent();
                LegacyTradeJournal.RequireOwnedStartupProducerWork(custody);
                // Register actual configured participants as a DENY-only fence.
                // Recovery still needs its private captured-record permission.
                LegacyOrdinaryAdmissionMutationFence.BindConfiguredDomain(configuration.Roots);
                // The journal validates and scans its exact captured targets and
                // deterministic scratch before the first recovery write.
                if (!LegacyTradeJournal.TryRecoverOwnedStartupItems(custody, out reason)) return false;
                custody.RequireCurrent();
                if (LegacyTradeJournal.HasPending(paths.Users))
                { reason = RecoveryRequired + ":ownedStartupJournalStillPending"; return false; }
                if (!LegacyItemUidRawCatalog.TryScan(paths.Items, paths.Mailbox, paths.Redeem, paths.Claim,
                    out var observed, out reason)) return false;
                if (observed.Maximum > current.HighWater)
                { reason = RecoveryRequired + ":unregisteredDataUid"; return false; }
                custody.RequireCurrent();
                if (LegacyTradeJournal.HasPending(paths.Users))
                { reason = RecoveryRequired + ":ownedStartupJournalAppeared"; return false; }
                var acquired = new LegacyItemUidRegistry(paths, lease, current);
                acquired.RememberArchiveIndex(current, ReadArchiveCatalog(paths, current, null, allowNext: false));
                registry = acquired;
                lease = null;
                reason = string.Empty;
                return true;
            }
            catch (Exception error) when (IsOwnedStartupFailure(error))
            { reason = RecoveryRequired + ":ownedStartup:" + error.GetType().Name; return false; }
            finally
            {
                if (lease is not null)
                    try { lease.Dispose(); }
                    catch (Exception closeError) when (IsOwnedStartupFailure(closeError))
                    { reason += ";leaseClose:" + closeError.GetType().Name; }
            }
        }

    }

    private static bool IsOwnedStartupFailure(Exception error) =>
        IsFileFailure(error) || error is InvalidDataException or InvalidOperationException;
}
