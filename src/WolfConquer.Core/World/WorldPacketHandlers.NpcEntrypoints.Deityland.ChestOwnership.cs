using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private sealed record DeityChestOwner(WorldSession Session, object Identity,
        LegacyNativeGameClientRuntime? Client, LegacyNativePlayerRuntime? Player,
        Dictionary<string, string> Attributes, KeyValuePair<string, string>[] Fields,
        LegacyDragonSkinInventory Items);

    private static bool TryCaptureDeityChestOwner(WorldPacketHandlerContext context, WorldSession supplied,
        out DeityChestOwner owner, out string reason)
    {
        owner = null!;
        reason = "deityChestCurrentOwnerUnavailable";
        if (context.Sessions is not { } sessions || context.Trades is not { } trades ||
            supplied is null || supplied.Character is null || supplied.AccountId is <= 0 or > uint.MaxValue ||
            supplied.Character.Uid is <= 0 or > uint.MaxValue || supplied.LegacyTradeActive ||
            trades.HasConnectionEnded(context.Connection) ||
            trades.TryGet(context.Connection, out var party) && (party.WindowOpen || party.Other is null ||
                party.Other.WindowOpen || party.Items.Count != 0 || party.Money != 0 || party.ConquerPoints != 0) ||
            context.TradeRecovery is not { } recovery || !recovery.IsReadyFor(supplied.UsersRoot, context.PlayerItemsRoot) ||
            !sessions.TryGet(context.Connection, out var current) || !ReferenceEquals(current, supplied) ||
            !LegacyNativeClientPublication.TryCaptureIdentity(sessions, context.Connection, supplied.Character.Uid, out var identity)) return false;
        // Script legitimately sets ActiveNpc using a record copy while context.Session
        // still refers to its predecessor. The supplied CURRENT Store ref is authority.
        if (supplied.Character.Attributes is not Dictionary<string, string> attributes ||
            attributes.GetType() != typeof(Dictionary<string, string>))
        { reason = "deityChestAttributesUnavailable"; return false; }
        var fields = attributes.ToArray();
        var unique = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in fields)
            if (string.IsNullOrEmpty(pair.Key) || pair.Value is null || !unique.TryAdd(pair.Key, pair.Value))
            { reason = "deityChestAttributesAmbiguous"; return false; }
        if (!LegacyTradeEscrowClaims.TryRead(unique, out var escrow, out _) || escrow is not null)
        { reason = "deityChestEscrowActiveOrInvalid"; return false; }
        if (!LegacyDragonSkinInventory.TryCapture(supplied.PlayerItems, out var items, out reason)) return false;
        var client = context.Connection.NativeGameClient;
        owner = new(supplied, identity, client, client?.Player, attributes, fields, items);
        if (IsCurrentDeityChestOwner(context, owner, supplied)) { reason = string.Empty; return true; }
        owner = null!;
        reason = "deityChestCurrentOwnerChanged";
        return false;
    }

    private static bool IsCurrentDeityChestOwner(WorldPacketHandlerContext context, DeityChestOwner owner,
        WorldSession expected) => context.Trades is { } trades && !trades.HasConnectionEnded(context.Connection) &&
        context.Sessions is { } sessions &&
        LegacyNativeClientPublication.TryCurrent(sessions, context.Connection, owner.Session.Character.Uid, owner.Identity, out var current) &&
        ReferenceEquals(current, expected) && ReferenceEquals(expected.NativeGameClient, owner.Client) &&
        ReferenceEquals(context.Connection.NativeGameClient, owner.Client) &&
        (owner.Client is null || ReferenceEquals(owner.Client.Player, owner.Player) && owner.Player is not null &&
            ReferenceEquals(owner.Player.Owner, owner.Client) && owner.Player.UID == owner.Session.Character.Uid);

    private static bool IsPreparedDeityChestOwner(WorldPacketHandlerContext context, DeityChestOwner owner,
        DeityChestPlan plan) => IsCurrentDeityChestOwner(context, owner, owner.Session) &&
        context.Trades is { } trades && !trades.HasConnectionEnded(context.Connection) &&
        !owner.Session.LegacyTradeActive && context.TradeRecovery is { } recovery &&
        recovery.IsReadyFor(owner.Session.UsersRoot, context.PlayerItemsRoot) &&
        DeityChestAttributesCurrent(owner) && owner.Items.IsCurrent(owner.Session.PlayerItems) &&
        plan.Items.IsCurrent(plan.Updated.PlayerItems);

    private static bool DeityChestAttributesCurrent(DeityChestOwner owner)
    {
        if (!ReferenceEquals(owner.Session.Character.Attributes, owner.Attributes) || owner.Attributes.Count != owner.Fields.Length) return false;
        var index = 0;
        try
        {
            foreach (var pair in owner.Attributes)
            {
                if (index == owner.Fields.Length || !string.Equals(pair.Key, owner.Fields[index].Key, StringComparison.Ordinal) ||
                    !string.Equals(pair.Value, owner.Fields[index].Value, StringComparison.Ordinal)) return false;
                index++;
            }
        }
        catch (InvalidOperationException) { return false; }
        return index == owner.Fields.Length;
    }
}
