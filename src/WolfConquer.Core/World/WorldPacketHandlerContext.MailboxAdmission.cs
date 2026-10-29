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
        var operations = TradeOperations;
        if (producer is null && (TradeRecovery is null || operations is null))
        { await handler(this, frame, cancellationToken).ConfigureAwait(false); return; }
        if (operations is null || producer is not null && Sessions is null)
        { await Log.WriteLineAsync("WorldServer mailbox recovery packet lacks its real admission owners.").ConfigureAwait(false); return; }
        if (producer is not null)
        {
            Sessions!.BindMailboxPackages(producer);
            producer.BindPacketOperations(operations);
        }
        using var tradeInvocation = TradeRecovery is { } recovery ? new LegacyTradePacketInvocation(operations, recovery) : null;
        using var invocation = producer is null ? null : new LegacyMailboxPacketInvocation(this, producer, frame);
        try
        {
            var selfOwned = WorldServerFrameHandler.HandlerOwnsOperation(frame, ProtocolProfile);
            using var lease = selfOwned || operations.OwnsActiveLease(FrameOperationLease) ? null :
                await operations.EnterSharedAsync(cancellationToken).ConfigureAwait(false);
            if (!selfOwned && TradeRecovery is { } live && !live.TryRequireLiveJournalClear(out var tradeReason))
            { await Log.WriteLineAsync($"WorldServer trade recovery denied packet: {tradeReason}.").ConfigureAwait(false); return; }
            if (producer is not null &&
                !LegacyMailboxPacketAdmission.TryRequireCurrent(producer, Sessions!, Connection, frame, ProtocolProfile, out var reason))
            { await Log.WriteLineAsync($"WorldServer mailbox recovery denied packet: {reason}.").ConfigureAwait(false); return; }
            await handler(this, frame, cancellationToken).ConfigureAwait(false);
        }
        catch (LegacyTradePacketDeniedException error)
        {
            await Log.WriteLineAsync($"WorldServer trade recovery denied packet: {error.Reason}.").ConfigureAwait(false);
        }
    }
}
