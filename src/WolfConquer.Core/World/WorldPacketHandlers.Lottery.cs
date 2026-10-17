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
    [PacketHandler(WorldPacketOpcodes.LegacyLotteryList)]
    private static async ValueTask HandleLegacyLotteryListAsync(
        WorldPacketHandlerContext context,
        PacketFrame frame,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!LegacyLotteryListPacket.TryParse(frame, out var packet))
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer rejected malformed LegacyLotteryList packet {FormatSession(context)}. {FormatFrameDiagnostic(frame)}")
                .ConfigureAwait(false);
            return;
        }

        if (packet.IsAwardListRequest)
        {
            var response = LegacyLotteryListPacket.EmptyAwardPage();
            await context.Connection.SendAsync(response.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    $"WorldServer handled LegacyLotteryList {FormatSession(context)} type=0 pages=1 items=0.")
                .ConfigureAwait(false);
            return;
        }

        await context.Log.WriteLineAsync(
                "WorldServer source-backed no-op LegacyLotteryList " +
                $"{FormatSession(context)} type={packet.Type} items={packet.Items.Count}; " +
                "source MsgLotteryList.Process returns without sending award pages when Type != 0.")
            .ConfigureAwait(false);
    }

}
