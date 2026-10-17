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
    [PacketHandler(WorldPacketOpcodes.LegacyRankMemberShow)]
    private static async ValueTask HandleLegacyRankMemberShowAsync(
        WorldPacketHandlerContext context,
        PacketFrame frame,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!LegacyRankMemberShowPacket.TryParse(frame, out var query))
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer rejected malformed LegacyRankMemberShow packet {FormatSession(context)}. {FormatFrameDiagnostic(frame)}")
                .ConfigureAwait(false);
            return;
        }

        if (context.Session is null)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyRankMemberShow " +
                    $"{FormatSession(context)} type={query.Type}; no character session is available.")
                .ConfigureAwait(false);
            return;
        }

        var response = LegacyRankMemberShowPacket.EmptyResponse(query.Type);
        await context.Connection.SendAsync(response.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer handled LegacyRankMemberShow " +
                $"{FormatSession(context)} type={query.Type} requestedItems={query.Items.Count} responseItems={response.Items.Count}.")
            .ConfigureAwait(false);
    }

}
