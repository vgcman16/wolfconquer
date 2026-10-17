using System.Buffers;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyDisCityBattlefieldServicesNpcTests
{
    private const uint TcBoxerNpcId = 101_611;
    private const uint TwinDisCityMainNpcId = 3_215;
    private const uint DbShowerNpcId = 666;
    private const uint BattleFieldMap1NpcId = 4_026_308;
    private const uint TreasureLeaveNpcId = 4_026_320;

    [Fact]
    public async Task HandlerSendsTranslatedDisCityRootDialogWithSourceOptions()
    {
        var result = await InvokeAsync(TwinDisCityMainNpcId, optionId: 0);

        Assert.True(result.Handled);
        var replies = ParseNpcReplies(result.Sent);
        Assert.Contains(replies, reply => reply.Text.Contains("Dis City opens from 7:30-7:35", StringComparison.Ordinal));
        AssertOption(replies, "Could you tell me more?", 1);
        AssertOption(replies, "I am here to drive away the evils!", 10);
        AssertOption(replies, "I have the Dark Horn.", 20);
        AssertOption(replies, "I'd better leave.", byte.MaxValue);
        Assert.All(replies.Where(reply => !string.IsNullOrEmpty(reply.Text)), reply => Assert.True(IsAscii(reply.Text), reply.Text));
        Assert.Contains("source=NpcHandler.TwinDisCityMain", result.Log);
    }

    [Fact]
    public async Task BoxerFeeUpdatesMoneyAndTeleportsToTrainingGround()
    {
        var result = await InvokeAsync(TcBoxerNpcId, optionId: 1, money: 1_500, mapId: 1_002, x: 430, y: 378);

        Assert.True(result.Handled);
        Assert.Equal(500, result.UpdatedSession.Character.Money);
        Assert.Equal(1_039, result.UpdatedSession.Character.MapId);
        Assert.Equal(226, result.UpdatedSession.Character.X);
        Assert.Equal(218, result.UpdatedSession.Character.Y);

        var update = Assert.Single(ParseMsgUpdates(result.Sent), packet => packet.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.Money));
        Assert.Contains(update.Updates, entry => entry.Type == LegacyMsgUpdateDataType.Money && entry.Value == 500);
        var teleport = Assert.Single(ParseMsgActions(result.Sent), action => action.Type == MsgActionType.Teleport);
        Assert.Equal(1_039U, teleport.DwParam);
        Assert.Equal(226U, teleport.PositionX);
        Assert.Equal(218U, teleport.PositionY);

        var replies = ParseNpcReplies(result.Sent);
        Assert.Contains(replies, reply => reply.Text.Contains("attack only dummies", StringComparison.Ordinal));
        Assert.All(replies.Where(reply => !string.IsNullOrEmpty(reply.Text)), reply => Assert.True(IsAscii(reply.Text), reply.Text));
        Assert.Contains("source=NpcHandler.TcBoxer sourceLines=NpcHandler.cs:28214-28262", result.Log);
    }

    [Fact]
    public async Task TreasureLeaveConfirmAndDeterministicTeleportUseLegacyOptionIds()
    {
        var confirm = await InvokeAsync(TreasureLeaveNpcId, optionId: 2, mapId: 1_768, x: 45, y: 45);

        Assert.True(confirm.Handled);
        var replies = ParseNpcReplies(confirm.Sent);
        Assert.Contains(replies, reply => reply.Text == "Are you sure you want to leave for Twin City?");
        AssertOption(replies, "Yes.", 3);
        AssertOption(replies, "Let me think.", byte.MaxValue);

        var exit = await InvokeAsync(TreasureLeaveNpcId, optionId: 3, mapId: 1_768, x: 45, y: 45);

        Assert.True(exit.Handled);
        Assert.Equal(1_002, exit.UpdatedSession.Character.MapId);
        Assert.Equal(410, exit.UpdatedSession.Character.X);
        Assert.Equal(354, exit.UpdatedSession.Character.Y);
        var teleport = Assert.Single(ParseMsgActions(exit.Sent), action => action.Type == MsgActionType.Teleport);
        Assert.Equal(1_002U, teleport.DwParam);
        Assert.Equal(410U, teleport.PositionX);
        Assert.Equal(354U, teleport.PositionY);
        Assert.Contains("source=NpcHandler.TreasureLeave map=1002 x=410 y=354", exit.Log);
    }

    [Fact]
    public async Task DynamicEventBranchesSendUnavailableDialogAndParityGap()
    {
        var disCityEntry = await InvokeAsync(TwinDisCityMainNpcId, optionId: 10, mapId: 1_002);

        Assert.True(disCityEntry.Handled);
        Assert.Contains(ParseNpcReplies(disCityEntry.Sent), reply => reply.Text == "Dis City entry is not available from this runtime yet.");
        Assert.DoesNotContain(ParseMsgActions(disCityEntry.Sent), action => action.Type == MsgActionType.Teleport);
        Assert.Equal(1_002, disCityEntry.UpdatedSession.Character.MapId);
        Assert.Contains("PARITY-GAP LegacyNpc DisCityBattlefieldServices", disCityEntry.Log);
        Assert.Contains("source=NpcHandler.TwinDisCityMain sourceLines=NpcHandler.cs:28671-28813 npcId=3215 optionId=10", disCityEntry.Log);

        var payout = await InvokeAsync(
            BattleFieldMap1NpcId,
            optionId: 2,
            mapId: 1_070,
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["BattleFieldPoints"] = "2500",
            });

        Assert.True(payout.Handled);
        Assert.Contains(ParseNpcReplies(payout.Sent), reply => reply.Text == "BattleField point payout is not available from this runtime yet.");
        Assert.DoesNotContain(ParseMsgActions(payout.Sent), action => action.Type == MsgActionType.Teleport);
        Assert.Equal(0U, payout.UpdatedSession.Character.ConquerPoints);
        Assert.Contains("source=NpcHandler.BattleFieldMap1 sourceLines=NpcHandler.cs:29565-29613 npcId=4026308 optionId=2", payout.Log);

        var dbShower = await InvokeAsync(DbShowerNpcId, optionId: 1);

        Assert.True(dbShower.Handled);
        Assert.Contains(ParseNpcReplies(dbShower.Sent), reply => reply.Text == "DB Shower entry and rewards are not available from this runtime yet.");
        Assert.Contains("source=NpcHandler.DBShower sourceLines=NpcHandler.cs:29288-29317 npcId=666 optionId=1", dbShower.Log);
    }

    [Fact]
    public async Task BattleFieldMapDialogReadsPointAttributeAndStaysAscii()
    {
        var result = await InvokeAsync(
            BattleFieldMap1NpcId,
            optionId: 0,
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["BattleFieldPoints"] = "2500",
            });

        Assert.True(result.Handled);
        var replies = ParseNpcReplies(result.Sent);
        Assert.Contains(replies, reply => reply.Text == "You have 2500 BattleFieldPoints. You need 2.000 Points for join in the map 2.");
        AssertOption(replies, "Send me there.", 1);
        AssertOption(replies, "Send me in Twin City.", 2);
        Assert.All(replies.Where(reply => !string.IsNullOrEmpty(reply.Text)), reply => Assert.True(IsAscii(reply.Text), reply.Text));
    }

    [Fact]
    public async Task UnknownNpcAndUnknownOptionReturnFalse()
    {
        var unknownNpc = await InvokeAsync(999_999_999, optionId: 0);
        var unknownOption = await InvokeAsync(TwinDisCityMainNpcId, optionId: 99);

        Assert.False(unknownNpc.Handled);
        Assert.False(unknownOption.Handled);
    }

    private static async Task<LegacyDisCityBattlefieldServicesInvokeResult> InvokeAsync(
        uint npcId,
        byte optionId,
        long money = 0,
        long mapId = 1_002,
        long x = 430,
        long y = 378,
        IReadOnlyDictionary<string, string>? attributes = null)
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(money, mapId, x, y, attributes);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacyDisCityBattlefieldServicesNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            session,
            npcId,
            optionId,
            CancellationToken.None);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        return new LegacyDisCityBattlefieldServicesInvokeResult(handled, sent, log.ToString(), updatedSession);
    }

    private static WorldSession CreateSession(
        long money,
        long mapId,
        long x,
        long y,
        IReadOnlyDictionary<string, string>? attributes)
    {
        return new WorldSession(
            AccountId: 100,
            Username: "dis-city-battlefield-test",
            Character: new CharacterSnapshot(
                Uid: 1_293_600,
                Name: "DisCityHero",
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
                Money: money,
                ConquerPoints: 0,
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

    private static void AssertOption(IReadOnlyList<LegacyNpcReplyPacket> replies, string text, byte optionId)
    {
        Assert.Contains(replies, reply => reply.Text == text && reply.OptionId == optionId);
    }

    private static bool IsAscii(string value)
    {
        return value.All(static character => character <= 127);
    }

    private sealed record LegacyDisCityBattlefieldServicesInvokeResult(
        bool Handled,
        IReadOnlyList<byte[]> Sent,
        string Log,
        WorldSession UpdatedSession);
}
