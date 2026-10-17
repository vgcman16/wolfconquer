using System.Buffers;
using WolfConquer.Core.Packets;
using DataRouletteTable = WolfConquer.Core.Data.RouletteTable;

namespace WolfConquer.Core.World;

public enum LegacyRouletteTableMoneyType : uint
{
    Money = 1,
    ConquerPoints = 2,
}

public readonly record struct LegacyRouletteTablePacket(
    uint Uid,
    uint TableNumber,
    LegacyRouletteTableMoneyType MoneyType,
    ushort X,
    ushort Y,
    ushort Mesh,
    byte PlayersCount = 0)
{
    private const int UidOffset = 0;
    private const int TableNumberOffset = UidOffset + sizeof(uint);
    private const int MoneyTypeOffset = TableNumberOffset + sizeof(uint);
    private const int XOffset = MoneyTypeOffset + sizeof(uint);
    private const int YOffset = XOffset + sizeof(ushort);
    private const int MeshOffset = YOffset + sizeof(ushort);
    private const int PlayersCountOffset = MeshOffset + sizeof(ushort);

    public const ushort Opcode = WorldPacketOpcodes.LegacyRouletteTable;
    public const int PayloadLength = PlayersCountOffset + sizeof(byte);
    public const int MapId = 3852;
    public const int ViewThreshold = 16;

    public static LegacyRouletteTablePacket FromRouletteTable(DataRouletteTable table)
    {
        ArgumentNullException.ThrowIfNull(table);
        if (table.Values.Count < 6)
        {
            throw new ArgumentOutOfRangeException(nameof(table), "Roulette table rows require six value fields.");
        }

        return new LegacyRouletteTablePacket(
            CheckedUInt32(table.Id, nameof(table.Id)),
            CheckedUInt32(table.Values[0], "roulette table number"),
            (LegacyRouletteTableMoneyType)CheckedUInt32(table.Values[1], "roulette table money type"),
            CheckedUInt16(table.Values[3], "roulette table x"),
            CheckedUInt16(table.Values[4], "roulette table y"),
            CheckedUInt16(table.Values[5], "roulette table mesh"));
    }

    public static bool TryParse(PacketFrame frame, out LegacyRouletteTablePacket packet)
    {
        packet = default;

        if (frame.Header.Opcode != Opcode ||
            frame.Payload.Length != PayloadLength ||
            !PacketReader.TryRead(frame.Frame.ToArray(), out var reader))
        {
            return false;
        }

        packet = new LegacyRouletteTablePacket(
            reader.ReadUInt32(UidOffset),
            reader.ReadUInt32(TableNumberOffset),
            (LegacyRouletteTableMoneyType)reader.ReadUInt32(MoneyTypeOffset),
            reader.ReadUInt16(XOffset),
            reader.ReadUInt16(YOffset),
            reader.ReadUInt16(MeshOffset),
            reader.Payload[PlayersCountOffset]);
        return true;
    }

    public byte[] ToFrameBytes()
    {
        var writer = new PacketWriter(Opcode, PayloadLength);
        writer.WriteUInt32(UidOffset, Uid);
        writer.WriteUInt32(TableNumberOffset, TableNumber);
        writer.WriteUInt32(MoneyTypeOffset, (uint)MoneyType);
        writer.WriteUInt16(XOffset, X);
        writer.WriteUInt16(YOffset, Y);
        writer.WriteUInt16(MeshOffset, Mesh);
        writer.Payload[PlayersCountOffset] = PlayersCount;
        return writer.ToArray();
    }

    private static ushort CheckedUInt16(int value, string name)
    {
        if (value is < 0 or > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(name, value, "Legacy roulette table value cannot be outside ushort range.");
        }

        return (ushort)value;
    }

    private static uint CheckedUInt32(int value, string name)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(name, value, "Legacy roulette table value cannot be outside uint range.");
        }

        return (uint)value;
    }
}
