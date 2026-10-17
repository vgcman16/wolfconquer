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
    [PacketHandler(WorldPacketOpcodes.LegacyTeamLeadership)]
    private static async ValueTask HandleLegacyTeamLeadershipAsync(
        WorldPacketHandlerContext context,
        PacketFrame frame,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!LegacyTeamLeadershipPacket.TryParse(frame, out var packet))
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer rejected malformed LegacyTeamLeadership packet {FormatSession(context)}.")
                .ConfigureAwait(false);
            return;
        }

        await context.Log.WriteLineAsync(
                "WorldServer handled LegacyTeamLeadership " +
                $"{FormatSession(context)} mode={packet.Mode} uid={packet.Uid} " +
                $"leaderUid={packet.LeaderUid} count={packet.Count}.")
            .ConfigureAwait(false);

        if (context.Session is null)
        {
            return;
        }

        await context.Connection.SendAsync(packet.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                $"WorldServer echoed LegacyTeamLeadership {FormatSession(context)} mode={packet.Mode} uid={packet.Uid}.")
            .ConfigureAwait(false);
    }

}
