using System.Buffers;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyQuestEventObjectNpcTests
{
    private const uint PeachTreeNpcId = 7_997;
    private const uint PeachTree2NpcId = 7_996;
    private const uint SquidwardOctopusNpcId = 9_534;
    private const uint Class6HouseNpcId = 18_950;

    [Theory]
    [InlineData(PeachTreeNpcId, "NpcHandler.PeachTree")]
    [InlineData(PeachTree2NpcId, "NpcHandler.PeachTree2")]
    public async Task PeachTreesFenceUnmodeledQuestPickState(uint npcId, string sourceMethod)
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession();

        var handled = await WorldPacketHandlers.TryHandleLegacyQuestEventObjectNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session),
            session,
            npcId,
            optionId: 0,
            CancellationToken.None);

        Assert.True(handled);
        var replies = ParseNpcReplies(sent);
        Assert.Contains(replies, reply => reply.Text == "Peach Tree picking is not available in this runtime yet.");
        Assert.Contains("PARITY-GAP LegacyNpc QuestEventObject unavailable", log.ToString());
        Assert.Contains($"source={sourceMethod}", log.ToString());
        Assert.Contains("QuestGUI.IsActiveQuest(1011)", log.ToString());
        AssertAscii(replies);
    }

    [Fact]
    public async Task SquidwardSendsDeterministicRewardDialog()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession();

        var handled = await WorldPacketHandlers.TryHandleLegacyQuestEventObjectNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session),
            session,
            SquidwardOctopusNpcId,
            optionId: 20,
            CancellationToken.None);

        Assert.True(handled);
        var replies = ParseNpcReplies(sent);
        Assert.Contains(replies, reply => reply.Text.Contains("Gold Coins can be exchanged"));
        Assert.Contains(replies, reply => reply.Text == "What do you mean by limited?" && reply.OptionId == 21);
        Assert.Contains("source=NpcHandler.SquidwardOctopus", log.ToString());
        Assert.DoesNotContain("PARITY-GAP", log.ToString());
        AssertAscii(replies);
    }

    [Fact]
    public async Task SquidwardFencesGlobalEventSignupState()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(level: 140);

        var handled = await WorldPacketHandlers.TryHandleLegacyQuestEventObjectNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session),
            session,
            SquidwardOctopusNpcId,
            optionId: 31,
            CancellationToken.None);

        Assert.True(handled);
        var replies = ParseNpcReplies(sent);
        Assert.Contains(replies, reply => reply.Text == "The Proud Sea teleport is not available in this runtime yet.");
        Assert.Contains("source=NpcHandler.SquidwardOctopus", log.ToString());
        Assert.Contains("MsgSchedules.SquidwardOctopus.Process", log.ToString());
        Assert.Contains("PARITY-GAP LegacyNpc QuestEventObject unavailable", log.ToString());
        Assert.Empty(ParseMsgActions(sent));
        AssertAscii(replies);
    }

    [Fact]
    public async Task SquidwardLowLevelBranchIsDeterministic()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(level: 79);

        var handled = await WorldPacketHandlers.TryHandleLegacyQuestEventObjectNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session),
            session,
            SquidwardOctopusNpcId,
            optionId: 31,
            CancellationToken.None);

        Assert.True(handled);
        var replies = ParseNpcReplies(sent);
        Assert.Contains(replies, reply => reply.Text.Contains("come back when you reach Level 80"));
        Assert.DoesNotContain("PARITY-GAP", log.ToString());
        Assert.Empty(ParseMsgActions(sent));
        AssertAscii(replies);
    }

    [Fact]
    public async Task Class6HouseSendsRootDialogAndMaterialMenu()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession();

        var handled = await WorldPacketHandlers.TryHandleLegacyQuestEventObjectNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session),
            session,
            Class6HouseNpcId,
            optionId: 0,
            CancellationToken.None);

        Assert.True(handled);
        var replies = ParseNpcReplies(sent);
        Assert.Contains(replies, reply => reply.Text.Contains("Class 6 model house"));
        AssertOption(replies, "Upgrade my house with CPs.", 1);
        AssertOption(replies, "Use materials to upgrade.", 3);
        AssertOption(replies, "How do I upgrade my house?", 6);
        AssertOption(replies, "Where is the exit?", 2);
        Assert.Contains("source=NpcHandler.Class6House", log.ToString());
        AssertAscii(replies);

        sent.Clear();
        handled = await WorldPacketHandlers.TryHandleLegacyQuestEventObjectNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session),
            session,
            Class6HouseNpcId,
            optionId: 3,
            CancellationToken.None);

        Assert.True(handled);
        replies = ParseNpcReplies(sent);
        Assert.Contains(replies, reply => reply.Text.Contains("500 Class 6 House Packs"));
        AssertOption(replies, "100.", 4);
        AssertOption(replies, "500.", 5);
        AssertAscii(replies);
    }

    [Fact]
    public async Task Class6HouseExitTeleportsToSourceDestination()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var sessions = new WorldSessionStore();
        var session = CreateSession(mapId: 1_002, x: 463, y: 367);
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacyQuestEventObjectNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            session,
            Class6HouseNpcId,
            optionId: 2,
            CancellationToken.None);

        Assert.True(handled);
        var teleport = Assert.Single(ParseMsgActions(sent), action => action.Type == MsgActionType.Teleport);
        Assert.Equal(1_036U, teleport.DwParam);
        Assert.Equal(200U, teleport.PositionX);
        Assert.Equal(95U, teleport.PositionY);
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(1_036, updated.Character.MapId);
        Assert.Equal(200, updated.Character.X);
        Assert.Equal(95, updated.Character.Y);
        Assert.Contains("source=NpcHandler.Class6House map=1036 x=200 y=95", log.ToString());
    }

    [Fact]
    public async Task Class6HouseFencesUnsafeUpgradeWhenCpIsAvailable()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(conquerPoints: 99_000);

        var handled = await WorldPacketHandlers.TryHandleLegacyQuestEventObjectNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session),
            session,
            Class6HouseNpcId,
            optionId: 1,
            CancellationToken.None);

        Assert.True(handled);
        var replies = ParseNpcReplies(sent);
        Assert.Contains(replies, reply => reply.Text == "Class 6 house CP upgrading is not available in this runtime yet.");
        Assert.Contains("source=NpcHandler.Class6House", log.ToString());
        Assert.Contains("MyHouse.Level", log.ToString());
        Assert.Contains("PARITY-GAP LegacyNpc QuestEventObject unavailable", log.ToString());
        Assert.Empty(ParseMsgActions(sent));
        AssertAscii(replies);
    }

    private static void AssertOption(IReadOnlyList<LegacyNpcReplyPacket> replies, string text, byte optionId)
    {
        Assert.Contains(replies, reply => reply.Text == text && reply.OptionId == optionId);
    }

    private static void AssertAscii(IEnumerable<LegacyNpcReplyPacket> replies)
    {
        Assert.All(
            replies.Where(reply => !string.IsNullOrEmpty(reply.Text)),
            reply => Assert.True(reply.Text.All(static character => character <= 127), reply.Text));
    }

    private static WorldSession CreateSession(
        long mapId = 1_002,
        long x = 463,
        long y = 367,
        long level = 140,
        uint conquerPoints = 0)
    {
        return new WorldSession(
            AccountId: 100,
            Username: "quest-event-test",
            Character: new CharacterSnapshot(
                Uid: 1_293_600,
                Name: "QuestHero",
                Body: 1003,
                Face: 0,
                Hair: 0,
                Class: 7051,
                FirstClass: 7051,
                SecondClass: 0,
                Reborn: 0,
                Level: level,
                Experience: 0,
                MapId: mapId,
                X: x,
                Y: y,
                PreviousMapId: mapId,
                PreviousMapX: x,
                PreviousMapY: y,
                Strength: 0,
                Agility: 0,
                Vitality: 0,
                Spirit: 0,
                AttributePoints: 0,
                MinHitPoints: 1,
                MinMana: 0,
                Money: 0,
                ConquerPoints: conquerPoints,
                BoundConquerPoints: 0,
                BankConquerPoints: 0,
                PkPoints: 0,
                Online: 0,
                SourceFile: "fixture",
                Attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["DynamicID"] = "0",
                },
                PaidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)),
            PlayerItems: PlayerItemSnapshot.Empty,
            PlayerSpells: PlayerSpellSnapshot.Empty,
            PlayerProficiencies: PlayerProficiencySnapshot.Empty);
    }

    private static ConquerTcpConnection CreateSendingConnection(List<byte[]> sent)
    {
        return new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958), (frame, _) =>
        {
            sent.Add(frame.ToArray());
            return ValueTask.CompletedTask;
        });
    }

    private static IReadOnlyList<LegacyNpcReplyPacket> ParseNpcReplies(IEnumerable<byte[]> sent)
    {
        var packets = new List<LegacyNpcReplyPacket>();
        foreach (var bytes in sent)
        {
            if (LegacyNpcReplyPacket.TryParse(ReadFrame(bytes), out var packet))
            {
                packets.Add(packet);
            }
        }

        return packets;
    }

    private static IReadOnlyList<MsgActionPacket> ParseMsgActions(IEnumerable<byte[]> sent)
    {
        var packets = new List<MsgActionPacket>();
        foreach (var bytes in sent)
        {
            if (MsgActionPacket.TryParse(ReadFrame(bytes), out var packet))
            {
                packets.Add(packet);
            }
        }

        return packets;
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }
}
