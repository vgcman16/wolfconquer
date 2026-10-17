using System.Buffers;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class MsgQuestDataPacketTests
{
    [Fact]
    public void MsgQuestDataOpcodeMatchesLegacyWorldOpcode()
    {
        Assert.Equal((ushort)2136, WorldPacketOpcodes.MsgQuestData);
        Assert.Equal(WorldPacketOpcodes.MsgQuestData, MsgQuestDataPacket.Opcode);
    }

    [Fact]
    public void MsgQuestDataPacketRoundTripsFields()
    {
        var packet = new MsgQuestDataPacket(
            Unknown: 0x01020304,
            Uid: 0x11121314,
            Intent: 0x21222324);
        var bytes = packet.ToFrameBytes();
        var frame = ReadFrame(bytes);

        Assert.Equal(PacketHeader.Size + MsgQuestDataPacket.PayloadLength, bytes.Length);
        Assert.True(MsgQuestDataPacket.TryParse(frame, out var parsed));
        Assert.Equal(packet, parsed);
    }

    [Theory]
    [InlineData(71_001U)]
    [InlineData(71_002U)]
    public void MsgQuestDataPacketParsesSyntheticLatestClientTaskDetailFrame(uint expectedUid)
    {
        var bytes = CreateTaskDetailFrame(control: 0, (expectedUid, new uint[7]));
        Assert.Equal(PacketHeader.Size + MsgQuestDataPacket.LatestClientPayloadLength, bytes.Length);
        var frame = ReadFrame(bytes);
        Assert.True(MsgQuestDataPacket.TryParseTaskDetailFrame(frame, out var parsed));
        Assert.Equal(0, parsed.Control);
        var record = Assert.Single(parsed.Records);
        Assert.Equal(expectedUid, record.Uid);
        Assert.Equal(new uint[7], record.Intentions);
        Assert.False(MsgQuestDataPacket.TryParse(frame, out _));
    }

    [Fact]
    public void MsgQuestDataPacketParsesLatestClientTaskDetailCounters()
    {
        var bytes = CreateTaskDetailFrame(
            control: 0,
            (6_126U, [1U, 2U, 3U, 4U, 5U, 6U, 7U]));

        Assert.True(MsgQuestDataPacket.TryParseTaskDetailFrame(ReadFrame(bytes), out var parsed));
        var record = Assert.Single(parsed.Records);
        Assert.Equal(6_126U, record.Uid);
        Assert.Equal([1U, 2U, 3U, 4U, 5U, 6U, 7U], record.Intentions);
    }

    [Fact]
    public void MsgQuestDataPacketParsesMultipleLatestClientTaskDetailRecords()
    {
        var bytes = CreateTaskDetailFrame(
            control: 0,
            (6_126U, [1U, 0U, 0U, 0U, 0U, 0U, 0U]),
            (35_024U, [7U, 9U, 0U, 0U, 0U, 0U, 0U]));

        Assert.Equal(PacketHeader.Size + 68, bytes.Length);
        Assert.True(MsgQuestDataPacket.TryParseTaskDetailFrame(ReadFrame(bytes), out var parsed));
        Assert.Equal(2, parsed.Records.Count);
        Assert.Equal(6_126U, parsed.Records[0].Uid);
        Assert.Equal(35_024U, parsed.Records[1].Uid);
        Assert.Equal([7U, 9U, 0U, 0U, 0U, 0U, 0U], parsed.Records[1].Intentions);
    }

    [Fact]
    public void MsgQuestDataObjectiveFrameWritesLegacyProgressShape()
    {
        var bytes = MsgQuestDataPacket.CreateObjectiveFrame(650, [7U, 9U]);

        Assert.Equal(
            Convert.FromHexString("18005808000002008A020000070000000900000000000000"),
            bytes);
        Assert.True(MsgQuestDataPacket.TryParseObjectiveFrame(ReadFrame(bytes), out var parsed));
        Assert.Equal(0, parsed.Unknown);
        Assert.Equal((ushort)2, parsed.Count);
        Assert.Equal(650U, parsed.Uid);
        Assert.Equal([7U, 9U], parsed.Intentions.ToArray());
        Assert.Equal([0U], parsed.ExtraValues.ToArray());
    }

    [Fact]
    public void MsgQuestDataObjectiveFrameWritesKingdom35024LegacyExtras()
    {
        var bytes = MsgQuestDataPacket.CreateObjectiveFrame(35_024, [3U, 4U]);

        Assert.Equal(
            Convert.FromHexString("2000580800000200D08800000300000004000000030000000400000000000000"),
            bytes);
        Assert.True(MsgQuestDataPacket.TryParseObjectiveFrame(ReadFrame(bytes), out var parsed));
        Assert.Equal(35_024U, parsed.Uid);
        Assert.Equal([3U, 4U], parsed.Intentions.ToArray());
        Assert.Equal([3U, 4U, 0U], parsed.ExtraValues.ToArray());
    }

    [Fact]
    public void MsgQuestDataObjectiveFrameWritesKingdom35007LegacyExtras()
    {
        var bytes = MsgQuestDataPacket.CreateObjectiveFrame(35_007, [11U]);

        Assert.Equal(
            Convert.FromHexString("2400580800000100BF8800000B000000000000000000000000000000000000000B000000"),
            bytes);
        Assert.True(MsgQuestDataPacket.TryParseObjectiveFrame(ReadFrame(bytes), out var parsed));
        Assert.Equal(35_007U, parsed.Uid);
        Assert.Equal([11U], parsed.Intentions.ToArray());
        Assert.Equal([0U, 0U, 0U, 0U, 11U], parsed.ExtraValues.ToArray());
    }

    [Fact]
    public void MsgQuestDataPacketWritesLegacyLittleEndianPayloadOrder()
    {
        var packet = new MsgQuestDataPacket(
            Unknown: 0x01020304,
            Uid: 0x11121314,
            Intent: 0x21222324);

        Assert.Equal(
        [
            0x10, 0x00, 0x58, 0x08,
            0x04, 0x03, 0x02, 0x01,
            0x14, 0x13, 0x12, 0x11,
            0x24, 0x23, 0x22, 0x21
        ],
        packet.ToFrameBytes());
    }

    [Fact]
    public void MsgQuestDataPacketRejectsWrongOpcode()
    {
        var writer = new PacketWriter(MsgQuestDataPacket.Opcode + 1, MsgQuestDataPacket.PayloadLength);
        writer.WriteUInt32(0, 0x01020304);
        writer.WriteUInt32(4, 0x11121314);
        writer.WriteUInt32(8, 0x21222324);
        var frame = ReadFrame(writer.ToArray());

        Assert.False(MsgQuestDataPacket.TryParse(frame, out _));
    }

    [Fact]
    public void MsgQuestDataPacketRejectsShortPayload()
    {
        var writer = new PacketWriter(MsgQuestDataPacket.Opcode, MsgQuestDataPacket.PayloadLength - 1);
        var frame = ReadFrame(writer.ToArray());

        Assert.False(MsgQuestDataPacket.TryParse(frame, out _));
    }

    [Fact]
    public void MsgQuestDataPacketRejectsLatestClientTaskDetailCountLengthMismatch()
    {
        var bytes = CreateTaskDetailFrame(
            control: 0,
            (6_126U, [0U, 0U, 0U, 0U, 0U, 0U, 0U]));
        bytes[PacketHeader.Size + sizeof(ushort)] = 2;

        Assert.False(MsgQuestDataPacket.TryParseTaskDetailFrame(ReadFrame(bytes), out _));
    }

    [Fact]
    public void MsgQuestDataPacketRejectsLatestClientTaskDetailNonzeroControl()
    {
        var bytes = CreateTaskDetailFrame(
            control: 1,
            (6_126U, [0U, 0U, 0U, 0U, 0U, 0U, 0U]));

        Assert.False(MsgQuestDataPacket.TryParseTaskDetailFrame(ReadFrame(bytes), out _));
    }

    [Fact]
    public async Task HandlerMsgQuestDataSendsCurrentObjectiveFrameForRuntimeQuest()
    {
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateConnection(sent);
        var session = CreateSession(
            new PlayerQuestSnapshot(
                [
                    new PlayerQuestSnapshotEntry(35_024, PlayerQuestStatus.Accepted, 0, [7U, 9U]),
                ]));
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);
        var packet = new MsgQuestDataPacket(Unknown: 0, Uid: 35_024, Intent: 0);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(connection, log, session),
            ReadFrame(packet.ToFrameBytes()));

        var responseBytes = Assert.Single(sent);
        Assert.True(MsgQuestDataPacket.TryParseObjectiveFrame(ReadFrame(responseBytes), out var response));
        Assert.Equal(35_024U, response.Uid);
        Assert.Equal([7U, 9U], response.Intentions.ToArray());
        Assert.Equal([7U, 9U, 0U], response.ExtraValues.ToArray());
        var output = log.ToString();
        Assert.Contains("WorldServer handled MsgQuestData", output);
        Assert.Contains("objectiveCount=2", output);
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        var sequence = new ReadOnlySequence<byte>(bytes);
        Assert.True(PacketFrameDecoder.TryRead(sequence, out var frame, out _, out _));
        return frame;
    }

    private static byte[] CreateTaskDetailFrame(
        ushort control,
        params (uint Uid, uint[] Intentions)[] records)
    {
        const int headerLength = 2 * sizeof(ushort);
        const int recordLength = 8 * sizeof(uint);
        var writer = new PacketWriter(
            MsgQuestDataPacket.Opcode,
            headerLength + (records.Length * recordLength));
        writer.WriteUInt16(0, control);
        writer.WriteUInt16(sizeof(ushort), checked((ushort)records.Length));

        var offset = headerLength;
        foreach (var record in records)
        {
            Assert.Equal(7, record.Intentions.Length);
            writer.WriteUInt32(offset, record.Uid);
            offset += sizeof(uint);
            foreach (var intention in record.Intentions)
            {
                writer.WriteUInt32(offset, intention);
                offset += sizeof(uint);
            }
        }

        return writer.ToArray();
    }

    private static ConquerTcpConnection CreateConnection(List<byte[]> sent)
    {
        return new ConquerTcpConnection(
            new IPEndPoint(IPAddress.Loopback, 0),
            new IPEndPoint(IPAddress.Loopback, 0),
            (frame, _) =>
            {
                sent.Add(frame.ToArray());
                return ValueTask.CompletedTask;
            });
    }

    private static WorldSession CreateSession(PlayerQuestSnapshot playerQuests)
    {
        return new WorldSession(
            AccountId: 41_001,
            Username: "fixture-quest",
            new CharacterSnapshot(
                Uid: 41_001,
                Name: "FixtureQuest",
                Body: 1008,
                Face: 154,
                Hair: 27,
                Class: 7051,
                FirstClass: 0,
                SecondClass: 7050,
                Reborn: 2,
                Level: 140,
                Experience: 0,
                MapId: 1015,
                X: 830,
                Y: 584,
                PreviousMapId: 1000,
                PreviousMapX: 500,
                PreviousMapY: 650,
                Strength: 0,
                Agility: 36,
                Vitality: 447,
                Spirit: 0,
                AttributePoints: 12,
                MinHitPoints: 61_258,
                MinMana: 5_000,
                Money: 98_602_439_956,
                ConquerPoints: 639_778_904,
                BoundConquerPoints: 456,
                BankConquerPoints: 123,
                PkPoints: 0,
                Online: 7,
                SourceFile: "41001.ini",
                Attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                PaidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase))
            {
                PlayerQuests = playerQuests,
            },
            PlayerItemSnapshot.Empty,
            PlayerSpellSnapshot.Empty,
            PlayerProficiencySnapshot.Empty);
    }
}
