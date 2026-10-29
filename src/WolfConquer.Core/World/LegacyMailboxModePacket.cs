using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

public enum LegacyMailboxModeType : ushort
{
    Content = 1,
    Delete = 2,
    ClaimMoney = 3,
    ClaimItem = 4,
    ClaimRecordType = 5,
    DeleteClaim = 6,
    ClaimAll = 7,
}

public sealed partial record LegacyMailboxModePacket(
    LegacyMailboxModeType Type,
    uint Id,
    IReadOnlyList<ulong> Items)
{
    private const int TypeOffset = 0;
    private const int IdOffset = TypeOffset + sizeof(ushort);
    private const int CountOffset = IdOffset + sizeof(uint);
    private const int ItemsOffset = CountOffset + sizeof(uint);

    public const ushort Opcode = WorldPacketOpcodes.LegacyMailboxMode;
    public const int HeaderPayloadLength = ItemsOffset;

    public uint Count => checked((uint)Items.Count);

    public static bool TryParse(PacketFrame frame, out LegacyMailboxModePacket packet) =>
        TryParse(frame, ConquerProtocolProfile.RootD11_7899, out packet);

    public static bool TryParse(PacketFrame frame, ConquerProtocolProfile profile, out LegacyMailboxModePacket packet)
    {
        packet = default!;
        if (profile is not (ConquerProtocolProfile.RootD11_7899 or ConquerProtocolProfile.VisibleDx8D13) ||
            frame.Header.Length < PacketHeader.Size + HeaderPayloadLength ||
            !PacketFrameEnvelope.TryRead(frame, Opcode, frame.Header.Length, out var reader))
        {
            return false;
        }

        var type = reader.ReadUInt16(TypeOffset);
        if (!Enum.IsDefined(typeof(LegacyMailboxModeType), type))
        {
            return false;
        }

        var count = reader.ReadUInt32(CountOffset);
        if (!HasKnownItemExtent((LegacyMailboxModeType)type, count, reader.Payload[ItemsOffset..], profile))
        {
            return false;
        }

        // The bounded physical item extent (and only its explicit zero trailer)
        // proves this allocation/read fits before any count multiplication.
        var items = new ulong[(int)count];
        var offset = ItemsOffset;
        for (var index = 0; index < items.Length; index++)
        {
            items[index] = reader.ReadUInt64(offset);
            offset += sizeof(ulong);
        }

        packet = new LegacyMailboxModePacket(
            (LegacyMailboxModeType)type,
            reader.ReadUInt32(IdOffset),
            items);
        return true;
    }

    public byte[] ToFrameBytes()
    {
        var writer = new PacketWriter(Opcode, checked(HeaderPayloadLength + (Items.Count * sizeof(ulong))));
        writer.WriteUInt16(TypeOffset, (ushort)Type);
        writer.WriteUInt32(IdOffset, Id);
        writer.WriteUInt32(CountOffset, Count);

        var offset = ItemsOffset;
        foreach (var item in Items)
        {
            writer.WriteUInt64(offset, item);
            offset += sizeof(ulong);
        }

        return writer.ToArray();
    }
}
