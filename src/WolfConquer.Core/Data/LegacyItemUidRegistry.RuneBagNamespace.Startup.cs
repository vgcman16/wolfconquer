using WolfConquer.Core.World;

namespace WolfConquer.Core.Data;

internal sealed partial class LegacyItemUidRegistry
{
    internal bool TryRetireStartupRuneBagNamespaces(
        LegacyOwnedAdmissionRuntime.RuneBagNamespaceStartupAuthority authority, out string reason)
    {
        lock (gate)
        {
            reason = RecoveryRequired + ":runeBagNamespaceStartupUnproven";
            if (authority is null || !authority.IsCurrentPlain(this)) return false;
            try
            {
                RequireRuneDecomposeStateCurrent();
                if (state.Version != 6) { reason = string.Empty; return true; }
                var configured = authority.Configuration;
                for (var retired = 0; retired < MaximumOperationReceipts; retired++)
                {
                    RequireRuneBagStartupClear(authority, configured);
                    var row = state.Operations!.FirstOrDefault(IsRuneBagNamespace);
                    if (row is null) { reason = string.Empty; return true; }
                    if (!TryRetireRuneBagNamespaceRow(row, out reason)) return false;
                }
                RequireRuneBagStartupClear(authority, configured);
                if (state.Operations!.Any(IsRuneBagNamespace))
                    throw new InvalidDataException("Rune bag cleanup exceeded its bounded namespace row set.");
                reason = string.Empty; return true;
            }
            catch (Exception error) when (IsOwnedStartupFailure(error))
            {
                reason = RecoveryRequired + ":runeBagNamespaceStartup:" + error.GetType().Name;
                blocked = reason; return false;
            }
        }
    }

    private void RequireRuneBagStartupClear(LegacyOwnedAdmissionRuntime.RuneBagNamespaceStartupAuthority authority,
        LegacyOwnedAdmissionOptions configured)
    {
        if (!authority.IsCurrentPlain(this)) throw new InvalidDataException("Rune bag startup owner changed.");
        RequireRuneDecomposeStateCurrent();
        if (configured.ExpectedDomain != Guid.ParseExact(state.Domain, "N") ||
            !MatchesConfiguration(state, paths, configured.RegistryOptions))
            throw new InvalidDataException("Rune bag startup configuration differs from the actual namespace.");
        _ = RequireCompletedOrdinaryHistory(configured);
        LegacyTradeJournal.RequireClearRuneDecomposeNamespace(configured);
        if (!LegacyItemUidRawCatalog.TryScan(paths.Items, paths.Mailbox, paths.Redeem, paths.Claim,
                out var observed, out var reason) || observed.Maximum > state.HighWater)
            throw new InvalidDataException("Rune bag startup raw namespace is unproven: " + reason);
        RequireRuneDecomposeStateCurrent();
        LegacyTradeJournal.RequireClearRuneDecomposeNamespace(configured);
        if (!authority.IsCurrentPlain(this)) throw new InvalidDataException("Rune bag startup owner changed during clearance.");
    }
}
