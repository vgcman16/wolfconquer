using WolfConquer.Core.World;

namespace WolfConquer.Core.Data;

internal sealed partial class LegacyItemUidRegistry
{
    internal bool TryRetireStartupRuneDecomposeNamespaces(
        LegacyOwnedAdmissionRuntime.RuneDecomposeNamespaceStartupAuthority authority, out string reason)
    {
        lock (gate)
        {
            reason = RecoveryRequired + ":decomposeNamespaceStartupUnproven";
            if (authority is null || !authority.IsCurrentPlain(this)) return false;
            try
            {
                RequireRuneDecomposeStateCurrent();
                if (state.Version != 5)
                { reason = string.Empty; return true; }
                var configured = authority.Configuration;
                for (var retired = 0; retired < MaximumOperationReceipts; retired++)
                {
                    RequireRuneDecomposeStartupClear(authority, configured);
                    var row = state.Operations!.FirstOrDefault(IsRuneDecomposeNamespace);
                    if (row is null) { reason = string.Empty; return true; }
                    if (!TryRetireRuneDecomposeNamespaceRow(row, out reason)) return false;
                }
                RequireRuneDecomposeStartupClear(authority, configured);
                if (state.Operations!.Any(IsRuneDecomposeNamespace))
                    throw new InvalidDataException("Decompose namespace cleanup exceeded its bounded row set.");
                reason = string.Empty; return true;
            }
            catch (Exception error) when (IsOwnedStartupFailure(error))
            {
                reason = RecoveryRequired + ":decomposeNamespaceStartup:" + error.GetType().Name;
                blocked = reason; return false;
            }
        }
    }

    private void RequireRuneDecomposeStartupClear(
        LegacyOwnedAdmissionRuntime.RuneDecomposeNamespaceStartupAuthority authority,
        LegacyOwnedAdmissionOptions configured)
    {
        if (!authority.IsCurrentPlain(this)) throw new InvalidDataException("Decompose startup owner changed.");
        RequireRuneDecomposeStateCurrent();
        if (configured.ExpectedDomain != Guid.ParseExact(state.Domain, "N") ||
            !MatchesConfiguration(state, paths, configured.RegistryOptions))
            throw new InvalidDataException("Decompose startup configuration differs from the actual namespace.");
        var operations = state.Operations ?? [];
        // Preserve the existing retained-work distinction. Ordinary untyped
        // receipts, including lookalike writer text, are never cleanup targets.
        if (operations.Any(IsRuneDecomposeNamespace)) RequireCompletedOrdinaryHistory(configured);
        LegacyTradeJournal.RequireClearRuneDecomposeNamespace(configured);
        if (!LegacyItemUidRawCatalog.TryScan(paths.Items, paths.Mailbox, paths.Redeem, paths.Claim,
                out var observed, out var reason) || observed.Maximum > state.HighWater)
            throw new InvalidDataException("Decompose startup raw namespace is unproven: " + reason);
        RequireRuneDecomposeStateCurrent();
        LegacyTradeJournal.RequireClearRuneDecomposeNamespace(configured);
        if (!authority.IsCurrentPlain(this)) throw new InvalidDataException("Decompose startup owner changed during clearance.");
    }
}
