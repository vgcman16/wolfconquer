using System.Buffers;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public readonly record struct LegacyAttributeSetPacket(
    uint Strength,
    uint Agility,
    uint Vitality,
    uint Spirit)
{
    private const int ReservedOffset = 0;
    private const int StrengthOffset = ReservedOffset + sizeof(uint);
    private const int AgilityOffset = StrengthOffset + sizeof(uint);
    private const int VitalityOffset = AgilityOffset + sizeof(uint);
    private const int SpiritOffset = VitalityOffset + sizeof(uint);

    public const ushort Opcode = WorldPacketOpcodes.LegacyAttributeSet;
    public const int PayloadLength = SpiritOffset + sizeof(uint);

    public uint Total => checked(Strength + Agility + Vitality + Spirit);

    public static bool TryParse(PacketFrame frame, out LegacyAttributeSetPacket packet)
    {
        packet = default;

        if (frame.Header.Opcode != Opcode ||
            frame.Payload.Length != PayloadLength ||
            !PacketReader.TryRead(frame.Frame.ToArray(), out var reader))
        {
            return false;
        }

        packet = new LegacyAttributeSetPacket(
            reader.ReadUInt32(StrengthOffset),
            reader.ReadUInt32(AgilityOffset),
            reader.ReadUInt32(VitalityOffset),
            reader.ReadUInt32(SpiritOffset));
        return true;
    }

    public byte[] ToFrameBytes()
    {
        var writer = new PacketWriter(Opcode, PayloadLength);
        writer.WriteUInt32(ReservedOffset, 0);
        writer.WriteUInt32(StrengthOffset, Strength);
        writer.WriteUInt32(AgilityOffset, Agility);
        writer.WriteUInt32(VitalityOffset, Vitality);
        writer.WriteUInt32(SpiritOffset, Spirit);
        return writer.ToArray();
    }
}
