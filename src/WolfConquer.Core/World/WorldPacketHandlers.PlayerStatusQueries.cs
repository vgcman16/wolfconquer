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

    [PacketHandler(WorldPacketOpcodes.LegacyGoldLeaguePoint)]
    private static async ValueTask HandleLegacyGoldLeaguePointAsync(
        WorldPacketHandlerContext context,
        PacketFrame frame,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (context.Session is not { } session)
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer skipped LegacyGoldLeaguePoint {FormatSession(context)}; no character session is available.")
                .ConfigureAwait(false);
            return;
        }

        var points = ReadAttributeUInt32(session.Character.Attributes, "ChampionPoints");
        var historyPoints = ReadAttributeUInt32(session.Character.Attributes, "HistoryChampionPoints");
        await context.Connection.SendAsync(
                new MsgGoldLeaguePointPacket(points, historyPoints).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                $"WorldServer handled LegacyGoldLeaguePoint {FormatSession(context)} points={points} historyPoints={historyPoints}.")
            .ConfigureAwait(false);
    }

    [PacketHandler(WorldPacketOpcodes.LegacyFlagIcon)]
    private static async ValueTask HandleLegacyFlagIconAsync(
        WorldPacketHandlerContext context,
        PacketFrame frame,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!LegacyFlagIconPacket.TryParse(frame, out var packet))
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer rejected malformed LegacyFlagIcon packet {FormatSession(context)}.")
                .ConfigureAwait(false);
            return;
        }

        if (context.Session is null)
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer skipped LegacyFlagIcon {FormatSession(context)}; no character session is available.")
                .ConfigureAwait(false);
            return;
        }

        var response = LegacyFlagIconPacket.CreateResponse(
            packet.Uid,
            packet.Icon,
            packet.Level,
            packet.Damage);
        await context.Connection.SendAsync(response.ToFrameBytes(), cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                $"WorldServer handled LegacyFlagIcon {FormatSession(context)} uid={packet.Uid} icon={packet.Icon} level={packet.Level}.")
            .ConfigureAwait(false);
    }


    [PacketHandler(WorldPacketOpcodes.LegacyPlayerStatus)]
    private static async ValueTask HandleLegacyPlayerStatusAsync(
        WorldPacketHandlerContext context,
        PacketFrame frame,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!LegacyPlayerStatusQueryPacket.TryParse(frame, out var query))
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer rejected malformed LegacyPlayerStatus packet {FormatSession(context)}. {FormatFrameDiagnostic(frame)}")
                .ConfigureAwait(false);
            return;
        }

        if (context.Session is not { } session)
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer skipped LegacyPlayerStatus {FormatSession(context)} uid={query.Uid}; no character session is available.")
                .ConfigureAwait(false);
            return;
        }

        var selfUid = CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid));
        var targetUid = query.ResolveTargetUid(session);
        var targetSession = targetUid == selfUid
            ? session
            : TryGetSessionByUid(context, targetUid, out var active)
                ? active.Session
                : null;

        if (targetSession is null)
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer skipped LegacyPlayerStatus {FormatSession(context)} uid={targetUid}; target session was not found.")
                .ConfigureAwait(false);
            return;
        }

        var response = CreateNativeMagicCachedPlayerStatusResponse(context, targetSession);
        await context.Connection.SendAsync(response.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                $"WorldServer handled LegacyPlayerStatus {FormatSession(context)} uid={targetUid}.")
            .ConfigureAwait(false);
    }

    private static LegacyPlayerStatusPacket CreateLegacyPlayerStatusResponse(
        WorldSession session,
        WorldDataCatalog? worldData)
    {
        return new LegacyPlayerStatusPacket(CalculateLegacyRuntimeStatusFields(session, worldData));
    }

}
