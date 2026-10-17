using System.Buffers;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacySobNpcSpawnPacketTests
{
    [Fact]
    public void LegacySobNpcSpawnOpcodeMatchesLegacyWorldOpcode()
    {
        Assert.Equal((ushort)2166, WorldPacketOpcodes.LegacySobNpcSpawn);
        Assert.Equal(WorldPacketOpcodes.LegacySobNpcSpawn, LegacySobNpcSpawnPacket.Opcode);
    }

    [Fact]
    public void LegacySobNpcSpawnPacketWritesLegacyProtobufShape()
    {
        var packet = new LegacySobNpcSpawnPacket(
            Uid: 25005,
            OwnerUid: 0,
            Unknown2: 0,
            MaxHitpoints: 1000,
            Hitpoints: 750,
            X: 10,
            Y: 20,
            Mesh: 1551,
            Type: 2,
            Sort: 3,
            Name: "Gate");

        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.Equal(LegacySobNpcSpawnPacket.Opcode, frame.Header.Opcode);
        Assert.Equal(
        [
            0x08, 0xAD, 0xC3, 0x01,
            0x10, 0x00,
            0x18, 0x00,
            0x20, 0xE8, 0x07,
            0x28, 0xEE, 0x05,
            0x30, 0x0A,
            0x38, 0x14,
            0x40, 0x8F, 0x0C,
            0x48, 0x02,
            0x50, 0x03,
            0x62, 0x04, 0x47, 0x61, 0x74, 0x65,
        ], frame.Payload.ToArray());
    }

    [Fact]
    public void LegacySobNpcSpawnPacketBuildsFromSeedSobNpc()
    {
        var sobNpc = new SobNpc(
            Id: 102177,
            Name: "Bank",
            Kind: 2,
            Lookface: 59850,
            MapId: 1002,
            X: 345,
            Y: 322,
            Life: 0,
            MaxLife: 0,
            Unknown9: 1,
            Unknown10: 11,
            LocalizedName: "Localized Bank",
            SourceLine: 3,
            RawLine: "102177,Bank,2,59850,1002,345,322,0,0,1,11,Localized Bank",
            Fields: ["102177", "Bank", "2", "59850", "1002", "345", "322", "0", "0", "1", "11", "Localized Bank"]);

        var packet = LegacySobNpcSpawnPacket.FromSobNpc(sobNpc);

        Assert.Equal(new LegacySobNpcSpawnPacket(102177, 0, 0, 0, 0, 345, 322, 59850, 2, 1, "Bank"), packet);
    }

    [Fact]
    public void LegacySobNpcSpawnPacketUsesLocalizedNameWhenRequested()
    {
        var sobNpc = new SobNpc(
            Id: 102177,
            Name: "Bank",
            Kind: 2,
            Lookface: 59850,
            MapId: 1002,
            X: 345,
            Y: 322,
            Life: 0,
            MaxLife: 0,
            Unknown9: 1,
            Unknown10: 11,
            LocalizedName: "Localized Bank",
            SourceLine: 3,
            RawLine: "102177,Bank,2,59850,1002,345,322,0,0,1,11,Localized Bank",
            Fields: ["102177", "Bank", "2", "59850", "1002", "345", "322", "0", "0", "1", "11", "Localized Bank"]);

        var packet = LegacySobNpcSpawnPacket.FromSobNpc(sobNpc, useLocalizedName: true);

        Assert.Equal("Localized Bank", packet.Name);
    }

    [Theory]
    [InlineData("木桩", "Wooden Stake")]
    [InlineData("魔法稻人", "Magic Scarecrow")]
    [InlineData("战神觉醒", "War God Awakening")]
    [InlineData("天石银行", "CP Bank")]
    public void LegacySobNpcSpawnPacketTranslatesSeedChineseNames(string sourceName, string expectedName)
    {
        var sobNpc = new SobNpc(
            102177,
            sourceName,
            2,
            59850,
            1002,
            345,
            322,
            0,
            0,
            1,
            11,
            "Arabic fallback is not English",
            1,
            $"102177,{sourceName},2,59850,1002,345,322,0,0,1,11,Arabic fallback is not English",
            ["102177", sourceName, "2", "59850", "1002", "345", "322", "0", "0", "1", "11", "Arabic fallback is not English"]);

        var packet = LegacySobNpcSpawnPacket.FromSobNpc(sobNpc);

        Assert.Equal(expectedName, packet.Name);
        Assert.DoesNotContain(packet.Name, character => character is >= '\u3400' and <= '\u9fff');
    }

    [Fact]
    public void LegacySobNpcSpawnPacketUsesGuildMapHitpointOverride()
    {
        var sobNpc = new SobNpc(
            Id: 25005,
            Name: "GuildGate",
            Kind: 2,
            Lookface: 1551,
            MapId: 1039,
            X: 10,
            Y: 20,
            Life: 0,
            MaxLife: 0,
            Unknown9: 1,
            Unknown10: null,
            LocalizedName: null,
            SourceLine: 1,
            RawLine: "25005,GuildGate,2,1551,1039,10,20,0,0,1,",
            Fields: ["25005", "GuildGate", "2", "1551", "1039", "10", "20", "0", "0", "1", ""]);

        var packet = LegacySobNpcSpawnPacket.FromSobNpc(sobNpc);

        Assert.Equal(10_000U, packet.Hitpoints);
        Assert.Equal(10_000U, packet.MaxHitpoints);
    }

    [Fact]
    public void LegacySobNpcSpawnPacketRoundTrips()
    {
        var packet = new LegacySobNpcSpawnPacket(25005, 0, 0, 1000, 750, 10, 20, 1551, 2, 3, "Gate");
        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.True(LegacySobNpcSpawnPacket.TryParse(frame, out var parsed));

        Assert.Equal(packet, parsed);
    }

    [Fact]
    public void LegacySobNpcSpawnPacketRejectsWrongOpcode()
    {
        var writer = new PacketWriter(LegacySobNpcSpawnPacket.Opcode + 1, payloadLength: 0);
        var frame = ReadFrame(writer.ToArray());

        Assert.False(LegacySobNpcSpawnPacket.TryParse(frame, out _));
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }
}
