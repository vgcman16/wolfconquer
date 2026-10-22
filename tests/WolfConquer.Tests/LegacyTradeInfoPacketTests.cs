using System.Buffers;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyTradeInfoPacketTests
{
    [Fact]
    public void NativeTradeInfoWritesAllTwentyRequiredFieldsInSourceOrder()
    {
        var packet = new LegacyTradeInfoPacket(Sender: 100, Receiver: 200, Name: "A", Level: 120, BattlePower: 300);
        byte[] expected =
        [
            0x08, 1, 0x10, 0, 0x18, 100, 0x22, 1, 0x41,
            0x28, 0xC8, 1, 0x30, 120, 0x38, 0xAC, 2,
            0x40, 0, 0x48, 0, 0x50, 0, 0x58, 0, 0x60, 0, 0x68, 0, 0x70, 0, 0x78, 0,
            0x80, 1, 0, 0x88, 1, 0, 0x90, 1, 0, 0x98, 1, 0, 0xA0, 1, 0
        ];

        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.Equal((ushort)2396, frame.Header.Opcode);
        Assert.Equal(LegacyTradeInfoPacket.Opcode, frame.Header.Opcode);
        Assert.Equal(expected, frame.Payload.ToArray());
    }

    [Fact]
    public void NativeTradeInfoWritesRelationsUtf8AndSignedUnknownWithoutDroppingDefaults()
    {
        var packet = new LegacyTradeInfoPacket(1, 2, "é", 3, 4)
        {
            Un1 = false, Un2 = 2, Un8 = 8, Un9 = 9,
            Spouse = true, Friend = true, TradePartner = true, Mentor = true,
            Apprentice = true, Teammate = true, GuildMember = true, Enemy = true,
            Un18 = 18, Un19 = 19, Un20 = -1
        };
        byte[] expected =
        [
            0x08, 0, 0x10, 2, 0x18, 1, 0x22, 2, 0xC3, 0xA9,
            0x28, 2, 0x30, 3, 0x38, 4, 0x40, 8, 0x48, 9,
            0x50, 1, 0x58, 1, 0x60, 1, 0x68, 1, 0x70, 1, 0x78, 1,
            0x80, 1, 1, 0x88, 1, 1, 0x90, 1, 18, 0x98, 1, 19,
            0xA0, 1, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 1
        ];

        Assert.Equal(expected, ReadFrame(packet.ToFrameBytes()).Payload.ToArray());
    }

    [Fact]
    public void NativeTradeInfoAllowsEmptyRequiredNameButRejectsNull()
    {
        var packet = new LegacyTradeInfoPacket(0, 0, "", 0, 0);
        var payload = ReadFrame(packet.ToFrameBytes()).Payload.ToArray();

        Assert.Equal(new byte[] { 0x22, 0 }, payload.AsSpan(6, 2).ToArray());
        Assert.Throws<ArgumentNullException>(() => new LegacyTradeInfoPacket(0, 0, null!, 0, 0).ToFrameBytes());
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }
}
