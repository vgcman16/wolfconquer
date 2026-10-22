namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static LegacyMsgMessagePacket CreateLegacyRuneRankingMessage(
        WorldPacketHandlerContext context, WorldSession session)
    {
        return context.RuneRankings is { } rankings
            ? LegacyRuneRankMessage.CreateLiveStartup(session.Character, session.PlayerItems, rankings)
            : LegacyRuneRankMessage.CreateStartup(
                session.Character, session.PlayerItems, context.WorldData?.RuneRankings);
    }

    private static void UpdateLegacyRuneRanking(WorldPacketHandlerContext context, WorldSession session)
    {
        var acceptedOriginalUid = session.Character.Uid;
        if (!TryRefreshLegacyPlayerMagicResourceSession(context, acceptedOriginalUid, ref session))
        {
            return;
        }

        context.RuneRankings?.Update(session.Character, session.PlayerItems);
    }
}
