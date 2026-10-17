using System.Buffers;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyEventGuideNpcTests
{
    private const uint EventGuideNpcId = 5_125;

    [Fact]
    public async Task HandlerSendsTranslatedRootGuideDialogWithSourceOptions()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession();
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacyEventGuideNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            session,
            EventGuideNpcId,
            optionId: 0,
            CancellationToken.None);

        Assert.True(handled);
        var replies = ParseNpcReplies(sent);
        Assert.Contains(replies, reply => reply.Text == "Hello. I can guide you to the server's events and wars.");
        AssertOption(replies, "Events by minute.", 1);
        AssertOption(replies, "Events by hour.", 2);
        AssertOption(replies, "Events by day.", 3);
        AssertOption(replies, "No, thanks.", byte.MaxValue);
        Assert.Contains(replies, reply => reply.InteractType == LegacyNpcReplyInteractType.Avatar && reply.InputMaxLength == 286);
        Assert.All(replies.Where(reply => !string.IsNullOrEmpty(reply.Text)), reply => Assert.True(IsAscii(reply.Text), reply.Text));
        Assert.Contains("source=NpcHandler.Helpr", log.ToString());
    }

    [Fact]
    public async Task HandlerSendsTranslatedMinuteMenuWithSourceOptionIds()
    {
        using var log = new StringWriter();
        var replies = await SendDialogAndParseRepliesAsync(log, optionId: 1);

        Assert.Contains(replies, reply => reply.Text == "These events run every few minutes.");
        AssertOption(replies, "The Grave [xx:02-xx:04].", 4);
        AssertOption(replies, "Treasure Thief [xx:16].", 35);
        AssertOption(replies, "Crazy War [xx:01].", 37);
        AssertOption(replies, "Anima Shower [xx:33].", 38);
        AssertOption(replies, "Find the GM [xx:05].", 39);
        AssertOption(replies, "xx:32.", 6);
        Assert.All(replies.Where(reply => !string.IsNullOrEmpty(reply.Text)), reply => Assert.True(IsAscii(reply.Text), reply.Text));
        Assert.Contains("source=NpcHandler.Helpr", log.ToString());
    }

    [Fact]
    public async Task HandlerSendsTranslatedHourMenuWithSourceOptionIds()
    {
        using var log = new StringWriter();
        var replies = await SendDialogAndParseRepliesAsync(log, optionId: 2);

        Assert.Contains(replies, reply => reply.Text == "These events run by the hour.");
        AssertOption(replies, "Pole Domination [15:00-16:00].", 17);
        AssertOption(replies, "Capture the Flag [21:00-22:00].", 34);
        AssertOption(replies, "Squidward Octopus [12:30-13:30].", 36);
        Assert.All(replies.Where(reply => !string.IsNullOrEmpty(reply.Text)), reply => Assert.True(IsAscii(reply.Text), reply.Text));
        Assert.Contains("source=NpcHandler.Helpr", log.ToString());
    }

    [Fact]
    public async Task HandlerSendsTranslatedDayMenuWithSourceOptionIds()
    {
        using var log = new StringWriter();
        var replies = await SendDialogAndParseRepliesAsync(log, optionId: 3);

        Assert.Contains(replies, reply => reply.Text == "These events run on specific days.");
        AssertOption(replies, "Guild War, Friday [21:00-22:00].", 40);
        AssertOption(replies, "Super Guild War, Tuesday [21:00-22:00].", 41);
        Assert.All(replies.Where(reply => !string.IsNullOrEmpty(reply.Text)), reply => Assert.True(IsAscii(reply.Text), reply.Text));
        Assert.Contains("source=NpcHandler.Helpr", log.ToString());
    }

    [Theory]
    [InlineData(4, 372U, 362U)]
    [InlineData(17, 334U, 341U)]
    [InlineData(36, 461U, 352U)]
    [InlineData(41, 344U, 250U)]
    public async Task HandlerTeleportsToSourceGuideDestination(byte optionId, uint expectedX, uint expectedY)
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(mapId: 700, x: 45, y: 55, dynamicId: "2468");
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacyEventGuideNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            session,
            EventGuideNpcId,
            optionId,
            CancellationToken.None);

        Assert.True(handled);
        var teleport = Assert.Single(ParseMsgActions(sent), action => action.Type == MsgActionType.Teleport);
        Assert.Equal(1_002U, teleport.DwParam);
        Assert.Equal(expectedX, teleport.PositionX);
        Assert.Equal(expectedY, teleport.PositionY);

        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(1_002, updated.Character.MapId);
        Assert.Equal(expectedX, (uint)updated.Character.X);
        Assert.Equal(expectedY, (uint)updated.Character.Y);
        Assert.Equal("0", updated.Character.Attributes["DynamicID"]);
        Assert.Contains($"source=NpcHandler.Helpr map=1002 x={expectedX} y={expectedY}", log.ToString());
    }

    private static async Task<IReadOnlyList<LegacyNpcReplyPacket>> SendDialogAndParseRepliesAsync(
        StringWriter log,
        byte optionId)
    {
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession();
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacyEventGuideNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            session,
            EventGuideNpcId,
            optionId,
            CancellationToken.None);

        Assert.True(handled);
        return ParseNpcReplies(sent);
    }

    private static void AssertOption(IReadOnlyList<LegacyNpcReplyPacket> replies, string text, byte optionId)
    {
        Assert.Contains(replies, reply => reply.Text == text && reply.OptionId == optionId);
    }

    private static WorldSession CreateSession(
        long mapId = 1_002,
        long x = 430,
        long y = 378,
        string dynamicId = "0")
    {
        return new WorldSession(
            AccountId: 100,
            Username: "event-guide-test",
            Character: new CharacterSnapshot(
                Uid: 1_293_600,
                Name: "GuideHero",
                Body: 1003,
                Face: 0,
                Hair: 0,
                Class: 7051,
                FirstClass: 7051,
                SecondClass: 0,
                Reborn: 0,
                Level: 140,
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
                ConquerPoints: 0,
                BoundConquerPoints: 0,
                BankConquerPoints: 0,
                PkPoints: 0,
                Online: 0,
                SourceFile: "fixture",
                Attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["DynamicID"] = dynamicId,
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

    private static bool IsAscii(string value)
    {
        return value.All(static character => character <= 127);
    }
}
