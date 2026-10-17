using System.Buffers;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class MsgQuestDataHandlerCompatibilityTests
{
    [Theory]
    [InlineData(6_126U)]
    [InlineData(35_024U)]
    [InlineData(35_007U)]
    public async Task LatestClientUnknownQuestQueryIsHandledWithoutResponse(uint questUid)
    {
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(
                CreateConnection(sent),
                log,
                CreateSession(PlayerQuestSnapshot.Empty)),
            ReadFrame(CreateLatestClientQuery(questUid)));

        Assert.Empty(sent);
        var output = log.ToString();
        Assert.Contains("WorldServer handled MsgQuestData", output);
        Assert.Contains("layout=latest-client-task-detail", output);
        Assert.Contains("control=0", output);
        Assert.Contains("recordCount=1", output);
        Assert.Contains($"taskUids={questUid}", output);
        Assert.Contains($"incomingIntentions={questUid}:[0,0,0,0,0,0,0]", output);
        Assert.Contains("runtimeQuestsFound=0", output);
        Assert.Contains("responses=0", output);
        Assert.Contains("no objective response sent", output);
        Assert.DoesNotContain("rejected malformed MsgQuestData", output);
    }

    [Fact]
    public async Task LatestClientRuntimeQuestQueryPreservesObjectiveResponse()
    {
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);
        var quests = new PlayerQuestSnapshot(
        [
            new PlayerQuestSnapshotEntry(35_024, PlayerQuestStatus.Accepted, 0, [7U, 9U]),
        ]);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(CreateConnection(sent), log, CreateSession(quests)),
            ReadFrame(CreateLatestClientQuery(35_024, [99U, 98U, 0U, 0U, 0U, 0U, 0U])));

        var responseBytes = Assert.Single(sent);
        Assert.True(MsgQuestDataPacket.TryParseObjectiveFrame(ReadFrame(responseBytes), out var response));
        Assert.Equal(35_024U, response.Uid);
        Assert.Equal([7U, 9U], response.Intentions.ToArray());
        Assert.Equal([7U, 9U, 0U], response.ExtraValues.ToArray());
        var output = log.ToString();
        Assert.Contains("layout=latest-client-task-detail", output);
        Assert.Contains("incomingIntentions=35024:[99,98,0,0,0,0,0]", output);
        Assert.Contains("runtimeQuestsFound=1", output);
        Assert.Contains("responses=1", output);
        Assert.DoesNotContain("rejected malformed MsgQuestData", output);
    }

    private static byte[] CreateLatestClientQuery(uint questUid, uint[]? intentions = null)
    {
        intentions ??= [0U, 0U, 0U, 0U, 0U, 0U, 0U];
        Assert.Equal(7, intentions.Length);
        var writer = new PacketWriter(
            WorldPacketOpcodes.MsgQuestData,
            MsgQuestDataPacket.LatestClientPayloadLength);
        writer.WriteUInt16(0, 0);
        writer.WriteUInt16(sizeof(ushort), 1);
        writer.WriteUInt32(2 * sizeof(ushort), questUid);
        var offset = (2 * sizeof(ushort)) + sizeof(uint);
        foreach (var intention in intentions)
        {
            writer.WriteUInt32(offset, intention);
            offset += sizeof(uint);
        }

        return writer.ToArray();
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        var sequence = new ReadOnlySequence<byte>(bytes);
        Assert.True(PacketFrameDecoder.TryRead(sequence, out var frame, out _, out _));
        return frame;
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
            AccountId: 1_293_600,
            Username: "HeroOne",
            new CharacterSnapshot(
                Uid: 1_293_600,
                Name: "HeroOne",
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
                SourceFile: "1293600.ini",
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
