namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private sealed record LegacyWarehouseShowOwner(WorldSession Session, object Identity,
        LegacyNativeGameClientRuntime? Client, LegacyNativePlayerRuntime? Player,
        KeyValuePair<string, string>[] Fields, LegacyItemSaveGraph Items);

    // Reading does not acquire economic, escrow, recovery or persistence authority.
    private static bool TryCaptureLegacyWarehouseShowOwner(WorldPacketHandlerContext context, out LegacyWarehouseShowOwner owner)
    {
        owner = null!;
        if (context.Sessions is not { } sessions ||
            !sessions.TryGet(context.Connection, out var session) || session?.Character is not { } character ||
            session.AccountId <= 0 || character.Uid is <= 0 or > uint.MaxValue || character.Attributes is null ||
            !LegacyNativeClientPublication.TryCaptureIdentity(sessions, context.Connection, character.Uid, out var identity)) return false;
        try
        {
            var fields = character.Attributes.ToArray();
            var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var field in fields)
                if (string.IsNullOrEmpty(field.Key) || field.Value is null || !keys.Add(field.Key)) return false;
            // Preserve the native outer admission gate before materializing the item graph.
            if (context.Trades?.HasConnectionEnded(context.Connection) == true ||
                context.Connection.NativeGameClient?.PokerPlayer is not null ||
                !LegacySecondaryPasswordRuntime.IsAdmitted(context.Connection, session)) return false;
            if (!LegacyItemSaveGraph.TryCapture(session.PlayerItems, out var items)) return false;
            var client = context.Connection.NativeGameClient;
            owner = new(session, identity, client, client?.Player, fields, items);
            return IsCurrentLegacyWarehouseShowOwner(context, owner);
        }
        catch (InvalidOperationException) { return false; }
    }

    private static bool IsCurrentLegacyWarehouseShowOwner(WorldPacketHandlerContext context, LegacyWarehouseShowOwner owner)
    {
        if (context.Sessions is not { } sessions || context.Trades?.HasConnectionEnded(context.Connection) == true ||
            !LegacyNativeClientPublication.TryCurrent(sessions, context.Connection, owner.Session.Character.Uid,
                owner.Identity, out var current) || !ReferenceEquals(current, owner.Session) ||
            !ReferenceEquals(current.NativeGameClient, owner.Client) ||
            !ReferenceEquals(context.Connection.NativeGameClient, owner.Client) ||
            owner.Client is not null && (owner.Client.PokerPlayer is not null || owner.Player is null ||
                !ReferenceEquals(owner.Client.Player, owner.Player) || !ReferenceEquals(owner.Player.Owner, owner.Client) ||
                owner.Player.UID != current.Character.Uid)) return false;
        try
        {
            return current.Character.Attributes.SequenceEqual(owner.Fields) &&
                owner.Items.IsCurrent(current.PlayerItems) &&
                LegacySecondaryPasswordRuntime.IsAdmitted(context.Connection, current);
        }
        catch (InvalidOperationException) { return false; }
    }
}
