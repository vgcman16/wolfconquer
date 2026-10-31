using System.Buffers;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const string LegacyDirectRuneBagSource = "MsgItemUsuagePacket.Rune";
    private const string LegacyDirectRuneBagLines = "2156-2188";

    private static async ValueTask HandleLegacyDirectRuneBagItemUseAsync(
        WorldPacketHandlerContext context, WorldSession session, MsgItemUsagePacket packet,
        PacketFrame frame, CancellationToken cancellationToken)
    {
        if (session.Character.Reborn < 2 || session.Character.Level < 15 ||
            !TryPrepareRuneBagEcho(packet, frame, out var echo))
        {
            await context.Log.WriteLineAsync($"WorldServer skipped direct rune bag itemUid={packet.Id}; " +
                "second reborn, level 15 and complete argument words are required.").ConfigureAwait(false);
            return;
        }
        await HandleOwnedDirectRuneBagAsync(context, session, packet, echo, cancellationToken).ConfigureAwait(false);
    }

    private static bool TryPrepareRuneBagEcho(MsgItemUsagePacket packet, PacketFrame frame, out byte[] echo)
    {
        echo = [];
        if (!PacketReader.TryRead(frame.Frame.ToArray(), out var reader) ||
            reader.Payload.Length > MsgItemUsagePacket.PayloadLength &&
            (reader.Payload.Length - MsgItemUsagePacket.PayloadLength) % sizeof(uint) != 0) return false;
        // The established parser owns the fixed typed fields. Only complete
        // appended words are echoed; their values are not allocation authority.
        echo = CreateLegacyItemUsageFrameWithArgs(packet, ReadLegacyItemUsageArgs(frame));
        return true;
    }

    private static bool IsLegacyRuneItemId(uint itemId) => itemId / 10_000 is >= 401 and <= 403;
}
