using System.Buffers;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class MsgItemUsagePacketTests
{
    [Theory]
    [InlineData(1, MsgItemUsageAction.BuyItem)]
    [InlineData(4, MsgItemUsageAction.Equip)]
    [InlineData(6, MsgItemUsageAction.Unequip)]
    [InlineData(9, MsgItemUsageAction.ShowWarehouseMoney)]
    [InlineData(10, MsgItemUsageAction.DepositWarehouse)]
    [InlineData(11, MsgItemUsageAction.WarehouseWithdraw)]
    [InlineData(27, MsgItemUsageAction.Ping)]
    [InlineData(44, MsgItemUsageAction.UnAlternante)]
    [InlineData(45, MsgItemUsageAction.Alternante)]
    [InlineData(52, MsgItemUsageAction.ShowItem)]
    [InlineData(55, MsgItemUsageAction.BuyItemFromForging)]
    [InlineData(57, MsgItemUsageAction.EquipRune)]
    [InlineData(58, MsgItemUsageAction.UnequipRune)]
    [InlineData(61, MsgItemUsageAction.Rune)]
    [InlineData(62, MsgItemUsageAction.DropRune)]
    [InlineData(63, MsgItemUsageAction.ComposeRune)]
    [InlineData(64, MsgItemUsageAction.ChanceRune)]
    [InlineData(65, MsgItemUsageAction.DecomposeRune)]
    [InlineData(66, MsgItemUsageAction.RedRuneProgressControl)]
    public void MsgItemUsageActionDefinesLegacyNumericValues(int expected, MsgItemUsageAction action)
    {
        Assert.Equal((ushort)expected, (ushort)action);
    }

    [Fact]
    public void MsgItemUsageOpcodeMatchesLegacyWorldOpcode()
    {
        Assert.Equal((ushort)2137, WorldPacketOpcodes.MsgItemUsage);
        Assert.Equal(WorldPacketOpcodes.MsgItemUsage, MsgItemUsagePacket.Opcode);
    }

    [Fact]
    public void MsgItemUsagePacketRoundTripsFixedHeaderFields()
    {
        var packet = new MsgItemUsagePacket(
            Id: 0x01020304,
            DwParam: 0x0102030405060708,
            Action: MsgItemUsageAction.Alternante,
            Timestamp: 0x0A0B0C0D,
            DwParam2: 0x11121314,
            DwParam3: 0x21222324,
            DwParam4: 0x31323334);
        var bytes = packet.ToFrameBytes();
        var frame = ReadFrame(bytes);

        Assert.Equal(PacketHeader.Size + MsgItemUsagePacket.PayloadLength, bytes.Length);
        Assert.True(MsgItemUsagePacket.TryParse(frame, out var parsed));
        Assert.Equal(packet, parsed);
    }

    [Fact]
    public void MsgItemUsagePacketWritesLegacyLittleEndianFrontFields()
    {
        var packet = new MsgItemUsagePacket(
            Id: 0x01020304,
            DwParam: 0x0102030405060708,
            Action: MsgItemUsageAction.Alternante,
            Timestamp: 0x0A0B0C0D,
            DwParam2: 0x11121314,
            DwParam3: 0x21222324,
            DwParam4: 0x31323334);
        var bytes = packet.ToFrameBytes();

        Assert.Equal(
        [
            0x5B, 0x00, 0x59, 0x08,
            0x04, 0x03, 0x02, 0x01,
            0x08, 0x07, 0x06, 0x05,
            0x04, 0x03, 0x02, 0x01,
            0x2D, 0x00,
            0x0D, 0x0C, 0x0B, 0x0A,
            0x14, 0x13, 0x12, 0x11,
            0x24, 0x23, 0x22, 0x21,
            0x34, 0x33, 0x32, 0x31,
        ], bytes[..(PacketHeader.Size + MsgItemUsagePacket.MinimumPayloadLength)]);
    }

    [Fact]
    public void MsgItemUsagePacketRejectsWrongOpcode()
    {
        var writer = new PacketWriter(MsgItemUsagePacket.Opcode + 1, MsgItemUsagePacket.PayloadLength);
        var frame = ReadFrame(writer.ToArray());

        Assert.False(MsgItemUsagePacket.TryParse(frame, out _));
    }

    [Fact]
    public void MsgItemUsagePacketRejectsShortPayload()
    {
        var writer = new PacketWriter(MsgItemUsagePacket.Opcode, MsgItemUsagePacket.MinimumPayloadLength - 1);
        var frame = ReadFrame(writer.ToArray());

        Assert.False(MsgItemUsagePacket.TryParse(frame, out _));
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        var sequence = new ReadOnlySequence<byte>(bytes);
        Assert.True(PacketFrameDecoder.TryRead(sequence, out var frame, out _, out _));
        return frame;
    }
}
