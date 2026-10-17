using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    [PacketHandler(WorldPacketOpcodes.LegacyArenaMatches)]
    private static async ValueTask HandleLegacyArenaMatchesAsync(
        WorldPacketHandlerContext context,
        PacketFrame frame,
        CancellationToken cancellationToken)
    {
        await HandleLegacyArenaMatchesCoreAsync(context, frame, cancellationToken).ConfigureAwait(false);
    }


    [PacketHandler(WorldPacketOpcodes.LegacyTeamArenaMatches)]
    private static async ValueTask HandleLegacyTeamArenaMatchesAsync(
        WorldPacketHandlerContext context,
        PacketFrame frame,
        CancellationToken cancellationToken)
    {
        await HandleLegacyArenaMatchesCoreAsync(context, frame, cancellationToken).ConfigureAwait(false);
    }


    private static async ValueTask HandleLegacyArenaMatchesCoreAsync(
        WorldPacketHandlerContext context,
        PacketFrame frame,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!LegacyArenaMatchesPacket.TryParseQuery(frame, out var query))
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer rejected malformed LegacyArenaMatches packet {FormatSession(context)} opcode={frame.Header.Opcode}.")
                .ConfigureAwait(false);
            return;
        }

        if (context.Session is not { })
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer skipped LegacyArenaMatches {FormatSession(context)} opcode={frame.Header.Opcode}; no character session is available.")
                .ConfigureAwait(false);
            return;
        }

        var response = frame.Header.Opcode == WorldPacketOpcodes.LegacyTeamArenaMatches
            ? CreateTeamArenaMatchesResponse(context, query.Page)
            : CreateArenaMatchesResponse(context, query.Page);
        await context.Connection.SendAsync(response.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer handled LegacyArenaMatches " +
                $"{FormatSession(context)} opcode={frame.Header.Opcode} page={query.Page} matches={response.MatchCount}.")
            .ConfigureAwait(false);
    }


    private static LegacyArenaMatchesPacket CreateTeamArenaMatchesResponse(
        WorldPacketHandlerContext context,
        uint page)
    {
        var entries = new List<LegacyArenaMatchesEntry>();
        var teamStates = context.TeamStates ?? new TeamSessionStateStore();
        foreach (var match in (context.TeamArenaStates ?? new TeamArenaSessionStateStore()).GetActiveMatches())
        {
            if (!TryGetTeamLeaderSession(context, match.First, out var firstLeader) ||
                !TryGetTeamLeaderSession(context, match.Second, out var secondLeader))
            {
                continue;
            }

            var firstTeamUid = teamStates.GetTeamUid(match.First);
            var secondTeamUid = teamStates.GetTeamUid(match.Second);
            if (firstTeamUid == 0 || secondTeamUid == 0)
            {
                continue;
            }

            entries.Add(new LegacyArenaMatchesEntry(
                firstTeamUid,
                firstLeader.Session.Character.Name,
                teamStates.GetMemberCount(match.First),
                secondTeamUid,
                secondLeader.Session.Character.Name,
                teamStates.GetMemberCount(match.Second)));
        }

        return LegacyArenaMatchesPacket.CreatePage(
            WorldPacketOpcodes.LegacyTeamArenaMatches,
            page,
            entries,
            playerCount: 0);
    }


    private static LegacyArenaMatchesPacket CreateArenaMatchesResponse(
        WorldPacketHandlerContext context,
        uint page)
    {
        var entries = new List<LegacyArenaMatchesRegularEntry>();
        foreach (var match in (context.ArenaStates ?? new ArenaSessionStateStore()).GetActiveMatches())
        {
            if (context.Sessions?.TryGet(match.First, out var firstSession) != true ||
                firstSession is null ||
                context.Sessions?.TryGet(match.Second, out var secondSession) != true ||
                secondSession is null)
            {
                continue;
            }

            entries.Add(new LegacyArenaMatchesRegularEntry(
                CreateArenaMatchesPlayerEntry(context, match.First, firstSession),
                CreateArenaMatchesPlayerEntry(context, match.Second, secondSession)));
        }

        return LegacyArenaMatchesPacket.CreateArenaPage(
            page,
            entries,
            playerCount: 0);
    }


    private static LegacyArenaMatchesPlayerEntry CreateArenaMatchesPlayerEntry(
        WorldPacketHandlerContext context,
        ConquerTcpConnection connection,
        WorldSession session)
    {
        var statistics = GetArenaStatisticsForSession(context, connection, session);
        return new LegacyArenaMatchesPlayerEntry(
            CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid)),
            ServerId: 0,
            BuildLegacyCharacterMesh(session.Character),
            session.Character.Name,
            CheckedUInt32(session.Character.Level, nameof(session.Character.Level)),
            CheckedUInt32(session.Character.Class, nameof(session.Character.Class)),
            Reserved: 0,
            statistics.TodayRank,
            statistics.ArenaPoints,
            statistics.TotalLose,
            statistics.TodayBattles,
            statistics.CurrentHonor,
            statistics.HistoryHonor);
    }
}
