using System.Buffers;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyProtoHeroInfoPacketTests
{
    [Fact]
    public void LegacyProtoHeroInfoOpcodeMatchesLegacyWorldOpcode()
    {
        Assert.Equal((ushort)2221, WorldPacketOpcodes.LegacyProtoHeroInfo);
        Assert.Equal(WorldPacketOpcodes.LegacyProtoHeroInfo, LegacyProtoHeroInfoPacket.Opcode);
    }

    [Fact]
    public void LegacyProtoHeroInfoFromSnapshotUsesLegacyFieldSources()
    {
        var snapshot = CreateSnapshot(
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["AparenceType"] = "3",
                ["TransformationID"] = "2",
                ["VirtutePoints"] = "11",
                ["QuizPoints"] = "12",
                ["MainFlag"] = "13",
                ["Enilghten"] = "200",
                ["EnlightenReceive"] = "2",
                ["VipLevel"] = "6",
                ["RacePoints"] = "15",
                ["CountryID"] = "16",
                ["DominoCode"] = "42042",
                ["ClassExperience"] = "5500",
                ["Spouse"] = "None",
                ["Signature"] = "Ready"
            },
            paidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Rank"] = "King"
            });

        var packet = LegacyProtoHeroInfoPacket.FromSnapshot(snapshot, serverId: 2);

        Assert.Equal(900_001UL, packet.GetNumericField(1));
        Assert.Equal(3UL, packet.GetNumericField(2));
        Assert.Equal(20_121_003UL, packet.GetNumericField(3));
        Assert.Equal(31UL, packet.GetNumericField(4));
        Assert.Equal(9_876_543_210UL, packet.GetNumericField(5));
        Assert.Equal(123_456UL, packet.GetNumericField(6));
        Assert.Equal(2UL, packet.GetNumericField(8));
        Assert.Equal(11UL, packet.GetNumericField(12));
        Assert.Equal(20UL, packet.GetNumericField(14));
        Assert.Equal(30UL, packet.GetNumericField(15));
        Assert.Equal(1_234UL, packet.GetNumericField(18));
        Assert.Equal(567UL, packet.GetNumericField(19));
        Assert.Equal(12UL, packet.GetNumericField(25));
        Assert.Equal(200UL, packet.GetNumericField(30));
        Assert.Equal(200UL, packet.GetNumericField(31));
        Assert.Equal(6UL, packet.GetNumericField(34));
        Assert.Equal(456UL, packet.GetNumericField(36));
        Assert.Equal(15UL, packet.GetNumericField(39));
        Assert.Equal(16UL, packet.GetNumericField(40));
        Assert.Equal(42_042UL, packet.GetNumericField(42));
        Assert.Equal(5_500UL, packet.GetNumericField(43));
        Assert.Equal("SyntheticHero", packet.GetStringField(46));
        Assert.Equal("None", packet.GetStringField(47));
        Assert.Equal("Ready", packet.GetStringField(50));
    }

    [Fact]
    public void LegacyProtoHeroInfoFromSnapshotDerivesActiveWingFromSpecialTitles()
    {
        var dbRoot = Path.Combine(Path.GetTempPath(), "wolf-proto-hero-title-" + Guid.NewGuid());
        try
        {
            var usersRoot = Path.Combine(dbRoot, "Users");
            Directory.CreateDirectory(usersRoot);
            File.WriteAllText(
                Path.Combine(dbRoot, "title_type.txt"),
                "6023@@6023@@SyntheticWing@@0@@0@@0@@0@@0@@");
            var snapshot = CreateSnapshot(
                attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["SpecialTitles"] = "1,6023,1,",
                    ["Spouse"] = "None",
                    ["Signature"] = string.Empty
                },
                paidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

            var packet = LegacyProtoHeroInfoPacket.FromSnapshot(snapshot, usersRoot: usersRoot);

            Assert.Equal(0UL, packet.GetNumericField(10));
            Assert.Equal(60_236_023UL, packet.GetNumericField(11));
        }
        finally
        {
            if (Directory.Exists(dbRoot))
            {
                Directory.Delete(dbRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void LegacyProtoHeroInfoPacketWritesSyntheticActiveWingPrefix()
    {
        var dbRoot = Path.Combine(Path.GetTempPath(), "wolf-proto-hero-synthetic-" + Guid.NewGuid());
        try
        {
            var usersRoot = Path.Combine(dbRoot, "Users");
            Directory.CreateDirectory(usersRoot);
            File.WriteAllText(
                Path.Combine(dbRoot, "title_type.txt"),
                "6023@@6023@@SyntheticWing@@0@@0@@0@@0@@0@@");
            var snapshot = CreateSnapshot(
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["SpecialTitles"] = "1,6023,1,"
                },
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
            var packet = LegacyProtoHeroInfoPacket.FromSnapshot(snapshot, usersRoot: usersRoot);
            var frame = ReadFrame(packet.ToFrameBytes());
            // Independently encoded fields 1..11 for the synthetic snapshot, including wing 6023/6023.
            byte[] expectedPrefix =
            [
                0x08, 0xA1, 0xF7, 0x36, 0x10, 0x00, 0x18, 0xAB, 0xB1, 0x07,
                0x20, 0x1F, 0x28, 0xEA, 0xAD, 0xC0, 0xE5, 0x24, 0x30, 0xC0,
                0xC4, 0x07, 0x38, 0xB9, 0x60, 0x40, 0x02, 0x48, 0x00, 0x50,
                0x00, 0x58, 0xF7, 0xC1, 0xDC, 0x1C,
            ];

            Assert.True(frame.Payload.ToArray().AsSpan(0, expectedPrefix.Length).SequenceEqual(expectedPrefix));
            Assert.Equal(60_236_023UL, packet.GetNumericField(11));
        }
        finally
        {
            if (Directory.Exists(dbRoot))
            {
                Directory.Delete(dbRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void LegacyProtoHeroInfoPacketRoundTripsAllRequiredFields()
    {
        var packet = LegacyProtoHeroInfoPacket.FromSnapshot(
            CreateSnapshot(
                attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["VipLevel"] = "1",
                    ["Spouse"] = "None",
                    ["Signature"] = ""
                },
                paidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["PaidRank"] = "9"
                }),
            serverId: 2);
        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.True(LegacyProtoHeroInfoPacket.TryParse(frame, out var parsed));

        Assert.Equal(packet.GetNumericField(1), parsed.GetNumericField(1));
        Assert.Equal(packet.GetNumericField(3), parsed.GetNumericField(3));
        Assert.Equal(packet.GetNumericField(25), parsed.GetNumericField(25));
        Assert.Equal(packet.GetNumericField(34), parsed.GetNumericField(34));
        Assert.Equal(packet.GetStringField(46), parsed.GetStringField(46));
        Assert.Equal(packet.GetStringField(47), parsed.GetStringField(47));
        Assert.Equal(string.Empty, parsed.GetStringField(50));
    }

    [Fact]
    public void LegacyProtoHeroInfoPacketRejectsMissingRequiredFields()
    {
        var writer = new PacketWriter(LegacyProtoHeroInfoPacket.Opcode, payloadLength: 2);
        writer.Payload[0] = 0x08;
        writer.Payload[1] = 0x00;
        var frame = ReadFrame(writer.ToArray());

        Assert.False(LegacyProtoHeroInfoPacket.TryParse(frame, out _));
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
            Uid: 900_001,
            Name: "SyntheticHero",
            Body: 1003,
            Face: 12,
            Hair: 31,
            Class: 1001,
            FirstClass: 0,
            SecondClass: 1000,
            Reborn: 1,
            Level: 42,
            Experience: 12_345,
            MapId: 1000,
            X: 123,
            Y: 456,
            PreviousMapId: 1000,
            PreviousMapX: 10,
            PreviousMapY: 20,
            Strength: 10,
            Agility: 20,
            Vitality: 30,
            Spirit: 40,
            AttributePoints: 5,
            MinHitPoints: 1_234,
            MinMana: 567,
            Money: 9_876_543_210,
            ConquerPoints: 123_456,
            BoundConquerPoints: 456,
            BankConquerPoints: 123,
            PkPoints: 0,
            Online: 7,
            SourceFile: "synthetic-character.ini",
            Attributes: attributes,
            PaidNobility: paidNobility);
    }

}
