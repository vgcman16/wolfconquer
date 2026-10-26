using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public sealed partial record WorldPacketHandlerContext : IPacketHandlerInvocation<WorldPacketHandlerContext>
{
    [System.Text.Json.Serialization.JsonIgnore]
    internal IDisposable? FrameOperationLease { get; init; }

    async ValueTask IPacketHandlerInvocation<WorldPacketHandlerContext>.InvokeHandlerAsync(
        PacketHandler<WorldPacketHandlerContext> handler, PacketFrame frame, CancellationToken cancellationToken)
    {
        var producer = MailboxPackages ?? Sessions?.MailboxPackages;
        if (producer is null)
        { await handler(this, frame, cancellationToken).ConfigureAwait(false); return; }
        if (Sessions is not { } sessions || TradeOperations is not { } operations)
        { await Log.WriteLineAsync("WorldServer mailbox recovery packet lacks its real admission owners.").ConfigureAwait(false); return; }
        sessions.BindMailboxPackages(producer);
        producer.BindPacketOperations(operations);
        using var invocation = new LegacyMailboxPacketInvocation(this, producer, frame);
        var selfOwned = WorldServerFrameHandler.HandlerOwnsOperation(frame, ProtocolProfile);
        using var lease = selfOwned || operations.OwnsActiveLease(FrameOperationLease) ? null :
            await operations.EnterSharedAsync(cancellationToken).ConfigureAwait(false);
        if (!LegacyMailboxPacketAdmission.TryRequireCurrent(producer, sessions, Connection, frame, out var reason))
        { await Log.WriteLineAsync($"WorldServer mailbox recovery denied packet: {reason}.").ConfigureAwait(false); return; }
        await handler(this, frame, cancellationToken).ConfigureAwait(false);
    }
}
