using WolfConquer.Core.World;

namespace WolfConquer.Core.Data;

internal sealed partial class LegacyItemUidRegistry
{
    internal const string RelicFuseWriter = "relic-fuse-v1";

    // Read-only capacity/ownership check against the same durable global
    // registry as every other owned item issuer. It issues nothing.
    internal void RequireRelicFuseCapacity(LegacyOwnedAdmissionOptions configuration)
    {
        lock (gate)
        {
            RequireRuneDecomposeStateCurrent(); // Shared physical lease/state proof; no decompose authority is issued.
            RequireCompletedOrdinaryHistory(configuration);
            if ((state.Operations?.Length ?? 0) >= MaximumOperationReceipts ||
                state.Generation > ulong.MaxValue - 2 || state.HighWater == uint.MaxValue)
                throw new InvalidDataException("relicFuseGlobalUidCapacityUnavailable");
        }
    }

    internal void RequireRelicFuseObservedMaximum(LegacyRelicFuseOwnedEvidence evidence, uint observed)
    {
        lock (gate)
        {
            evidence.RequireRegistry(this);
            RequireRuneDecomposeStateCurrent();
            if (observed > state.HighWater) throw new InvalidDataException("relicFuseObservedUidExceedsGlobalOwner");
        }
    }
    // Deny-only after an issued allocation becomes uncertain. No range is
    // recycled; configured recovery owns any later transition.
    internal void BlockRelicFuseAllocation()
    {
        lock (gate) blocked = RecoveryRequired + ":relicFuseAllocationUnproven";
    }
}
