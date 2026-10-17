using System.Buffers;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyMiscWarEventNpcTests
{
    private const uint FindTheSpyNpcId = 126_789;
    private const uint BotJailNpcId = 9_176;
    private const uint CrazyWarNpcId = 101_010;
    private const uint WinCrazyWarNpcId = 101_011;
    private const uint LettersNpcId = 101_012;

    [Fact]
    public async Task FindTheSpyIntroSendsSourceBackedOptionWhenOpen()
    {
        var result = await InvokeMiscWarEventHandlerAsync(
            FindTheSpyNpcId,
            optionId: 0,
            now: new DateTime(2026, 6, 20, 12, 15, 0));

        Assert.True(result.Handled);
        var replies = ParseNpcReplies(result.Sent);
        Assert.Contains(replies, reply => reply.Text == "Hello, MiscHero. Do you want to enter Find the Spy?");
        Assert.Contains(replies, reply => reply.Text == "I can help during 12:00-17:30 and 21:00-23:59.");
        Assert.Contains(replies, reply => reply.Text == "Yes." && reply.OptionId == 1);
        Assert.All(replies.Where(reply => !string.IsNullOrEmpty(reply.Text)), reply => Assert.True(IsAscii(reply.Text), reply.Text));
        Assert.Contains("source=NpcHandler.FindTheSpy", result.Log);
    }

    [Fact]
    public async Task FindTheSpyEnterTeleportsDuringLegacyWindow()
    {
        var result = await InvokeMiscWarEventHandlerAsync(
            FindTheSpyNpcId,
            optionId: 1,
            now: new DateTime(2026, 6, 20, 17, 30, 0));

        Assert.True(result.Handled);
        var teleport = Assert.Single(ParseMsgActions(result.Sent), action => action.Type == MsgActionType.Teleport);
        Assert.Equal(3_845U, teleport.DwParam);
        Assert.Equal(172U, teleport.PositionX);
        Assert.Equal(147U, teleport.PositionY);
        Assert.Equal(3_845, result.UpdatedSession.Character.MapId);
        Assert.Equal(172, result.UpdatedSession.Character.X);
        Assert.Equal(147, result.UpdatedSession.Character.Y);
    }

    [Fact]
    public async Task BotJailRootAndCountUseTranslatedTextAndSourceOptionIds()
    {
        var root = await InvokeMiscWarEventHandlerAsync(BotJailNpcId, optionId: 0);

        Assert.True(root.Handled);
        var rootReplies = ParseNpcReplies(root.Sent);
        Assert.Contains(rootReplies, reply => reply.Text == "Welcome to Bot Jail #10.");
        Assert.Contains(rootReplies, reply => reply.Text == "I am sorry. Please send me to Twin City." && reply.OptionId == 3);
        Assert.Contains(rootReplies, reply => reply.Text == "How many times have I entered?" && reply.OptionId == 4);
        Assert.Contains(rootReplies, reply => reply.Text == "Sorry, I am poor." && reply.OptionId == byte.MaxValue);

        var count = await InvokeMiscWarEventHandlerAsync(
            BotJailNpcId,
            optionId: 4,
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["botjail"] = "2",
            });

        Assert.True(count.Handled);
        var countReplies = ParseNpcReplies(count.Sent);
        Assert.Contains(countReplies, reply => reply.Text == "You have entered Bot Jail 2 time(s). Good luck.");
        Assert.All(rootReplies.Concat(countReplies).Where(reply => !string.IsNullOrEmpty(reply.Text)), reply => Assert.True(IsAscii(reply.Text), reply.Text));
    }

    [Fact]
    public async Task CrazyWarRootAndEnterUseSourceOptionAndTeleport()
    {
        var root = await InvokeMiscWarEventHandlerAsync(CrazyWarNpcId, optionId: 0);

        Assert.True(root.Handled);
        var rootReplies = ParseNpcReplies(root.Sent);
        Assert.Contains(rootReplies, reply => reply.Text == "Hey, MiscHero, do you want to join Crazy War? It starts every hour at xx:01.");
        Assert.Contains(rootReplies, reply => reply.Text == "Yes, please." && reply.OptionId == 26);

        var enter = await InvokeMiscWarEventHandlerAsync(
            CrazyWarNpcId,
            optionId: 26,
            now: new DateTime(2026, 6, 20, 10, 3, 0));

        Assert.True(enter.Handled);
        var teleport = Assert.Single(ParseMsgActions(enter.Sent), action => action.Type == MsgActionType.Teleport);
        Assert.Equal(10_599U, teleport.DwParam);
        Assert.Equal(160U, teleport.PositionX);
        Assert.Equal(170U, teleport.PositionY);
        Assert.Equal(10_599, enter.UpdatedSession.Character.MapId);
    }

    [Fact]
    public async Task WinCrazyWarTeleportsSingleLiveParticipantHomeWithoutRewardGrant()
    {
        var result = await InvokeMiscWarEventHandlerAsync(
            WinCrazyWarNpcId,
            optionId: 26,
            now: new DateTime(2026, 6, 20, 10, 8, 0),
            mapId: 10_599,
            x: 160,
            y: 170);

        Assert.True(result.Handled);
        var teleport = Assert.Single(ParseMsgActions(result.Sent), action => action.Type == MsgActionType.Teleport);
        Assert.Equal(1_002U, teleport.DwParam);
        Assert.Equal(410U, teleport.PositionX);
        Assert.Equal(355U, teleport.PositionY);
        Assert.Equal(0, result.UpdatedSession.Character.ConquerPoints);
        Assert.DoesNotContain(ParseMsgUpdates(result.Sent), update => update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.Conquer));
        Assert.Contains("Single live Crazy War participant was locally proven", result.Log);
        Assert.Contains("PARITY-GAP LegacyNpc MiscWarEvent", result.Log);
    }

    [Fact]
    public async Task LettersRootAndMissingExchangeSendUnavailableEnglishDialogs()
    {
        var root = await InvokeMiscWarEventHandlerAsync(LettersNpcId, optionId: 0);

        Assert.True(root.Handled);
        var rootReplies = ParseNpcReplies(root.Sent);
        Assert.Contains(rootReplies, reply => reply.Text == "This NPC feature is unavailable for now. Please wait for a GM announcement.");
        Assert.Contains(rootReplies, reply => reply.Text == "Sorry to bother you." && reply.OptionId == byte.MaxValue);

        var exchange = await InvokeMiscWarEventHandlerAsync(LettersNpcId, optionId: 1);

        Assert.True(exchange.Handled);
        var exchangeReplies = ParseNpcReplies(exchange.Sent);
        Assert.Contains(exchangeReplies, reply => reply.Text == "You do not have all letters for C,O,N,Q,U,E,R,(14), or this exchange is unavailable here.");
        Assert.Equal(0, exchange.UpdatedSession.Character.ConquerPoints);
        Assert.DoesNotContain(ParseMsgUpdates(exchange.Sent), update => update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.Conquer));
        Assert.All(rootReplies.Concat(exchangeReplies).Where(reply => !string.IsNullOrEmpty(reply.Text)), reply => Assert.True(IsAscii(reply.Text), reply.Text));
    }

    private static async Task<MiscWarEventInvokeResult> InvokeMiscWarEventHandlerAsync(
        uint npcId,
        byte optionId,
        DateTime? now = null,
        long mapId = 1_002,
        long x = 430,
        long y = 378,
        long conquerPoints = 0,
        Dictionary<string, string>? attributes = null)
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(mapId, x, y, conquerPoints, attributes);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacyMiscWarEventNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            session,
            npcId,
            optionId,
            now ?? new DateTime(2026, 6, 20, 10, 0, 0),
            CancellationToken.None);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        return new MiscWarEventInvokeResult(handled, sent, log.ToString(), updatedSession);
    }

    private static WorldSession CreateSession(
        long mapId,
        long x,
        long y,
        long conquerPoints,
        Dictionary<string, string>? attributes)
    {
        attributes ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        attributes.TryAdd("DynamicID", "0");

        return new WorldSession(
            AccountId: 100,
            Username: "misc-war-event-test",
            Character: new CharacterSnapshot(
                Uid: 1_293_600,
                Name: "MiscHero",
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
                ConquerPoints: conquerPoints,
                BoundConquerPoints: 0,
                BankConquerPoints: 0,
                PkPoints: 0,
                Online: 0,
                SourceFile: "fixture",
                Attributes: attributes,
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

    private static IReadOnlyList<LegacyMsgUpdatePacket> ParseMsgUpdates(IEnumerable<byte[]> sent)
    {
        var packets = new List<LegacyMsgUpdatePacket>();
        foreach (var bytes in sent)
        {
            if (LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var packet))
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

    private sealed record MiscWarEventInvokeResult(
        bool Handled,
        IReadOnlyList<byte[]> Sent,
        string Log,
        WorldSession UpdatedSession);
}
