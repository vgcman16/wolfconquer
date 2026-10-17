using System.Buffers;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyShowEquipmentPacketTests
{
    [Fact]
    public void LegacyShowEquipmentPacketWritesSourceShowShape()
    {
        var packet = new LegacyShowEquipmentPacket(
            WParam: 123,
            Alternante: 1,
            LegacyShowEquipmentPacket.ShowAction,
            new LegacyShowEquipmentSlots(
                Head: 1,
                Necklace: 2,
                Armor: 3,
                RightWeapon: 4,
                LeftWeapon: 5,
                Ring: 6,
                Bottle: 7,
                Boots: 8,
                Garment: 9,
                RightWeaponAccessory: 15,
                LeftWeaponAccessory: 16,
                SteedMount: 17,
                RidingCrop: 18,
                Wing: 19));

        var bytes = packet.ToFrameBytes();
        var frame = ReadFrame(bytes);

        Assert.Equal(87, bytes.Length);
        Assert.Equal(WorldPacketOpcodes.MsgItemUsage, frame.Header.Opcode);
        Assert.Equal(LegacyShowEquipmentPacket.PayloadLength, frame.Payload.Length);
        Assert.True(LegacyShowEquipmentPacket.TryParse(frame, out var parsed));
        Assert.Equal(packet, parsed);
    }

    [Fact]
    public void LegacyShowEquipmentPacketMapsAlternateSlotsToVisibleEquipmentFields()
    {
        var playerItems = new PlayerItemSnapshot(
            InventoryItems: [],
            RelicFuseItems: [],
            MythsoulBagItems: [],
            EquipmentItems:
            [
                Equipment(51_001, 51),
                Equipment(54_001, 54),
                Equipment(55_001, 55),
                Equipment(59_001, 59),
                Equipment(17_001, 17),
            ],
            RuneItems: []);
        var packet = LegacyShowEquipmentPacket.CreateEquipmentRefresh(
            playerItems,
            alternateEquipmentActive: true);

        Assert.Equal(LegacyShowEquipmentPacket.ShowAction, packet.Action);
        Assert.Equal(1U, packet.Alternante);
        Assert.Equal(51_001U, packet.EquipmentSlots.Head);
        Assert.Equal(54_001U, packet.EquipmentSlots.RightWeapon);
        Assert.Equal(55_001U, packet.EquipmentSlots.LeftWeapon);
        Assert.Equal(59_001U, packet.EquipmentSlots.Garment);
        Assert.Equal(17_001U, packet.EquipmentSlots.SteedMount);
    }

    private static PlayerItemSnapshotItem Equipment(uint uid, ushort position)
    {
        return new PlayerItemSnapshotItem
        {
            Uid = uid,
            Position = position,
        };
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }
}
