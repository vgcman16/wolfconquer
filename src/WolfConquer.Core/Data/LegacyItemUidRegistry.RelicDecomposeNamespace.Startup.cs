using WolfConquer.Core.World;

namespace WolfConquer.Core.Data;

internal sealed partial class LegacyItemUidRegistry
{
    internal bool TryRetireStartupRelicDecomposeNamespaces(LegacyOwnedAdmissionRuntime.RelicDecomposeStartupAuthority authority,
        out string reason)
    {
        lock (gate)
        {
            reason = RecoveryRequired + ":relicDecomposeStartupUnproven";
            if (authority is null || !authority.IsCurrentPlain(this)) return false;
            try
            {
                RequireRuneDecomposeStateCurrent();
                if (state.Version != 8) { reason = string.Empty; return true; }
                var configured = authority.Configuration;
                for (var retired = 0; retired < MaximumOperationReceipts; retired++)
                {
                    var row = state.Operations!.FirstOrDefault(IsRelicDecomposeNamespace);
                    if (row is null) { reason = string.Empty; return true; }
                    RequireRelicDecomposeStartupClear(authority, configured);
                    if (!TryRetireRelicDecomposeNamespaceRow(row, out reason)) return false;
                }
                if (state.Operations!.Any(IsRelicDecomposeNamespace)) throw new InvalidDataException("relicDecomposeStartupCleanupExceededBound");
                reason = string.Empty; return true;
            }
            catch (Exception error) when (IsOwnedStartupFailure(error))
            { reason = RecoveryRequired + ":relicDecomposeStartup:" + error.GetType().Name; blocked = reason; return false; }
        }
    }

    private void RequireRelicDecomposeStartupClear(LegacyOwnedAdmissionRuntime.RelicDecomposeStartupAuthority authority,
        LegacyOwnedAdmissionOptions configured)
    {
        if (!authority.IsCurrentPlain(this)) throw new InvalidDataException("relicDecomposeStartupOwnerChanged");
        RequireRuneDecomposeStateCurrent();
        if (configured.ExpectedDomain != Guid.ParseExact(state.Domain, "N") || !MatchesConfiguration(state, paths, configured.RegistryOptions))
            throw new InvalidDataException("relicDecomposeStartupDomainChanged");
        if ((state.Operations ?? []).Any(IsRelicDecomposeNamespace)) RequireCompletedOrdinaryHistory(configured);
        LegacyTradeJournal.RequireClearRuneDecomposeNamespace(configured); // Shared actual game-journal/staging clearance.
        if (!LegacyItemUidRawCatalog.TryScan(paths.Items, paths.Mailbox, paths.Redeem, paths.Claim, out var observed, out var reason) ||
            observed.Maximum > state.HighWater) throw new InvalidDataException("relicDecomposeStartupRawNamespaceUnproven:" + reason);
        RequireRuneDecomposeStateCurrent();
        LegacyTradeJournal.RequireClearRuneDecomposeNamespace(configured);
        if (!authority.IsCurrentPlain(this)) throw new InvalidDataException("relicDecomposeStartupOwnerChanged");
    }
}
