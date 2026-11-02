using WolfConquer.Core.World;

namespace WolfConquer.Core.Data;

internal sealed partial class LegacyItemUidRegistry
{
    internal const string RelicDecomposeWriter = "relic-decompose-v1";

    // Read-only capacity/ownership check against the same durable global
    // registry as every other owned item issuer. It issues nothing.
    internal static bool ValidRelicDecomposeCount(int count) => count is >= 9 and <= 441 && count % 9 == 0;

    internal void RequireRelicDecomposeCapacity(LegacyOwnedAdmissionOptions configuration, int count)
    {
        lock (gate) { RequireRuneDecomposeStateCurrent(); RequireRelicDecomposeCapacityPlain(configuration, count); }
    }

    private void RequireRelicDecomposeCapacityPlain(LegacyOwnedAdmissionOptions configuration, int count)
    {
        if (!Monitor.IsEntered(gate) || !ValidRelicDecomposeCount(count))
            throw new InvalidDataException("relicDecomposeBatchCountInvalid");
        RequireCompletedOrdinaryHistory(configuration);
        if ((state.Operations?.Length ?? 0) >= MaximumOperationReceipts ||
            state.Generation > ulong.MaxValue - 2 || (ulong)state.HighWater + (uint)count > uint.MaxValue)
            throw new InvalidDataException("relicDecomposeGlobalUidCapacityUnavailable");
    }

    internal void RequireRelicDecomposeObservedMaximum(LegacyRelicDecomposeOwnedEvidence evidence, uint observed)
    {
        lock (gate)
        {
            evidence.RequireRegistry(this);
            RequireRuneDecomposeStateCurrent();
            if (observed > state.HighWater) throw new InvalidDataException("relicDecomposeObservedUidExceedsGlobalOwner");
        }
    }
    // Deny-only after an issued allocation becomes uncertain. No range is
    // recycled; configured recovery owns any later transition.
    internal void BlockRelicDecomposeAllocation()
    {
        lock (gate) blocked = RecoveryRequired + ":relicDecomposeAllocationUnproven";
    }
}
