using System.Buffers;
using System.Buffers.Binary;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyNobilityIconPacketTests
{
    [Fact]
    public void LegacyNobilityIconOpcodeMatchesLegacyWorldOpcode()
    {
        Assert.Equal((ushort)2091, WorldPacketOpcodes.LegacyNobilityIcon);
        Assert.Equal(WorldPacketOpcodes.LegacyNobilityIcon, LegacyNobilityIconPacket.Opcode);
    }

    [Fact]
    public void LegacyNobilityIconPacketWritesLegacyShape()
    {
        var packet = new LegacyNobilityIconPacket(
            Uid: 9_000_001,
            Donation: 500_000,
            Rank: 12,
            Position: -1);
        var bytes = packet.ToFrameBytes();
        var frame = ReadFrame(bytes);

        Assert.True(PacketReader.TryRead(bytes, out var reader));
        Assert.Equal(LegacyNobilityIconPacket.Opcode, frame.Header.Opcode);
        Assert.Equal(143, frame.Header.Length);
        Assert.Equal(139, frame.Payload.Length);
        Assert.Equal(3U, reader.ReadUInt32(0));
        Assert.Equal(9_000_001U, reader.ReadUInt32(4));
        Assert.All(reader.Payload.Slice(8, 108).ToArray(), value => Assert.Equal((byte)0, value));
        Assert.Equal((byte)1, reader.Payload[116]);
        Assert.Equal((ushort)20, reader.ReadUInt16(117));
        Assert.Equal("9000001 500000 12 -1", packet.Text);
    }

    [Fact]
    public void LegacyNobilityIconPacketWritesSyntheticTextFrame()
    {
        var packet = new LegacyNobilityIconPacket(Uid: 9_000_001, Donation: 1234, Rank: 3, Position: 2);
        var text = Encoding.UTF8.GetBytes("9000001 1234 3 2");
        var expected = new byte[123 + text.Length];
        BinaryPrimitives.WriteUInt16LittleEndian(expected.AsSpan(0, 2), (ushort)expected.Length);
        BinaryPrimitives.WriteUInt16LittleEndian(expected.AsSpan(2, 2), 2091);
        BinaryPrimitives.WriteUInt32LittleEndian(expected.AsSpan(4, 4), 3);
        BinaryPrimitives.WriteUInt32LittleEndian(expected.AsSpan(8, 4), 9_000_001);
        expected[120] = 1;
        BinaryPrimitives.WriteUInt16LittleEndian(expected.AsSpan(121, 2), (ushort)text.Length);
        text.CopyTo(expected, 123);

        Assert.Equal(expected, packet.ToFrameBytes());
        Assert.True(LegacyNobilityIconPacket.TryParse(ReadFrame(expected), out var parsed));
        Assert.Equal(packet, parsed);
    }

    [Fact]
    public void LegacyNobilityIconFromSnapshotUsesPaidNobilityFields()
    {
        var snapshot = CreateSnapshot(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["LastNobilityDonation"] = "500000",
                ["Rank"] = "King"
            });

        var packet = LegacyNobilityIconPacket.FromSnapshot(snapshot);

        Assert.Equal(9_000_001UL, packet.Uid);
        Assert.Equal(500_000UL, packet.Donation);
        Assert.Equal((byte)12, packet.Rank);
        Assert.Equal(-1, packet.Position);
    }

    [Fact]
    public void LegacyNobilityIconFromSnapshotIgnoresInactivePaidNobilityForRegularDonation()
    {
        var snapshot = CreateSnapshot(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["LastNobilityDonation"] = "0",
                ["PaidRank"] = "0",
                ["IsActive"] = "False"
            },
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["DonationNobility"] = "200000000"
            });

        var packet = LegacyNobilityIconPacket.FromSnapshot(snapshot);

        Assert.Equal(9_000_001UL, packet.Uid);
        Assert.Equal(200_000_000UL, packet.Donation);
        Assert.Equal((byte)5, packet.Rank);
        Assert.Equal(-1, packet.Position);
    }

    [Fact]
    public void LegacyNobilityIconPacketRoundTrips()
    {
        var packet = new LegacyNobilityIconPacket(9_000_001, 500_000, 12, -1);
        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.True(LegacyNobilityIconPacket.TryParse(frame, out var parsed));

        Assert.Equal(packet, parsed);
    }

    [Fact]
    public void LegacyNobilityRequestPacketParsesRankListenRequest()
    {
        var packet = new LegacyNobilityRequestPacket(
            LegacyNobilityAction.RankListen,
            Value: 2,
            DonationType: 0);
        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.True(LegacyNobilityRequestPacket.TryParse(frame, out var parsed));

        Assert.Equal(packet, parsed);
    }

    [Fact]
    public void LegacyNobilityRequestPacketWritesSourceBackedReservedShape()
    {
        var packet = new LegacyNobilityRequestPacket(
            LegacyNobilityAction.Donate,
            Value: 5_000_000_000,
            DonationType: (byte)LegacyNobilityDonationType.ConquerPoints);
        var bytes = packet.ToFrameBytes();
        var frame = ReadFrame(bytes);

        Assert.True(PacketReader.TryRead(bytes, out var reader));
        Assert.True(LegacyNobilityRequestPacket.TryParse(frame, out var parsed));
        Assert.Equal(packet, parsed);
        Assert.Equal(105, frame.Header.Length);
        Assert.Equal(LegacyNobilityRequestPacket.LegacyPayloadLength, frame.Payload.Length);
        Assert.Equal(1U, reader.ReadUInt32(0));
        Assert.Equal(5_000_000_000UL, reader.ReadUInt64(4));
        Assert.All(reader.Payload.Slice(12, 88).ToArray(), value => Assert.Equal((byte)0, value));
        Assert.Equal((byte)LegacyNobilityDonationType.ConquerPoints, reader.Payload[100]);
    }

    [Fact]
    public void LegacyNobilityRankListPacketWritesEmptySourceBackedShape()
    {
        var packet = LegacyNobilityRankListPacket.Empty(page: 2);
        var bytes = packet.ToFrameBytes();
        var frame = ReadFrame(bytes);

        Assert.True(PacketReader.TryRead(bytes, out var reader));
        Assert.True(LegacyNobilityRankListPacket.TryParse(frame, out var parsed));
        Assert.Equal(packet, parsed);
        Assert.Equal(LegacyNobilityRankListPacket.Opcode, frame.Header.Opcode);
        Assert.Equal(120, frame.Header.Length);
        Assert.Equal(116, frame.Payload.Length);
        Assert.Equal(2U, reader.ReadUInt32(0));
        Assert.Equal((ushort)2, reader.ReadUInt16(4));
        Assert.Equal((ushort)0, reader.ReadUInt16(6));
        Assert.Equal((ushort)0, reader.ReadUInt16(8));
        Assert.All(reader.Payload.Slice(10, 102).ToArray(), value => Assert.Equal((byte)0, value));
        Assert.Equal(1U, reader.ReadUInt32(112));
    }

    [Fact]
    public void LegacyNobilityRankListPacketWritesSourceBackedEntries()
    {
        var packet = LegacyNobilityRankListPacket.FromEntries(
            page: 0,
            [
                new LegacyNobilityRankListEntry(
                    Uid: 9_000_001,
                    Gender: 1,
                    Mesh: 1_001_008,
                    Name: "FixtureHero",
                    Donation: 250_000_000,
                    Rank: 12,
                    Position: 0)
            ]);
        var bytes = packet.ToFrameBytes();
        var frame = ReadFrame(bytes);

        Assert.True(PacketReader.TryRead(bytes, out var reader));
        Assert.True(LegacyNobilityRankListPacket.TryParse(frame, out var parsed));
        Assert.Equal(LegacyNobilityRankListPacket.Opcode, frame.Header.Opcode);
        Assert.Equal(184, frame.Header.Length);
        Assert.Equal(180, frame.Payload.Length);
        Assert.Equal((ushort)1, parsed.TotalCount);
        var entry = Assert.Single(parsed.Entries);
        Assert.Equal(9_000_001U, entry.Uid);
        Assert.Equal(1U, entry.Gender);
        Assert.Equal(1_001_008U, entry.Mesh);
        Assert.Equal("FixtureHero", entry.Name);
        Assert.Equal(250_000_000UL, entry.Donation);
        Assert.Equal((byte)12, entry.Rank);
        Assert.Equal(0, entry.Position);
        Assert.Equal(9_000_001U, reader.ReadUInt32(116));
    }

    [Fact]
    public void LegacyNobilityInformationPacketWritesSourceBackedDefaults()
    {
        var packet = new LegacyNobilityInformationPacket();
        var bytes = packet.ToFrameBytes();
        var frame = ReadFrame(bytes);

        Assert.True(PacketReader.TryRead(bytes, out var reader));
        Assert.True(LegacyNobilityInformationPacket.TryParse(frame, out _));
        Assert.Equal(LegacyNobilityInformationPacket.Opcode, frame.Header.Opcode);
        Assert.Equal(104, frame.Header.Length);
        Assert.Equal(100, frame.Payload.Length);
        Assert.Equal(4U, reader.ReadUInt32(0));
        Assert.Equal(30_000_000UL, reader.ReadUInt64(4));
        Assert.Equal(uint.MaxValue, reader.ReadUInt32(12));
        Assert.Equal(1U, reader.ReadUInt32(16));
        Assert.Equal(100_000_000UL, reader.ReadUInt64(20));
        Assert.Equal(3U, reader.ReadUInt32(32));
        Assert.Equal(200_000_000UL, reader.ReadUInt64(36));
        Assert.Equal(5U, reader.ReadUInt32(48));
        Assert.Equal(12U, reader.ReadUInt32(96));
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        var sequence = new ReadOnlySequence<byte>(bytes);
        Assert.True(PacketFrameDecoder.TryRead(sequence, out var frame, out _, out _));
        return frame;
    }

    private static CharacterSnapshot CreateSnapshot(
        IReadOnlyDictionary<string, string> paidNobility,
        IReadOnlyDictionary<string, string>? attributes = null)
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
            SourceFile: "9000001.ini",
            Attributes: attributes ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            PaidNobility: paidNobility);
    }
}
