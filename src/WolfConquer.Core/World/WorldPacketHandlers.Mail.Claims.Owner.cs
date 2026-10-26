namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private sealed record LegacyMailboxClaimOwner(WorldPacketHandlerContext Context, WorldSession Expected,
        object Identity, LegacyNativeGameClientRuntime? Client, LegacyNativePlayerRuntime? Player,
        KeyValuePair<string, string>[] Fields);

    private static bool TryCaptureLegacyMailboxClaimOwner(WorldPacketHandlerContext context,
        out LegacyMailboxClaimOwner owner)
    {
        owner = null!;
        if (context.Sessions is not { } sessions || context.Trades is not { } trades ||
            !Monitor.IsEntered(trades.SyncRoot) || context.Mailboxes is not { } mailboxes ||
            !Monitor.IsEntered(mailboxes.SyncRoot) || context.TradeRecovery is not { } recovery ||
            !sessions.TryGet(context.Connection, out var session) || session?.Character is not { } character ||
            session.AccountId <= 0 || character.Uid is <= 0 or > uint.MaxValue || character.Money < 0 ||
            character.ConquerPoints is < 0 or > uint.MaxValue || character.Attributes is null ||
            !recovery.IsReadyForMailbox(session.UsersRoot, context.PlayerItemsRoot, mailboxes.ClaimRoot) ||
            !LegacyNativeClientPublication.TryCaptureIdentity(sessions, context.Connection, character.Uid, out var identity)) return false;
        var fields = character.Attributes.ToArray();
        var unique = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in fields)
            if (string.IsNullOrEmpty(field.Key) || field.Value is null || !unique.TryAdd(field.Key, field.Value)) return false;
        var client = context.Connection.NativeGameClient;
        owner = new(context, session, identity, client, client?.Player, fields);
        return HasCoherentLegacyWarehouseTransferTrade(context, session, unique) &&
            IsCurrentLegacyMailboxClaimOwner(owner, session);
    }

    private static bool IsCurrentLegacyMailboxClaimOwner(LegacyMailboxClaimOwner owner, WorldSession expected)
    {
        var context = owner.Context;
        return context.Trades is { } trades && !trades.HasConnectionEnded(context.Connection) &&
            context.Sessions is { } sessions &&
            LegacyNativeClientPublication.TryCurrent(sessions, context.Connection, owner.Expected.Character.Uid,
                owner.Identity, out var current) && ReferenceEquals(current, expected) &&
            ReferenceEquals(expected.NativeGameClient, owner.Client) &&
            ReferenceEquals(context.Connection.NativeGameClient, owner.Client) &&
            (owner.Client is null || owner.Player is not null && ReferenceEquals(owner.Client.Player, owner.Player) &&
                ReferenceEquals(owner.Player.Owner, owner.Client) && owner.Player.UID == expected.Character.Uid) &&
            LegacySecondaryPasswordRuntime.IsAdmitted(context.Connection, expected);
    }

    private static bool MailboxClaimAdmissionCurrent(LegacyMailboxClaimOwner owner) =>
        owner.Context.TradeRecovery!.IsReadyForMailbox(owner.Expected.UsersRoot, owner.Context.PlayerItemsRoot,
            owner.Context.Mailboxes!.ClaimRoot) &&
        owner.Expected.Character.Attributes.SequenceEqual(owner.Fields) &&
        HasCoherentLegacyWarehouseTransferTrade(owner.Context, owner.Expected, owner.Expected.Character.Attributes) &&
        IsCurrentLegacyMailboxClaimOwner(owner, owner.Expected);
}
