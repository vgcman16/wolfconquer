using System.Buffers;
using System.Buffers.Binary;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacySpecialTitlePacketTests
{
    [Fact]
    public void LegacySpecialTitleOpcodeMatchesLegacyWorldOpcode()
    {
        Assert.Equal((ushort)2120, WorldPacketOpcodes.LegacySpecialTitle);
        Assert.Equal(WorldPacketOpcodes.LegacySpecialTitle, LegacySpecialTitlePacket.Opcode);
    }

    [Fact]
    public void LegacySpecialTitlePacketWritesSyntheticFixedLayout()
    {
        var bytes = LegacySpecialTitlePacket.FromTitleId(901).ToFrameBytes();
        var expected = new byte[36];
        BinaryPrimitives.WriteUInt16LittleEndian(expected.AsSpan(0, 2), 36);
        BinaryPrimitives.WriteUInt16LittleEndian(expected.AsSpan(2, 2), 2120);
        BinaryPrimitives.WriteUInt32LittleEndian(expected.AsSpan(4, 4), 65_539);
        BinaryPrimitives.WriteUInt32LittleEndian(expected.AsSpan(24, 4), 901);
        BinaryPrimitives.WriteUInt32LittleEndian(expected.AsSpan(28, 4), 2);

        Assert.Equal(expected, bytes);
        Assert.Equal(LegacySpecialTitlePacket.FrameLength, bytes.Length);

        var frame = ReadFrame(bytes);

        Assert.Equal(LegacySpecialTitlePacket.Opcode, frame.Header.Opcode);
        Assert.Equal(LegacySpecialTitlePacket.PayloadLength, frame.Payload.Length);
    }

    [Fact]
    public void LegacySpecialTitlePacketRoundTripsFixedPayload()
    {
        var packet = new LegacySpecialTitlePacket(
            HeaderValue: LegacySpecialTitlePacket.DefaultHeaderValue,
            Reserved1: 0,
            Reserved2: 0,
            Reserved3: 0,
            Reserved4: 0,
            TitleId: 35_024,
            Action: LegacySpecialTitlePacket.DefaultAction,
            Reserved5: 0);
        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.True(LegacySpecialTitlePacket.TryParse(frame, out var parsed));

        Assert.Equal(packet, parsed);
    }

    [Fact]
    public void LegacySpecialTitleFromSnapshotSelectsSpecialTitlesWithZeroState()
    {
        var expectedIds = new uint[]
        {
            35_024,
            35_007,
            35_025,
            35_028,
            35_034,
            6_390,
            6_329,
            6_245,
            6_049,
            6_366,
            6_014,
            2_375,
            6_126,
        };
        var rawSpecialTitles = string.Join(",", expectedIds.Select(id => $"{id},0")) + ",9999,1";
        var snapshot = CreateSnapshot(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["SpecialTitles"] = rawSpecialTitles
            });

        var packets = LegacySpecialTitlePacket.FromPersistedSpecialTitles(snapshot);

        Assert.Equal(expectedIds, packets.Select(packet => packet.TitleId));
        Assert.All(packets, packet =>
        {
            Assert.Equal(LegacySpecialTitlePacket.DefaultHeaderValue, packet.HeaderValue);
            Assert.Equal(LegacySpecialTitlePacket.DefaultAction, packet.Action);
        });
    }

    [Fact]
    public void LegacySpecialTitleFromPersistedSpecialTitlesAcceptsLegacyCountPrefixAndTrailingComma()
    {
        var snapshot = CreateSnapshot(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["SpecialTitles"] = "3,6014,0,6023,1,6049,0,"
            });

        var packets = LegacySpecialTitlePacket.FromSnapshot(snapshot);

        Assert.Equal([6014U, 6049U], packets.Select(packet => packet.TitleId));
    }

    [Fact]
    public void LegacySpecialTitleFromPersistedSpecialTitlesTreatsLegacyEmptyDefaultAsNoTitles()
    {
        var snapshot = CreateSnapshot(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["SpecialTitles"] = "0,0,"
            });

        Assert.True(LegacySpecialTitlePacket.TryCreateFromPersistedSpecialTitles(snapshot, out var packets));
        Assert.Empty(packets);
    }

    [Theory]
    [InlineData("6126")]
    [InlineData("6126,0,bad,0")]
    [InlineData("3,6014,0,6023,1,")]
    public void LegacySpecialTitleFromSnapshotReturnsFalseAndEmptyForMalformedPairs(string specialTitles)
    {
        var snapshot = CreateSnapshot(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["SpecialTitles"] = specialTitles
            });

        Assert.False(LegacySpecialTitlePacket.TryCreateFromPersistedSpecialTitles(snapshot, out var packets));
        Assert.Empty(packets);
        Assert.Empty(LegacySpecialTitlePacket.FromSnapshot(snapshot));
    }

    [Fact]
    public void LegacySpecialTitlePacketRejectsWrongPayloadLength()
    {
        var writer = new PacketWriter(LegacySpecialTitlePacket.Opcode, payloadLength: 4);
        writer.WriteUInt32(0, LegacySpecialTitlePacket.DefaultHeaderValue);
        var frame = ReadFrame(writer.ToArray());

        Assert.False(LegacySpecialTitlePacket.TryParse(frame, out _));
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }

    private static CharacterSnapshot CreateSnapshot(IReadOnlyDictionary<string, string> attributes)
    {
        return new CharacterSnapshot(
            Uid: 9_000_001,
            Name: "FixtureHero",
            Body: 1008,
            Face: 1,
            Hair: 1,
            Class: 10,
            FirstClass: 0,
            SecondClass: 0,
            Reborn: 0,
            Level: 40,
            Experience: 0,
            MapId: 1002,
            X: 20,
            Y: 30,
            PreviousMapId: 1002,
            PreviousMapX: 20,
            PreviousMapY: 30,
            Strength: 10,
            Agility: 10,
            Vitality: 10,
            Spirit: 10,
            AttributePoints: 0,
            MinHitPoints: 100,
            MinMana: 50,
            Money: 1000,
            ConquerPoints: 0,
            BoundConquerPoints: 0,
            BankConquerPoints: 0,
            PkPoints: 0,
            Online: 0,
            SourceFile: "synthetic-character.ini",
            Attributes: attributes,
            PaidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
    }
}
