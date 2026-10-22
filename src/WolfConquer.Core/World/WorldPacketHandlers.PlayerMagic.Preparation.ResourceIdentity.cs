namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static bool TryRefreshLegacyPlayerMagicResourceSession(
        WorldPacketHandlerContext context,
        long acceptedAttackerUid,
        ref WorldSession session,
        object? expectedNativeClientIdentity = null)
    {
        if (expectedNativeClientIdentity is { } identity)
        {
            if (context.Sessions is not { } world ||
                !LegacyNativeClientPublication.TryCurrent(world, context.Connection,
                    acceptedAttackerUid, identity, out var current)) return false;
            session = current;
            return true;
        }

        if (context.Sessions is not { } sessions)
        {
            return true;
        }

        if (!sessions.TryGet(context.Connection, out var currentSession) ||
            currentSession is null || currentSession.Character.Uid != acceptedAttackerUid)
        {
            return false;
        }

        session = currentSession;
        return true;
    }

    private static bool TryCommitLegacyPlayerMagicPreparation(
        WorldPacketHandlerContext context, ref WorldSession session,
        Func<WorldSession, WorldSession> update, object? expectedNativeClientIdentity)
    {
        if (expectedNativeClientIdentity is { } identity)
        {
            if (context.Sessions is not { } world ||
                !LegacyNativeClientPublication.TryUpdate(world, context.Connection,
                    session.Character.Uid, identity, update, out var current)) return false;
            session = current;
            return true;
        }
        session = update(session);
        context.Sessions?.Set(context.Connection, session); // Existing nonfinite/unbound behavior.
        return true;
    }
}
