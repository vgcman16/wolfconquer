using System.Buffers;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public enum LegacyFloorItemDropType : byte
{
    Visible = 1,
    Remove = 2,
    Pickup = 3,
    DropDetain = 4,
    Effect = 10,
    RemoveEffect = 12,
    Earth = 13,
}

public sealed record LegacyFloorItemPacket(
    uint Uid,
    uint ItemId,
    ushort X,
    ushort Y,
    ushort MaxLife,
    LegacyFloorItemDropType DropType,
    uint Life,
    byte PlusOrColor,
    uint ItemOwnerUid,
    uint GuildId,
    byte FlowerType,
    ulong Timer,
    ushort OwnerX,
    ushort OwnerY,
    string Name)
{
    private const int UidOffset = 0;
    private const int ItemIdOffset = UidOffset + sizeof(uint);
    private const int XOffset = ItemIdOffset + sizeof(uint);
    private const int YOffset = XOffset + sizeof(ushort);
    private const int MaxLifeOffset = YOffset + sizeof(ushort);
    private const int DropTypeOffset = MaxLifeOffset + sizeof(ushort);
    private const int LifeOffset = DropTypeOffset + sizeof(byte);
    private const int PlusOrColorOffset = LifeOffset + sizeof(uint);
    private const int ItemOwnerUidOffset = PlusOrColorOffset + sizeof(byte);
    private const int Reserved24Offset = ItemOwnerUidOffset + sizeof(uint);
    private const int Reserved28Offset = Reserved24Offset + sizeof(uint);
    private const int GuildIdOffset = Reserved28Offset + sizeof(uint);
    private const int FlowerTypeOffset = GuildIdOffset + sizeof(uint);
    private const int TimerOffset = FlowerTypeOffset + sizeof(byte);
    private const int Reserved45Offset = TimerOffset + sizeof(ulong);
    private const int Reserved49Offset = Reserved45Offset + sizeof(uint);
    private const int OwnerXOffset = Reserved49Offset + sizeof(uint);
    private const int OwnerYOffset = OwnerXOffset + sizeof(ushort);
    private const int Reserved57Offset = OwnerYOffset + sizeof(ushort);
    private const int NameOffset = Reserved57Offset + Reserved57Length;
    private const int Reserved129Offset = NameOffset + FixedNameLength;
    private const int Reserved133Offset = Reserved129Offset + sizeof(uint);
    private const int Reserved137Offset = Reserved133Offset + sizeof(uint);
    private const int Reserved141Offset = Reserved137Offset + sizeof(uint);
    private const int Reserved145Offset = Reserved141Offset + sizeof(uint);

    public const ushort Opcode = WorldPacketOpcodes.LegacyFloorItem;
    public const int Reserved57Length = 40;
    public const int FixedNameLength = 32;
    public const int PayloadLength = Reserved145Offset + sizeof(uint);

    public static LegacyFloorItemPacket FromDroppedItem(
        PlayerItemSnapshotItem item,
        CharacterSnapshot owner,
        uint floorUid)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(owner);

        return new LegacyFloorItemPacket(
            floorUid,
            item.ItemId,
            CheckedUInt16(owner.X, nameof(owner.X)),
            CheckedUInt16(owner.Y, nameof(owner.Y)),
            MaxLife: 40,
            LegacyFloorItemDropType.Visible,
            Life: 0,
            item.Plus,
            CheckedUInt32(owner.Uid, nameof(owner.Uid)),
            GuildId: 0,
            FlowerType: 0,
            Timer: 0,
            OwnerX: 0,
            OwnerY: 0,
            Name: "");
    }

    public static LegacyFloorItemPacket FromMonsterDrop(
        uint itemId,
        LegacyMonsterInstance monster,
        CharacterSnapshot owner,
        uint floorUid)
    {
        ArgumentNullException.ThrowIfNull(monster);

        return FromMonsterDrop(
            itemId,
            monster.X,
            monster.Y,
            owner,
            floorUid);
    }

    public static LegacyFloorItemPacket FromMonsterDrop(
        uint itemId,
        ushort x,
        ushort y,
        CharacterSnapshot owner,
        uint floorUid)
    {
        ArgumentNullException.ThrowIfNull(owner);

        return new LegacyFloorItemPacket(
            floorUid,
            itemId,
            x,
            y,
            MaxLife: 40,
            LegacyFloorItemDropType.Visible,
            Life: 0,
            PlusOrColor: 0,
            CheckedUInt32(owner.Uid, nameof(owner.Uid)),
            GuildId: 0,
            FlowerType: 0,
            Timer: 0,
            OwnerX: 0,
            OwnerY: 0,
            Name: "");
    }

    public static bool TryParse(PacketFrame frame, out LegacyFloorItemPacket packet)
    {
        packet = default!;

        if (frame.Header.Opcode != Opcode ||
            frame.Payload.Length != PayloadLength ||
            !PacketReader.TryRead(frame.Frame.ToArray(), out var reader) ||
            reader.ReadUInt32(Reserved24Offset) != 0 ||
            reader.ReadUInt32(Reserved28Offset) != 0 ||
            reader.ReadUInt32(Reserved45Offset) != 0 ||
            reader.ReadUInt32(Reserved49Offset) != 0 ||
            reader.Payload.Slice(Reserved57Offset, Reserved57Length).IndexOfAnyExcept((byte)0) >= 0 ||
            reader.ReadUInt32(Reserved129Offset) != 0 ||
            reader.ReadUInt32(Reserved133Offset) != 0 ||
            reader.ReadUInt32(Reserved137Offset) != 0 ||
            reader.ReadUInt32(Reserved141Offset) != 0 ||
            reader.ReadUInt32(Reserved145Offset) != 0)
        {
            return false;
        }

        var dropType = reader.Payload[DropTypeOffset];
        if (!Enum.IsDefined(typeof(LegacyFloorItemDropType), dropType))
        {
            return false;
        }

        packet = new LegacyFloorItemPacket(
            reader.ReadUInt32(UidOffset),
            reader.ReadUInt32(ItemIdOffset),
            reader.ReadUInt16(XOffset),
            reader.ReadUInt16(YOffset),
            reader.ReadUInt16(MaxLifeOffset),
            (LegacyFloorItemDropType)dropType,
            reader.ReadUInt32(LifeOffset),
            reader.Payload[PlusOrColorOffset],
            reader.ReadUInt32(ItemOwnerUidOffset),
            reader.ReadUInt32(GuildIdOffset),
            reader.Payload[FlowerTypeOffset],
            reader.ReadUInt64(TimerOffset),
            reader.ReadUInt16(OwnerXOffset),
            reader.ReadUInt16(OwnerYOffset),
            reader.ReadCString(NameOffset, FixedNameLength, Encoding.UTF8));
        return true;
    }

    public static bool TryParsePickupRequest(PacketFrame frame, out uint floorUid)
    {
        floorUid = 0;

        if (frame.Header.Opcode != Opcode ||
            frame.Payload.Length < sizeof(uint) ||
            !PacketReader.TryRead(frame.Frame.ToArray(), out var reader))
        {
            return false;
        }

        floorUid = reader.ReadUInt32(UidOffset);
        return true;
    }

    public byte[] ToFrameBytes()
    {
        var writer = new PacketWriter(Opcode, PayloadLength);
        writer.WriteUInt32(UidOffset, Uid);
        writer.WriteUInt32(ItemIdOffset, ItemId);
        writer.WriteUInt16(XOffset, X);
        writer.WriteUInt16(YOffset, Y);
        writer.WriteUInt16(MaxLifeOffset, MaxLife);
        writer.Payload[DropTypeOffset] = (byte)DropType;
        writer.WriteUInt32(LifeOffset, Life);
        writer.Payload[PlusOrColorOffset] = PlusOrColor;
        writer.WriteUInt32(ItemOwnerUidOffset, ItemOwnerUid);
        writer.WriteUInt32(Reserved24Offset, 0);
        writer.WriteUInt32(Reserved28Offset, 0);
        writer.WriteUInt32(GuildIdOffset, GuildId);
        writer.Payload[FlowerTypeOffset] = FlowerType;
        writer.WriteUInt64(TimerOffset, Timer);
        writer.WriteUInt32(Reserved45Offset, 0);
        writer.WriteUInt32(Reserved49Offset, 0);
        writer.WriteUInt16(OwnerXOffset, OwnerX);
        writer.WriteUInt16(OwnerYOffset, OwnerY);
        WriteFixedUtf8(writer.Payload.Slice(NameOffset, FixedNameLength), Name);
        writer.WriteUInt32(Reserved129Offset, 0);
        writer.WriteUInt32(Reserved133Offset, 0);
        writer.WriteUInt32(Reserved137Offset, 0);
        writer.WriteUInt32(Reserved141Offset, 0);
        writer.WriteUInt32(Reserved145Offset, 0);
        return writer.ToArray();
    }

    private static ushort CheckedUInt16(long value, string name)
    {
        return value is < ushort.MinValue or > ushort.MaxValue
            ? throw new ArgumentOutOfRangeException(name, "Legacy floor item fields must fit in UInt16.")
            : (ushort)value;
    }

    private static uint CheckedUInt32(long value, string name)
    {
        return value is < uint.MinValue or > uint.MaxValue
            ? throw new ArgumentOutOfRangeException(name, "Legacy floor item fields must fit in UInt32.")
            : (uint)value;
    }

    private static void WriteFixedUtf8(Span<byte> destination, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        bytes.AsSpan(0, Math.Min(bytes.Length, destination.Length)).CopyTo(destination);
    }
}
