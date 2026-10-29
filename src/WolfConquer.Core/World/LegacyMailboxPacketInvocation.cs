using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

// Propagates only through this real invocation. Inner self-exclusive handlers
// recheck admission AFTER their actual world lease is granted, never by a bool.
internal sealed class LegacyMailboxPacketInvocation : IDisposable
{
    private static readonly AsyncLocal<LegacyMailboxPacketInvocation?> Current = new();
    private readonly LegacyMailboxPacketInvocation? previous;
    private readonly WorldPacketHandlerContext context;
    private readonly LegacyMailboxPackageProducer producer;
    private readonly PacketFrame frame;
    private bool active = true;

    internal LegacyMailboxPacketInvocation(WorldPacketHandlerContext context,
        LegacyMailboxPackageProducer producer, PacketFrame frame)
    {
        this.context = context; this.producer = producer; this.frame = frame;
        previous = Current.Value;
        if (previous is not null) throw new InvalidOperationException("Nested mailbox packet invocation is unsupported.");
        Current.Value = this;
    }

    internal static void RequireAfterLease(WorldOperationBarrier operations)
    {
        var invocation = Current.Value;
        if (invocation is null) return;
        if (!invocation.active || !ReferenceEquals(invocation.context.TradeOperations, operations) ||
            !invocation.producer.OwnsPacketOperations(operations) ||
            !LegacyMailboxPacketAdmission.TryRequireCurrent(invocation.producer, invocation.context.Sessions!,
                invocation.context.Connection, invocation.frame, invocation.context.ProtocolProfile, out _))
            throw new InvalidOperationException("Mailbox recovery denied the queued packet after world admission.");
    }

    public void Dispose()
    {
        active = false;
        if (ReferenceEquals(Current.Value, this)) Current.Value = previous;
    }
}
