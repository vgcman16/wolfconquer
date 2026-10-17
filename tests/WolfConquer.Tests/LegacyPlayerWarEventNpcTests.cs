using System.Buffers;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyPlayerWarEventNpcTests
{
    private const uint CouplesPkNpcId = 87_765;
    private const uint WarOfPlayersNpcId = 6_906;
    private const uint ChampionsOfWarrNpcId = 4_433;
    private const uint ChampionsOfWarrRewardItemId = 4_200_013;
    private const uint ChampionsOfWarrChampionHaloTitleId = 8_030;
    private const uint ChampionsOfWarrSirenSongWingsTitleId = 6_017;
    private const uint ChampionsOfWarrPromiseOfSkyTitleId = 2_235;
    private const uint LegacySlideCrossServerChatType = 2_401;
    private const long ChampionsOfWarrRewardConquerPoints = 150_000;

    [Fact]
    public async Task HandlerSendsTranslatedCouplesPkIntroAndUnavailableRegistration()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(mapId: 1_002);
        var context = new WorldPacketHandlerContext(connection, log, Session: session);

        var introHandled = await WorldPacketHandlers.TryHandleLegacyPlayerWarEventNpcAsync(
            context,
            session,
            CouplesPkNpcId,
            optionId: 0,
            CancellationToken.None);

        Assert.True(introHandled);
        var replies = ParseNpcReplies(sent);
        Assert.Contains(replies, reply => reply.Text.Contains("Couples PK event", StringComparison.Ordinal));
        Assert.Contains(replies, reply => reply.Text == "Register me." && reply.OptionId == 1);
        Assert.Contains(replies, reply => reply.Text == "I will come back later." && reply.OptionId == byte.MaxValue);
        Assert.All(replies.Where(reply => !string.IsNullOrEmpty(reply.Text)), reply => Assert.True(IsAscii(reply.Text), reply.Text));

        sent.Clear();
        var registerHandled = await WorldPacketHandlers.TryHandleLegacyPlayerWarEventNpcAsync(
            context,
            session,
            CouplesPkNpcId,
            optionId: 1,
            CancellationToken.None);

        Assert.True(registerHandled);
        replies = ParseNpcReplies(sent);
        Assert.Contains(replies, reply => reply.Text == "Couples PK registration is not available here yet. Please come back between 7:00 and 7:15.");
        Assert.Contains("PARITY-GAP LegacyNpc PlayerWarEvents", log.ToString());
        Assert.Contains("MsgSchedules.CouplesPKWar.Join", log.ToString());
    }

    [Fact]
    public async Task HandlerSendsWarOfPlayersIntroAndStaticEnterTeleport()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(mapId: 1_002, x: 430, y: 378);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions);

        var introHandled = await WorldPacketHandlers.TryHandleLegacyPlayerWarEventNpcAsync(
            context,
            session,
            WarOfPlayersNpcId,
            optionId: 0,
            CancellationToken.None);

        Assert.True(introHandled);
        var replies = ParseNpcReplies(sent);
        Assert.Contains(replies, reply => reply.Text == "Hi, I lead the War of Players event. What can I do for you? The event runs from 14:00 to 14:30.");
        Assert.Contains(replies, reply => reply.Text == "Enter." && reply.OptionId == 1);
        Assert.Contains(replies, reply => reply.Text == "Reward." && reply.OptionId == 2);
        Assert.Contains(replies, reply => reply.Text == "Just passing by." && reply.OptionId == byte.MaxValue);
        Assert.All(replies.Where(reply => !string.IsNullOrEmpty(reply.Text)), reply => Assert.True(IsAscii(reply.Text), reply.Text));

        sent.Clear();
        var enterHandled = await WorldPacketHandlers.TryHandleLegacyPlayerWarEventNpcAsync(
            context,
            session,
            WarOfPlayersNpcId,
            optionId: 1,
            CancellationToken.None);

        Assert.True(enterHandled);
        AssertLegacyTeleport(sent, expectedClientMapId: 3_825, expectedX: 29, expectedY: 48);
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(6_729, updated.Character.MapId);
        Assert.Equal(29, updated.Character.X);
        Assert.Equal(48, updated.Character.Y);
        Assert.Contains("source=NpcHandler.WarOfPlayers map=6729 x=29 y=48", log.ToString());
    }

    [Fact]
    public async Task WarOfPlayersRewardIsSourceAuditedNoReward()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(mapId: 1_002, conquerPoints: 123_456);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions);

        var handled = await WorldPacketHandlers.TryHandleLegacyPlayerWarEventNpcAsync(
            context,
            session,
            WarOfPlayersNpcId,
            optionId: 2,
            CancellationToken.None);

        Assert.True(handled);
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(123_456, updated.Character.ConquerPoints);
        Assert.Equal(0, updated.Character.Money);
        Assert.Empty(updated.PlayerItems.InventoryItems);

        var replies = ParseNpcReplies(sent);
        Assert.Contains(replies, reply => reply.Text == "No War of Players reward is available here yet.");
        Assert.All(replies.Where(reply => !string.IsNullOrEmpty(reply.Text)), reply => Assert.True(IsAscii(reply.Text), reply.Text));
        Assert.DoesNotContain(
            ParseMsgUpdates(sent),
            packet => packet.Updates.Any(entry =>
                entry.Type is LegacyMsgUpdateDataType.Conquer or
                    LegacyMsgUpdateDataType.ConquerPoints or
                    LegacyMsgUpdateDataType.Money));
        Assert.Empty(ParseGameItems(sent));

        var output = log.ToString();
        Assert.Contains("WorldServer handled source-audited LegacyNpc PlayerWarEvents no reward", output);
        Assert.Contains("source=NpcHandler.WarOfPlayers option=2", output);
        Assert.Contains("Claim~reward.[WarOfPlayers]", output);
        Assert.Contains("no case 4 reward body", output);
        Assert.Contains("PrizeAllTops.WarOfPlayer runtime usage", output);
        Assert.DoesNotContain("PARITY-GAP LegacyNpc PlayerWarEvents", output);
    }

    [Fact]
    public async Task HandlerSendsChampionsIntroAndStaticEnterTeleport()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(mapId: 1_002, x: 430, y: 378, minHitPoints: 0);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions);

        var introHandled = await WorldPacketHandlers.TryHandleLegacyPlayerWarEventNpcAsync(
            context,
            session,
            ChampionsOfWarrNpcId,
            optionId: 0,
            CancellationToken.None);

        Assert.True(introHandled);
        var replies = ParseNpcReplies(sent);
        Assert.Contains(replies, reply => reply.Text == "Hi, I lead the Champions of War event. What can I do for you? The event runs daily from 18:00 to 18:30.");
        Assert.Contains(replies, reply => reply.Text == "Enter." && reply.OptionId == 1);
        Assert.Contains(replies, reply => reply.Text == "Reward." && reply.OptionId == 2);
        Assert.All(replies.Where(reply => !string.IsNullOrEmpty(reply.Text)), reply => Assert.True(IsAscii(reply.Text), reply.Text));

        sent.Clear();
        var enterHandled = await WorldPacketHandlers.TryHandleLegacyPlayerWarEventNpcAsync(
            context,
            session,
            ChampionsOfWarrNpcId,
            optionId: 1,
            CancellationToken.None);

        Assert.True(enterHandled);
        AssertLegacyTeleport(sent, expectedClientMapId: 3_872, expectedX: 34, expectedY: 55);
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(6_546, updated.Character.MapId);
        Assert.Equal(34, updated.Character.X);
        Assert.Equal(55, updated.Character.Y);
        Assert.Equal(1, updated.Character.MinHitPoints);
    }

    [Fact]
    public async Task ChampionsOfWarrRewardWithoutRuntimeStateKeepsPlayerSafe()
    {
        var result = await ClaimChampionsOfWarrRewardAsync();

        Assert.True(result.Handled);
        AssertChampionsOfWarrRewardNotGranted(result.Sent, result.UpdatedSession, expectedConquerPoints: 0);
        var replies = ParseNpcReplies(result.Sent);
        Assert.Contains(
            replies,
            reply => reply.Text.Contains("reward", StringComparison.OrdinalIgnoreCase) &&
                (reply.Text.Contains("no ", StringComparison.OrdinalIgnoreCase) ||
                    reply.Text.Contains("not available", StringComparison.OrdinalIgnoreCase)));
        Assert.All(replies.Where(reply => !string.IsNullOrEmpty(reply.Text)), reply => Assert.True(IsAscii(reply.Text), reply.Text));
        Assert.Contains("source=NpcHandler.MsgChampionsOfWarr", result.Log);
    }

    [Fact]
    public async Task ChampionsOfWarrRewardEligibleWinnerGrantsLegacyChampionWarReward()
    {
        var result = await ClaimChampionsOfWarrRewardAsync(
            attributes: CreateChampionsOfWarrWinnerAttributes(),
            conquerPoints: 100);

        Assert.True(result.Handled);
        Assert.Equal(100 + ChampionsOfWarrRewardConquerPoints, result.UpdatedSession.Character.ConquerPoints);
        Assert.Contains(
            ParseMsgUpdates(result.Sent),
            packet => packet.Updates.Any(entry =>
                entry.Type == LegacyMsgUpdateDataType.ConquerPoints &&
                entry.Value == 100UL + (ulong)ChampionsOfWarrRewardConquerPoints));

        var sentRewardItem = ParseGameItems(result.Sent)
            .Any(item => item.ItemId == ChampionsOfWarrRewardItemId &&
                item.Mode == LegacyGameItemPacket.AddItemMode &&
                item.StackSize == 1);
        var storedRewardItem = result.UpdatedSession.PlayerItems.InventoryItems
            .Any(item => item.ItemId == ChampionsOfWarrRewardItemId && item.StackSize == 1);
        Assert.True(sentRewardItem || storedRewardItem, "Expected option 2 to grant item 4200013 stack 1 to the winning guild leader.");

        AssertChampionsOfWarrSpecialTitleGranted(result.Sent, result.UpdatedSession, ChampionsOfWarrChampionHaloTitleId);
        AssertChampionsOfWarrSpecialTitleGranted(result.Sent, result.UpdatedSession, ChampionsOfWarrSirenSongWingsTitleId);
        AssertChampionsOfWarrSpecialTitleGranted(result.Sent, result.UpdatedSession, ChampionsOfWarrPromiseOfSkyTitleId);
        AssertChampionsOfWarrRoundOwnerCleared(result.UpdatedSession);

        var messages = ParseMessages(result.Sent);
        Assert.Contains(
            messages,
            message => message.ChatType == LegacyMsgMessagePacket.CenterChatType &&
                MessageMentionsChampionWarReward(message));
        Assert.Contains(
            messages,
            message => message.ChatType == LegacySlideCrossServerChatType &&
                MessageMentionsChampionWarReward(message));
        Assert.Contains("source=NpcHandler.MsgChampionsOfWarr", result.Log);
        Assert.Contains("sourceReward=NewSystem.PrizeAllTops.ChampionWar", result.Log);
    }

    [Fact]
    public async Task ChampionsOfWarrRewardNonLeaderIsDeniedWithSafeDialog()
    {
        var result = await ClaimChampionsOfWarrRewardAsync(
            attributes: CreateChampionsOfWarrWinnerAttributes(guildRank: "6"),
            conquerPoints: 100);

        Assert.True(result.Handled);
        AssertChampionsOfWarrRewardNotGranted(result.Sent, result.UpdatedSession, expectedConquerPoints: 100);
        var replies = ParseNpcReplies(result.Sent);
        Assert.Contains(replies, reply => reply.Text == "Only the guild leader can claim the reward.");
        Assert.All(replies.Where(reply => !string.IsNullOrEmpty(reply.Text)), reply => Assert.True(IsAscii(reply.Text), reply.Text));
    }

    [Fact]
    public async Task ChampionsOfWarrRewardWrongGuildIsDeniedWithSafeDialog()
    {
        var result = await ClaimChampionsOfWarrRewardAsync(
            attributes: CreateChampionsOfWarrWinnerAttributes(playerGuildId: "88", playerGuildName: "RivalGuild"),
            conquerPoints: 100);

        Assert.True(result.Handled);
        AssertChampionsOfWarrRewardNotGranted(result.Sent, result.UpdatedSession, expectedConquerPoints: 100);
        var replies = ParseNpcReplies(result.Sent);
        Assert.Contains(replies, reply => reply.Text == "You do not have any reward to claim.");
        Assert.All(replies.Where(reply => !string.IsNullOrEmpty(reply.Text)), reply => Assert.True(IsAscii(reply.Text), reply.Text));
    }

    [Fact]
    public async Task ChampionsOfWarrRewardDuplicateClaimDoesNotDoublePay()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            mapId: 1_002,
            attributes: CreateChampionsOfWarrWinnerAttributes(),
            conquerPoints: 100);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var firstHandled = await ClickChampionsOfWarrRewardAsync(connection, log, sessions, session);

        Assert.True(firstHandled);
        Assert.True(sessions.TryGet(connection, out var firstUpdated));
        Assert.NotNull(firstUpdated);
        Assert.Equal(100 + ChampionsOfWarrRewardConquerPoints, firstUpdated.Character.ConquerPoints);
        var firstRewardItemCount = firstUpdated.PlayerItems.InventoryItems.Count(item => item.ItemId == ChampionsOfWarrRewardItemId);

        sent.Clear();
        var secondHandled = await ClickChampionsOfWarrRewardAsync(connection, log, sessions, firstUpdated);

        Assert.True(secondHandled);
        Assert.True(sessions.TryGet(connection, out var secondUpdated));
        Assert.NotNull(secondUpdated);
        Assert.Equal(100 + ChampionsOfWarrRewardConquerPoints, secondUpdated.Character.ConquerPoints);
        Assert.Equal(
            firstRewardItemCount,
            secondUpdated.PlayerItems.InventoryItems.Count(item => item.ItemId == ChampionsOfWarrRewardItemId));
        Assert.DoesNotContain(
            ParseMsgUpdates(sent),
            packet => packet.Updates.Any(entry =>
                entry.Type is LegacyMsgUpdateDataType.Conquer or LegacyMsgUpdateDataType.ConquerPoints));
        Assert.DoesNotContain(
            ParseGameItems(sent),
            item => item.ItemId == ChampionsOfWarrRewardItemId);
    }

    private static WorldSession CreateSession(
        long mapId,
        long x = 50,
        long y = 50,
        int minHitPoints = 1,
        Dictionary<string, string>? attributes = null,
        long conquerPoints = 0)
    {
        attributes ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        attributes.TryAdd("DynamicID", "0");

        return new WorldSession(
            AccountId: 100,
            Username: "player-war-events-test",
            Character: new CharacterSnapshot(
                Uid: 1_293_600,
                Name: "WarHero",
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

    private static Dictionary<string, string> CreateChampionsOfWarrWinnerAttributes(
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
            ["LegacyChampionsOfWarrProcess"] = "Dead",
            ["ChampionsOfWarrProcess"] = "Dead",
            ["RuntimeChampionsOfWarrProcess"] = "Dead",
            ["LegacyChampionsOfWarrProces"] = "Dead",
            ["ChampionsOfWarrProces"] = "Dead",
            ["RuntimeChampionsOfWarrProces"] = "Dead",
            ["LegacyChampionsOfWarrRoundOwnerGuildID"] = winnerGuildId,
            ["LegacyChampionsOfWarrRoundOwnerGuildId"] = winnerGuildId,
            ["ChampionsOfWarrRoundOwnerGuildID"] = winnerGuildId,
            ["ChampionsOfWarrRoundOwnerGuildId"] = winnerGuildId,
            ["LegacyChampionsOfWarrRoundOwnerGuildName"] = winnerGuildName,
            ["ChampionsOfWarrRoundOwnerGuildName"] = winnerGuildName,
            ["LegacyChampionsOfWarrRoundOwnerScore"] = winnerScore,
            ["ChampionsOfWarrRoundOwnerScore"] = winnerScore,
        };
    }

    private static async Task<ChampionsOfWarrRewardResult> ClaimChampionsOfWarrRewardAsync(
        Dictionary<string, string>? attributes = null,
        long conquerPoints = 0)
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            mapId: 1_002,
            attributes: attributes,
            conquerPoints: conquerPoints);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await ClickChampionsOfWarrRewardAsync(connection, log, sessions, session);

        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        return new ChampionsOfWarrRewardResult(handled, sent, updated, log.ToString());
    }

    private static async Task<bool> ClickChampionsOfWarrRewardAsync(
        ConquerTcpConnection connection,
        StringWriter log,
        WorldSessionStore sessions,
        WorldSession session)
    {
        return await WorldPacketHandlers.TryHandleLegacyPlayerWarEventNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            session,
            ChampionsOfWarrNpcId,
            optionId: 2,
            CancellationToken.None);
    }

    private static void AssertChampionsOfWarrRewardNotGranted(
        IEnumerable<byte[]> sent,
        WorldSession updatedSession,
        long expectedConquerPoints)
    {
        Assert.Equal(expectedConquerPoints, updatedSession.Character.ConquerPoints);
        Assert.DoesNotContain(
            ParseMsgUpdates(sent),
            packet => packet.Updates.Any(entry =>
                entry.Type is LegacyMsgUpdateDataType.Conquer or LegacyMsgUpdateDataType.ConquerPoints));
        Assert.DoesNotContain(
            ParseGameItems(sent),
            item => item.ItemId == ChampionsOfWarrRewardItemId);
        Assert.DoesNotContain(
            updatedSession.PlayerItems.InventoryItems,
            item => item.ItemId == ChampionsOfWarrRewardItemId);
    }

    private static void AssertChampionsOfWarrSpecialTitleGranted(
        IEnumerable<byte[]> sent,
        WorldSession updatedSession,
        uint titleId)
    {
        var sentTitle = ParseSpecialTitles(sent).Any(packet => packet.TitleId == titleId);
        var storedTitle = CharacterHasSpecialTitle(updatedSession.Character, titleId);
        Assert.True(sentTitle || storedTitle, $"Expected ChampionsOfWarr reward to grant special title {titleId}.");
    }

    private static void AssertChampionsOfWarrRoundOwnerCleared(WorldSession updatedSession)
    {
        AssertChampionsOfWarrRoundOwnerAttributeCleared(updatedSession, "LegacyChampionsOfWarrRoundOwnerGuildID");
        AssertChampionsOfWarrRoundOwnerAttributeCleared(updatedSession, "LegacyChampionsOfWarrRoundOwnerGuildId");
        AssertChampionsOfWarrRoundOwnerAttributeCleared(updatedSession, "ChampionsOfWarrRoundOwnerGuildID");
        AssertChampionsOfWarrRoundOwnerAttributeCleared(updatedSession, "ChampionsOfWarrRoundOwnerGuildId");
        AssertChampionsOfWarrRoundOwnerAttributeCleared(updatedSession, "LegacyChampionsOfWarrRoundOwnerGuildName");
        AssertChampionsOfWarrRoundOwnerAttributeCleared(updatedSession, "ChampionsOfWarrRoundOwnerGuildName");
        AssertChampionsOfWarrRoundOwnerAttributeCleared(updatedSession, "LegacyChampionsOfWarrRoundOwnerScore");
        AssertChampionsOfWarrRoundOwnerAttributeCleared(updatedSession, "ChampionsOfWarrRoundOwnerScore");
    }

    private static void AssertChampionsOfWarrRoundOwnerAttributeCleared(WorldSession updatedSession, string key)
    {
        var attributes = updatedSession.Character.Attributes;
        var value = attributes.TryGetValue(key, out var raw) ? raw : "<missing>";
        Assert.False(
            HasNonEmptyAttribute(attributes, key),
            $"Expected source RoundOwner clear to remove or zero '{key}' after ChampionsOfWarr reward claim, but value was '{value}'.");
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

    private static bool MessageMentionsChampionWarReward(LegacyMsgMessagePacket message)
    {
        var text = string.Join(' ', message.Content);
        return text.Contains(ChampionsOfWarrRewardConquerPoints.ToString(), StringComparison.Ordinal) &&
            (text.Contains("ChampionsOfWar", StringComparison.Ordinal) ||
                text.Contains("Champions of War", StringComparison.Ordinal));
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

    private static void AssertLegacyTeleport(IReadOnlyList<byte[]> sent, uint expectedClientMapId, uint expectedX, uint expectedY)
    {
        var teleport = Assert.Single(ParseMsgActions(sent), action => action.Type == MsgActionType.Teleport);
        Assert.Equal(expectedClientMapId, teleport.DwParam);
        Assert.Equal(expectedX, teleport.PositionX);
        Assert.Equal(expectedY, teleport.PositionY);
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

    private static IReadOnlyList<LegacyGameItemPacket> ParseGameItems(IEnumerable<byte[]> sent)
    {
        var packets = new List<LegacyGameItemPacket>();
        foreach (var bytes in sent)
        {
            if (LegacyGameItemPacket.TryParse(ReadFrame(bytes), out var packet))
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

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }

    private static bool IsAscii(string value)
    {
        return value.All(static character => character <= 127);
    }

    private sealed record ChampionsOfWarrRewardResult(
        bool Handled,
        IReadOnlyList<byte[]> Sent,
        WorldSession UpdatedSession,
        string Log);
}
