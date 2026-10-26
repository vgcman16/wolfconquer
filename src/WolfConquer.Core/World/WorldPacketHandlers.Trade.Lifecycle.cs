using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask HandleLegacyTradeRuntimeAsync(WorldPacketHandlerContext context,
        PacketFrame frame, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!LegacyTradePacket.TryParse(frame, out var packet))
        {
            await context.Log.WriteLineAsync($"WorldServer rejected malformed LegacyTrade {FormatSession(context)}.");
            return;
        }
        if (context.Trades is not { } trades || context.Sessions is not { } sessions)
        {
            await context.Log.WriteLineAsync("WorldServer skipped LegacyTrade: managed trade/session owner unavailable.");
            return;
        }

        var sends = new List<LegacyTradeSend>();
        var reason = "ignoredAction";
        // The live handler supplies the shared operation owner. Keep the exclusive
        // lease over mutation/persistence only, not the subsequent socket writes.
        using (var operation = context.TradeOperations is { } operations
            ? await operations.EnterPacketExclusiveAsync(cancellationToken).ConfigureAwait(false)
            : null)
        lock (trades.SyncRoot)
        {
            if (context.TradeRecovery?.IsBlocked == true)
            {
                reason = "tradeRecoveryRequired";
            }
            else if (!sessions.TryGet(context.Connection, out var session) || session is null ||
                session.Character.Uid is <= 0 or > uint.MaxValue)
            {
                reason = "currentSessionMissing";
            }
            else if (!IsLegacyTradeSecurityAdmitted(context.Connection, session))
            {
                reason = "secondaryPasswordUnverifiedOrMalformed";
            }
            else if (packet.Type == 0)
            {
                reason = RequestLegacyTrade(context, trades, session, packet, sends);
            }
            else if (trades.TryGet(context.Connection, out var party) && party.Uid == session.Character.Uid)
            {
                if (packet.Type == 1) reason = AcceptLegacyTradeRequest(context, party, packet, sends);
                else if (packet.Type == 2) reason = CloseLegacyTrade(context, trades, party, packet, sends) ? "cancelled" : "cancelRetained";
                else if (party.WindowOpen && party.Other.WindowOpen)
                    reason = ApplyLegacyTradeExchangeAction(context, trades, party, packet, sends);
            }
            else if (packet.Type == 9)
            {
                sends.Add(new(context.Connection, (uint)session.Character.Uid,
                    (TradeReply(packet, 4) with { UID = (uint)session.Character.Uid }).ToNativeFrameBytes()));
                reason = "unpairedConfirmationFinished";
            }
        }
        await SendLegacyTradeFramesAsync(context, sends, cancellationToken).ConfigureAwait(false);
        await context.Log.WriteLineAsync($"WorldServer handled LegacyTrade {FormatSession(context)} type={packet.Type}; result={reason} " +
            "sourceName=CMsgTrade managedSnapshotTrade=True secondaryPassword=currentVerifiedOwner nativePokerAdmission=unverified nativeLifetime=False.");
    }

    private static string RequestLegacyTrade(WorldPacketHandlerContext context, LegacyTradeStateStore trades,
        WorldSession session, LegacyTradePacket packet, List<LegacyTradeSend> sends)
    {
        if (packet.UID is 0 or > uint.MaxValue || packet.UID == (ulong)session.Character.Uid) return "invalidTarget";
        var matches = context.Sessions!.Snapshot().Where(x => x.Session.Character.Uid == (long)packet.UID).ToArray();
        if (matches.Length != 1) return "targetMissingOrAmbiguous";
        var target = matches[0];
        if (trades.HasConnectionEnded(context.Connection) || trades.HasConnectionEnded(target.Connection))
            return "connectionEnded";
        if (!LegacyTradeEscrowClaims.TryRead(session.Character.Attributes, out var ownClaim, out _) || ownClaim is not null ||
            !LegacyTradeEscrowClaims.TryRead(target.Session.Character.Attributes, out var otherClaim, out _) || otherClaim is not null)
            return "unrecoveredEscrowClaim";
        if (!IsLegacyTradeSecurityAdmitted(target.Connection, target.Session)) return "targetSecondaryPasswordUnverifiedOrMalformed";
        if (session.Character.MapId != target.Session.Character.MapId ||
            LegacyGetDistance(session.Character.X, session.Character.Y, target.Session.Character.X, target.Session.Character.Y) > LegacyViewThreshold)
            return "targetOutsideManagedView";
        if (trades.Contains(context.Connection) || trades.Contains(target.Connection) ||
            session.LegacyTradeActive || target.Session.LegacyTradeActive)
        {
            TradeMessage(sends, context.Connection, (uint)session.Character.Uid, "Player already in a trade.");
            return "busy";
        }
        // Verify both current bindings together before reserving a pending pair. No disk change yet.
        if (!context.Sessions.TryCommitPair(context.Connection, session, session,
                target.Connection, target.Session, target.Session, static () => true)) return "staleRequest";
        var party = trades.Create(context.Connection, (uint)session.Character.Uid, target.Connection, (uint)target.Session.Character.Uid);
        TradePacket(sends, party, packet);
        TradePacket(sends, party.Other, packet with { UID = party.Uid });
        var info = new LegacyTradeInfoPacket(party.Uid, party.Other.Uid, session.Character.Name,
            (uint)Math.Clamp(session.Character.Level, 0L, uint.MaxValue), ReadAttributeUInt32(session.Character.Attributes, "BattlePower"))
        {
            Spouse = target.Session.Character.Name == ReadAttributeString(session.Character.Attributes, "Spouse"),
            Friend = HasLegacyTradeRelation(context, party.Uid, party.Other.Uid, 1),
            TradePartner = HasLegacyTradeRelation(context, party.Uid, party.Other.Uid, 3),
            Apprentice = HasLegacyTradeRelation(context, party.Uid, party.Other.Uid, 5),
            Teammate = context.TeamStates?.IsTeamMember(context.Connection, party.Other.Uid) == true,
        };
        sends.Add(new(party.Other.Connection, party.Other.Uid, info.ToFrameBytes()));
        // Guild/enemy/mentor native owners and derived native BattlePower are not fabricated here.
        return "requested";
    }

    private static string AcceptLegacyTradeRequest(WorldPacketHandlerContext context, LegacyTradeParty party,
        LegacyTradePacket packet, List<LegacyTradeSend> sends)
    {
        if (party.IsRequester || party.WindowOpen || packet.UID != party.Other.Uid ||
            !TryGetLegacyTradeSessions(context, party, out var own, out var other)) return "requestNotReciprocal";
        if (!IsLegacyTradeSecurityAdmitted(party.Other.Connection, other)) return "partnerSecondaryPasswordUnverifiedOrMalformed";
        if (own.Character.MapId != other.Character.MapId ||
            LegacyGetDistance(own.Character.X, own.Character.Y, other.Character.X, other.Character.Y) > LegacyViewThreshold)
            return "acceptOutsideManagedView";
        if (!context.Sessions!.TryCommitPair(party.Connection, own, own with { LegacyTradeActive = true },
            party.Other.Connection, other, other with { LegacyTradeActive = true }, static () => true)) return "staleAccept";
        party.WindowOpen = party.Other.WindowOpen = true;
        party.ResetConfirmations();
        TradePacket(sends, party, TradeReply(packet, 3) with { UID = party.Other.Uid });
        TradePacket(sends, party.Other, TradeReply(packet, 3) with { UID = party.Uid });
        return "opened";
    }

    private static bool CloseLegacyTrade(WorldPacketHandlerContext context, LegacyTradeStateStore trades,
        LegacyTradeParty party, LegacyTradePacket packet, List<LegacyTradeSend> sends)
    {
        if (!TryGetLegacyTradeSessions(context, party, out var own, out var other)) return false;
        WorldSession updatedOwn, updatedOther;
        try
        {
            updatedOwn = WithLegacyTradeWallet(own, checked(own.Character.Money + party.Money),
                checked(own.Character.ConquerPoints + party.ConquerPoints)) with { LegacyTradeActive = false };
            updatedOther = WithLegacyTradeWallet(other, checked(other.Character.Money + party.Other.Money),
                checked(other.Character.ConquerPoints + party.Other.ConquerPoints)) with { LegacyTradeActive = false };
            updatedOwn = LegacyTradeEscrowClaims.WithClaim(updatedOwn, null);
            updatedOther = LegacyTradeEscrowClaims.WithClaim(updatedOther, null);
            if (updatedOwn.Character.Money < 0 || updatedOther.Character.Money < 0 ||
                updatedOwn.Character.ConquerPoints is < 0 or > uint.MaxValue ||
                updatedOther.Character.ConquerPoints is < 0 or > uint.MaxValue) throw new OverflowException();
        }
        catch (OverflowException)
        {
            TradeMessage(sends, party.Connection, party.Uid, "Trade refund could not be applied; escrow is retained.");
            return false;
        }
        var reason = string.Empty;
        if (!context.Sessions!.TryCommitPair(party.Connection, own, updatedOwn, party.Other.Connection, other, updatedOther,
                () => TryPersistLegacyTradePair(context, own, updatedOwn, other, updatedOther, out reason)))
        {
            party.ResetConfirmations();
            TradeMessage(sends, party.Connection, party.Uid, IsLegacyTradeRecoveryRequired(reason)
                ? "Trade recovery is required. Actions are paused until server recovery."
                : "Trade could not be saved; escrow is retained. Retry cancellation.");
            if (IsLegacyTradeRecoveryRequired(reason))
                TradeMessage(sends, party.Other.Connection, party.Other.Uid,
                    "Trade recovery is required. Actions are paused until server recovery.");
            return false;
        }
        // The persisted refund consumes this claim even if later preview serialization
        // or transport fails. A retried cancellation must never credit it twice.
        trades.Remove(party);
        TradePacket(sends, party, TradeReply(packet, 5) with { UID = party.Other.Uid });
        TradePacket(sends, party.Other, TradeReply(packet, 5) with { UID = party.Uid });
        TradeWallet(sends, party, updatedOwn);
        TradeWallet(sends, party.Other, updatedOther);
        RefreshLegacyTradeInventoryPreviews(sends, party, updatedOwn.PlayerItems);
        RefreshLegacyTradeInventoryPreviews(sends, party.Other, updatedOther.PlayerItems);
        return true;
    }

    private static void RefreshLegacyTradeInventoryPreviews(List<LegacyTradeSend> sends,
        LegacyTradeParty party, PlayerItemSnapshot currentItems)
    {
        // Hardening deviation from native DestroyItems' stale offer references: never
        // recreate a missing/ambiguous client row; refresh only current inventory truth.
        foreach (var uid in party.Items.Keys)
        {
            var current = currentItems.InventoryItems.Where(item => item.Uid == uid).Take(2).ToArray();
            if (current.Length != 1) continue;
            try { TradeItem(sends, party, current[0], LegacyGameItemPacket.AddItemMode); }
            catch (Exception ex) when (ex is ArgumentException or OverflowException)
            {
                // A stale item's unrepresentable preview must not undo a refund
                // or preserve an already-consumed escrow claim. Resync separately.
            }
        }
    }

    // Root calls this before session removal and outside connection gates. False means escrow is NOT refunded.
    internal static async ValueTask<bool> CancelLegacyTradeForDisconnectAsync(WorldPacketHandlerContext context,
        CancellationToken cancellationToken = default)
    {
        if (context.Trades is not { } trades) return true;
        var sends = new List<LegacyTradeSend>();
        bool closed;
        using (var operation = context.TradeOperations is { } operations
            ? await operations.EnterExclusiveAsync(cancellationToken).ConfigureAwait(false)
            : null)
        lock (trades.SyncRoot)
            closed = context.TradeRecovery?.IsBlocked != true && (!trades.TryGet(context.Connection, out var party) ||
                CloseLegacyTrade(context, trades, party, new LegacyTradePacket(2) { Action = 2 }, sends));
        // Do not attempt writes to the disconnected socket; partner notifications remain private.
        sends.RemoveAll(x => ReferenceEquals(x.Connection, context.Connection));
        await SendLegacyTradeFramesAsync(context, sends, cancellationToken).ConfigureAwait(false);
        await context.Log.WriteLineAsync($"WorldServer LegacyTrade disconnect cleanup refundedAndClosed={closed}.");
        return closed;
    }

    private static bool TryGetLegacyTradeSessions(WorldPacketHandlerContext context, LegacyTradeParty party,
        out WorldSession own, out WorldSession other)
    {
        own = other = null!;
        return context.Sessions is { } sessions && sessions.TryGet(party.Connection, out var a) && a is not null &&
            sessions.TryGet(party.Other.Connection, out var b) && b is not null && a.Character.Uid == party.Uid &&
            b.Character.Uid == party.Other.Uid && !ReferenceEquals(party.Connection, party.Other.Connection) &&
            (own = a) is not null && (other = b) is not null;
    }

    private static bool HasLegacyTradeRelation(WorldPacketHandlerContext context, uint owner, uint target, int type) =>
        context.WorldData?.SocialAssociations.Any(x => x.OwnerId == owner && x.TargetId == target && x.RelationType == type) == true;

    private static bool IsLegacyTradeSecurityAdmitted(ConquerTcpConnection connection, WorldSession session) =>
        LegacySecondaryPasswordRuntime.IsAdmitted(connection, session);

    private static LegacyTradePacket TradeReply(LegacyTradePacket packet, uint action) => packet with { Type = action, Action = action };
    private static void TradePacket(List<LegacyTradeSend> sends, LegacyTradeParty to, LegacyTradePacket packet) =>
        sends.Add(new(to.Connection, to.Uid, packet.ToNativeFrameBytes()));
    private static void TradeMessage(List<LegacyTradeSend> sends, ConquerTcpConnection to, uint uid, string text) =>
        sends.Add(new(to, uid, LegacyMsgMessagePacket.SystemTalk(text).ToFrameBytes()));
    private static void TradeItem(List<LegacyTradeSend> sends, LegacyTradeParty to, PlayerItemSnapshotItem item, ushort mode) =>
        sends.Add(new(to.Connection, to.Uid, LegacyGameItemPacket.FromSnapshotItem(item, mode: mode).ToFrameBytes()));
    private static void TradeWallet(List<LegacyTradeSend> sends, LegacyTradeParty to, WorldSession session) =>
        sends.Add(new(to.Connection, to.Uid, new LegacyMsgUpdatePacket(to.Uid,
            [new(LegacyMsgUpdateDataType.Money, checked((ulong)session.Character.Money)),
             new(LegacyMsgUpdateDataType.ConquerPoints, checked((ulong)session.Character.ConquerPoints))]).ToFrameBytes()));

    private static async ValueTask SendLegacyTradeFramesAsync(WorldPacketHandlerContext context,
        IEnumerable<LegacyTradeSend> sends, CancellationToken cancellationToken)
    {
        foreach (var send in sends)
        {
            if (context.Sessions is not { } sessions || !sessions.TryGet(send.Connection, out var current) ||
                current is null || current.Character.Uid != send.ExpectedUid) continue;
            try { await send.Connection.SendAsync(send.Frame, cancellationToken).ConfigureAwait(false); }
            catch (Exception ex) when (ex is IOException or InvalidOperationException)
            {
                await context.Log.WriteLineAsync($"WorldServer LegacyTrade send failed after committed state: {ex.GetType().Name}.");
            }
        }
    }
}
