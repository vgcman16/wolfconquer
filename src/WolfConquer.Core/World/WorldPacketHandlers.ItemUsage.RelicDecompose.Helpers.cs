using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    // Existing shared RuneBag/Mythsoul/RuneEquipment echo layout. Only access
    // changes; decomposition now captures this output before UID reservation.
    internal static byte[] CreateLegacyItemUsageFrameWithArgs(MsgItemUsagePacket packet, IReadOnlyList<uint> args)
    {
        var writer = new PacketWriter(WorldPacketOpcodes.MsgItemUsage,
            MsgItemUsagePacket.PayloadLength + args.Count * sizeof(uint));
        writer.WriteUInt32(0, packet.Id);
        writer.WriteUInt64(4, packet.DwParam);
        writer.WriteUInt16(12, (ushort)packet.Action);
        writer.WriteUInt32(14, packet.Timestamp);
        writer.WriteUInt32(18, packet.DwParam2);
        writer.WriteUInt32(22, packet.DwParam3);
        writer.WriteUInt32(26, packet.DwParam4);
        for (var index = 0; index < args.Count; index++)
            writer.WriteUInt32(MsgItemUsagePacket.PayloadLength + index * sizeof(uint), args[index]);
        return writer.ToArray();
    }
}
