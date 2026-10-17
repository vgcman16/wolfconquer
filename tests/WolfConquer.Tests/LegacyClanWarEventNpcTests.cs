using System.Buffers;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyClanWarEventNpcTests
{
    private const uint ClanDominationNpcId = 15_554;
    private const uint ClanWarScoreNpcId = 6_526;
    private const uint ClanWarScoreRewardItemId = 4_300_004;
    private const long GuildWarScoreRewardConquerPoints = 300_000;

    [Fact]
    public async Task ClanDominationIntroSendsTranslatedRewardOptions()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(mapId: 1_002);

        var handled = await WorldPacketHandlers.TryHandleLegacyClanWarEventNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session),
            session,
            ClanDominationNpcId,
            optionId: 0,
            CancellationToken.None);

        Assert.True(handled);
        var replies = ParseNpcReplies(sent);
        Assert.Contains(replies, reply => reply.Text == "This is the Clan Domination reward center.");
        Assert.Contains(replies, reply => reply.Text == "Only the winning clan leader can claim a flag reward. The war starts at 22:00.");
        Assert.Contains(replies, reply => reply.Text == "Claim the Twin City flag reward." && reply.OptionId == 1);
        Assert.Contains(replies, reply => reply.Text == "Claim the Phoenix Castle flag reward." && reply.OptionId == 2);
        Assert.Contains(replies, reply => reply.Text == "Claim the Ape City flag reward." && reply.OptionId == 3);
        Assert.Contains(replies, reply => reply.Text == "Claim the Desert City flag reward." && reply.OptionId == 4);
        Assert.Contains(replies, reply => reply.Text == "Claim the Bird Island flag reward." && reply.OptionId == 5);
        Assert.All(replies.Where(reply => !string.IsNullOrEmpty(reply.Text)), reply => Assert.True(IsAscii(reply.Text), reply.Text));
        Assert.Contains("source=NpcHandler.ClanDomination", log.ToString());
    }

    [Fact]
    public async Task ClanWarScoreIntroSendsTranslatedOptions()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(mapId: 1_002);

        var handled = await WorldPacketHandlers.TryHandleLegacyClanWarEventNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session),
            session,
            ClanWarScoreNpcId,
            optionId: 0,
            CancellationToken.None);

        Assert.True(handled);
        var replies = ParseNpcReplies(sent);
        Assert.Contains(replies, reply => reply.Text == "Hi, I manage Clan War Score. What can I do for you? The event runs from 16:30 to 16:59.");
        Assert.Contains(replies, reply => reply.Text == "Enter" && reply.OptionId == 1);
        Assert.Contains(replies, reply => reply.Text == "Claim reward" && reply.OptionId == 2);
        Assert.Contains(replies, reply => reply.Text == "Just looking around." && reply.OptionId == LegacyNpcReplyPacket.ClosedOptionId);
        Assert.All(replies.Where(reply => !string.IsNullOrEmpty(reply.Text)), reply => Assert.True(IsAscii(reply.Text), reply.Text));
    }

    [Fact]
    public async Task ClanWarScoreEnterTeleportsWhenSourceProcessIsAlive()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            mapId: 1_002,
            x: 430,
            y: 378,
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["LegacyClanWarScoreProcess"] = "Alive",
            });
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacyClanWarEventNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            session,
            ClanWarScoreNpcId,
            optionId: 1,
            CancellationToken.None);

        Assert.True(handled);
        var teleport = Assert.Single(ParseMsgActions(sent), action => action.Type == MsgActionType.Teleport);
        Assert.Equal(3_839U, teleport.DwParam);
        Assert.Equal(100U, teleport.PositionX);
        Assert.Equal(100U, teleport.PositionY);

        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(3_839, updated.Character.MapId);
        Assert.Equal(100, updated.Character.X);
        Assert.Equal(100, updated.Character.Y);
        Assert.Contains("source=NpcHandler.ClanWarScore map=3839 x=100 y=100", log.ToString());
    }

    [Fact]
    public async Task ClanWarScoreRewardWithoutRuntimeStateKeepsPlayerSafe()
    {
        var result = await ClaimClanWarScoreRewardAsync();

        Assert.True(result.Handled);
        AssertClanWarScoreRewardNotGranted(result.Sent, result.UpdatedSession, expectedConquerPoints: 0);
        var replies = ParseNpcReplies(result.Sent);
        Assert.Contains(replies, reply => reply.Text == "There is no Guild War Score reward available now.");
        Assert.All(replies.Where(reply => !string.IsNullOrEmpty(reply.Text)), reply => Assert.True(IsAscii(reply.Text), reply.Text));
        Assert.Contains("source=NpcHandler.ClanWarScore", result.Log);
    }

    [Fact]
    public async Task ClanWarScoreRewardEligibleWinnerGrantsLegacyGuildWarScoreReward()
    {
        var result = await ClaimClanWarScoreRewardAsync(
            attributes: CreateClanWarScoreWinnerAttributes(),
            conquerPoints: 100);

        Assert.True(result.Handled);
        Assert.Equal(100 + GuildWarScoreRewardConquerPoints, result.UpdatedSession.Character.ConquerPoints);
        Assert.Contains(
            ParseMsgUpdates(result.Sent),
            packet => packet.Updates.Any(entry =>
                entry.Type == LegacyMsgUpdateDataType.ConquerPoints &&
                entry.Value == 100UL + (ulong)GuildWarScoreRewardConquerPoints));

        var sentRewardItem = ParseGameItems(result.Sent)
            .Any(item => item.ItemId == ClanWarScoreRewardItemId && item.Mode == LegacyGameItemPacket.AddItemMode);
        var storedRewardItem = result.UpdatedSession.PlayerItems.InventoryItems
            .Any(item => item.ItemId == ClanWarScoreRewardItemId);
        Assert.True(sentRewardItem || storedRewardItem, "Expected option 2 to grant item 4300004 to the winning guild leader.");

        Assert.Contains(
            ParseMessages(result.Sent),
            message => message.ChatType == LegacyMsgMessagePacket.CenterChatType &&
                message.Content.Any(part => part.Contains("GuildWarScore", StringComparison.Ordinal) &&
                    part.Contains(GuildWarScoreRewardConquerPoints.ToString(), StringComparison.Ordinal)));
        Assert.Contains("source=NpcHandler.ClanWarScore", result.Log);
        Assert.Contains("sourceReward=NewSystem.PrizeAllTops.GuildWarScore", result.Log);
    }

    [Fact]
    public async Task ClanWarScoreRewardNonLeaderIsDeniedWithSafeDialog()
    {
        var result = await ClaimClanWarScoreRewardAsync(
            attributes: CreateClanWarScoreWinnerAttributes(guildRank: "6"),
            conquerPoints: 100);

        Assert.True(result.Handled);
        AssertClanWarScoreRewardNotGranted(result.Sent, result.UpdatedSession, expectedConquerPoints: 100);
        var replies = ParseNpcReplies(result.Sent);
        Assert.Contains(replies, reply => reply.Text == "Only the guild leader can claim the reward.");
        Assert.All(replies.Where(reply => !string.IsNullOrEmpty(reply.Text)), reply => Assert.True(IsAscii(reply.Text), reply.Text));
    }

    [Fact]
    public async Task ClanWarScoreRewardWrongGuildIsDeniedWithSafeDialog()
    {
        var result = await ClaimClanWarScoreRewardAsync(
            attributes: CreateClanWarScoreWinnerAttributes(playerGuildId: "88", playerGuildName: "RivalGuild"),
            conquerPoints: 100);

        Assert.True(result.Handled);
        AssertClanWarScoreRewardNotGranted(result.Sent, result.UpdatedSession, expectedConquerPoints: 100);
        var replies = ParseNpcReplies(result.Sent);
        Assert.Contains(replies, reply => reply.Text == "You do not have any reward to claim.");
        Assert.All(replies.Where(reply => !string.IsNullOrEmpty(reply.Text)), reply => Assert.True(IsAscii(reply.Text), reply.Text));
    }

    [Fact]
    public async Task ClanWarScoreRewardDuplicateClaimDoesNotDoublePay()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            mapId: 1_002,
            attributes: CreateClanWarScoreWinnerAttributes(),
            conquerPoints: 100);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var firstHandled = await ClickClanWarScoreRewardAsync(connection, log, sessions, session);

        Assert.True(firstHandled);
        Assert.True(sessions.TryGet(connection, out var firstUpdated));
        Assert.NotNull(firstUpdated);
        Assert.Equal(100 + GuildWarScoreRewardConquerPoints, firstUpdated.Character.ConquerPoints);
        var firstRewardItemCount = firstUpdated.PlayerItems.InventoryItems.Count(item => item.ItemId == ClanWarScoreRewardItemId);

        sent.Clear();
        var secondHandled = await ClickClanWarScoreRewardAsync(connection, log, sessions, firstUpdated);

        Assert.True(secondHandled);
        Assert.True(sessions.TryGet(connection, out var secondUpdated));
        Assert.NotNull(secondUpdated);
        Assert.Equal(100 + GuildWarScoreRewardConquerPoints, secondUpdated.Character.ConquerPoints);
        Assert.Equal(
            firstRewardItemCount,
            secondUpdated.PlayerItems.InventoryItems.Count(item => item.ItemId == ClanWarScoreRewardItemId));
        Assert.DoesNotContain(
            ParseMsgUpdates(sent),
            packet => packet.Updates.Any(entry =>
                entry.Type is LegacyMsgUpdateDataType.Conquer or LegacyMsgUpdateDataType.ConquerPoints));
    }

    [Fact]
    public async Task ClanWarScoreRewardPersistsWalletTitlesClaimAndItem()
    {
        var root = Path.Combine(Path.GetTempPath(), "wolfconquer-clanwar-score-" + Guid.NewGuid().ToString("N"));
        var usersRoot = Path.Combine(root, "Users");
        var playerItemsRoot = Path.Combine(root, "PlayerItems");
        Directory.CreateDirectory(usersRoot);
        Directory.CreateDirectory(playerItemsRoot);
        try
        {
            var userPath = Path.Combine(usersRoot, "100.ini");
            await File.WriteAllTextAsync(
                userPath,
                """
                [Character]
                Name=ClanHero
                UID=1293601
                ConquerPoints=100
                SpecialTitles=0,0,
                SpecialTitleID=0
                SpecialWingID=0
                SpecialTitleScore=0
                """);

            var result = await ClaimClanWarScoreRewardAsync(
                attributes: CreateClanWarScoreWinnerAttributes(),
                conquerPoints: 100,
                usersRoot: usersRoot,
                playerItemsRoot: playerItemsRoot);

            var persisted = await File.ReadAllTextAsync(userPath);
            var persistedItems = new PlayerItemSnapshotLoader(playerItemsRoot)
                .LoadByCharacterUid((uint)result.UpdatedSession.Character.Uid);

            Assert.Equal(100 + GuildWarScoreRewardConquerPoints, result.UpdatedSession.Character.ConquerPoints);
            Assert.Contains($"ConquerPoints={100 + GuildWarScoreRewardConquerPoints}", persisted);
            Assert.Contains("LegacyGuildWarScoreRewardClaimed=true", persisted);
            Assert.Contains("SpecialTitles=", persisted);
            Assert.Contains("8045,1", persisted);
            Assert.Contains("6003,1", persisted);
            Assert.Contains("2024,1", persisted);
            Assert.Contains("SpecialTitleID=2024", persisted);
            Assert.Contains("SpecialWingID=6003", persisted);
            Assert.Contains(persistedItems.InventoryItems, item => item.ItemId == ClanWarScoreRewardItemId);
            Assert.Contains("WorldServer persisted LegacyNpc ClanWarScore reward", result.Log);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static WorldSession CreateSession(
        long mapId,
        long x = 50,
        long y = 50,
        Dictionary<string, string>? attributes = null,
        long conquerPoints = 0,
        string? usersRoot = null)
    {
        attributes ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        attributes.TryAdd("DynamicID", "0");

        return new WorldSession(
            AccountId: 100,
            Username: "clan-war-event-test",
            Character: new CharacterSnapshot(
                Uid: 1_293_601,
                Name: "ClanHero",
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
            PlayerProficiencies: PlayerProficiencySnapshot.Empty,
            UsersRoot: usersRoot);
    }

    private static Dictionary<string, string> CreateClanWarScoreWinnerAttributes(
        string playerGuildId = "77",
        string playerGuildName = "SourceGuild",
        string guildRank = "1",
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

    private static async Task<ClanWarScoreRewardResult> ClaimClanWarScoreRewardAsync(
        Dictionary<string, string>? attributes = null,
        long conquerPoints = 0,
        string? usersRoot = null,
        string? playerItemsRoot = null)
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            mapId: 1_002,
            attributes: attributes,
            conquerPoints: conquerPoints,
            usersRoot: usersRoot);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await ClickClanWarScoreRewardAsync(connection, log, sessions, session, playerItemsRoot);

        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        return new ClanWarScoreRewardResult(handled, sent, updated, log.ToString());
    }

    private static async Task<bool> ClickClanWarScoreRewardAsync(
        ConquerTcpConnection connection,
        StringWriter log,
        WorldSessionStore sessions,
        WorldSession session,
        string? playerItemsRoot = null)
    {
        return await WorldPacketHandlers.TryHandleLegacyClanWarEventNpcAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                Session: session,
                Sessions: sessions,
                PlayerItemsRoot: playerItemsRoot),
            session,
            ClanWarScoreNpcId,
            optionId: 2,
            CancellationToken.None);
    }

    private static void AssertClanWarScoreRewardNotGranted(
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
            item => item.ItemId == ClanWarScoreRewardItemId);
        Assert.DoesNotContain(
            updatedSession.PlayerItems.InventoryItems,
            item => item.ItemId == ClanWarScoreRewardItemId);
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

    private sealed record ClanWarScoreRewardResult(
        bool Handled,
        IReadOnlyList<byte[]> Sent,
        WorldSession UpdatedSession,
        string Log);
}
