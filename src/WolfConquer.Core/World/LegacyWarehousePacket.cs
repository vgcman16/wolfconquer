using System.Buffers;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public enum LegacyWarehouseAction : ushort
{
    Show = 2560,
    DepositItem = 2561,
    WithdrawItem = 2562,
    Show_WH_House = 5120,
    DepositItem_WH_House = 5121,
    WithdrawItem_WH_House = 5122,
    ShashShow = 7680,
    ShashDepositItem = 7681,
    ShashWithdrawItem = 7682,
    ShowInventorySash = 10240,
    InventorySashDepositItem = 10241,
    InventorySashWithdrawItem = 10242,
}

public sealed record LegacyWarehousePacket(
    uint NpcId,
    LegacyWarehouseAction Action,
    uint ItemUid,
    ushort Capacity,
    IReadOnlyList<PlayerItemSnapshotItem> Items)
{
    private const int NpcIdOffset = 0;
    private const int Reserved4Offset = NpcIdOffset + sizeof(uint);
    private const int ActionOffset = Reserved4Offset + sizeof(uint);
    private const int CapacityOffset = ActionOffset + sizeof(uint);
    private const int ItemUidOffset = CapacityOffset + sizeof(ushort);
    private const int CountOffset = ItemUidOffset + sizeof(uint);
    private const int ItemsOffset = CountOffset + sizeof(ushort);

    private const int RequestMinimumPayloadLength = ItemUidOffset + sizeof(uint);

    private const int ItemUidItemOffset = 0;
    private const int ItemIdOffset = ItemUidItemOffset + sizeof(uint);
    private const int Reserved8Offset = ItemIdOffset + sizeof(uint);
    private const int SocketOneOffset = Reserved8Offset + sizeof(byte);
    private const int SocketTwoOffset = SocketOneOffset + sizeof(byte);
    private const int SpellIdOffset = SocketTwoOffset + sizeof(byte);
    private const int EffectShortOffset = SpellIdOffset + sizeof(uint);
    private const int PlusOffset = EffectShortOffset + sizeof(ushort);
    private const int BlessOffset = PlusOffset + sizeof(byte);
    private const int BoundOffset = BlessOffset + sizeof(byte);
    private const int EnchantOffset = BoundOffset + sizeof(byte);
    private const int ProgressGreenOffset = EnchantOffset + sizeof(byte);
    private const int EffectOffset = ProgressGreenOffset + sizeof(byte);
    private const int LockedOffset = EffectOffset + sizeof(uint);
    private const int Reserved27Offset = LockedOffset + sizeof(byte);
    private const int SocketProgressOffset = Reserved27Offset + sizeof(byte);
    private const int PlusProgressOffset = SocketProgressOffset + sizeof(uint);
    private const int TimeLeftSecondsOffset = PlusProgressOffset + sizeof(uint);
    private const int Reserved40Offset = TimeLeftSecondsOffset + sizeof(uint);
    private const int StackSizeOffset = Reserved40Offset + sizeof(uint);
    private const int PurificationItemIdOffset = StackSizeOffset + sizeof(ushort);
    private const int Reserved50Offset = PurificationItemIdOffset + sizeof(uint);
    private const int PerfectionLevelOffset = Reserved50Offset + sizeof(ushort);
    private const int PerfectionProgressOffset = PerfectionLevelOffset + sizeof(uint);
    private const int OwnerUidOffset = PerfectionProgressOffset + sizeof(uint);
    private const int OwnerNameOffset = OwnerUidOffset + sizeof(uint);
    private const int SignatureOffset = OwnerNameOffset + OwnerNameLength;
    private const int RuneExperienceOffset = SignatureOffset + SignatureLength;
    private const int Attribute1Offset = RuneExperienceOffset + sizeof(uint);
    private const int Attribute2Offset = Attribute1Offset + sizeof(uint);
    private const int Attribute3Offset = Attribute2Offset + sizeof(uint);
    private const int Attribute4Offset = Attribute3Offset + sizeof(uint);
    private const int Attribute5Offset = Attribute4Offset + sizeof(uint);
    private const int AnimaItemIdOffset = Attribute5Offset + sizeof(uint);
    private const int Reserved184Offset = AnimaItemIdOffset + sizeof(uint);
    private const int MythSoulOffset = Reserved184Offset + sizeof(uint);
    private const int MythSoulPointsOffset = MythSoulOffset + sizeof(uint);
    private const int Reserved196Offset = MythSoulPointsOffset + sizeof(uint);
    private const int Reserved207Offset = Reserved196Offset + 11;
    private const int Reserved208Offset = Reserved207Offset + sizeof(byte);
    private const int Reserved212Offset = Reserved208Offset + sizeof(uint);
    private const int Reserved216Offset = Reserved212Offset + sizeof(uint);
    private const int Reserved220Offset = Reserved216Offset + sizeof(uint);

    private const int OwnerNameLength = 32;
    private const int SignatureLength = 60;

    public const ushort Opcode = WorldPacketOpcodes.LegacyWarehouse;
    public const int HeaderPayloadLength = ItemsOffset;
    public const int ItemPayloadLength = Reserved220Offset + sizeof(uint);
    public const ushort DefaultCapacity = 60;

    public ushort DeclaredItemCount { get; init; } = CheckedUInt16((uint)Items.Count, nameof(Items));

    public static bool TryParse(PacketFrame frame, out LegacyWarehousePacket packet)
    {
        packet = default!;

        if (frame.Header.Opcode != Opcode ||
            !PacketReader.TryRead(frame.Frame.ToArray(), out var reader) ||
            reader.Payload.Length < HeaderPayloadLength)
        {
            return false;
        }

        var count = reader.ReadUInt16(CountOffset);
        var itemPayloadBytes = reader.Payload.Length - HeaderPayloadLength;
        if (itemPayloadBytes < 0 || itemPayloadBytes % ItemPayloadLength != 0)
        {
            return false;
        }

        var embeddedItemCount = itemPayloadBytes / ItemPayloadLength;
        if (embeddedItemCount > count)
        {
            return false;
        }

        var items = new List<PlayerItemSnapshotItem>(embeddedItemCount);
        for (var index = 0; index < embeddedItemCount; index++)
        {
            items.Add(ReadItem(reader.Payload.Slice(ItemsOffset + index * ItemPayloadLength, ItemPayloadLength)));
        }

        packet = new LegacyWarehousePacket(
            reader.ReadUInt32(NpcIdOffset),
            (LegacyWarehouseAction)CheckedUInt16(reader.ReadUInt32(ActionOffset), nameof(Action)),
            reader.ReadUInt32(ItemUidOffset),
            reader.ReadUInt16(CapacityOffset),
            items)
        {
            DeclaredItemCount = count,
        };
        return true;
    }

    public static bool TryParseRequest(PacketFrame frame, out LegacyWarehousePacket packet)
    {
        packet = default!;

        if (frame.Header.Opcode != Opcode ||
            !PacketReader.TryRead(frame.Frame.ToArray(), out var reader) ||
            reader.Payload.Length < RequestMinimumPayloadLength)
        {
            return false;
        }

        packet = new LegacyWarehousePacket(
            reader.ReadUInt32(NpcIdOffset),
            (LegacyWarehouseAction)reader.ReadUInt16(ActionOffset),
            reader.ReadUInt32(ItemUidOffset),
            reader.Payload.Length >= HeaderPayloadLength ? reader.ReadUInt16(CapacityOffset) : (ushort)0,
            []);
        return true;
    }

    public static bool IsShowAction(LegacyWarehouseAction action)
    {
        return action is
            LegacyWarehouseAction.Show or
            LegacyWarehouseAction.Show_WH_House or
            LegacyWarehouseAction.ShowInventorySash;
    }

    public byte[] ToFrameBytes()
    {
        ArgumentNullException.ThrowIfNull(Items);

        if (Items.Count > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(Items), Items.Count, "Legacy warehouse item count must fit UInt16.");
        }

        if (DeclaredItemCount < Items.Count)
        {
            throw new ArgumentOutOfRangeException(
                nameof(DeclaredItemCount),
                DeclaredItemCount,
                "Legacy warehouse declared count cannot be less than embedded item count.");
        }

        var writer = new PacketWriter(Opcode, HeaderPayloadLength + Items.Count * ItemPayloadLength);
        writer.WriteUInt32(NpcIdOffset, NpcId);
        writer.WriteUInt32(Reserved4Offset, 0);
        writer.WriteUInt32(ActionOffset, (uint)Action);
        writer.WriteUInt16(CapacityOffset, Capacity);
        writer.WriteUInt32(ItemUidOffset, ItemUid);
        writer.WriteUInt16(CountOffset, DeclaredItemCount);

        for (var index = 0; index < Items.Count; index++)
        {
            WriteItem(writer.Payload.Slice(ItemsOffset + index * ItemPayloadLength, ItemPayloadLength), Items[index]);
        }

        return writer.ToArray();
    }

    private static PlayerItemSnapshotItem ReadItem(ReadOnlySpan<byte> payload)
    {
        return new PlayerItemSnapshotItem
        {
            Uid = ReadUInt32(payload, ItemUidItemOffset),
            ItemId = ReadUInt32(payload, ItemIdOffset),
            SocketOne = payload[SocketOneOffset],
            SocketTwo = payload[SocketTwoOffset],
            Effect = ReadUInt32(payload, EffectOffset),
            Plus = payload[PlusOffset],
            Bless = payload[BlessOffset],
            Bound = payload[BoundOffset],
            Enchant = payload[EnchantOffset],
            Locked = payload[LockedOffset],
            SocketProgress = ReadUInt32(payload, SocketProgressOffset),
            PlusProgress = ReadUInt32(payload, PlusProgressOffset),
            TimeLeftInMinutes = ReadUInt32(payload, TimeLeftSecondsOffset) / 60,
            StackSize = ReadUInt16(payload, StackSizeOffset),
            PurificationItemId = ReadUInt32(payload, PurificationItemIdOffset),
            PerfectionLevel = ReadUInt32(payload, PerfectionLevelOffset),
            PerfectionProgress = ReadUInt32(payload, PerfectionProgressOffset),
            OwnerUid = ReadUInt32(payload, OwnerUidOffset),
            OwnerName = ReadFixedUtf8(payload, OwnerNameOffset, OwnerNameLength),
            Signature = ReadFixedUtf8(payload, SignatureOffset, SignatureLength),
            RuneExperience = ReadUInt32(payload, RuneExperienceOffset),
            Attribute1 = ReadUInt32(payload, Attribute1Offset),
            Attribute2 = ReadUInt32(payload, Attribute2Offset),
            Attribute3 = ReadUInt32(payload, Attribute3Offset),
            Attribute4 = ReadUInt32(payload, Attribute4Offset),
            Attribute5 = ReadUInt32(payload, Attribute5Offset),
            AnimaItemId = ReadUInt32(payload, AnimaItemIdOffset),
            MythSoul = ReadUInt32(payload, MythSoulOffset),
            MythSoulPoints = ReadUInt32(payload, MythSoulPointsOffset),
        };
    }

    private static void WriteItem(Span<byte> payload, PlayerItemSnapshotItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        WriteUInt32(payload, ItemUidItemOffset, item.Uid);
        WriteUInt32(payload, ItemIdOffset, item.ItemId);
        payload[Reserved8Offset] = 0;
        payload[SocketOneOffset] = item.SocketOne;
        payload[SocketTwoOffset] = item.SocketTwo;
        WriteUInt32(payload, SpellIdOffset, 0);
        WriteUInt16(payload, EffectShortOffset, CheckedUInt16(item.Effect, nameof(item.Effect)));
        payload[PlusOffset] = item.Plus;
        payload[BlessOffset] = item.Bless;
        payload[BoundOffset] = item.Bound;
        payload[EnchantOffset] = item.Enchant;
        payload[ProgressGreenOffset] = 0;
        WriteUInt32(payload, EffectOffset, item.Effect);
        payload[LockedOffset] = item.Locked;
        payload[Reserved27Offset] = 0;
        WriteUInt32(payload, SocketProgressOffset, item.SocketProgress);
        WriteUInt32(payload, PlusProgressOffset, item.PlusProgress);
        WriteUInt32(payload, TimeLeftSecondsOffset, checked(item.TimeLeftInMinutes * 60u));
        WriteUInt32(payload, Reserved40Offset, 0);
        WriteUInt16(payload, StackSizeOffset, item.StackSize);
        WriteUInt32(payload, PurificationItemIdOffset, item.PurificationItemId);
        WriteUInt16(payload, Reserved50Offset, 0);
        WriteUInt32(payload, PerfectionLevelOffset, item.PerfectionLevel);
        WriteUInt32(payload, PerfectionProgressOffset, item.PerfectionProgress);
        WriteUInt32(payload, OwnerUidOffset, item.OwnerUid);
        WriteFixedUtf8(payload.Slice(OwnerNameOffset, OwnerNameLength), item.OwnerName);
        WriteFixedUtf8(payload.Slice(SignatureOffset, SignatureLength), item.Signature);
        WriteUInt32(payload, RuneExperienceOffset, item.RuneExperience);
        WriteUInt32(payload, Attribute1Offset, item.Attribute1);
        WriteUInt32(payload, Attribute2Offset, item.Attribute2);
        WriteUInt32(payload, Attribute3Offset, item.Attribute3);
        WriteUInt32(payload, Attribute4Offset, item.Attribute4);
        WriteUInt32(payload, Attribute5Offset, item.Attribute5);
        WriteUInt32(payload, AnimaItemIdOffset, item.AnimaItemId);
        WriteUInt32(payload, Reserved184Offset, 0);
        WriteUInt32(payload, MythSoulOffset, item.MythSoul);
        WriteUInt32(payload, MythSoulPointsOffset, item.MythSoulPoints);
        payload.Slice(Reserved196Offset, 11).Clear();
        payload[Reserved207Offset] = 0;
        WriteUInt32(payload, Reserved208Offset, 0);
        WriteUInt32(payload, Reserved212Offset, 0);
        WriteUInt32(payload, Reserved216Offset, 0);
        WriteUInt32(payload, Reserved220Offset, 0);
    }

    private static ushort CheckedUInt16(uint value, string name)
    {
        if (value > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(name, value, "Legacy warehouse field must fit UInt16.");
        }

        return (ushort)value;
    }

    private static ushort CheckedUInt16(ulong value, string name)
    {
        if (value > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(name, value, "Legacy warehouse field must fit UInt16.");
        }

        return (ushort)value;
    }

    private static ushort ReadUInt16(ReadOnlySpan<byte> payload, int offset)
    {
        return System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(offset, sizeof(ushort)));
    }

    private static uint ReadUInt32(ReadOnlySpan<byte> payload, int offset)
    {
        return System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(payload.Slice(offset, sizeof(uint)));
    }

    private static void WriteUInt16(Span<byte> payload, int offset, ushort value)
    {
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(payload.Slice(offset, sizeof(ushort)), value);
    }

    private static void WriteUInt32(Span<byte> payload, int offset, uint value)
    {
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(payload.Slice(offset, sizeof(uint)), value);
    }

    private static string ReadFixedUtf8(ReadOnlySpan<byte> payload, int offset, int length)
    {
        var bytes = payload.Slice(offset, length);
        var nullIndex = bytes.IndexOf((byte)0);
        if (nullIndex >= 0)
        {
            bytes = bytes[..nullIndex];
        }

        return Encoding.UTF8.GetString(bytes);
    }

    private static void WriteFixedUtf8(Span<byte> destination, string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return;
        }

        var bytes = Encoding.UTF8.GetBytes(value);
        bytes.AsSpan(0, Math.Min(bytes.Length, destination.Length)).CopyTo(destination);
    }
}
