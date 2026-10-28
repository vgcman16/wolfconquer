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
        if (context.RuneRankings is not { } rankings) return;
        if (context.Sessions is not null)
        {
            // Refresh only under the identity captured before this invocation's
            // first transport. The Store leaf still rejects a stale exact record.
            context.RuneRankingPublicationOwner?.TryUpdateCurrent(context, session);
            return;
        }
        // Only truly standalone/unbound callers retain direct rank updates.
        if (context.Connection.NativeGameClient?.OrdinarySpellBinding is not null ||
            session.NativeGameClient?.OrdinarySpellBinding is not null) return;
        rankings.Update(session.Character, session.PlayerItems);
    }

    private static WorldPacketHandlerContext CaptureLegacyRuneRankingPublicationOwner(WorldPacketHandlerContext context)
    {
        if (context.RuneRankings is null || context.Sessions is null ||
            context.RuneRankingPublicationOwner is not null) return context;
        return context with { RuneRankingPublicationOwner = LegacyRuneRankingPublicationOwner.Capture(context) };
    }
}
