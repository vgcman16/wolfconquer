using System.Buffers;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public readonly record struct LegacyStartupItemUsagePacket(
    ushort Action,
    LegacyShowEquipmentSlots EquipmentSlots,
    LegacyStartupItemUsageGap Gap = LegacyStartupItemUsageGap.Zero)
{
    private const int IdOffset = 0;
    private const int DwParamOffset = IdOffset + sizeof(uint);
    private const int ActionOffset = DwParamOffset + sizeof(ulong);
    private const int TimestampOffset = ActionOffset + sizeof(ushort);
    private const int DwParam2Offset = TimestampOffset + sizeof(uint);
    private const int DwParam3Offset = DwParam2Offset + sizeof(uint);
    private const int DwParam4Offset = DwParam3Offset + sizeof(uint);
    private const int EquipmentSlotsOffset = 27;

    public const ushort Opcode = WorldPacketOpcodes.MsgItemUsage;
    public const int PayloadLength = 83;
    public const ushort EquipmentStatusAction = (ushort)MsgItemUsageAction.LegacyStartupStatus;

    public static LegacyStartupItemUsagePacket EquipmentStatus { get; } =
        new(EquipmentStatusAction, default);

    public static LegacyStartupItemUsagePacket CreateEquipmentStatus(
        PlayerItemSnapshot playerItems,
        LegacyStartupItemUsageGap gap = LegacyStartupItemUsageGap.Zero)
    {
        ArgumentNullException.ThrowIfNull(playerItems);
        return new LegacyStartupItemUsagePacket(
            EquipmentStatusAction,
            LegacyShowEquipmentSlots.FromEquipmentItems(playerItems.EquipmentItems),
            gap);
    }

    public static bool TryParse(PacketFrame frame, out LegacyStartupItemUsagePacket packet)
    {
        packet = default;

        if (frame.Header.Opcode != Opcode ||
            frame.Payload.Length != PayloadLength ||
            !PacketReader.TryRead(frame.Frame.ToArray(), out var reader))
        {
            return false;
        }

        packet = new LegacyStartupItemUsagePacket(
            reader.ReadUInt16(ActionOffset),
            new LegacyShowEquipmentSlots(
                reader.ReadUInt32(EquipmentSlotsOffset),
                reader.ReadUInt32(EquipmentSlotsOffset + sizeof(uint)),
                reader.ReadUInt32(EquipmentSlotsOffset + (2 * sizeof(uint))),
                reader.ReadUInt32(EquipmentSlotsOffset + (3 * sizeof(uint))),
                reader.ReadUInt32(EquipmentSlotsOffset + (4 * sizeof(uint))),
                reader.ReadUInt32(EquipmentSlotsOffset + (5 * sizeof(uint))),
                reader.ReadUInt32(EquipmentSlotsOffset + (6 * sizeof(uint))),
                reader.ReadUInt32(EquipmentSlotsOffset + (7 * sizeof(uint))),
                reader.ReadUInt32(EquipmentSlotsOffset + (8 * sizeof(uint))),
                reader.ReadUInt32(EquipmentSlotsOffset + (9 * sizeof(uint))),
                reader.ReadUInt32(EquipmentSlotsOffset + (10 * sizeof(uint))),
                reader.ReadUInt32(EquipmentSlotsOffset + (11 * sizeof(uint))),
                reader.ReadUInt32(EquipmentSlotsOffset + (12 * sizeof(uint))),
                reader.ReadUInt32(EquipmentSlotsOffset + (13 * sizeof(uint)))),
            LegacyStartupItemUsageGap.Zero);
        return true;
    }

    public byte[] ToFrameBytes()
    {
        var writer = new PacketWriter(Opcode, PayloadLength);
        writer.WriteUInt16(ActionOffset, Action);
        writer.WriteUInt32(EquipmentSlotsOffset, EquipmentSlots.Head);
        writer.WriteUInt32(EquipmentSlotsOffset + sizeof(uint), EquipmentSlots.Necklace);
        writer.WriteUInt32(EquipmentSlotsOffset + (2 * sizeof(uint)), EquipmentSlots.Armor);
        writer.WriteUInt32(EquipmentSlotsOffset + (3 * sizeof(uint)), EquipmentSlots.RightWeapon);
        writer.WriteUInt32(EquipmentSlotsOffset + (4 * sizeof(uint)), EquipmentSlots.LeftWeapon);
        writer.WriteUInt32(EquipmentSlotsOffset + (5 * sizeof(uint)), EquipmentSlots.Ring);
        writer.WriteUInt32(EquipmentSlotsOffset + (6 * sizeof(uint)), EquipmentSlots.Bottle);
        writer.WriteUInt32(EquipmentSlotsOffset + (7 * sizeof(uint)), EquipmentSlots.Boots);
        writer.WriteUInt32(EquipmentSlotsOffset + (8 * sizeof(uint)), EquipmentSlots.Garment);
        writer.WriteUInt32(EquipmentSlotsOffset + (9 * sizeof(uint)), EquipmentSlots.RightWeaponAccessory);
        writer.WriteUInt32(EquipmentSlotsOffset + (10 * sizeof(uint)), EquipmentSlots.LeftWeaponAccessory);
        writer.WriteUInt32(EquipmentSlotsOffset + (11 * sizeof(uint)), EquipmentSlots.SteedMount);
        writer.WriteUInt32(EquipmentSlotsOffset + (12 * sizeof(uint)), EquipmentSlots.RidingCrop);
        writer.WriteUInt32(EquipmentSlotsOffset + (13 * sizeof(uint)), EquipmentSlots.Wing);
        return writer.ToArray();
    }

}

public enum LegacyStartupItemUsageGap
{
    Zero = 0
}

public readonly record struct LegacyShowEquipmentSlots(
    uint Head,
    uint Necklace,
    uint Armor,
    uint RightWeapon,
    uint LeftWeapon,
    uint Ring,
    uint Bottle,
    uint Boots,
    uint Garment,
    uint RightWeaponAccessory,
    uint LeftWeaponAccessory,
    uint SteedMount,
    uint RidingCrop,
    uint Wing)
{
    private const ushort HeadPosition = 1;
    private const ushort NecklacePosition = 2;
    private const ushort ArmorPosition = 3;
    private const ushort RightWeaponPosition = 4;
    private const ushort LeftWeaponPosition = 5;
    private const ushort RingPosition = 6;
    private const ushort BottlePosition = 7;
    private const ushort BootsPosition = 8;
    private const ushort GarmentPosition = 9;
    private const ushort RightWeaponAccessoryPosition = 15;
    private const ushort LeftWeaponAccessoryPosition = 16;
    private const ushort SteedMountPosition = 17;
    private const ushort RidingCropPosition = 18;
    private const ushort WingPosition = 19;

    public static LegacyShowEquipmentSlots FromEquipmentItems(IReadOnlyList<PlayerItemSnapshotItem> equipmentItems)
    {
        ArgumentNullException.ThrowIfNull(equipmentItems);
        return FromEquipmentItems(equipmentItems, alternateEquipmentActive: false);
    }

    public static LegacyShowEquipmentSlots FromEquipmentItems(
        IReadOnlyList<PlayerItemSnapshotItem> equipmentItems,
        bool alternateEquipmentActive)
    {
        ArgumentNullException.ThrowIfNull(equipmentItems);

        return new LegacyShowEquipmentSlots(
            SelectUid(equipmentItems, alternateEquipmentActive ? (ushort)51 : HeadPosition),
            SelectUid(equipmentItems, alternateEquipmentActive ? (ushort)52 : NecklacePosition),
            SelectUid(equipmentItems, alternateEquipmentActive ? (ushort)53 : ArmorPosition),
            SelectUid(equipmentItems, alternateEquipmentActive ? (ushort)54 : RightWeaponPosition),
            SelectUid(equipmentItems, alternateEquipmentActive ? (ushort)55 : LeftWeaponPosition),
            SelectUid(equipmentItems, alternateEquipmentActive ? (ushort)56 : RingPosition),
            SelectUid(equipmentItems, alternateEquipmentActive ? (ushort)57 : BottlePosition),
            SelectUid(equipmentItems, alternateEquipmentActive ? (ushort)58 : BootsPosition),
            SelectUid(equipmentItems, alternateEquipmentActive ? (ushort)59 : GarmentPosition),
            SelectUid(equipmentItems, RightWeaponAccessoryPosition),
            SelectUid(equipmentItems, LeftWeaponAccessoryPosition),
            SelectUid(equipmentItems, SteedMountPosition),
            SelectUid(equipmentItems, RidingCropPosition),
            SelectUid(equipmentItems, WingPosition));
    }

    private static uint SelectUid(IReadOnlyList<PlayerItemSnapshotItem> equipmentItems, ushort position)
    {
        foreach (var item in equipmentItems)
        {
            if (item.Position == position)
            {
                return item.Uid;
            }
        }

        return 0;
    }
}
