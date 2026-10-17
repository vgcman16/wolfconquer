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
    [PacketHandler(WorldPacketOpcodes.LegacyTeamElitePkBrackets)]
    private static async ValueTask HandleLegacyTeamElitePkBracketsAsync(
        WorldPacketHandlerContext context,
        PacketFrame frame,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!LegacyTeamElitePkBracketsPacket.TryParse(frame, out var packet, out var matches))
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer rejected malformed LegacyTeamElitePkBrackets packet {FormatSession(context)}. {FormatFrameDiagnostic(frame)}")
                .ConfigureAwait(false);
            return;
        }

        var response = LegacyTeamElitePkBracketsPacket.CreateSourceEcho(packet);
        await context.Connection.SendAsync(response.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer echoed source-backed LegacyTeamElitePkBrackets " +
                $"{FormatSession(context)} action={packet.Action} page={packet.Page} " +
                $"packetNumber={packet.PacketNumber} group={packet.Group} gui={packet.GuiType} " +
                $"matches={packet.MatchCount} totalMatches={packet.TotalMatches} parsedMatchRows={matches.Count}; " +
                "no live team elite tournament matches are modeled.")
            .ConfigureAwait(false);
    }

}
