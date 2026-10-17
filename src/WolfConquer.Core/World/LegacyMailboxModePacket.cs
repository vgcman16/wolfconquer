using System.Buffers;
using System.Buffers.Binary;
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
        if (frame.Header.Opcode != Opcode ||
            frame.Payload.Length < HeaderPayloadLength)
        {
            return false;
        }

        var payload = frame.Payload.ToArray();
        var type = BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(TypeOffset, sizeof(ushort)));
        if (!Enum.IsDefined(typeof(LegacyMailboxModeType), type))
        {
            return false;
        }

        var count = BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(CountOffset, sizeof(uint)));
        if (count > int.MaxValue)
        {
            return false;
        }

        var itemCount = (int)count;
        var payloadLength = checked(HeaderPayloadLength + (itemCount * sizeof(ulong)));
        if (payload.Length != payloadLength)
        {
            return false;
        }

        var items = new ulong[itemCount];
        var offset = ItemsOffset;
        for (var index = 0; index < items.Length; index++)
        {
            items[index] = BinaryPrimitives.ReadUInt64LittleEndian(payload.AsSpan(offset, sizeof(ulong)));
            offset += sizeof(ulong);
        }

        packet = new LegacyMailboxModePacket(
            (LegacyMailboxModeType)type,
            BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(IdOffset, sizeof(uint))),
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
