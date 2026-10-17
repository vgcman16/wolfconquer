using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    [PacketHandler(WorldPacketOpcodes.LegacyArenaRanking)]
    private static async ValueTask HandleLegacyArenaRankingAsync(
        WorldPacketHandlerContext context,
        PacketFrame frame,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!LegacyArenaRankingPacket.TryParseQuery(frame, out var query))
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer rejected malformed LegacyArenaRanking packet {FormatSession(context)}.")
                .ConfigureAwait(false);
            return;
        }

        if (context.Session is not { })
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer skipped LegacyArenaRanking {FormatSession(context)}; no character session is available.")
                .ConfigureAwait(false);
            return;
        }

        var response = LegacyArenaRankingPacket.CreatePage(
            query.RankType,
            query.Page,
            (context.ArenaStatistics ?? ArenaStatisticsProvider.Empty).Records);

        await context.Connection.SendAsync(response.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer handled LegacyArenaRanking " +
                $"{FormatSession(context)} rankType={query.RankType} page={query.Page} total={response.TotalCount} rows={response.Entries.Count}.")
            .ConfigureAwait(false);
    }


    [PacketHandler(WorldPacketOpcodes.LegacyArenaRank10)]
    private static async ValueTask HandleLegacyArenaRank10Async(
        WorldPacketHandlerContext context,
        PacketFrame frame,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!LegacyArenaRank10Packet.IsQuery(frame))
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer rejected malformed LegacyArenaRank10 packet {FormatSession(context)}.")
                .ConfigureAwait(false);
            return;
        }

        if (context.Session is not { })
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer skipped LegacyArenaRank10 {FormatSession(context)}; no character session is available.")
                .ConfigureAwait(false);
            return;
        }

        var response = LegacyArenaRank10Packet.Create(
            (context.ArenaStatistics ?? ArenaStatisticsProvider.Empty).Records);
        await context.Connection.SendAsync(response.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                $"WorldServer handled LegacyArenaRank10 {FormatSession(context)} rows={response.Entries.Count}.")
            .ConfigureAwait(false);
    }


    [PacketHandler(WorldPacketOpcodes.LegacyTeamArenaRanking)]
    private static async ValueTask HandleLegacyTeamArenaRankingAsync(
        WorldPacketHandlerContext context,
        PacketFrame frame,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!LegacyTeamArenaRankingPacket.TryParseQuery(frame, out var query))
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer rejected malformed LegacyTeamArenaRanking packet {FormatSession(context)}.")
                .ConfigureAwait(false);
            return;
        }

        if (context.Session is not { })
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer skipped LegacyTeamArenaRanking {FormatSession(context)}; no character session is available.")
                .ConfigureAwait(false);
            return;
        }

        var response = LegacyTeamArenaRankingPacket.CreatePage(
            query.Page,
            (context.TeamArenaStatistics ?? TeamArenaStatisticsProvider.Empty).Records);

        await context.Connection.SendAsync(response.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer handled LegacyTeamArenaRanking " +
                $"{FormatSession(context)} page={query.Page} total={response.Count} rows={response.Entries.Count}.")
            .ConfigureAwait(false);
    }


    [PacketHandler(WorldPacketOpcodes.LegacyTeamArenaRank10)]
    private static async ValueTask HandleLegacyTeamArenaRank10Async(
        WorldPacketHandlerContext context,
        PacketFrame frame,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!LegacyTeamArenaRank10Packet.IsQuery(frame))
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer rejected malformed LegacyTeamArenaRank10 packet {FormatSession(context)}.")
                .ConfigureAwait(false);
            return;
        }

        if (context.Session is not { })
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer skipped LegacyTeamArenaRank10 {FormatSession(context)}; no character session is available.")
                .ConfigureAwait(false);
            return;
        }

        var response = LegacyTeamArenaRank10Packet.Create(
            (context.TeamArenaStatistics ?? TeamArenaStatisticsProvider.Empty).Records);
        await context.Connection.SendAsync(response.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                $"WorldServer handled LegacyTeamArenaRank10 {FormatSession(context)} rows={response.Entries.Count}.")
            .ConfigureAwait(false);
    }
}
