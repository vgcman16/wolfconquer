using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    [PacketHandler(WorldPacketOpcodes.LegacyGenericRanking)]
    private static async ValueTask HandleLegacyGenericRankingAsync(
        WorldPacketHandlerContext context,
        PacketFrame frame,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!LegacyGenericRankingPacket.TryParse(frame, out var packet))
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer rejected malformed LegacyGenericRanking packet {FormatSession(context)}. {FormatFrameDiagnostic(frame)}")
                .ConfigureAwait(false);
            return;
        }

        if (context.Session is not { } session)
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer skipped LegacyGenericRanking {FormatSession(context)} action={packet.Action} rankType={packet.RankType}; no character session is available.")
                .ConfigureAwait(false);
            return;
        }

        if (packet.Action is LegacyGenericRankingAction.Ranking or LegacyGenericRankingAction.PrestigeRanks)
        {
            var emptyRanking = LegacyGenericRankingPacket.EmptyResponse(packet.Action, packet.RankType, packet.Page);
            await context.Connection.SendAsync(emptyRanking.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    $"WorldServer handled LegacyGenericRanking {FormatSession(context)} action={packet.Action} rankType={packet.RankType} entries=0.")
                .ConfigureAwait(false);
            return;
        }

        if (packet.Action is LegacyGenericRankingAction.UpdateScreen or LegacyGenericRankingAction.InformationRequest)
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer ignored LegacyGenericRanking {FormatSession(context)} action={packet.Action} rankType={packet.RankType}; source has no inbound response.")
                .ConfigureAwait(false);
            return;
        }

        if (packet.Action != LegacyGenericRankingAction.QueryCount)
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer skipped LegacyGenericRanking {FormatSession(context)} action={packet.Action} rankType={packet.RankType}; action is not modeled.")
                .ConfigureAwait(false);
            return;
        }

        var chiState = LegacyChiState.FromSnapshot(session.Character);
        if (!TryGetLegacyGenericRankingChiPower(packet.RankType, chiState, out var power))
        {
            var emptyQuery = LegacyGenericRankingPacket.EmptyResponse(packet.Action, packet.RankType, packet.Page);
            await context.Connection.SendAsync(emptyQuery.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    $"WorldServer handled LegacyGenericRanking {FormatSession(context)} action={packet.Action} rankType={packet.RankType} entries=0.")
                .ConfigureAwait(false);
            return;
        }

        var response = LegacyGenericRankingPacket.ChiQueryCount(
            session.Character.Uid,
            session.Character.Name,
            power);
        await context.Connection.SendAsync(response.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                $"WorldServer handled LegacyGenericRanking {FormatSession(context)} action={packet.Action} rankType={packet.RankType}.")
            .ConfigureAwait(false);
    }

    private static bool TryGetLegacyGenericRankingChiPower(
        LegacyGenericRankingRankType rankType,
        LegacyChiState state,
        out LegacyChiPowerState power)
    {
        var powerType = rankType switch
        {
            LegacyGenericRankingRankType.DragonChi => LegacyChiPowerType.Dragon,
            LegacyGenericRankingRankType.PhoenixChi => LegacyChiPowerType.Phoenix,
            LegacyGenericRankingRankType.TigerChi => LegacyChiPowerType.Tiger,
            LegacyGenericRankingRankType.TurtleChi => LegacyChiPowerType.Turtle,
            _ => LegacyChiPowerType.None,
        };

        if (powerType != LegacyChiPowerType.None)
        {
            foreach (var candidate in state.Powers)
            {
                if (candidate.Type == powerType)
                {
                    power = candidate;
                    return true;
                }
            }
        }

        power = default!;
        return false;
    }

}
