using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

// Captured from the actual holder before transport; a saved UID is not issuance.
internal sealed class LegacyRuneRankingPublicationOwner
{
    private readonly WorldSessionStore store;
    private readonly ConquerTcpConnection connection;
    private readonly LegacyRuneRankingState ranking;
    private readonly object identity;
    private readonly long accountId, uid;
    private readonly LegacyNativeGameClientRuntime? client;
    private readonly LegacyNativePlayerRuntime? player;

    private LegacyRuneRankingPublicationOwner(WorldSessionStore store, ConquerTcpConnection connection,
        LegacyRuneRankingState ranking, WorldSession expected, object identity)
    {
        this.store = store; this.connection = connection; this.ranking = ranking; this.identity = identity;
        accountId = expected.AccountId; uid = expected.Character.Uid;
        client = expected.NativeGameClient; player = client?.Player;
    }

    internal static LegacyRuneRankingPublicationOwner? Capture(WorldPacketHandlerContext context)
    {
        if (context.RuneRankings is not { } ranking || context.Sessions is not { } store ||
            context.Session is not { } expected ||
            !ReferenceEquals(expected.NativeGameClient, context.Connection.NativeGameClient) ||
            !LegacyNativeClientPublication.TryCaptureIdentity(store, context.Connection,
                expected.Character.Uid, out var identity) ||
            !LegacyNativeClientPublication.TryCurrent(store, context.Connection, expected.Character.Uid,
                identity, out var current) || !ReferenceEquals(current, expected)) return null;
        var owner = new LegacyRuneRankingPublicationOwner(store, context.Connection, ranking, expected, identity);
        return owner.TryCurrent(context, expected, out _) ? owner : null;
    }

    internal bool TryCurrent(WorldPacketHandlerContext context, WorldSession invocation, out WorldSession current)
    {
        current = null!;
        if (!ReferenceEquals(context.Sessions, store) || !ReferenceEquals(context.Connection, connection) ||
            !ReferenceEquals(context.RuneRankings, ranking) || invocation.AccountId != accountId ||
            invocation.Character.Uid != uid || !ReferenceEquals(invocation.NativeGameClient, client) ||
            !ReferenceEquals(connection.NativeGameClient, client) ||
            !LegacyNativeClientPublication.TryCurrent(store, connection, uid, identity, out var captured) ||
            captured.AccountId != accountId || !ReferenceEquals(captured.NativeGameClient, client) ||
            client is not null && (player is null || !ReferenceEquals(client.Player, player) ||
                !ReferenceEquals(player.Owner, client) || player.UID != uid)) return false;
        current = captured;
        return true;
    }

    internal bool TryUpdateCurrent(WorldPacketHandlerContext context, WorldSession invocation) =>
        TryCurrent(context, invocation, out var current) &&
        store.TryUpdateRuneRankingWithNativeIdentity(connection, current, identity, ranking);
}
