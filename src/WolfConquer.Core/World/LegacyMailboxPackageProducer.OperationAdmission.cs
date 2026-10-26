namespace WolfConquer.Core.World;

internal sealed partial class LegacyMailboxPackageProducer
{
    private WorldOperationBarrier? packetOperations;

    internal void BindPacketOperations(WorldOperationBarrier operations)
    {
        ArgumentNullException.ThrowIfNull(operations);
        var previous = Interlocked.CompareExchange(ref packetOperations, operations, null);
        if (previous is not null && !ReferenceEquals(previous, operations))
            throw new InvalidOperationException("Mailbox producer already owns another world operation barrier.");
    }

    internal bool OwnsPacketOperations(WorldOperationBarrier operations) =>
        ReferenceEquals(Volatile.Read(ref packetOperations), operations);
}
