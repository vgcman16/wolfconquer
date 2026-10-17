namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static bool TryRefreshLegacyPlayerMagicResourceSession(
        WorldPacketHandlerContext context,
        long acceptedAttackerUid,
        ref WorldSession session)
    {
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
}
