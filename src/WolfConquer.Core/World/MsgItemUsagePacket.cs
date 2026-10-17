using System.Buffers;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public enum MsgItemUsageAction : ushort
{
    BuyItem = 1,
    SellItem = 2,
    RemoveInventory = 3,
    Equip = 4,
    SetEquipPosition = 5,
    Unequip = 6,
    ShowWarehouseMoney = 9,
    DepositWarehouse = 10,
    WarehouseWithdraw = 11,
    RepairItem = 14,
    UpdateDurability = 17,
    ShowVendingList = 21,
    AddVendingItemGold = 22,
    RemoveVendingItem = 23,
    BuyVendingItem = 24,
    Ping = 27,
    AddVendingItemConquerPoints = 29,
    DropItem = 37,
    ActiveItems = 41,
    CreateSocketItem = 43,
    UnAlternante = 44,
    Alternante = 45,
    LegacyStartupStatus = 46,
    ShowItem = 52,
    BuyItemFromForging = 55,
    EquipRune = 57,
    UnequipRune = 58,
    Rune = 61,
    DropRune = 62,
    ComposeRune = 63,
    ChanceRune = 64,
    DecomposeRune = 65,
    RedRuneProgressControl = 66,
    DecomposeRelics = 67,
    MythForggingMythsoulBagAdd = 78,
    MythForggingMythsoulBagRemove = 79
}

public readonly record struct MsgItemUsagePacket(
    uint Id,
    ulong DwParam,
    MsgItemUsageAction Action,
    uint Timestamp,
    uint DwParam2,
    uint DwParam3,
    uint DwParam4)
{
    private const int IdOffset = 0;
    private const int DwParamOffset = IdOffset + sizeof(uint);
    private const int ActionOffset = DwParamOffset + sizeof(ulong);
    private const int TimestampOffset = ActionOffset + sizeof(ushort);
    private const int DwParam2Offset = TimestampOffset + sizeof(uint);
    private const int DwParam3Offset = DwParam2Offset + sizeof(uint);
    private const int DwParam4Offset = DwParam3Offset + sizeof(uint);

    public const ushort Opcode = WorldPacketOpcodes.MsgItemUsage;
    public const int MinimumPayloadLength = DwParam4Offset + sizeof(uint);
    public const int PayloadLength = 87;

    public static bool TryParse(PacketFrame frame, out MsgItemUsagePacket packet)
    {
        packet = default;

        if (frame.Header.Opcode != Opcode ||
            frame.Payload.Length < MinimumPayloadLength ||
            !PacketReader.TryRead(frame.Frame.ToArray(), out var reader))
        {
            return false;
        }

        packet = new MsgItemUsagePacket(
            reader.ReadUInt32(IdOffset),
            reader.ReadUInt64(DwParamOffset),
            (MsgItemUsageAction)reader.ReadUInt16(ActionOffset),
            reader.ReadUInt32(TimestampOffset),
            reader.ReadUInt32(DwParam2Offset),
            reader.ReadUInt32(DwParam3Offset),
            reader.ReadUInt32(DwParam4Offset));
        return true;
    }

    public byte[] ToFrameBytes()
    {
        var writer = new PacketWriter(Opcode, PayloadLength);
        writer.WriteUInt32(IdOffset, Id);
        writer.WriteUInt64(DwParamOffset, DwParam);
        writer.WriteUInt16(ActionOffset, (ushort)Action);
        writer.WriteUInt32(TimestampOffset, Timestamp);
        writer.WriteUInt32(DwParam2Offset, DwParam2);
        writer.WriteUInt32(DwParam3Offset, DwParam3);
        writer.WriteUInt32(DwParam4Offset, DwParam4);
        return writer.ToArray();
    }
}
