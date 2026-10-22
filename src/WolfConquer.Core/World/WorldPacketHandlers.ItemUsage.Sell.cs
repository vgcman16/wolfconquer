using System.Globalization;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask HandleSellItemAsync(WorldPacketHandlerContext context, WorldSession supplied,
        MsgItemUsagePacket packet, CancellationToken cancellationToken)
    {
        var reason = "currentSessionMissing";
        var sends = new List<byte[]>();
        LegacyNpcSalePlan? committed = null;
        WorldSession? original = null;
        // Dispatch must not retain a shared lease for Sell. Keep mutation and
        // durable publication exclusive, but never await a socket under this lease.
        using (var operation = context.TradeOperations is { } operations
            ? await operations.EnterExclusiveAsync(cancellationToken).ConfigureAwait(false) : null)
        {
            if (context.TradeRecovery?.IsBlocked == true) reason = "recoveryRequired";
            else if (context.Trades?.HasConnectionEnded(context.Connection) == true) reason = "connectionEnded";
            else if (!TryAdmitLegacySecondaryPasswordItemUsage(context, supplied, out var current, out reason)) { }
            else if (!TryPlanLegacyNpcSale(context, current, packet, out var plan, out reason, out var rejection))
            {
                if (rejection is not null) sends.Add(rejection);
            }
            else if (context.Sessions is { } sessions)
            {
                original = current;
                // Serialize everything before the durable mutation as well.
                var frames = CreateLegacyNpcSaleFrames(plan);
                var failure = string.Empty;
                if (sessions.TryCommitSession(context.Connection, current, plan.Updated, () =>
                {
                    if (!LegacySecondaryPasswordRuntime.IsAdmitted(context.Connection, current))
                    { failure = "secondaryPasswordOwnerChanged"; return false; }
                    return LegacyNpcSalePersistence.TryPersist(current, plan.Updated, context.PlayerItemsRoot, out failure);
                }))
                {
                    if (LegacySecondaryPasswordRuntime.IsAdmitted(context.Connection, plan.Updated))
                    {
                        committed = plan;
                        sends.AddRange(frames);
                        reason = "committed";
                    }
                    else
                    {
                        context.TradeRecovery?.Block("npcSaleRecoveryRequired:ownerChangedAfterCommit");
                        reason = "ownerChangedAfterCommit";
                    }
                }
                else
                {
                    if (failure.StartsWith(LegacyTradeJournal.RecoveryRequired, StringComparison.Ordinal))
                        context.TradeRecovery?.Block(failure);
                    reason = "commitRejected:" + (failure.Length == 0 ? "sessionChanged" : failure);
                }
            }
            else reason = "sessionStoreUnavailable";
        }
        foreach (var bytes in sends)
            await context.Connection.SendAsync(bytes, cancellationToken).ConfigureAwait(false);
        if (reason == "anima-pin-not-activated")
            await context.Log.WriteLineAsync("WorldServer blocked SecurityAnima item operation " +
                $"{FormatSession(context)} source={LegacySecurityAnimaSellItemSource} reason={reason} " +
                $"wire=7899 npcReplyOpcode={LegacyNpcReplyPacket.Opcode}.").ConfigureAwait(false);
        else if (committed is not null)
            await context.Log.WriteLineAsync("WorldServer handled MsgItemUsage sell " +
                $"{FormatSession(context with { Session = original })} npcUid={packet.Id} itemUid={committed.ItemUid} " +
                $"value={committed.Value}; persistedBeforeReply=True.").ConfigureAwait(false);
        else
            await context.Log.WriteLineAsync($"WorldServer skipped MsgItemUsage sell {FormatSession(context)}; {reason}.")
                .ConfigureAwait(false);
    }

    private static List<byte[]> CreateLegacyNpcSaleFrames(LegacyNpcSalePlan plan)
    {
        var frames = new List<byte[]>
        {
            plan.InventoryFrame,
        };
        if (!plan.UpdatesMoney) return frames;
        if (plan.WarehouseOverflow > 0)
            frames.Add((LegacyMsgMessagePacket.SystemTalk(plan.WarehouseOverflow.ToString(CultureInfo.InvariantCulture) +
                " Gold has transfare to your warehouse you can't hold more than 500,000,000,000 into your inventory.")
                with { ChatType = 2600, Color = LegacyMsgMessagePacket.YellowColor }).ToFrameBytes());
        var money = new LegacyMsgUpdatePacket(checked((uint)plan.Updated.Character.Uid),
            [new(LegacyMsgUpdateDataType.Money, checked((ulong)plan.Updated.Character.Money))]);
        // Native Money setter sends once; SellItem explicitly sends again.
        frames.Add(money.ToFrameBytes());
        frames.Add(money.ToFrameBytes());
        return frames;
    }
}
