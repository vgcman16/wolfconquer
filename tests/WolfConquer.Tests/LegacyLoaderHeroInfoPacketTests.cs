using System.Buffers;
using System.Buffers.Binary;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyLoaderHeroInfoPacketTests
{
    [Fact]
    public void LegacyLoaderHeroInfoOpcodeMatchesLegacyWorldOpcode()
    {
        Assert.Equal((ushort)22210, WorldPacketOpcodes.LegacyLoaderHeroInfo);
        Assert.Equal(WorldPacketOpcodes.LegacyLoaderHeroInfo, LegacyLoaderHeroInfoPacket.Opcode);
    }

    [Fact]
    public void LegacyLoaderHeroInfoPacketWritesLegacyShape()
    {
        var packet = new LegacyLoaderHeroInfoPacket(
            Uid: 9_000_001,
            Mesh: 11_008,
            ServerId: 2,
            Level: 40,
            NobilityRank: 12,
            VipLevel: 6,
            Class: 10,
            Name: "FixtureHero");
        var bytes = packet.ToFrameBytes();
        var frame = ReadFrame(bytes);

        Assert.True(PacketReader.TryRead(bytes, out var reader));
        Assert.Equal(LegacyLoaderHeroInfoPacket.Opcode, frame.Header.Opcode);
        Assert.Equal(37, frame.Header.Length);
        Assert.Equal(33, frame.Payload.Length);
        Assert.Equal(9_000_001U, reader.ReadUInt32(0));
        Assert.Equal(11_008U, reader.ReadUInt32(4));
        Assert.Equal((ushort)2, reader.ReadUInt16(8));
        Assert.Equal((byte)40, reader.Payload[10]);
        Assert.Equal((byte)12, reader.Payload[11]);
        Assert.Equal((byte)6, reader.Payload[12]);
        Assert.Equal(10U, reader.ReadUInt32(13));
        Assert.Equal((ushort)100, reader.ReadUInt16(17));
        Assert.Equal((byte)1, reader.Payload[19]);
        Assert.Equal((ushort)11, reader.ReadUInt16(20));
        Assert.Equal(Encoding.UTF8.GetBytes("FixtureHero"), reader.Payload[22..].ToArray());
    }

    [Fact]
    public void LegacyLoaderHeroInfoPacketWritesSyntheticUtf8ProfileFrame()
    {
        var packet = new LegacyLoaderHeroInfoPacket(
            Uid: 9_000_001, Mesh: 11_008, ServerId: 7, Level: 42,
            NobilityRank: 3, VipLevel: 1, Class: 20, Name: "Fixture-\u03a9");
        var name = Encoding.UTF8.GetBytes("Fixture-\u03a9");
        var expected = new byte[26 + name.Length];
        BinaryPrimitives.WriteUInt16LittleEndian(expected.AsSpan(0, 2), (ushort)expected.Length);
        BinaryPrimitives.WriteUInt16LittleEndian(expected.AsSpan(2, 2), 22210);
        BinaryPrimitives.WriteUInt32LittleEndian(expected.AsSpan(4, 4), 9_000_001);
        BinaryPrimitives.WriteUInt32LittleEndian(expected.AsSpan(8, 4), 11_008);
        BinaryPrimitives.WriteUInt16LittleEndian(expected.AsSpan(12, 2), 7);
        expected[14] = 42;
        expected[15] = 3;
        expected[16] = 1;
        BinaryPrimitives.WriteUInt32LittleEndian(expected.AsSpan(17, 4), 20);
        BinaryPrimitives.WriteUInt16LittleEndian(expected.AsSpan(21, 2), 100);
        expected[23] = 1;
        BinaryPrimitives.WriteUInt16LittleEndian(expected.AsSpan(24, 2), (ushort)name.Length);
        name.CopyTo(expected, 26);

        Assert.Equal(expected, packet.ToFrameBytes());
        Assert.True(LegacyLoaderHeroInfoPacket.TryParse(ReadFrame(expected), out var parsed));
        Assert.Equal(packet, parsed);
    }

    [Fact]
    public void LegacyLoaderHeroInfoFromSnapshotUsesLegacyFieldSources()
    {
        var snapshot = CreateSnapshot(
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["TransformationID"] = "2",
                ["VipLevel"] = "6"
            },
            paidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Rank"] = "King"
            });

        var packet = LegacyLoaderHeroInfoPacket.FromSnapshot(snapshot, serverId: 2);

        Assert.Equal(9_000_001U, packet.Uid);
        Assert.Equal(20_011_008U, packet.Mesh);
        Assert.Equal((ushort)2, packet.ServerId);
        Assert.Equal((byte)40, packet.Level);
        Assert.Equal((byte)12, packet.NobilityRank);
        Assert.Equal((byte)6, packet.VipLevel);
        Assert.Equal(10U, packet.Class);
        Assert.Equal("FixtureHero", packet.Name);
    }

    [Fact]
    public void LegacyLoaderHeroInfoFromSnapshotSupportsNumericPaidRank()
    {
        var snapshot = CreateSnapshot(
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            paidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["PaidRank"] = "9"
            });

        var packet = LegacyLoaderHeroInfoPacket.FromSnapshot(snapshot, serverId: 2);

        Assert.Equal((byte)9, packet.NobilityRank);
        Assert.Equal(11_008U, packet.Mesh);
    }

    [Fact]
    public void LegacyLoaderHeroInfoPacketRoundTripsUtf8Name()
    {
        var packet = new LegacyLoaderHeroInfoPacket(
            Uid: 9_000_001,
            Mesh: 11_008,
            ServerId: 2,
            Level: 40,
            NobilityRank: 0,
            VipLevel: 0,
            Class: 10,
            Name: "Test-\u03bb");
        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.True(LegacyLoaderHeroInfoPacket.TryParse(frame, out var parsed));

        Assert.Equal(packet, parsed);
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        var sequence = new ReadOnlySequence<byte>(bytes);
        Assert.True(PacketFrameDecoder.TryRead(sequence, out var frame, out _, out _));
        return frame;
    }

    private static CharacterSnapshot CreateSnapshot(
        IReadOnlyDictionary<string, string> attributes,
        IReadOnlyDictionary<string, string> paidNobility)
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
            PaidNobility: paidNobility);
    }
}
