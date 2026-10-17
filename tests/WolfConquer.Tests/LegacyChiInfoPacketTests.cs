using System.Buffers;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyChiInfoPacketTests
{
    private const string PowerLine = "1/5963000/8/2500/0/3/200/0/6/3500/0/2/200/0/";

    [Fact]
    public void LegacyChiInfoOpcodeMatchesLegacyWorldOpcode()
    {
        Assert.Equal((ushort)2204, WorldPacketOpcodes.LegacyChiInfo);
        Assert.Equal(WorldPacketOpcodes.LegacyChiInfo, LegacyChiInfoPacket.Opcode);
    }

    [Fact]
    public void LegacyChiInfoStartupPacketWritesUnlockedPowerItems()
    {
        var state = new LegacyChiState(
            ChiPoints: 153_850,
            Powers:
            [
                LegacyChiPowerState.Parse(LegacyChiPowerType.Dragon, PowerLine),
                LegacyChiPowerState.Parse(LegacyChiPowerType.Phoenix, PowerLine),
                LegacyChiPowerState.Parse(LegacyChiPowerType.Tiger, PowerLine),
                LegacyChiPowerState.Parse(LegacyChiPowerType.Turtle, PowerLine),
            ]);
        var packet = LegacyChiInfoPacket.Startup(1_293_600, state);
        var bytes = packet.ToFrameBytes();
        var frame = ReadFrame(bytes);

        Assert.Equal(WorldPacketOpcodes.LegacyChiInfo, frame.Header.Opcode);
        Assert.Equal(LegacyChiInfoPacket.PayloadLengthForPowerCount(4), frame.Payload.Length);
        Assert.True(LegacyChiInfoPacket.TryParse(frame, out var parsed));
        Assert.Equal(LegacyChiInfoAction.Upgrade, parsed.Action);
        Assert.Equal(1_293_600U, parsed.Uid);
        Assert.Equal(153_850, parsed.ChiPoints);
        Assert.Equal(0U, parsed.LockedFlags);
        Assert.Equal([LegacyChiPowerType.Dragon, LegacyChiPowerType.Phoenix, LegacyChiPowerType.Tiger, LegacyChiPowerType.Turtle], parsed.Powers.Select(power => power.Type));
        Assert.All(parsed.Powers, power => Assert.Equal(5_963_000U, power.Experience));
        Assert.Equal(new LegacyChiAttributeState((byte)LegacyChiAttributeType.AddMagicAttack, 2500, Locked: false), parsed.Powers[0].Attributes[0]);
        Assert.Equal(new LegacyChiAttributeState((byte)LegacyChiAttributeType.Immunity, 200, Locked: false), parsed.Powers[0].Attributes[1]);
        Assert.Equal(new LegacyChiAttributeState((byte)LegacyChiAttributeType.HPAdd, 3500, Locked: false), parsed.Powers[0].Attributes[2]);
        Assert.Equal(new LegacyChiAttributeState((byte)LegacyChiAttributeType.SkillCriticalStrike, 200, Locked: false), parsed.Powers[0].Attributes[3]);

        Assert.True(PacketReader.TryRead(bytes, out var reader));
        Assert.Equal(82_500, reader.ReadInt32(LegacyChiInfoPacket.PowersOffset + sizeof(byte)));
    }

    [Fact]
    public void LegacyChiStateParsesMisspelledPhoenixSnapshotKey()
    {
        var attributes = new Dictionary<string, string>
        {
            ["ChiPoints"] = "153850",
            ["Dragon"] = PowerLine,
            ["Pheonix"] = PowerLine,
            ["Tiger"] = PowerLine,
            ["Turtle"] = PowerLine,
        };
        var state = LegacyChiState.FromAttributes(attributes);

        Assert.Equal(153_850, state.ChiPoints);
        Assert.Equal(4, state.Powers.Count);
        Assert.Equal([LegacyChiPowerType.Dragon, LegacyChiPowerType.Phoenix, LegacyChiPowerType.Tiger, LegacyChiPowerType.Turtle], state.Powers.Select(power => power.Type));
        Assert.All(state.Powers, power => Assert.True(power.Unlocked));
        Assert.Equal(5_963_000U, state.Powers[1].Experience);
    }

    [Fact]
    public void LegacyChiInfoStateUsesEmptyValuesForMissingSnapshot()
    {
        var state = LegacyChiState.FromAttributes(new Dictionary<string, string>());
        var packet = LegacyChiInfoPacket.Startup(1_293_600, state);
        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.Equal(0, state.ChiPoints);
        Assert.Empty(state.Powers);
        Assert.True(LegacyChiInfoPacket.TryParse(frame, out var parsed));
        Assert.Empty(parsed.Powers);
        Assert.Equal(LegacyChiInfoPacket.PayloadLengthForPowerCount(0), frame.Payload.Length);
    }

    [Fact]
    public void LegacyChiInfoPacketRejectsPayloadWhenCountAndLengthDisagree()
    {
        var writer = new PacketWriter(LegacyChiInfoPacket.Opcode, LegacyChiInfoPacket.PayloadLengthForPowerCount(0));
        writer.WriteUInt16(0, (ushort)LegacyChiInfoAction.Upgrade);
        writer.WriteUInt32(2, 1_293_600);
        writer.WriteInt32(6, 153_850);
        writer.WriteUInt32(10, 0);
        writer.WriteUInt32(14, 1);
        var frame = ReadFrame(writer.ToArray());

        Assert.False(LegacyChiInfoPacket.TryParse(frame, out _));
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }
}
