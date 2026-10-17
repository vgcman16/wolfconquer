using System.Buffers;
using System.Buffers.Binary;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyStartupItemUsagePacketTests
{
    [Theory]
    [InlineData(1_000U)]
    [InlineData(2_000U)]
    public void LegacyStartupItemUsagePacketWritesSyntheticEquipmentSlotLayout(uint baseUid)
    {
        ushort[] positions = [1, 2, 3, 4, 5, 6, 7, 8, 9, 15, 16, 17, 18, 19];
        var items = positions.Select(position => Equipment(baseUid + position, position)).ToArray();
        var packet = LegacyStartupItemUsagePacket.CreateEquipmentStatus(Snapshot(items));
        var bytes = packet.ToFrameBytes();

        // Assemble the wire contract from invented slot identifiers and explicit offsets.
        var expected = new byte[87];
        BinaryPrimitives.WriteUInt16LittleEndian(expected.AsSpan(0), 87);
        BinaryPrimitives.WriteUInt16LittleEndian(expected.AsSpan(2), 2137);
        BinaryPrimitives.WriteUInt16LittleEndian(expected.AsSpan(16), 46);
        for (var index = 0; index < positions.Length; index++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(expected.AsSpan(31 + 4 * index), baseUid + positions[index]);
        }
        Assert.Equal(expected, bytes);
        Assert.True(LegacyStartupItemUsagePacket.TryParse(ReadFrame(bytes), out var parsed));
        Assert.Equal(packet, parsed);
        Assert.Equal(LegacyStartupItemUsageGap.Zero, parsed.Gap);
        Assert.Equal(baseUid + 1, parsed.EquipmentSlots.Head);
        Assert.Equal(baseUid + 15, parsed.EquipmentSlots.RightWeaponAccessory);
        Assert.Equal(baseUid + 16, parsed.EquipmentSlots.LeftWeaponAccessory);
        Assert.Equal(baseUid + 19, parsed.EquipmentSlots.Wing);
    }

    [Fact]
    public void LegacyStartupItemUsagePacketLeavesMissingSlotsAndGapZero()
    {
        var packet = LegacyStartupItemUsagePacket.CreateEquipmentStatus(Snapshot(
            [Equipment(3_001, 1), Equipment(3_010, 10), Equipment(3_011, 11), Equipment(3_019, 19)]));
        var bytes = packet.ToFrameBytes();
        Assert.Equal(new byte[13], bytes[18..31]);
        Assert.True(LegacyStartupItemUsagePacket.TryParse(ReadFrame(bytes), out var parsed));
        Assert.Equal(3_001U, parsed.EquipmentSlots.Head);
        Assert.Equal(0U, parsed.EquipmentSlots.Necklace);
        Assert.Equal(0U, parsed.EquipmentSlots.RightWeaponAccessory);
        Assert.Equal(0U, parsed.EquipmentSlots.LeftWeaponAccessory);
        Assert.Equal(3_019U, parsed.EquipmentSlots.Wing);
        Assert.Equal(LegacyStartupItemUsageGap.Zero, parsed.Gap);
    }

    [Fact]
    public void LegacyStartupItemUsagePacketKeepsZeroSlotSingleton()
    {
        var packet = LegacyStartupItemUsagePacket.EquipmentStatus;
        var bytes = packet.ToFrameBytes();
        var frame = ReadFrame(bytes);
        Assert.Equal(87, bytes.Length);
        Assert.Equal(WorldPacketOpcodes.MsgItemUsage, frame.Header.Opcode);
        Assert.True(LegacyStartupItemUsagePacket.TryParse(frame, out var parsed));
        Assert.Equal(LegacyStartupItemUsagePacket.EquipmentStatusAction, parsed.Action);
        Assert.Equal(default, parsed.EquipmentSlots);
    }

    private static PlayerItemSnapshot Snapshot(PlayerItemSnapshotItem[] equipment) =>
        new(InventoryItems: [], RelicFuseItems: [], MythsoulBagItems: [], EquipmentItems: equipment, RuneItems: []);

    private static PlayerItemSnapshotItem Equipment(uint uid, ushort position) =>
        new() { Uid = uid, Position = position };

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }
}
