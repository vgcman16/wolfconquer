namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private sealed record LegacySashUpgradeOwner(WorldSession Session, object Identity,
        LegacyNativeGameClientRuntime? Client, LegacyNativePlayerRuntime? Player, ulong Count,
        KeyValuePair<string, string>[] Fields);

    private static bool TryCaptureLegacySashUpgradeOwner(WorldPacketHandlerContext context, out LegacySashUpgradeOwner owner)
    {
        owner = null!;
        if (context.Sessions is not { } sessions || context.Trades is not { } trades ||
            !sessions.TryGet(context.Connection, out var session) || session?.Character is not { } character ||
            session.AccountId <= 0 || character.Uid is <= 0 or > uint.MaxValue || character.ConquerPoints < 0 ||
            character.Money < 0 || character.Attributes is null || session.LegacyTradeActive ||
            trades.Contains(context.Connection) || trades.HasConnectionEnded(context.Connection) ||
            !LegacyNativeClientPublication.TryCaptureIdentity(sessions, context.Connection, character.Uid, out var identity))
            return false;
        try
        {
            var fields = character.Attributes.ToArray();
            var owned = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var field in fields)
                if (string.IsNullOrEmpty(field.Key) || field.Value is null || !owned.TryAdd(field.Key, field.Value)) return false;
            if (!LegacyInventorySashUpgradePersistence.TryReadCount(owned, out var count) ||
                !LegacyTradeEscrowClaims.TryRead(owned, out var claim, out _) || claim is not null) return false;
            var client = context.Connection.NativeGameClient;
            owner = new(session, identity, client, client?.Player, count, fields);
            return IsCurrentLegacySashUpgradeOwner(context, owner, session) && LegacySashUpgradeAttributesCurrent(owner);
        }
        catch (InvalidOperationException) { return false; }
    }

    private static bool IsCurrentLegacySashUpgradeOwner(WorldPacketHandlerContext context,
        LegacySashUpgradeOwner owner, WorldSession expected) => context.Sessions is { } sessions &&
        context.Trades is { } trades && !trades.HasConnectionEnded(context.Connection) &&
        LegacyNativeClientPublication.TryCurrent(sessions, context.Connection, expected.Character.Uid,
            owner.Identity, out var current) && ReferenceEquals(current, expected) &&
        ReferenceEquals(expected.NativeGameClient, owner.Client) &&
        ReferenceEquals(context.Connection.NativeGameClient, owner.Client) &&
        (owner.Client is null || owner.Player is not null && ReferenceEquals(owner.Client.Player, owner.Player) &&
            ReferenceEquals(owner.Player.Owner, owner.Client) && owner.Player.UID == expected.Character.Uid);

    private static bool LegacySashUpgradeAttributesCurrent(LegacySashUpgradeOwner owner)
    {
        try { return owner.Session.Character.Attributes.SequenceEqual(owner.Fields); }
        catch (InvalidOperationException) { return false; }
    }
}
