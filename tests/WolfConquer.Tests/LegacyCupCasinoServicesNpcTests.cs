using System.Buffers;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyCupCasinoServicesNpcTests
{
    public static TheoryData<uint, string, string, ushort> PhoenixCupCases => new()
    {
        { 19_507, "Holy Phoenix Cup", "source=NpcHandler.HolyPhoenixCup", 318 },
        { 19_506, "Golden Phoenix Cup", "source=NpcHandler.GoldenPhoenixCup", 317 },
        { 19_505, "Silver Phoenix Cup", "source=NpcHandler.SilverPheoenixCup", 316 },
        { 19_504, "Bronze Phoenix Cup", "source=NpcHandler.BronzePhoenixCup", 315 },
    };

    public static TheoryData<uint, byte, string> CasinoExitCases => new()
    {
        { 6_299, 1, "source=NpcHandler.PokerCpsCasino" },
        { 6_298, 1, "source=NpcHandler.PokerCasinoHostess" },
        { 16_915, 1, "source=NpcHandler.CasinoHostess" },
    };

    public static TheoryData<uint, byte, int, int, int, string> GameManagerTeleportCases => new()
    {
        { 6_297, 1, 1_858, 157, 119, "source=NpcHandler.GameManager" },
        { 6_297, 2, 1_860, 55, 62, "source=NpcHandler.GameManager" },
    };

    public static TheoryData<uint, byte, string, string> SafeGapCases => new()
    {
        { 19_424, 30, "source=NpcHandler.CSElitePkManager", "sourceLines=23083-23103" },
        { 554_432, 121, "source=NpcHandler.ChiMaker", "sourceLines=23326-23453" },
        { 35_016, 3, "source=NpcHandler.WuxingOven", "sourceLines=27362-27404" },
        { 6_299, 2, "source=NpcHandler.PokerCpsCasino", "sourceLines=27424-27427" },
    };

    [Theory]
    [MemberData(nameof(PhoenixCupCases))]
    public async Task PhoenixCupDialogsAreAsciiAndUseLegacySource(uint npcId, string expectedTitle, string expectedSource, ushort avatarId)
    {
        var result = await InvokeCupCasinoServicesAsync(npcId, optionId: 0);

        Assert.True(result.Handled);
        var replies = ParseNpcReplies(result.Sent);
        Assert.Contains(replies, reply => reply.Text.Contains(expectedTitle, StringComparison.Ordinal));
        Assert.Contains(replies, reply => reply.Text.Contains("Duration:", StringComparison.Ordinal));
        Assert.Contains(replies, reply => reply.InteractType == LegacyNpcReplyInteractType.Option && reply.OptionId == byte.MaxValue);
        Assert.Contains(replies, reply => reply.InteractType == LegacyNpcReplyInteractType.Avatar && reply.InputMaxLength == avatarId);
        Assert.All(replies, AssertAsciiReply);
        Assert.Contains(expectedSource, result.Log);
    }

    [Fact]
    public async Task CSElitePkManagerRootAndRulesPreserveSourceOptions()
    {
        var root = await InvokeCupCasinoServicesAsync(19_424, optionId: 0);

        Assert.True(root.Handled);
        var replies = ParseNpcReplies(root.Sent);
        Assert.Equal([1, 2, 10, 30, 20, byte.MaxValue], replies.Where(reply => reply.InteractType == LegacyNpcReplyInteractType.Option).Select(reply => reply.OptionId).ToArray());
        Assert.Contains(replies, reply => reply.Text.Contains("Cross-server Elite PK Tournament", StringComparison.Ordinal));
        Assert.All(replies, AssertAsciiReply);
        Assert.Contains("source=NpcHandler.CSElitePkManager", root.Log);

        var rules = await InvokeCupCasinoServicesAsync(19_424, optionId: 2);

        Assert.True(rules.Handled);
        replies = ParseNpcReplies(rules.Sent);
        Assert.Equal([3, 4, 5, byte.MaxValue], replies.Where(reply => reply.InteractType == LegacyNpcReplyInteractType.Option).Select(reply => reply.OptionId).ToArray());
        Assert.Contains(replies, reply => reply.Text.Contains("three level divisions", StringComparison.Ordinal));
        Assert.All(replies, AssertAsciiReply);
    }

    [Fact]
    public async Task ChiMakerRootAndStageMenusTranslateAsciiAndKeepOptionIds()
    {
        var root = await InvokeCupCasinoServicesAsync(554_432, optionId: 0);

        Assert.True(root.Handled);
        var replies = ParseNpcReplies(root.Sent);
        Assert.Equal([5, 120, 6, 50, 100, 15, byte.MaxValue], replies.Where(reply => reply.InteractType == LegacyNpcReplyInteractType.Option).Select(reply => reply.OptionId).ToArray());
        Assert.Contains(replies, reply => reply.Text.Contains("perfect one Chi gate", StringComparison.Ordinal));
        Assert.All(replies, AssertAsciiReply);
        Assert.Contains("source=NpcHandler.ChiMaker", root.Log);

        var stage = await InvokeCupCasinoServicesAsync(554_432, optionId: 120);

        Assert.True(stage.Handled);
        replies = ParseNpcReplies(stage.Sent);
        Assert.Equal([121, 122, 123, 124, byte.MaxValue], replies.Where(reply => reply.InteractType == LegacyNpcReplyInteractType.Option).Select(reply => reply.OptionId).ToArray());
        Assert.Contains(replies, reply => reply.Text.Contains("choose a Chi gate", StringComparison.Ordinal));
        Assert.All(replies, AssertAsciiReply);
    }

    [Fact]
    public async Task WuxingOvenDialogsPreserveHelpAndConfirmOptionIds()
    {
        var root = await InvokeCupCasinoServicesAsync(35_016, optionId: 0);

        Assert.True(root.Handled);
        var replies = ParseNpcReplies(root.Sent);
        Assert.Equal([1, 14, 15, 19, byte.MaxValue], replies.Where(reply => reply.InteractType == LegacyNpcReplyInteractType.Option).Select(reply => reply.OptionId).ToArray());
        Assert.All(replies, AssertAsciiReply);
        Assert.Contains("source=NpcHandler.WuxingOven", root.Log);

        var confirm = await InvokeCupCasinoServicesAsync(35_016, optionId: 50);

        Assert.True(confirm.Handled);
        replies = ParseNpcReplies(confirm.Sent);
        Assert.Equal([51, 1, byte.MaxValue], replies.Where(reply => reply.InteractType == LegacyNpcReplyInteractType.Option).Select(reply => reply.OptionId).ToArray());
        Assert.Contains(replies, reply => reply.Text.Contains("Wing", StringComparison.Ordinal));
        Assert.All(replies, AssertAsciiReply);
    }

    [Theory]
    [MemberData(nameof(CasinoExitCases))]
    public async Task CasinoExitOptionsUseDeterministicTwinCityTeleport(uint npcId, byte optionId, string expectedSource)
    {
        var result = await InvokeCupCasinoServicesAsync(npcId, optionId, mapId: 1_858, x: 157, y: 119);

        Assert.True(result.Handled);
        Assert.True(result.Sessions.TryGet(result.Connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(1_002, updated.Character.MapId);
        Assert.Equal(410, updated.Character.X);
        Assert.Equal(354, updated.Character.Y);
        Assert.Equal(1_858, updated.Character.PreviousMapId);
        Assert.Contains(ParseMsgActions(result.Sent), action => action.Type == MsgActionType.Teleport && action.DwParam == 1_002);
        Assert.Contains(expectedSource, result.Log);
    }

    [Fact]
    public async Task GameManagerDialogUsesLegacyPokerRoomOptions()
    {
        var result = await InvokeCupCasinoServicesAsync(6_297, optionId: 0);

        Assert.True(result.Handled);
        var replies = ParseNpcReplies(result.Sent);
        Assert.Contains(replies, reply => reply.Text.Contains("poker room", StringComparison.Ordinal));
        Assert.Contains(replies, reply => reply.InteractType == LegacyNpcReplyInteractType.Option && reply.OptionId == 1);
        Assert.Contains(replies, reply => reply.InteractType == LegacyNpcReplyInteractType.Option && reply.OptionId == byte.MaxValue);
        Assert.Contains(replies, reply => reply.InteractType == LegacyNpcReplyInteractType.Avatar && reply.InputMaxLength == 175);
        Assert.All(replies, AssertAsciiReply);
        Assert.Contains("source=NpcHandler.GameManager", result.Log);
    }

    [Theory]
    [MemberData(nameof(GameManagerTeleportCases))]
    public async Task GameManagerTeleportsUseLegacyDestinations(
        uint npcId,
        byte optionId,
        int expectedMapId,
        int expectedX,
        int expectedY,
        string expectedSource)
    {
        var result = await InvokeCupCasinoServicesAsync(npcId, optionId);

        Assert.True(result.Handled);
        Assert.True(result.Sessions.TryGet(result.Connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(expectedMapId, updated.Character.MapId);
        Assert.Equal(expectedX, updated.Character.X);
        Assert.Equal(expectedY, updated.Character.Y);
        Assert.Contains(ParseMsgActions(result.Sent), action => action.Type == MsgActionType.Teleport && action.DwParam == (uint)expectedMapId);
        Assert.Contains(expectedSource, result.Log);
    }

    [Theory]
    [MemberData(nameof(SafeGapCases))]
    public async Task MutatingAndGlobalBranchesSendUnavailableDialogAndRequiredParityGap(
        uint npcId,
        byte optionId,
        string expectedSource,
        string expectedSourceLines)
    {
        var result = await InvokeCupCasinoServicesAsync(npcId, optionId);

        Assert.True(result.Handled);
        var replies = ParseNpcReplies(result.Sent);
        Assert.Contains(replies, reply => reply.Text.Contains("unavailable here", StringComparison.Ordinal));
        Assert.All(replies, AssertAsciiReply);

        Assert.Contains("PARITY-GAP LegacyNpc CupCasinoServices", result.Log);
        Assert.Contains(expectedSource, result.Log);
        Assert.Contains(expectedSourceLines, result.Log);
        Assert.Contains("npcId=" + npcId, result.Log);
        Assert.Contains("optionId=" + optionId, result.Log);
    }

    [Fact]
    public async Task UnknownNpcAndUnknownOptionReturnFalse()
    {
        var unknownNpc = await InvokeCupCasinoServicesAsync(123, optionId: 0);
        Assert.False(unknownNpc.Handled);
        Assert.Empty(unknownNpc.Sent);

        var unknownOption = await InvokeCupCasinoServicesAsync(19_507, optionId: 9);
        Assert.False(unknownOption.Handled);
        Assert.Empty(unknownOption.Sent);
    }

    private static async Task<CupCasinoServicesInvokeResult> InvokeCupCasinoServicesAsync(
        uint npcId,
        byte optionId,
        long mapId = 1_002,
        long x = 430,
        long y = 378)
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(mapId, x, y);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacyCupCasinoServicesNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            session,
            npcId,
            optionId,
            CancellationToken.None);

        return new CupCasinoServicesInvokeResult(handled, sent, log.ToString(), connection, sessions);
    }

    private static WorldSession CreateSession(long mapId, long x, long y)
    {
        return new WorldSession(
            AccountId: 100,
            Username: "cup-casino-services-test",
            Character: new CharacterSnapshot(
                Uid: 1_293_600,
                Name: "CupCasinoTester",
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
                ConquerPoints: 20_000_000,
                BoundConquerPoints: 0,
                BankConquerPoints: 0,
                PkPoints: 0,
                Online: 0,
                SourceFile: "fixture",
                Attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
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

    private static void AssertAsciiReply(LegacyNpcReplyPacket reply)
    {
        Assert.All(reply.Text, character => Assert.InRange(character, (char)0x20, (char)0x7E));
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }

    private sealed record CupCasinoServicesInvokeResult(
        bool Handled,
        IReadOnlyList<byte[]> Sent,
        string Log,
        ConquerTcpConnection Connection,
        WorldSessionStore Sessions);
}
