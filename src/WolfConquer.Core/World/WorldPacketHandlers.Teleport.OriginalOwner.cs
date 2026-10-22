using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

// Opt-in identity captured BEFORE the caller's first awaited publication.
internal sealed class LegacyTeleportOwner(
    WorldSessionStore world, ConquerTcpConnection connection, long uid, object identity)
{
    internal WorldSessionStore World { get; } = world;
    internal ConquerTcpConnection Connection { get; } = connection;
    internal long Uid { get; } = uid;
    internal object Identity { get; } = identity;

    internal bool TryCurrent(out WorldSession session) =>
        LegacyNativeClientPublication.TryCurrent(World, Connection, Uid, Identity, out session);

    internal bool TryCompareAndSet(WorldSession expected, WorldSession destination)
    {
        if (expected.Character.Uid != Uid || destination.Character.Uid != Uid)
            return false;
        var committed = false;
        World.WithCurrentNativeClientIdentity(Connection, Uid, current =>
        {
            if (ReferenceEquals(current, Identity))
                committed = World.TryCompareAndSet(Connection, expected, destination);
        });
        return committed;
    }
}

public static partial class WorldPacketHandlers
{
    internal static bool TryCaptureLegacyTeleportOwner(
        WorldPacketHandlerContext context, out LegacyTeleportOwner? owner)
    {
        owner = null;
        if (context.Sessions is not { } world || context.Session is not { } session ||
            !LegacyNativeClientPublication.TryCaptureIdentity(world, context.Connection,
                session.Character.Uid, out var identity))
            return false;
        owner = new(world, context.Connection, session.Character.Uid, identity);
        return true;
    }

    // Callers that already own a native-client lease construct this identity from THAT lease;
    // they MUST NOT recapture by UID after an await.
    internal static async ValueTask<WorldSession?> ExecuteOwnedLegacyTeleportAsync(
        WorldPacketHandlerContext context, LegacyTeleportOwner owner,
        LegacyTeleportRequest request, LegacyTeleportDestination destination,
        uint timestamp, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(owner);
        if (request.RequestedMapId != destination.MapId || request.RequestedDynamicId < 0 ||
            (ulong)request.RequestedDynamicId != destination.DynamicId)
            throw new ArgumentException("Owned teleport preflight and destination must describe the same request.");
        if (!ReferenceEquals(context.Connection, owner.Connection) ||
            !ReferenceEquals(context.Sessions, owner.World) || !owner.TryCurrent(out var session))
            return null;
        var owned = context with { Session = session, TeleportOwner = owner };
        try
        {
            var preflight = await PrepareLegacyTeleportAsync(
                owned, session, request, cancellationToken).ConfigureAwait(false);
            if (!owner.TryCurrent(out session) || !preflight.Allowed)
                return null;
            var result = await ExecuteLegacyTeleportDestinationAsync(
                owned with { Session = session }, session, destination,
                timestamp, cancellationToken).ConfigureAwait(false);
            return result is not null && owner.TryCurrent(out var current) ? current : null;
        }
        catch (LegacyTeleportOwnerRetiredException)
        {
            // No rollback of earlier original-client writes or already-entered SendAsync.
            return null;
        }
    }

    private static void RequireLegacyTeleportOwner(WorldPacketHandlerContext context)
    {
        if (context.TeleportOwner is { } owner &&
            (!ReferenceEquals(context.Connection, owner.Connection) ||
             !ReferenceEquals(context.Sessions, owner.World) || !owner.TryCurrent(out _)))
            throw new LegacyTeleportOwnerRetiredException();
    }

    // Explicit context flow, NOT AsyncLocal/network-global state. A new reentrant packet context
    // does not inherit a retired caller's guard. Peer sends are gated by the SOURCE invocation too.
    private static async ValueTask SendLegacyTeleportFrameAsync(
        WorldPacketHandlerContext context, ConquerTcpConnection recipient,
        ReadOnlyMemory<byte> frame, CancellationToken cancellationToken)
    {
        RequireLegacyTeleportOwner(context);
        await recipient.SendAsync(frame, cancellationToken).ConfigureAwait(false);
        RequireLegacyTeleportOwner(context);
    }

    internal static bool TryCommitOwnedLegacyTeleport(
        WorldPacketHandlerContext context, WorldSession expected, WorldSession destination)
    {
        if (context.TeleportOwner is not { } owner)
            return context.Sessions?.TryCompareAndSet(context.Connection, expected, destination) == true;
        return ReferenceEquals(context.Connection, owner.Connection) &&
            ReferenceEquals(context.Sessions, owner.World) && owner.TryCompareAndSet(expected, destination);
    }

    private sealed class LegacyTeleportOwnerRetiredException : Exception { }
}
