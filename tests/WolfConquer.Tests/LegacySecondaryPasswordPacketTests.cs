using System.Buffers;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacySecondaryPasswordPacketTests
{
    [Fact]
    public void LegacySecondaryPasswordOpcodeMatchesLegacyWorldOpcode()
    {
        Assert.Equal((ushort)2005, WorldPacketOpcodes.LegacySecondaryPassword);
        Assert.Equal(WorldPacketOpcodes.LegacySecondaryPassword, LegacySecondaryPasswordPacket.Opcode);
    }

    [Fact]
    public void LegacySecondaryPasswordStartupPacketWritesFixedLegacyShape()
    {
        var bytes = LegacySecondaryPasswordPacket.StartupPasswordCorrect.ToFrameBytes();

        Assert.Equal(LegacySecondaryPasswordPacket.FrameLength, bytes.Length);
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        Assert.Equal(LegacySecondaryPasswordPacket.Opcode, frame.Header.Opcode);
        Assert.Equal(LegacySecondaryPasswordPacket.PayloadLength, frame.Payload.Length);
        Assert.True(PacketReader.TryRead(bytes, out var reader));
        Assert.Equal(LegacySecondaryPasswordPacket.PasswordCorrectAction, reader.ReadUInt32(0));
        Assert.Equal(LegacySecondaryPasswordPacket.StartupOldPassword, reader.ReadUInt32(4));
        Assert.Equal(LegacySecondaryPasswordPacket.StartupNewPassword, reader.ReadUInt32(8));
    }

    [Fact]
    public void LegacySecondaryPasswordTryCreateUsesSecurityPassFirstField()
    {
        var withPassword = Snapshot(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["SecurityPass"] = "123456,0,0",
        });
        var withoutPassword = Snapshot(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["SecurityPass"] = "0,0,0",
        });
        var missing = Snapshot(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

        Assert.True(LegacySecondaryPasswordPacket.TryCreateStartup(withPassword, out var packet));
        Assert.Equal(LegacySecondaryPasswordPacket.StartupPasswordCorrect, packet);
        Assert.False(LegacySecondaryPasswordPacket.TryCreateStartup(withoutPassword, out _));
        Assert.False(LegacySecondaryPasswordPacket.TryCreateStartup(missing, out _));
    }

    [Fact]
    public void LegacySecondaryPasswordPacketRoundTrips()
    {
        var packet = new LegacySecondaryPasswordPacket(6, 1, 0);
        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.True(LegacySecondaryPasswordPacket.TryParse(frame, out var parsed));
        Assert.Equal(packet, parsed);
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }

    private static CharacterSnapshot Snapshot(IReadOnlyDictionary<string, string> attributes)
    {
        return new CharacterSnapshot(
            1293600,
            "HeroOne",
            1008,
            154,
            27,
            7051,
            0,
            7050,
            2,
            140,
            0,
            1015,
            830,
            584,
            1000,
            500,
            650,
            0,
            36,
            447,
            0,
            12,
            61258,
            5000,
            98602439956,
            639778904,
            456,
            123,
            0,
            7,
            "1293600.ini",
            attributes,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
    }
}
