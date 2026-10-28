using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

// An entered native publication pins a live client, not a record-copy or a persisted UID alias.
internal static class LegacyNativeClientPublication
{
    internal static bool TryCaptureIdentity(WorldSessionStore world, ConquerTcpConnection connection,
        long uid, out object identity)
    {
        object? captured = null;
        var registered = world.WithCurrentNativeClientIdentity(connection, uid, value => captured = value);
        identity = captured!;
        return registered;
    }

    internal static bool TryCurrent(WorldSessionStore world, ConquerTcpConnection connection,
        long uid, object identity, out WorldSession session)
    {
        WorldSession? captured = null;
        var matched = false;
        var registered = world.WithCurrentNativeClientIdentity(connection, uid, current =>
        {
            if (ReferenceEquals(current, identity)) matched = world.TryGet(connection, out captured);
        });
        session = captured!;
        return registered && matched && captured is not null;
    }

    internal static bool TryUpdate(WorldSessionStore world, ConquerTcpConnection connection,
        long uid, object identity, Func<WorldSession, WorldSession> update, out WorldSession session)
    {
        var changed = world.TryUpdateCurrentNativeSession(connection, uid, identity, update, out var written);
        session = written!;
        return changed;
    }
    internal static async ValueTask<WorldSession?> SendOwnerAndViewAsync(
        WorldSessionStore world, ConquerTcpConnection connection, long uid, object identity,
        ReadOnlyMemory<byte> capturedFrame, Func<CharacterSnapshot, CharacterSnapshot, bool> canSee,
        CancellationToken cancellationToken)
    {
        if (!TryCurrent(world, connection, uid, identity, out var current)) return null;
        await connection.SendAsync(capturedFrame, cancellationToken).ConfigureAwait(false);
        if (!TryCurrent(world, connection, uid, identity, out current)) return null;

        // View begins AFTER owner publication; capture original recipients at this source phase.
        var viewers = new List<(ConquerTcpConnection Connection, long Uid, object Identity)>();
        foreach (var candidate in world.Snapshot())
        {
            var peerUid = candidate.Session.Character.Uid;
            if (ReferenceEquals(candidate.Connection, connection) || peerUid == uid ||
                !TryCaptureIdentity(world, candidate.Connection, peerUid, out var peerIdentity) ||
                !TryCurrent(world, candidate.Connection, peerUid, peerIdentity, out var peer) ||
                !canSee(current.Character, peer.Character)) continue;
            viewers.Add((candidate.Connection, peerUid, peerIdentity));
        }
        foreach (var viewer in viewers)
        {
            if (!TryCurrent(world, connection, uid, identity, out current)) return null;
            if (!TryCurrent(world, viewer.Connection, viewer.Uid, viewer.Identity, out var peer) ||
                !canSee(current.Character, peer.Character)) continue;
            await viewer.Connection.SendAsync(capturedFrame, cancellationToken).ConfigureAwait(false);
            if (!TryCurrent(world, connection, uid, identity, out current)) return null;
        }
        return current;
    }
}
