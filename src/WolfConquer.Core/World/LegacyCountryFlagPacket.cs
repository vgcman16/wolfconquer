using System.Buffers;
using System.Buffers.Binary;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public readonly record struct LegacyCountryFlagPacket(uint Uid, ushort CountryId)
{
    private const int UidOffset = 0;
    private const int CountryIdOffset = UidOffset + sizeof(uint);

    public const ushort Opcode = WorldPacketOpcodes.LegacyCountryFlag;
    public const int PayloadLength = CountryIdOffset + sizeof(ushort);

    public static bool TryParse(PacketFrame frame, out LegacyCountryFlagPacket packet)
    {
        packet = default;

        if (frame.Header.Opcode != Opcode ||
            frame.Payload.Length != PayloadLength)
        {
            return false;
        }

        var payload = frame.Payload.ToArray();
        packet = new LegacyCountryFlagPacket(
            BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(UidOffset, sizeof(uint))),
            BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(CountryIdOffset, sizeof(ushort))));
        return true;
    }

    public byte[] ToFrameBytes()
    {
        var writer = new PacketWriter(Opcode, PayloadLength);
        writer.WriteUInt32(UidOffset, Uid);
        writer.WriteUInt16(CountryIdOffset, CountryId);
        return writer.ToArray();
    }
}
