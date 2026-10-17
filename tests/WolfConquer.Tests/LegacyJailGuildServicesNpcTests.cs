using System.Buffers;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyJailGuildServicesNpcTests
{
    [Theory]
    [InlineData(9_464U, "NpcHandler.NemesysConductort", "100 silver", 1)]
    [InlineData(1_002U, "NpcHandler.WardenZhang", "bounty hunters", 1)]
    [InlineData(42U, "NpcHandler.JailWarden", "murder charges", 2)]
    [InlineData(43U, "NpcHandler.JailJoin", "What can I do", 1)]
    [InlineData(380U, "NpcHandler.GuildConductor", "guild war", 1)]
    [InlineData(2_006U, "NpcHandler.MCaptain", "brave adventurers", 103)]
    [InlineData(4_450U, "NpcHandler.Confiscator", "detained equipment", 1)]
    [InlineData(5_004U, "NpcHandler.MillionaireLee", "treasure", 36)]
    [InlineData(3_825U, "NpcHandler.UnknowMan", "DragonBall", 1)]
    [InlineData(3_952U, "NpcHandler.LotteryCollectorWong", "Disguise Amulets", 1)]
    [InlineData(924U, "NpcHandler.LotteryLadyLuck", "LuckyBox", 1)]
    [InlineData(923U, "NpcHandler.MarketLadyLuck", "Lottery Center", 1)]
    [InlineData(9_072U, "NpcHandler.RebirthMaster", "secret of rebirth", 1)]
    [InlineData(5_600U, "NpcHandler.MountTrainer", "good mounts", 10)]
    public async Task RootDialogsAreAsciiAndPreserveSourceOptions(
        uint npcId,
        string sourceMethod,
        string expectedTextFragment,
        byte expectedOptionId)
    {
        var result = await InvokeJailGuildServicesHandlerAsync(npcId, optionId: 0);

        Assert.True(result.Handled);
        var replies = ParseNpcReplies(result.Sent);
        Assert.Contains(replies, reply => reply.Text.Contains(expectedTextFragment, StringComparison.Ordinal));
        Assert.Contains(replies, reply => reply.OptionId == expectedOptionId);
        Assert.All(replies.Where(reply => !string.IsNullOrEmpty(reply.Text)), reply => Assert.True(IsAscii(reply.Text), reply.Text));
        Assert.Contains("source=" + sourceMethod, result.Log);
    }

    [Theory]
    [InlineData(9_464U, 1, 250, 0, 0, 0, 1_000U, 502U, 646U, 150UL, "NpcHandler.NemesysConductort")]
    [InlineData(43U, 2, 1_001, 0, 0, 0, 6_000U, 29U, 72U, 1UL, "NpcHandler.JailJoin")]
    [InlineData(2_006U, 211, 0, 0, 0, 0, 1_011U, 229U, 258U, 0UL, "NpcHandler.MCaptain")]
    [InlineData(924U, 1, 0, 0, 0, 0, 1_002U, 410U, 354U, 0UL, "NpcHandler.LotteryLadyLuck")]
    [InlineData(923U, 1, 0, 0, 70, 0, 700U, 39U, 45U, 0UL, "NpcHandler.MarketLadyLuck")]
    [InlineData(380U, 6, 0, 0, 0, 0, 3_868U, 232U, 385U, 0UL, "NpcHandler.GuildConductor")]
    public async Task DeterministicTeleportsUseSourceCoordinatesAndChargeWhenLegacyDoes(
        uint npcId,
        byte optionId,
        long money,
        long conquerPoints,
        long level,
        long pkPoints,
        uint expectedMapId,
        uint expectedX,
        uint expectedY,
        ulong expectedMoney,
        string sourceMethod)
    {
        var result = await InvokeJailGuildServicesHandlerAsync(
            npcId,
            optionId,
            money: money,
            conquerPoints: conquerPoints,
            level: level == 0 ? 140 : level,
            pkPoints: pkPoints);

        Assert.True(result.Handled);
        var teleports = ParseMsgActions(result.Sent)
            .Where(action => action.Type == MsgActionType.Teleport)
            .ToArray();
        Assert.NotEmpty(teleports);
        var teleport = teleports[0];
        Assert.Equal(expectedMapId, teleport.DwParam);
        Assert.Equal(expectedX, teleport.PositionX);
        Assert.Equal(expectedY, teleport.PositionY);
        Assert.Equal(expectedMapId, (uint)result.UpdatedSession.Character.MapId);
        if (expectedMapId == 6_000)
        {
            Assert.Equal(2, teleports.Length);
            Assert.Equal(6_000U, teleports[1].DwParam);
            Assert.Equal(32U, teleports[1].PositionX);
            Assert.Equal(73U, teleports[1].PositionY);
            Assert.Equal(32, result.UpdatedSession.Character.X);
            Assert.Equal(73, result.UpdatedSession.Character.Y);
        }
        else
        {
            Assert.Single(teleports);
            Assert.Equal(expectedX, (uint)result.UpdatedSession.Character.X);
            Assert.Equal(expectedY, (uint)result.UpdatedSession.Character.Y);
        }
        Assert.Equal((long)expectedMoney, result.UpdatedSession.Character.Money);
        Assert.Contains("source=" + sourceMethod, result.Log);

        if ((ulong)money != expectedMoney)
        {
            Assert.Contains(ParseMsgUpdates(result.Sent), update => update.Updates.Any(entry =>
                entry.Type == LegacyMsgUpdateDataType.Money &&
                entry.Value == expectedMoney));
        }
    }

    [Fact]
    public async Task GuildConductorEnterUsesFixedGuildWarTeleportWhenGlobalProcessIsResolved()
    {
        var result = await InvokeJailGuildServicesHandlerAsync(
            380,
            optionId: 1,
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["LegacyNewGuildWarProcess"] = "Dead",
            });

        Assert.True(result.Handled);
        var teleport = Assert.Single(ParseMsgActions(result.Sent), action => action.Type == MsgActionType.Teleport);
        Assert.Equal(1_038U, teleport.DwParam);
        Assert.Equal(349U, teleport.PositionX);
        Assert.Equal(340U, teleport.PositionY);
        Assert.Contains("source=NpcHandler.GuildConductor", result.Log);
    }

    [Theory]
    [InlineData(1_002U, 1, "NpcHandler.WardenZhang", "31477-31538")]
    [InlineData(42U, 4, "NpcHandler.JailWarden", "31540-31645")]
    [InlineData(380U, 3, "NpcHandler.GuildConductor", "38191-38361")]
    [InlineData(4_450U, 2, "NpcHandler.Confiscator", "39577-39640")]
    [InlineData(5_004U, 37, "NpcHandler.MillionaireLee", "39725-40127")]
    public async Task SafeGappedBranchesLogSourceLinesAndDoNotMutateScalarState(
        uint npcId,
        byte optionId,
        string sourceMethod,
        string sourceLines)
    {
        var result = await InvokeJailGuildServicesHandlerAsync(
            npcId,
            optionId,
            money: 1_000_000,
            conquerPoints: 10_000,
            pkPoints: 100);

        Assert.True(result.Handled);
        Assert.Equal(1_000_000, result.UpdatedSession.Character.Money);
        Assert.Equal(10_000, result.UpdatedSession.Character.ConquerPoints);
        Assert.Equal(100, result.UpdatedSession.Character.PkPoints);
        Assert.DoesNotContain(ParseMsgUpdates(result.Sent), update => update.Updates.Any(entry =>
            entry.Type is LegacyMsgUpdateDataType.Money or LegacyMsgUpdateDataType.Conquer));
        Assert.Contains(ParseNpcReplies(result.Sent), reply =>
            reply.Text == "That service is not available yet. No items, CPs, silver, or character state were changed.");
        AssertSafeGapLog(result.Log, sourceMethod, sourceLines, npcId, optionId);
    }

    [Fact]
    public async Task UnknowManExperienceExchangeSafeGapDoesNotConsumeDragonBall()
    {
        var dragonBall = new PlayerItemSnapshotItem { Uid = 108_800, ItemId = 1_088_000, StackSize = 1 };
        var result = await InvokeJailGuildServicesHandlerAsync(
            3_825,
            optionId: 2,
            level: 139,
            inventoryItems: [dragonBall]);

        Assert.True(result.Handled);
        Assert.Single(result.UpdatedSession.PlayerItems.InventoryItems);
        Assert.Equal(1_088_000U, result.UpdatedSession.PlayerItems.InventoryItems[0].ItemId);
        AssertSafeGapLog(result.Log, "NpcHandler.UnknowMan", "40129-40210", 3_825, 2);
    }

    [Theory]
    [InlineData(999_999U, 0)]
    [InlineData(9_464U, 99)]
    public async Task UnknownNpcOrOptionReturnsFalse(uint npcId, byte optionId)
    {
        var result = await InvokeJailGuildServicesHandlerAsync(npcId, optionId);

        Assert.False(result.Handled);
        Assert.Empty(result.Sent);
        Assert.Equal("", result.Log);
    }

    private static async Task<JailGuildServicesInvokeResult> InvokeJailGuildServicesHandlerAsync(
        uint npcId,
        byte optionId,
        long mapId = 1_002,
        long x = 430,
        long y = 378,
        long money = 0,
        long conquerPoints = 0,
        long pkPoints = 0,
        long level = 140,
        long reborn = 0,
        long classCode = 7051,
        IReadOnlyDictionary<string, string>? attributes = null,
        IReadOnlyList<PlayerItemSnapshotItem>? inventoryItems = null,
        PlayerSpellSnapshot? playerSpells = null)
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            mapId,
            x,
            y,
            money,
            conquerPoints,
            pkPoints,
            level,
            reborn,
            classCode,
            attributes,
            inventoryItems,
            playerSpells);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacyJailGuildServicesNpcAsync(
                new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
                session,
                npcId,
                optionId,
                CancellationToken.None)
            .ConfigureAwait(false);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        return new JailGuildServicesInvokeResult(handled, sent, log.ToString(), updatedSession);
    }

    private static WorldSession CreateSession(
        long mapId,
        long x,
        long y,
        long money,
        long conquerPoints,
        long pkPoints,
        long level,
        long reborn,
        long classCode,
        IReadOnlyDictionary<string, string>? attributes,
        IReadOnlyList<PlayerItemSnapshotItem>? inventoryItems,
        PlayerSpellSnapshot? playerSpells)
    {
        return new WorldSession(
            AccountId: 1_293_800,
            Username: "jail-guild-services-test",
            Character: new CharacterSnapshot(
                Uid: 1_293_800,
                Name: "JailGuildHero",
                Body: 1003,
                Face: 0,
                Hair: 0,
                Class: classCode,
                FirstClass: classCode,
                SecondClass: 0,
                Reborn: reborn,
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
                Money: money,
                ConquerPoints: conquerPoints,
                BoundConquerPoints: 0,
                BankConquerPoints: 0,
                PkPoints: pkPoints,
                Online: 0,
                SourceFile: "fixture",
                Attributes: attributes ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                PaidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)),
            PlayerItems: inventoryItems is null
                ? PlayerItemSnapshot.Empty
                : new PlayerItemSnapshot(inventoryItems, [], [], [], []),
            PlayerSpells: playerSpells ?? PlayerSpellSnapshot.Empty,
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

    private static void AssertSafeGapLog(string log, string sourceMethod, string sourceLines, uint npcId, byte optionId)
    {
        Assert.Contains("PARITY-GAP LegacyNpc JailGuildServices", log);
        Assert.Contains("source=" + sourceMethod, log);
        Assert.Contains("sourceLines=" + sourceLines, log);
        Assert.Contains("npcId=" + npcId, log);
        Assert.Contains("optionId=" + optionId, log);
    }

    private static bool IsAscii(string value)
    {
        return value.All(static character => character <= 127);
    }

    private sealed record JailGuildServicesInvokeResult(
        bool Handled,
        IReadOnlyList<byte[]> Sent,
        string Log,
        WorldSession UpdatedSession);
}
