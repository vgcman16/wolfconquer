namespace WolfConquer.Core.World;

// Carries denial knowledge through the actual registered invocation. It grants
// no owner/commit authority, and ordinary cleanup leases do not consult it.
internal sealed class LegacyTradePacketInvocation : IDisposable
{
    private static readonly AsyncLocal<LegacyTradePacketInvocation?> Current = new();
    private readonly LegacyTradePacketInvocation? previous;
    private readonly WorldOperationBarrier operations;
    private readonly LegacyTradeRecoveryState recovery;
    private volatile bool active = true;

    internal LegacyTradePacketInvocation(WorldOperationBarrier operations, LegacyTradeRecoveryState recovery)
    {
        this.operations = operations;
        this.recovery = recovery;
        previous = Current.Value;
        Current.Value = this;
    }

    internal static void RequireAfterLease(WorldOperationBarrier operations)
    {
        var invocation = Current.Value;
        if (invocation is null) return;
        if (!invocation.active || !ReferenceEquals(invocation.operations, operations))
            throw new LegacyTradePacketDeniedException("tradeJournalRecoveryRequired:packetOwnerEnded");
        if (!invocation.recovery.TryRequireLiveJournalClear(out var reason))
            throw new LegacyTradePacketDeniedException(reason);
    }

    public void Dispose()
    {
        active = false;
        if (ReferenceEquals(Current.Value, this)) Current.Value = previous;
    }
}

internal sealed class LegacyTradePacketDeniedException(string reason) : InvalidOperationException(
    "Trade recovery denied the packet after world admission.")
{
    internal string Reason { get; } = reason;
}
