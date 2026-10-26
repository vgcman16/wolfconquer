using WolfConquer.Core.Packets;

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

public sealed record LegacyMailboxModePacket(
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

    public static bool TryParse(PacketFrame frame, out LegacyMailboxModePacket packet)
    {
        packet = default!;
        if (frame.Header.Length < PacketHeader.Size + HeaderPayloadLength ||
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
        var remainingBytes = reader.Payload.Length - ItemsOffset;
        if (remainingBytes % sizeof(ulong) != 0 ||
            count != (uint)(remainingBytes / sizeof(ulong)))
        {
            return false;
        }

        // Exact equality with the bounded snapshot proves this allocation and
        // every read fit; untrusted count multiplication cannot overflow first.
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
