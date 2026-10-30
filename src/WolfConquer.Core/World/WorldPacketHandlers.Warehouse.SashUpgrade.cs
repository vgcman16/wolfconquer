namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const long LegacyInventorySashSlotCost = 90;
    private const ulong LegacyInventorySashMaxSlots = 300;

    private static async ValueTask HandleUpdateInventorySashAsync(
        WorldPacketHandlerContext context, CancellationToken cancellationToken)
    {
        if (context.Sessions is null && context.Session is null)
        {
            await context.Log.WriteLineAsync(
                "WorldServer skipped MsgAction UpdateInventorySash; no active world session.").ConfigureAwait(false);
            return;
        }
        if (context.TradeOperations is not { } operations || context.TradeRecovery is not { } recovery ||
            context.Trades is not { } trades || context.Sessions is null)
        {
            await context.Log.WriteLineAsync("WorldServer skipped MsgAction UpdateInventorySash; required owner unavailable.")
                .ConfigureAwait(false);
            return;
        }

        LegacySashUpgradePlan? committed = null;
        WorldSession? canonical = null;
        LegacySashUpgradeOwner? denied = null;
        var reason = "sashUpgradeOwnerUnavailable";
        // Frame delegates this exact action to the exclusive owner; never upgrade a shared lease.
        using (await operations.EnterPacketExclusiveAsync(cancellationToken).ConfigureAwait(false))
        lock (trades.SyncRoot)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (TryCaptureLegacySashUpgradeOwner(context, out var owner) &&
                recovery.IsReadyForUsers(owner.Session.UsersRoot))
            {
                if (owner.Session.Character.ConquerPoints < LegacyInventorySashSlotCost ||
                    owner.Count >= LegacyInventorySashMaxSlots)
                {
                    denied = owner;
                    reason = "sashUpgradeFundsOrLimit";
                }
                else
                {
                    var plan = PrepareLegacySashUpgrade(owner);
                    if (TryCommitLegacySashUpgrade(context, plan, cancellationToken, out reason, out var published))
                    {
                        committed = plan;
                        canonical = published ?? throw new LegacyCommittedSpellPublicationException();
                    }
                }
            }
            else if (!context.Sessions.TryGet(context.Connection, out _))
            {
                reason = "sashUpgradeNoActiveSession";
            }
        }

        if (committed is null || canonical is null)
        {
            if (reason == "sashUpgradeNoActiveSession")
            {
                await context.Log.WriteLineAsync(
                    "WorldServer skipped MsgAction UpdateInventorySash; no active world session.").ConfigureAwait(false);
                return;
            }
            if (denied is not null && IsCurrentLegacySashUpgradeOwner(context, denied, denied.Session))
                await context.Connection.SendAsync(LegacyMsgMessagePacket.SystemTalk(
                    "You need 90 CPs in your inventory to open 1 slot in your sash.").ToFrameBytes(), cancellationToken)
                    .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                $"WorldServer skipped MsgAction UpdateInventorySash reason={reason}; " +
                (recovery.IsBlocked ? "world fenced; durable outcome must be reconciled, not reported unchanged."
                    : "no sash upgrade committed.")).ConfigureAwait(false);
            return;
        }

        // The persisted CP/count pair and current-session publication already succeeded.
        // Send/log exceptions propagate; they do not refund this purchase or report rejection.
        await context.Log.WriteLineAsync(
            $"WorldServer persisted MsgAction UpdateInventorySash {FormatSession(context with { Session = canonical })} " +
            "keys=ConquerPoints,InventorySashCount; committed, delivery pending.").ConfigureAwait(false);
        foreach (var frame in committed.Frames)
        {
            if (!IsCurrentLegacySashUpgradeOwner(context, committed.Owner, canonical) ||
                !IsCurrentLegacyWarehouseJoinedItemOwner(context.Sessions!, canonical))
            {
                await context.Log.WriteLineAsync("WorldServer UpdateInventorySash delivery stopped after commit; owner retired or changed.")
                    .ConfigureAwait(false);
                return;
            }
            await context.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
        }
        await context.Log.WriteLineAsync(
            $"WorldServer handled MsgAction UpdateInventorySash {FormatSession(context with { Session = canonical })} " +
            $"inventorySashCount={committed.Owner.Count + 1} cost={LegacyInventorySashSlotCost} " +
            $"conquerPoints={canonical.Character.ConquerPoints}.").ConfigureAwait(false);
    }

    private static LegacySashUpgradePlan PrepareLegacySashUpgrade(LegacySashUpgradeOwner owner)
    {
        var character = WithCharacterAttribute(WithCharacterAttribute(
            owner.Session.Character with { ConquerPoints = owner.Session.Character.ConquerPoints - LegacyInventorySashSlotCost },
            "ConquerPoints", checked((ulong)(owner.Session.Character.ConquerPoints - LegacyInventorySashSlotCost))),
            "InventorySashCount", owner.Count + 1);
        var uid = checked((uint)character.Uid);
        byte[] Update(LegacyMsgUpdateDataType kind, ulong value) =>
            new LegacyMsgUpdatePacket(uid, [new LegacyMsgUpdateEntry(kind, value)]).ToFrameBytes();
        return new(owner, owner.Session with { Character = character },
        [
            Update(LegacyMsgUpdateDataType.ConquerPoints, checked((ulong)character.ConquerPoints)),
            Update(LegacyMsgUpdateDataType.InventorySashMax, LegacyInventorySashMaxSlots),
            Update(LegacyMsgUpdateDataType.InventorySash, owner.Count + 1),
        ]);
    }

    private static bool TryCommitLegacySashUpgrade(WorldPacketHandlerContext context, LegacySashUpgradePlan plan,
        CancellationToken cancellationToken, out string reason, out WorldSession? canonical)
    {
        var failure = "sashUpgradeOwnerChanged";
        var saved = context.Sessions!.TryCommitSession(context.Connection, plan.Owner.Session, plan.Updated, () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!context.TradeRecovery!.IsReadyForUsers(plan.Owner.Session.UsersRoot) ||
                !IsCurrentLegacySashUpgradeOwner(context, plan.Owner, plan.Owner.Session) ||
                !LegacySashUpgradeAttributesCurrent(plan.Owner) ||
                context.Trades!.Contains(context.Connection)) return false;
            var persisted = LegacyInventorySashUpgradePersistence.TryPersist(
                plan.Owner.Session, plan.Owner.Count, plan.Updated.Character.ConquerPoints, out failure);
            if (!persisted && failure.StartsWith(LegacyInventorySashUpgradePersistence.RecoveryRequired, StringComparison.Ordinal))
                context.TradeRecovery.Block(failure);
            return persisted;
        }, out canonical);
        reason = saved ? "committed" : failure;
        return saved;
    }

    private sealed record LegacySashUpgradePlan(LegacySashUpgradeOwner Owner, WorldSession Updated, byte[][] Frames);
}
