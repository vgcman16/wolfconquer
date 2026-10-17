using System.Buffers;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyMagicColdTimePacketTests
{
    [Fact]
    public void ColdTimePacketRoundTripsLegacyOpcodeAndSpellTimes()
    {
        var packet = new LegacyMagicColdTimePacket(
            [
                new LegacyMagicColdTimeSpell(1_000, 60_000),
                new LegacyMagicColdTimeSpell(11_120, 1_400),
            ]);
        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.Equal((ushort)2083, frame.Header.Opcode);
        Assert.Equal(WorldPacketOpcodes.LegacyMagicColdTime, frame.Header.Opcode);
        Assert.True(LegacyMagicColdTimePacket.TryParse(frame, out var parsed));
        Assert.Equal(packet.Spells, parsed.Spells);
    }

    [Fact]
    public void ZeroEagleEyeWritesSourceBackedCooldownClearEntry()
    {
        var frame = ReadFrame(LegacyMagicColdTimePacket.ZeroEagleEye().ToFrameBytes());

        Assert.True(LegacyMagicColdTimePacket.TryParse(frame, out var parsed));
        Assert.Equal(
            [new LegacyMagicColdTimeSpell(LegacyMagicColdTimePacket.EagleEyeSpellId, Time: 0)],
            parsed.Spells);
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }
}
