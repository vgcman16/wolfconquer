using System.Buffers;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyTopHoursNpcTests
{
    private const uint TopHoursxx17NpcId = 19_945;
    private const uint TopHoursxx32NpcId = 19_948;
    private const uint TopHoursOutxx42NpcId = 19_963;
    private const uint TopHoursOutxx55NpcId = 19_966;

    [Fact]
    public async Task HandlerSendsTranslatedTopHoursEntryDialogWithSourceOptions()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(mapId: 1_002);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacyTopHoursNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            session,
            TopHoursxx17NpcId,
            optionId: 0,
            new DateTime(2026, 6, 20, 8, 16, 0),
            CancellationToken.None);

        Assert.True(handled);
        var replies = ParseNpcReplies(sent);
        Assert.Contains(replies, reply => reply.Text == "Hello! TopHours registration is open from xx:17 through xx:20.");
        AssertOption(replies, "Sign me up.", 1);
        AssertOption(replies, "I will come later.", byte.MaxValue);
        Assert.Contains(replies, reply => reply.InteractType == LegacyNpcReplyInteractType.Avatar && reply.InputMaxLength == 63);
        Assert.All(replies.Where(reply => !string.IsNullOrEmpty(reply.Text)), reply => Assert.True(IsAscii(reply.Text), reply.Text));
        Assert.Contains("source=NpcHandler.TopHoursxx17", log.ToString());
    }

    [Fact]
    public async Task HandlerTeleportsTopHoursEntryWhenSourceMinuteGateIsOpen()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(mapId: 1_002, x: 363, y: 304);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacyTopHoursNpcAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                Session: session,
                ArenaStates: new ArenaSessionStateStore(),
                Sessions: sessions),
            session,
            TopHoursxx32NpcId,
            optionId: 1,
            new DateTime(2026, 6, 20, 8, 33, 0),
            CancellationToken.None);

        Assert.True(handled);
        var teleport = Assert.Single(ParseMsgActions(sent), action => action.Type == MsgActionType.Teleport);
        Assert.Equal(10_440U, teleport.DwParam);
        Assert.Equal(38U, teleport.PositionX);
        Assert.Equal(38U, teleport.PositionY);

        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(10_440, updated.Character.MapId);
        Assert.Equal(38, updated.Character.X);
        Assert.Equal(38, updated.Character.Y);
        Assert.Contains("source=NpcHandler.TopHoursxx32 map=10440 x=38 y=38", log.ToString());
    }

    [Fact]
    public async Task HandlerSendsExitConfirmationAndTeleportsBackToTwinCity()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(mapId: 10_404, x: 45, y: 45);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions);

        var confirmHandled = await WorldPacketHandlers.TryHandleLegacyTopHoursNpcAsync(
            context,
            session,
            TopHoursOutxx55NpcId,
            optionId: 2,
            new DateTime(2026, 6, 20, 8, 58, 0),
            CancellationToken.None);

        Assert.True(confirmHandled);
        var replies = ParseNpcReplies(sent);
        Assert.Contains(replies, reply => reply.Text == "Do you want to teleport back to Twin City? Are you sure?");
        AssertOption(replies, "Yes", 3);

        sent.Clear();
        var exitHandled = await WorldPacketHandlers.TryHandleLegacyTopHoursNpcAsync(
            context,
            session,
            TopHoursOutxx55NpcId,
            optionId: 3,
            new DateTime(2026, 6, 20, 8, 58, 0),
            CancellationToken.None);

        Assert.True(exitHandled);
        var teleport = Assert.Single(ParseMsgActions(sent), action => action.Type == MsgActionType.Teleport);
        Assert.Equal(1_002U, teleport.DwParam);
        Assert.Equal(410U, teleport.PositionX);
        Assert.Equal(354U, teleport.PositionY);

        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(1_002, updated.Character.MapId);
        Assert.Equal(410, updated.Character.X);
        Assert.Equal(354, updated.Character.Y);
        Assert.Contains("source=NpcHandler.TopHoursOutxx55 map=1002 x=410 y=354", log.ToString());
    }

    [Fact]
    public async Task HandlerSafeGapsWinnerRewardClaimDuringLegacyRewardWindow()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(mapId: 10_441, x: 45, y: 45, conquerPoints: 123);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacyTopHoursNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            session,
            TopHoursOutxx42NpcId,
            optionId: 1,
            new DateTime(2026, 6, 20, 8, 46, 0),
            CancellationToken.None);

        Assert.True(handled);
        var replies = ParseNpcReplies(sent);
        Assert.Contains(replies, reply => reply.Text == "TopHours winner rewards are not available yet.");
        AssertOption(replies, "I understand.", byte.MaxValue);
        Assert.DoesNotContain(ParseMsgUpdates(sent), update => update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.Conquer));

        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(123, updated.Character.ConquerPoints);
        var output = log.ToString();
        Assert.Contains("PARITY-GAP LegacyNpc TopHours", output);
        Assert.Contains("source=NpcHandler.TopHoursOutxx42 lineRange=NpcHandler.cs:1887-1965 option=1", output);
    }

    private static void AssertOption(IReadOnlyList<LegacyNpcReplyPacket> replies, string text, byte optionId)
    {
        Assert.Contains(replies, reply => reply.Text == text && reply.OptionId == optionId);
    }

    private static WorldSession CreateSession(
        long mapId,
        long x = 50,
        long y = 50,
        uint conquerPoints = 0,
        IReadOnlyDictionary<string, string>? attributes = null)
    {
        return new WorldSession(
            AccountId: 100,
            Username: "top-hours-test",
            Character: new CharacterSnapshot(
                Uid: 1_293_600,
                Name: "TopHourHero",
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
                Attributes: attributes ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
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
}
