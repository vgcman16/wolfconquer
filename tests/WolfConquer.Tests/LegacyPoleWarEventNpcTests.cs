using System.Buffers;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyPoleWarEventNpcTests
{
    private const uint PoleDominationNpcId = 770_001;
    private const uint TopPoleNpcId = 43_431;
    private const uint QoutTopPoleNpcId = 43_432;
    private const uint GuildWarScoreNpcId = 6_525;
    private const uint Guild6PoleWarNpcId = 73_135;
    private const long GuildWarScoreRewardConquerPoints = 300_000;
    private const uint GuildWarScoreRewardItemId = 4_300_004;
    private const long Guild6PoleWarRewardConquerPoints = 150_000;
    private const long Guild6PoleWarRewardMoney = 150_000;
    private const uint Guild6PoleWarCelestialRainbowTitleId = 8_049;
    private const uint Guild6PoleWarRadiantWingsTitleId = 6_002;
    private const uint Guild6PoleWarScholarTitleId = 2_027;
    private const uint LegacySlideCrossServerChatType = 2_401;

    [Fact]
    public async Task HandlerSendsPoleDominationIntroWithSourceOptionIds()
    {
        var result = await InvokePoleWarEventHandlerAsync(PoleDominationNpcId, optionId: 0);

        Assert.True(result.Handled);
        var replies = ParseNpcReplies(result.Sent);
        Assert.Contains(replies, reply => reply.Text == "This is the Pole Domination reward center.");
        Assert.Contains(replies, reply => reply.Text.Contains("war starts at 15:00", StringComparison.Ordinal));
        Assert.Contains(replies, reply => reply.Text == "Join the first pole battle." && reply.OptionId == 5);
        Assert.Contains(replies, reply => reply.Text == "Claim the first pole reward." && reply.OptionId == 1);
        Assert.Contains(replies, reply => reply.Text == "Claim the fourth pole reward." && reply.OptionId == 4);
        Assert.All(replies.Where(reply => !string.IsNullOrEmpty(reply.Text)), reply => Assert.True(IsAscii(reply.Text), reply.Text));
        Assert.Contains("source=NpcHandler.PoleDomination", result.Log);
    }

    [Fact]
    public async Task HandlerTeleportsPoleDominationEnterDuringLegacyWindow()
    {
        var result = await InvokePoleWarEventHandlerAsync(
            PoleDominationNpcId,
            optionId: 5,
            now: new DateTime(2026, 6, 20, 15, 10, 0));

        Assert.True(result.Handled);
        AssertLegacyNpcTeleportTransition(result.Sent, result.Session, expectedMapId: 11_083, expectedX: 231, expectedY: 242);
        Assert.Equal(11_083, result.UpdatedSession.Character.MapId);
        Assert.Equal(231, result.UpdatedSession.Character.X);
        Assert.Equal(242, result.UpdatedSession.Character.Y);
        Assert.Contains("source=NpcHandler.PoleDomination map=11083 x=231 y=242", result.Log);
    }

    [Fact]
    public async Task HandlerSendsTopPoleIntroWithSourceOptionIds()
    {
        var result = await InvokePoleWarEventHandlerAsync(TopPoleNpcId, optionId: 0);

        Assert.True(result.Handled);
        var replies = ParseNpcReplies(result.Sent);
        Assert.Contains(replies, reply => reply.Text.Contains("Top Pole leader", StringComparison.Ordinal));
        Assert.Contains(replies, reply => reply.Text == "Enter." && reply.OptionId == 1);
        Assert.Contains(replies, reply => reply.Text == "Claim reward." && reply.OptionId == 2);
        Assert.Contains(replies, reply => reply.Text == "Just passing by." && reply.OptionId == byte.MaxValue);
        Assert.All(replies.Where(reply => !string.IsNullOrEmpty(reply.Text)), reply => Assert.True(IsAscii(reply.Text), reply.Text));
        Assert.Contains("source=NpcHandler.TopPole", result.Log);
    }

    [Fact]
    public async Task HandlerTeleportsTopPoleEnterDuringLegacyWindow()
    {
        var result = await InvokePoleWarEventHandlerAsync(
            TopPoleNpcId,
            optionId: 1,
            now: new DateTime(2026, 6, 20, 11, 5, 0),
            minHitPoints: 0);

        Assert.True(result.Handled);
        AssertLegacyNpcTeleportTransition(result.Sent, result.Session, expectedMapId: 1_737, expectedX: 90, expectedY: 90);
        Assert.Equal(1_737, result.UpdatedSession.Character.MapId);
        Assert.Equal(90, result.UpdatedSession.Character.X);
        Assert.Equal(90, result.UpdatedSession.Character.Y);
        Assert.Equal(1, result.UpdatedSession.Character.MinHitPoints);
        Assert.Contains("source=NpcHandler.TopPole map=1737 x=90 y=90", result.Log);
    }

    [Fact]
    public async Task HandlerTeleportsQoutTopPoleReturnToTwinCity()
    {
        var result = await InvokePoleWarEventHandlerAsync(QoutTopPoleNpcId, optionId: 1, mapId: 1_737, x: 90, y: 90);

        Assert.True(result.Handled);
        AssertLegacyNpcTeleportTransition(result.Sent, result.Session, expectedMapId: 1_002, expectedX: 400, expectedY: 355);
        Assert.Equal(1_002, result.UpdatedSession.Character.MapId);
        Assert.Equal(400, result.UpdatedSession.Character.X);
        Assert.Equal(355, result.UpdatedSession.Character.Y);
        Assert.Contains("source=NpcHandler.QoutTopPole map=1002 x=400 y=355", result.Log);
    }

    [Fact]
    public async Task HandlerTeleportsGuildWarScoreEnterDuringLegacyWindow()
    {
        var result = await InvokePoleWarEventHandlerAsync(
            GuildWarScoreNpcId,
            optionId: 1,
            now: new DateTime(2026, 6, 20, 19, 45, 0));

        Assert.True(result.Handled);
        AssertLegacyNpcTeleportTransition(
            result.Sent,
            result.Session,
            expectedMapId: 1_505,
            expectedX: 150,
            expectedY: 96,
            expectedSourceMapId: 6_784);
        Assert.Equal(6_784, result.UpdatedSession.Character.MapId);
        Assert.Equal(150, result.UpdatedSession.Character.X);
        Assert.Equal(96, result.UpdatedSession.Character.Y);
        Assert.Contains("source=NpcHandler.GuildWarScore map=6784 x=150 y=96", result.Log);
    }

    [Fact]
    public async Task GuildWarScorePoleNpcRewardEligibleWinnerRoutesToExistingReward()
    {
        var result = await InvokePoleWarEventHandlerAsync(
            GuildWarScoreNpcId,
            optionId: 2,
            attributes: CreateGuildWarScoreWinnerAttributes(),
            conquerPoints: 100);

        Assert.True(result.Handled);
        Assert.Equal(100 + GuildWarScoreRewardConquerPoints, result.UpdatedSession.Character.ConquerPoints);
        Assert.Contains(
            ParseMsgUpdates(result.Sent),
            packet => packet.Updates.Any(entry =>
                entry.Type == LegacyMsgUpdateDataType.ConquerPoints &&
                entry.Value == 100UL + (ulong)GuildWarScoreRewardConquerPoints));
        Assert.Contains(
            result.UpdatedSession.PlayerItems.InventoryItems,
            item => item.ItemId == GuildWarScoreRewardItemId);
        Assert.Contains("source=NpcHandler.GuildWarScore", result.Log);
        Assert.Contains("sourceReward=NewSystem.PrizeAllTops.GuildWarScore", result.Log);
    }

    [Fact]
    public async Task HandlerSendsGuild6PoleWarIntroWithSourceOptionIds()
    {
        var result = await InvokePoleWarEventHandlerAsync(Guild6PoleWarNpcId, optionId: 0);

        Assert.True(result.Handled);
        var replies = ParseNpcReplies(result.Sent);
        Assert.Contains(replies, reply => reply.Text.Contains("GuildPoleWar manager", StringComparison.Ordinal));
        Assert.Contains(replies, reply => reply.Text.Contains("00:00 To 00:29", StringComparison.Ordinal));
        Assert.Contains(replies, reply => reply.Text == "Claim reward." && reply.OptionId == 2);
        Assert.Contains(replies, reply => reply.Text == "Okay. I see." && reply.OptionId == byte.MaxValue);
        Assert.All(replies.Where(reply => !string.IsNullOrEmpty(reply.Text)), reply => Assert.True(IsAscii(reply.Text), reply.Text));
        Assert.Contains("source=NpcHandler.Guild6PoleWar", result.Log);
    }

    [Fact]
    public async Task HandlerSendsGuild6PoleWarRewardSubmenuWithSourceOptionIds()
    {
        var result = await InvokePoleWarEventHandlerAsync(Guild6PoleWarNpcId, optionId: 2);

        Assert.True(result.Handled);
        var replies = ParseNpcReplies(result.Sent);
        for (byte optionId = 3; optionId <= 8; optionId++)
        {
            var poleNumber = optionId - 2;
            Assert.Contains(
                replies,
                reply => reply.Text == $"Claim GuildPole[{poleNumber}]War." && reply.OptionId == optionId);
        }

        Assert.All(replies.Where(reply => !string.IsNullOrEmpty(reply.Text)), reply => Assert.True(IsAscii(reply.Text), reply.Text));
        Assert.Contains("source=NpcHandler.Guild6PoleWar", result.Log);
    }

    [Theory]
    [InlineData("Guild6PoleWar", 3, "GuildPole[1]")]
    [InlineData("Guild6PoleWar6", 8, "GuildPole[6]")]
    public async Task Guild6PoleWarRewardEligibleWinnerGrantsLegacyReward(
        string scheduleName,
        byte optionId,
        string poleLabel)
    {
        var result = await ClaimGuild6PoleWarRewardAsync(
            scheduleName,
            optionId,
            attributes: CreateGuild6PoleWarWinnerAttributes(scheduleName),
            conquerPoints: 100,
            money: 200);

        Assert.True(result.Handled);
        Assert.Equal(100 + Guild6PoleWarRewardConquerPoints, result.UpdatedSession.Character.ConquerPoints);
        Assert.Equal(200 + Guild6PoleWarRewardMoney, result.UpdatedSession.Character.Money);
        Assert.Contains(
            ParseMsgUpdates(result.Sent),
            packet => packet.Updates.Any(entry =>
                entry.Type == LegacyMsgUpdateDataType.ConquerPoints &&
                entry.Value == 100UL + (ulong)Guild6PoleWarRewardConquerPoints));

        AssertGuild6PoleWarSpecialTitleGranted(result.Sent, result.UpdatedSession, Guild6PoleWarCelestialRainbowTitleId);
        AssertGuild6PoleWarSpecialTitleGranted(result.Sent, result.UpdatedSession, Guild6PoleWarRadiantWingsTitleId);
        AssertGuild6PoleWarSpecialTitleGranted(result.Sent, result.UpdatedSession, Guild6PoleWarScholarTitleId);
        AssertGuild6PoleWarRoundOwnerCleared(result.UpdatedSession, scheduleName);

        var messages = ParseMessages(result.Sent);
        Assert.Contains(
            messages,
            message => message.ChatType == LegacyMsgMessagePacket.CenterChatType &&
                MessageMentionsGuild6PoleWarReward(message, poleLabel));
        Assert.Contains(
            messages,
            message => message.ChatType == LegacySlideCrossServerChatType &&
                MessageMentionsGuild6PoleWarReward(message, poleLabel));
        Assert.Contains("source=NpcHandler.Guild6PoleWar", result.Log);
        Assert.Contains("sourceReward=NewSystem.PrizeAllTops.Guild6PoleWar", result.Log);
    }

    [Fact]
    public async Task Guild6PoleWarRewardWrongGuildIsDeniedWithSafeDialog()
    {
        var result = await ClaimGuild6PoleWarRewardAsync(
            "Guild6PoleWar",
            optionId: 3,
            attributes: CreateGuild6PoleWarWinnerAttributes("Guild6PoleWar", playerGuildId: "88", playerGuildName: "RivalGuild"),
            conquerPoints: 100,
            money: 200);

        Assert.True(result.Handled);
        AssertGuild6PoleWarRewardNotGranted(result.Sent, result.UpdatedSession, expectedConquerPoints: 100, expectedMoney: 200);
        var replies = ParseNpcReplies(result.Sent);
        Assert.Contains(replies, reply => reply.Text == "You do not have any reward to claim.");
        Assert.All(replies.Where(reply => !string.IsNullOrEmpty(reply.Text)), reply => Assert.True(IsAscii(reply.Text), reply.Text));
    }

    [Fact]
    public async Task Guild6PoleWarRewardNonLeaderIsDeniedWithSafeDialog()
    {
        var result = await ClaimGuild6PoleWarRewardAsync(
            "Guild6PoleWar",
            optionId: 3,
            attributes: CreateGuild6PoleWarWinnerAttributes("Guild6PoleWar", guildRank: "6"),
            conquerPoints: 100,
            money: 200);

        Assert.True(result.Handled);
        AssertGuild6PoleWarRewardNotGranted(result.Sent, result.UpdatedSession, expectedConquerPoints: 100, expectedMoney: 200);
        var replies = ParseNpcReplies(result.Sent);
        Assert.Contains(replies, reply => reply.Text == "Only the guild leader can claim the reward.");
        Assert.All(replies.Where(reply => !string.IsNullOrEmpty(reply.Text)), reply => Assert.True(IsAscii(reply.Text), reply.Text));
    }

    [Fact]
    public async Task Guild6PoleWarRewardMissingWinnerIsDeniedWithSafeDialog()
    {
        var result = await ClaimGuild6PoleWarRewardAsync(
            "Guild6PoleWar",
            optionId: 3,
            attributes: CreateGuild6PoleWarWinnerAttributes("Guild6PoleWar", winnerGuildId: "0", winnerGuildName: ""),
            conquerPoints: 100,
            money: 200);

        Assert.True(result.Handled);
        AssertGuild6PoleWarRewardNotGranted(result.Sent, result.UpdatedSession, expectedConquerPoints: 100, expectedMoney: 200);
        var replies = ParseNpcReplies(result.Sent);
        Assert.Contains(replies, reply => reply.Text == "There is no reward available now.");
        Assert.All(replies.Where(reply => !string.IsNullOrEmpty(reply.Text)), reply => Assert.True(IsAscii(reply.Text), reply.Text));
    }

    [Fact]
    public async Task Guild6PoleWarRewardActiveProcessIsDeniedWithSafeDialog()
    {
        var result = await ClaimGuild6PoleWarRewardAsync(
            "Guild6PoleWar",
            optionId: 3,
            attributes: CreateGuild6PoleWarWinnerAttributes("Guild6PoleWar", process: "Alive"),
            conquerPoints: 100,
            money: 200);

        Assert.True(result.Handled);
        AssertGuild6PoleWarRewardNotGranted(result.Sent, result.UpdatedSession, expectedConquerPoints: 100, expectedMoney: 200);
        var replies = ParseNpcReplies(result.Sent);
        Assert.Contains(replies, reply => reply.Text == "Please come back when GuildPoleWar is finished.");
        Assert.All(replies.Where(reply => !string.IsNullOrEmpty(reply.Text)), reply => Assert.True(IsAscii(reply.Text), reply.Text));
    }

    [Fact]
    public async Task Guild6PoleWarRewardDuplicateClaimDoesNotDoublePay()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            mapId: 1_002,
            x: 430,
            y: 378,
            minHitPoints: 1,
            attributes: CreateGuild6PoleWarWinnerAttributes("Guild6PoleWar"),
            conquerPoints: 100,
            money: 200);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var firstHandled = await ClickGuild6PoleWarRewardAsync(connection, log, sessions, session, optionId: 3);

        Assert.True(firstHandled);
        Assert.True(sessions.TryGet(connection, out var firstUpdated));
        Assert.NotNull(firstUpdated);
        Assert.Equal(100 + Guild6PoleWarRewardConquerPoints, firstUpdated.Character.ConquerPoints);
        Assert.Equal(200 + Guild6PoleWarRewardMoney, firstUpdated.Character.Money);

        sent.Clear();
        var secondHandled = await ClickGuild6PoleWarRewardAsync(connection, log, sessions, firstUpdated, optionId: 3);

        Assert.True(secondHandled);
        Assert.True(sessions.TryGet(connection, out var secondUpdated));
        Assert.NotNull(secondUpdated);
        Assert.Equal(100 + Guild6PoleWarRewardConquerPoints, secondUpdated.Character.ConquerPoints);
        Assert.Equal(200 + Guild6PoleWarRewardMoney, secondUpdated.Character.Money);
        Assert.DoesNotContain(
            ParseMsgUpdates(sent),
            packet => packet.Updates.Any(entry =>
                entry.Type is LegacyMsgUpdateDataType.Conquer or LegacyMsgUpdateDataType.ConquerPoints));
    }

    [Theory]
    [InlineData(PoleDominationNpcId, 1, "NpcHandler.PoleDomination")]
    public async Task HandlerRewardBranchesSendSafeUnavailableDialogAndLogParityGap(
        uint npcId,
        byte optionId,
        string sourceMethod)
    {
        var result = await InvokePoleWarEventHandlerAsync(npcId, optionId);

        Assert.True(result.Handled);
        var replies = ParseNpcReplies(result.Sent);
        Assert.Contains(replies, reply => reply.Text == "No reward is available from this NPC in the current runtime state.");
        Assert.Equal(0, result.UpdatedSession.Character.ConquerPoints);
        Assert.Contains("PARITY-GAP LegacyNpc PoleWarEvent reward unavailable", result.Log);
        Assert.Contains($"source={sourceMethod} option={optionId}", result.Log);
    }

    private static async Task<PoleWarEventInvokeResult> InvokePoleWarEventHandlerAsync(
        uint npcId,
        byte optionId,
        DateTime? now = null,
        long mapId = 1_002,
        long x = 430,
        long y = 378,
        int minHitPoints = 1,
        Dictionary<string, string>? attributes = null,
        long conquerPoints = 0,
        long money = 0)
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(mapId, x, y, minHitPoints, attributes, conquerPoints, money);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions);

        var handled = await WorldPacketHandlers.TryHandleLegacyPoleWarEventNpcAsync(
            context,
            session,
            npcId,
            optionId,
            now ?? new DateTime(2026, 6, 20, 10, 0, 0),
            CancellationToken.None);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        return new PoleWarEventInvokeResult(handled, sent, log.ToString(), session, updatedSession);
    }

    private static WorldSession CreateSession(
        long mapId,
        long x,
        long y,
        int minHitPoints,
        Dictionary<string, string>? attributes = null,
        long conquerPoints = 0,
        long money = 0)
    {
        attributes ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        attributes.TryAdd("DynamicID", "0");

        return new WorldSession(
            AccountId: 100,
            Username: "pole-war-test",
            Character: new CharacterSnapshot(
                Uid: 1_293_600,
                Name: "PoleHero",
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
                MinHitPoints: minHitPoints,
                MinMana: 0,
                Money: money,
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

    private static Dictionary<string, string> CreateGuild6PoleWarWinnerAttributes(
        string scheduleName,
        string playerGuildId = "77",
        string playerGuildName = "SourceGuild",
        string guildRank = "100",
        string process = "Dead",
        string winnerGuildId = "77",
        string winnerGuildName = "SourceGuild",
        string winnerScore = "456789")
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["GuildID"] = playerGuildId,
            ["GuildName"] = playerGuildName,
            ["GuildRank"] = guildRank,
            ["GuildMemberRank"] = guildRank,
        };

        SetGuild6PoleWarScheduleAttributeTriplet(attributes, scheduleName, "Process", process);
        SetGuild6PoleWarScheduleAttributeTriplet(attributes, scheduleName, "Proces", process);
        SetGuild6PoleWarScheduleAttributeTriplet(attributes, scheduleName, "RoundOwnerGuildID", winnerGuildId);
        SetGuild6PoleWarScheduleAttributeTriplet(attributes, scheduleName, "RoundOwnerGuildId", winnerGuildId);
        SetGuild6PoleWarScheduleAttributeTriplet(attributes, scheduleName, "RoundOwnerGuildName", winnerGuildName);
        SetGuild6PoleWarScheduleAttributeTriplet(attributes, scheduleName, "RoundOwnerScore", winnerScore);
        return attributes;
    }

    private static Dictionary<string, string> CreateGuildWarScoreWinnerAttributes(
        string playerGuildId = "77",
        string playerGuildName = "SourceGuild",
        string guildRank = "100",
        string winnerGuildId = "77",
        string winnerGuildName = "SourceGuild",
        string winnerScore = "456789")
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["GuildID"] = playerGuildId,
            ["GuildName"] = playerGuildName,
            ["GuildRank"] = guildRank,
            ["GuildMemberRank"] = guildRank,
            ["LegacyTopWarScoreProces"] = "Dead",
            ["TopWarScoreProces"] = "Dead",
            ["LegacyGuildWarScoreProcess"] = "Dead",
            ["GuildWarScoreProcess"] = "Dead",
            ["LegacyTopWarScoreRoundOwnerGuildID"] = winnerGuildId,
            ["TopWarScoreRoundOwnerGuildID"] = winnerGuildId,
            ["LegacyTopWarScoreRoundOwnerGuildName"] = winnerGuildName,
            ["TopWarScoreRoundOwnerGuildName"] = winnerGuildName,
            ["LegacyTopWarScoreRoundOwnerScore"] = winnerScore,
            ["TopWarScoreRoundOwnerScore"] = winnerScore,
            ["LegacyGuildWarScoreRoundOwnerGuildID"] = winnerGuildId,
            ["GuildWarScoreRoundOwnerGuildID"] = winnerGuildId,
            ["LegacyGuildWarScoreRoundOwnerGuildName"] = winnerGuildName,
            ["GuildWarScoreRoundOwnerGuildName"] = winnerGuildName,
            ["LegacyGuildWarScoreRoundOwnerScore"] = winnerScore,
            ["GuildWarScoreRoundOwnerScore"] = winnerScore,
        };
    }

    private static void SetGuild6PoleWarScheduleAttributeTriplet(
        Dictionary<string, string> attributes,
        string scheduleName,
        string suffix,
        string value)
    {
        attributes["Legacy" + scheduleName + suffix] = value;
        attributes[scheduleName + suffix] = value;
        attributes["Runtime" + scheduleName + suffix] = value;
    }

    private static async Task<PoleWarEventInvokeResult> ClaimGuild6PoleWarRewardAsync(
        string scheduleName,
        byte optionId,
        Dictionary<string, string>? attributes = null,
        long conquerPoints = 0,
        long money = 0)
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            mapId: 1_002,
            x: 430,
            y: 378,
            minHitPoints: 1,
            attributes: attributes ?? CreateGuild6PoleWarWinnerAttributes(scheduleName),
            conquerPoints: conquerPoints,
            money: money);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await ClickGuild6PoleWarRewardAsync(connection, log, sessions, session, optionId);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        return new PoleWarEventInvokeResult(handled, sent, log.ToString(), session, updatedSession);
    }

    private static async Task<bool> ClickGuild6PoleWarRewardAsync(
        ConquerTcpConnection connection,
        StringWriter log,
        WorldSessionStore sessions,
        WorldSession session,
        byte optionId)
    {
        return await WorldPacketHandlers.TryHandleLegacyPoleWarEventNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            session,
            Guild6PoleWarNpcId,
            optionId,
            new DateTime(2026, 6, 20, 10, 0, 0),
            CancellationToken.None);
    }

    private static void AssertGuild6PoleWarRewardNotGranted(
        IEnumerable<byte[]> sent,
        WorldSession updatedSession,
        long expectedConquerPoints,
        long expectedMoney)
    {
        Assert.Equal(expectedConquerPoints, updatedSession.Character.ConquerPoints);
        Assert.Equal(expectedMoney, updatedSession.Character.Money);
        Assert.DoesNotContain(
            ParseMsgUpdates(sent),
            packet => packet.Updates.Any(entry =>
                entry.Type is LegacyMsgUpdateDataType.Conquer or LegacyMsgUpdateDataType.ConquerPoints));
        Assert.DoesNotContain(ParseSpecialTitles(sent), packet => packet.TitleId == Guild6PoleWarCelestialRainbowTitleId);
        Assert.DoesNotContain(ParseSpecialTitles(sent), packet => packet.TitleId == Guild6PoleWarRadiantWingsTitleId);
        Assert.DoesNotContain(ParseSpecialTitles(sent), packet => packet.TitleId == Guild6PoleWarScholarTitleId);
    }

    private static void AssertGuild6PoleWarSpecialTitleGranted(
        IEnumerable<byte[]> sent,
        WorldSession updatedSession,
        uint titleId)
    {
        var sentTitle = ParseSpecialTitles(sent).Any(packet => packet.TitleId == titleId);
        var storedTitle = CharacterHasSpecialTitle(updatedSession.Character, titleId);
        Assert.True(sentTitle || storedTitle, $"Expected Guild6PoleWar reward to grant special title {titleId}.");
    }

    private static void AssertGuild6PoleWarRoundOwnerCleared(WorldSession updatedSession, string scheduleName)
    {
        AssertGuild6PoleWarRoundOwnerAttributeCleared(updatedSession, "Legacy" + scheduleName + "RoundOwnerGuildID");
        AssertGuild6PoleWarRoundOwnerAttributeCleared(updatedSession, "Legacy" + scheduleName + "RoundOwnerGuildId");
        AssertGuild6PoleWarRoundOwnerAttributeCleared(updatedSession, scheduleName + "RoundOwnerGuildID");
        AssertGuild6PoleWarRoundOwnerAttributeCleared(updatedSession, scheduleName + "RoundOwnerGuildId");
        AssertGuild6PoleWarRoundOwnerAttributeCleared(updatedSession, "Runtime" + scheduleName + "RoundOwnerGuildID");
        AssertGuild6PoleWarRoundOwnerAttributeCleared(updatedSession, "Runtime" + scheduleName + "RoundOwnerGuildId");
        AssertGuild6PoleWarRoundOwnerAttributeCleared(updatedSession, "Legacy" + scheduleName + "RoundOwnerGuildName");
        AssertGuild6PoleWarRoundOwnerAttributeCleared(updatedSession, scheduleName + "RoundOwnerGuildName");
        AssertGuild6PoleWarRoundOwnerAttributeCleared(updatedSession, "Runtime" + scheduleName + "RoundOwnerGuildName");
        AssertGuild6PoleWarRoundOwnerAttributeCleared(updatedSession, "Legacy" + scheduleName + "RoundOwnerScore");
        AssertGuild6PoleWarRoundOwnerAttributeCleared(updatedSession, scheduleName + "RoundOwnerScore");
        AssertGuild6PoleWarRoundOwnerAttributeCleared(updatedSession, "Runtime" + scheduleName + "RoundOwnerScore");
    }

    private static void AssertGuild6PoleWarRoundOwnerAttributeCleared(WorldSession updatedSession, string key)
    {
        var attributes = updatedSession.Character.Attributes;
        var value = attributes.TryGetValue(key, out var raw) ? raw : "<missing>";
        Assert.False(
            HasNonEmptyAttribute(attributes, key),
            $"Expected source RoundOwner clear to remove or zero '{key}' after Guild6PoleWar reward claim, but value was '{value}'.");
    }

    private static bool CharacterHasSpecialTitle(CharacterSnapshot character, uint titleId)
    {
        if (!character.Attributes.TryGetValue("SpecialTitles", out var rawSpecialTitles))
        {
            return false;
        }

        var title = titleId.ToString();
        return rawSpecialTitles
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Contains(title, StringComparer.Ordinal);
    }

    private static bool HasNonEmptyAttribute(IReadOnlyDictionary<string, string> attributes, string key)
    {
        return attributes.TryGetValue(key, out var value) &&
            !string.IsNullOrWhiteSpace(value) &&
            !string.Equals(value, "0", StringComparison.Ordinal);
    }

    private static bool MessageMentionsGuild6PoleWarReward(LegacyMsgMessagePacket message, string poleLabel)
    {
        var text = string.Join(' ', message.Content);
        return text.Contains(Guild6PoleWarRewardConquerPoints.ToString(), StringComparison.Ordinal) &&
            text.Contains(poleLabel, StringComparison.Ordinal);
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

    private static IReadOnlyList<LegacySpecialTitlePacket> ParseSpecialTitles(IEnumerable<byte[]> sent)
    {
        var packets = new List<LegacySpecialTitlePacket>();
        foreach (var bytes in sent)
        {
            if (LegacySpecialTitlePacket.TryParse(ReadFrame(bytes), out var packet))
            {
                packets.Add(packet);
            }
        }

        return packets;
    }

    private static IReadOnlyList<LegacyMsgMessagePacket> ParseMessages(IEnumerable<byte[]> sent)
    {
        var packets = new List<LegacyMsgMessagePacket>();
        foreach (var bytes in sent)
        {
            if (LegacyMsgMessagePacket.TryParse(ReadFrame(bytes), out var packet))
            {
                packets.Add(packet);
            }
        }

        return packets;
    }

    private static void AssertLegacyNpcTeleportTransition(
        IReadOnlyList<byte[]> sent,
        WorldSession session,
        uint expectedMapId,
        uint expectedX,
        uint expectedY,
        uint? expectedSourceMapId = null)
    {
        var sourceMapId = expectedSourceMapId ?? expectedMapId;

        Assert.Equal(5, sent.Count);
        Assert.True(MsgActionPacket.TryParse(ReadFrame(sent[0]), out var action));
        Assert.Equal((uint)session.Character.Uid, action.ObjId);
        Assert.Equal(expectedMapId, action.DwParam);
        Assert.Equal(MsgActionType.Teleport, action.Type);
        Assert.Equal(expectedX, action.PositionX);
        Assert.Equal(expectedY, action.PositionY);
        Assert.Equal((long)expectedMapId, action.DwParam3);

        Assert.True(MsgActionPacket.TryParse(ReadFrame(sent[1]), out var merchantStartup));
        Assert.Equal(MsgActionType.LegacyMerchantStartup, merchantStartup.Type);
        Assert.Equal(2U, merchantStartup.DwParam);
        Assert.Equal((long)sourceMapId, merchantStartup.DwParam3);

        Assert.True(MsgActionPacket.TryParse(ReadFrame(sent[2]), out var stopVending));
        Assert.Equal(MsgActionType.StopVending, stopVending.Type);
        Assert.Equal(sourceMapId, stopVending.DwParam);
        Assert.Equal((long)sourceMapId, stopVending.DwParam3);

        Assert.True(MsgActionPacket.TryParse(ReadFrame(sent[3]), out var color));
        Assert.Equal(MsgActionType.LegacyColor, color.Type);
        Assert.Equal(0U, color.DwParam);

        Assert.True(LegacyMapStatusPacket.TryParse(ReadFrame(sent[4]), out var mapStatus));
        Assert.Equal(LegacyMapStatusPacket.FromMap(sourceMapId, mapsById: null), mapStatus);
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

    private sealed record PoleWarEventInvokeResult(
        bool Handled,
        IReadOnlyList<byte[]> Sent,
        string Log,
        WorldSession Session,
        WorldSession UpdatedSession);
}
