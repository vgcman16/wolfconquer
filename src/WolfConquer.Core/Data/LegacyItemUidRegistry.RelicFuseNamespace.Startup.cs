using WolfConquer.Core.World;

namespace WolfConquer.Core.Data;

internal sealed partial class LegacyItemUidRegistry
{
    internal bool TryRetireStartupRelicFuseNamespaces(LegacyOwnedAdmissionRuntime.RelicFuseStartupAuthority authority,
        out string reason)
    {
        lock (gate)
        {
            reason = RecoveryRequired + ":relicFuseStartupUnproven";
            if (authority is null || !authority.IsCurrentPlain(this)) return false;
            try
            {
                RequireRuneDecomposeStateCurrent();
                if (state.Version is not (7 or 8)) { reason = string.Empty; return true; }
                // Version 8 can originate solely from relic decomposition.
                // Do not impose an unrelated namespace's clearance when no
                // actual row of that kind requires retirement.
                if (state.Version == 8 && !state.Operations!.Any(IsRelicFuseNamespace))
                { reason = string.Empty; return true; }
                var configured = authority.Configuration;
                for (var retired = 0; retired < MaximumOperationReceipts; retired++)
                {
                    RequireRelicFuseStartupClear(authority, configured);
                    var row = state.Operations!.FirstOrDefault(IsRelicFuseNamespace);
                    if (row is null) { reason = string.Empty; return true; }
                    if (!TryRetireRelicFuseNamespaceRow(row, out reason)) return false;
                }
                RequireRelicFuseStartupClear(authority, configured);
                if (state.Operations!.Any(IsRelicFuseNamespace)) throw new InvalidDataException("relicFuseStartupCleanupExceededBound");
                reason = string.Empty; return true;
            }
            catch (Exception error) when (IsOwnedStartupFailure(error))
            { reason = RecoveryRequired + ":relicFuseStartup:" + error.GetType().Name; blocked = reason; return false; }
        }
    }

    private void RequireRelicFuseStartupClear(LegacyOwnedAdmissionRuntime.RelicFuseStartupAuthority authority,
        LegacyOwnedAdmissionOptions configured)
    {
        if (!authority.IsCurrentPlain(this)) throw new InvalidDataException("relicFuseStartupOwnerChanged");
        RequireRuneDecomposeStateCurrent();
        if (configured.ExpectedDomain != Guid.ParseExact(state.Domain, "N") || !MatchesConfiguration(state, paths, configured.RegistryOptions))
            throw new InvalidDataException("relicFuseStartupDomainChanged");
        if ((state.Operations ?? []).Any(IsRelicFuseNamespace)) RequireCompletedOrdinaryHistory(configured);
        LegacyTradeJournal.RequireClearRuneDecomposeNamespace(configured); // Shared actual game-journal/staging clearance.
        if (!LegacyItemUidRawCatalog.TryScan(paths.Items, paths.Mailbox, paths.Redeem, paths.Claim, out var observed, out var reason) ||
            observed.Maximum > state.HighWater) throw new InvalidDataException("relicFuseStartupRawNamespaceUnproven:" + reason);
        RequireRuneDecomposeStateCurrent();
        LegacyTradeJournal.RequireClearRuneDecomposeNamespace(configured);
        if (!authority.IsCurrentPlain(this)) throw new InvalidDataException("relicFuseStartupOwnerChanged");
    }
}
