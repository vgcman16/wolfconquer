using System.Buffers;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public readonly record struct LegacyShowEquipmentPacket(
    uint WParam,
    uint Alternante,
    ushort Action,
    LegacyShowEquipmentSlots EquipmentSlots)
{
    private const int WParamOffset = 0;
    private const int AlternanteOffset = WParamOffset + sizeof(uint);
    private const int ReservedOffset = AlternanteOffset + sizeof(uint);
    private const int ActionOffset = ReservedOffset + sizeof(uint);
    private const int EquipmentSlotsOffset = 27;

    public const ushort Opcode = WorldPacketOpcodes.MsgItemUsage;
    public const int PayloadLength = LegacyStartupItemUsagePacket.PayloadLength;
    public const ushort ShowAction = 46;
    public const ushort AllowAlternanteAction = 44;

    public static LegacyShowEquipmentPacket CreateEquipmentRefresh(
        PlayerItemSnapshot playerItems,
        bool alternateEquipmentActive)
    {
        ArgumentNullException.ThrowIfNull(playerItems);

        return new LegacyShowEquipmentPacket(
            WParam: 0,
            Alternante: alternateEquipmentActive ? 1u : 0u,
            ShowAction,
            LegacyShowEquipmentSlots.FromEquipmentItems(playerItems.EquipmentItems, alternateEquipmentActive));
    }

    public static bool TryParse(PacketFrame frame, out LegacyShowEquipmentPacket packet)
    {
        packet = default;

        if (frame.Header.Opcode != Opcode ||
            frame.Payload.Length != PayloadLength ||
            !PacketReader.TryRead(frame.Frame.ToArray(), out var reader))
        {
            return false;
        }

        var action = reader.ReadUInt16(ActionOffset);
        if (action is not (ShowAction or AllowAlternanteAction))
        {
            return false;
        }

        packet = new LegacyShowEquipmentPacket(
            reader.ReadUInt32(WParamOffset),
            reader.ReadUInt32(AlternanteOffset),
            action,
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
                reader.ReadUInt32(EquipmentSlotsOffset + (13 * sizeof(uint)))));
        return true;
    }

    public byte[] ToFrameBytes()
    {
        var writer = new PacketWriter(Opcode, PayloadLength);
        writer.WriteUInt32(WParamOffset, WParam);
        writer.WriteUInt32(AlternanteOffset, Alternante);
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
