namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private sealed record LegacyWarehouseItemTransferOwner(WorldSession Session, object Identity,
        LegacyNativeGameClientRuntime? Client, LegacyNativePlayerRuntime? Player, KeyValuePair<string, string>[] Fields);

    private static bool TryCaptureLegacyWarehouseItemTransferOwner(WorldPacketHandlerContext context, out LegacyWarehouseItemTransferOwner owner)
    {
        owner = null!;
        if (context.Sessions is not { } sessions || context.Trades is not { } trades ||
            !sessions.TryGet(context.Connection, out var session) || session?.Character is not { } character ||
            session.AccountId <= 0 || character.Uid is <= 0 or > uint.MaxValue || character.Money < 0 || character.ConquerPoints < 0 ||
            character.Attributes is null ||
            trades.HasConnectionEnded(context.Connection) ||
            !LegacyNativeClientPublication.TryCaptureIdentity(sessions, context.Connection, character.Uid, out var identity)) return false;
        try
        {
            var fields = character.Attributes.ToArray();
            var captured = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var field in fields)
                if (string.IsNullOrEmpty(field.Key) || field.Value is null || !captured.TryAdd(field.Key, field.Value)) return false;
            if (!HasCoherentLegacyWarehouseTransferTrade(context, session, captured) ||
                !LegacySecondaryPasswordRuntime.IsAdmitted(context.Connection, session)) return false;
            var client = context.Connection.NativeGameClient;
            owner = new(session, identity, client, client?.Player, fields);
            return IsCurrentLegacyWarehouseItemTransferOwner(context, owner, session) && LegacyWarehouseItemTransferAttributesCurrent(owner);
        }
        catch (InvalidOperationException) { return false; }
    }

    private static bool IsCurrentLegacyWarehouseItemTransferOwner(WorldPacketHandlerContext context, LegacyWarehouseItemTransferOwner owner,
        WorldSession expected) => context.Sessions is { } sessions && context.Trades is { } trades &&
        !trades.HasConnectionEnded(context.Connection) &&
        LegacyNativeClientPublication.TryCurrent(sessions, context.Connection, expected.Character.Uid, owner.Identity, out var current) &&
        ReferenceEquals(current, expected) && ReferenceEquals(expected.NativeGameClient, owner.Client) &&
        ReferenceEquals(context.Connection.NativeGameClient, owner.Client) &&
        (owner.Client is null || owner.Client.PokerPlayer is null && owner.Player is not null && ReferenceEquals(owner.Client.Player, owner.Player) &&
            ReferenceEquals(owner.Player.Owner, owner.Client) && owner.Player.UID == expected.Character.Uid);

    private static bool LegacyWarehouseItemTransferAttributesCurrent(LegacyWarehouseItemTransferOwner owner)
    {
        try { return owner.Session.Character.Attributes.SequenceEqual(owner.Fields); }
        catch (InvalidOperationException) { return false; }
    }

    // Called only under the actual Trades gate. Storage may change an offered
    // graph; final Trade validation owns its abort/refund, not this item-only move.
    private static bool HasCoherentLegacyWarehouseTransferTrade(WorldPacketHandlerContext context, WorldSession session,
        IReadOnlyDictionary<string, string> fields)
    {
        var trades = context.Trades!;
        if (!LegacyTradeEscrowClaims.TryRead(fields, out var claim, out _)) return false;
        if (!trades.TryGet(context.Connection, out var party)) return !session.LegacyTradeActive && claim is null;
        if (!ReferenceEquals(party.Connection, context.Connection) || party.Uid != session.Character.Uid ||
            party.TradeId == Guid.Empty || party.Money < 0 || party.WindowOpen != session.LegacyTradeActive ||
            party.Other is not { } peer || ReferenceEquals(peer, party) ||
            !ReferenceEquals(peer.Other, party) || peer.TradeId != party.TradeId ||
            !trades.TryGet(peer.Connection, out var registeredPeer) || !ReferenceEquals(peer, registeredPeer) ||
            context.Sessions is not { } sessions || !sessions.TryGet(peer.Connection, out var peerSession) ||
            peerSession?.Character.Uid != peer.Uid) return false;
        return claim is null ? party.Money == 0 && party.ConquerPoints == 0 :
            claim.TradeId == party.TradeId && claim.Money == party.Money && claim.ConquerPoints == party.ConquerPoints;
    }
}
