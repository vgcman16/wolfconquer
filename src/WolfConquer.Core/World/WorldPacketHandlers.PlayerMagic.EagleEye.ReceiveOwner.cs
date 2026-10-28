using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    // One actual 8418 EagleEye issuance, captured before its transport/rate
    // callbacks. A same UID/record after removal is not the original issuance.
    private sealed class LegacyEagleEyeReceiveOwner
    {
        private readonly WorldSessionStore store;
        private readonly ConquerTcpConnection connection;
        private readonly object identity;
        private readonly long accountId, uid;
        private readonly string? usersRoot;
        private readonly LegacyNativeGameClientRuntime? client;
        private readonly LegacyNativePlayerRuntime? player;
        private readonly LegacyOrdinarySpellBinding? binding;

        private LegacyEagleEyeReceiveOwner(WorldSessionStore store, ConquerTcpConnection connection,
            WorldSession expected, object identity)
        {
            this.store = store; this.connection = connection; this.identity = identity;
            accountId = expected.AccountId; uid = expected.Character.Uid; usersRoot = expected.UsersRoot;
            client = expected.NativeGameClient; player = client?.Player; binding = client?.OrdinarySpellBinding;
        }

        internal static bool TryCapture(WorldPacketHandlerContext context, WorldSession expected,
            out LegacyEagleEyeReceiveOwner? owner)
        {
            owner = null;
            if (context.Sessions is not { } store || !ReferenceEquals(context.Connection.NativeGameClient, expected.NativeGameClient) ||
                !LegacyNativeClientPublication.TryCaptureIdentity(store, context.Connection, expected.Character.Uid, out var identity))
                return false;
            var captured = new LegacyEagleEyeReceiveOwner(store, context.Connection, expected, identity);
            if (!captured.IsCurrent(context, expected)) return false;
            owner = captured; return true;
        }

        internal bool IsCurrent(WorldPacketHandlerContext context, WorldSession expected) =>
            ReferenceEquals(context.Sessions, store) && ReferenceEquals(context.Connection, connection) &&
            expected.AccountId == accountId && expected.Character.Uid == uid && expected.UsersRoot == usersRoot &&
            ReferenceEquals(connection.NativeGameClient, client) && ReferenceEquals(expected.NativeGameClient, client) &&
            (client is null || player is not null && ReferenceEquals(client.Player, player) &&
                ReferenceEquals(player.Owner, client) && player.UID == uid && ReferenceEquals(client.OrdinarySpellBinding, binding) &&
                (binding is null || store.IsBoundSpellOwnerCurrent(binding))) &&
            LegacyNativeClientPublication.TryCurrent(store, connection, uid, identity, out var current) &&
            ReferenceEquals(current, expected);

        internal bool TryPublishPrepared(WorldPacketHandlerContext context, WorldSession expected,
            WorldSession prepared, out WorldSession published)
        {
            published = null!;
            if (!IsCurrent(context, expected) || !store.TryCompareAndSetWithNativeIdentity(connection, expected,
                    identity, prepared, out var final) || final is null || !IsCurrent(context, final)) return false;
            published = final; return true;
        }
    }
}
