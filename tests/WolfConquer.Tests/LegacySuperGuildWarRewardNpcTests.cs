using System.Buffers;
using System.Globalization;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("LegacySuperGuildWarRuntime")]
public sealed class LegacySuperGuildWarRewardNpcTests
{
    private const uint SuperGuildWarNpcId = 25_015;
    private const long SuperGuildWarLeaderRewardConquerPoints = 300_000;
    private const long SuperGuildWarLeaderRewardMoney = 500_000;
    private const uint SuperGuildWarRewardItemId = 2_100_065;
    private const int TopSuperGuildWarFiveStarsFlag = 151;
    private const int TopSuperGuildWarThreeStarsFlag = 152;

    [Fact]
    public async Task WinningGuildLeaderAfterEventEndedClaimsRewardAndSessionClaimState()
    {
        WorldPacketHandlers.ResetLegacySuperGuildWarStateForTests();
        var result = await ClaimSuperGuildWarRewardAsync(
            guildId: 77,
            guildRank: 1,
            conquerPoints: 100,
            money: 200);

        Assert.True(result.Handled);
        Assert.Equal(100 + SuperGuildWarLeaderRewardConquerPoints, result.UpdatedSession.Character.ConquerPoints);
        Assert.Equal(200 + SuperGuildWarLeaderRewardMoney, result.UpdatedSession.Character.Money);
        Assert.Contains(
            ParseMsgUpdates(result.Sent),
            packet => packet.Updates.Any(entry =>
                entry.Type == LegacyMsgUpdateDataType.ConquerPoints &&
                entry.Value == 100UL + (ulong)SuperGuildWarLeaderRewardConquerPoints));
        Assert.Contains(
            ParseMsgUpdates(result.Sent),
            packet => packet.Updates.Any(entry =>
                entry.Type == LegacyMsgUpdateDataType.Money &&
                entry.Value == 200UL + (ulong)SuperGuildWarLeaderRewardMoney));

        Assert.Contains(result.UpdatedSession.PlayerItems.InventoryItems, item => item.ItemId == SuperGuildWarRewardItemId);
        Assert.Contains(ParseGameItems(result.Sent), item => item.ItemId == SuperGuildWarRewardItemId);
        AssertSuperGuildWarFlagGranted(result.UpdatedSession, TopSuperGuildWarFiveStarsFlag, "TopSuperGuildWarFiveStars");
        AssertSuperGuildWarClaimMarked(result.UpdatedSession, "Leader");
        Assert.Contains("source=NpcHandler.SuperGuildWar", result.Log);
        Assert.DoesNotContain("PARITY-GAP LegacyNpc CompetitionManagers", result.Log);
    }

    [Fact]
    public async Task WinningGuildDeputyAfterEventEndedClaimsThreeStarsFlagOnly()
    {
        WorldPacketHandlers.ResetLegacySuperGuildWarStateForTests();
        var result = await ClaimSuperGuildWarRewardAsync(
            guildId: 77,
            guildRank: 2,
            conquerPoints: 100,
            money: 200);

        Assert.True(result.Handled);
        Assert.Equal(100, result.UpdatedSession.Character.ConquerPoints);
        Assert.Equal(200, result.UpdatedSession.Character.Money);
        Assert.DoesNotContain(result.UpdatedSession.PlayerItems.InventoryItems, item => item.ItemId == SuperGuildWarRewardItemId);
        Assert.DoesNotContain(ParseGameItems(result.Sent), item => item.ItemId == SuperGuildWarRewardItemId);
        Assert.DoesNotContain(
            ParseMsgUpdates(result.Sent),
            packet => packet.Updates.Any(entry =>
                entry.Type is LegacyMsgUpdateDataType.Conquer or
                    LegacyMsgUpdateDataType.ConquerPoints or
                    LegacyMsgUpdateDataType.Money));
        AssertSuperGuildWarFlagGranted(result.UpdatedSession, TopSuperGuildWarThreeStarsFlag, "TopSuperGuildWarThreeStars");
        AssertSuperGuildWarFlagNotGranted(result.UpdatedSession, TopSuperGuildWarFiveStarsFlag, "TopSuperGuildWarFiveStars");
        AssertSuperGuildWarClaimMarked(result.UpdatedSession, "Deputy");
        Assert.Contains("source=NpcHandler.SuperGuildWar", result.Log);
        Assert.DoesNotContain("PARITY-GAP LegacyNpc CompetitionManagers", result.Log);
    }

    [Theory]
    [InlineData(88UL, 1UL)]
    [InlineData(77UL, 6UL)]
    public async Task NonWinnerOrUnsupportedRankDoesNotMutateRewardState(ulong guildId, ulong guildRank)
    {
        WorldPacketHandlers.ResetLegacySuperGuildWarStateForTests();
        var result = await ClaimSuperGuildWarRewardAsync(
            guildId: guildId,
            guildRank: guildRank,
            conquerPoints: 100,
            money: 200);

        Assert.True(result.Handled);
        AssertSuperGuildWarRewardNotGranted(result, expectedConquerPoints: 100, expectedMoney: 200);
        var replies = ParseNpcReplies(result.Sent);
        Assert.Contains(replies, reply => reply.Text is not null && reply.Text.Contains("reward", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(replies, reply => reply.Text == "Super Guild War rewards are not available from this runtime yet.");
        Assert.All(replies.Where(reply => !string.IsNullOrEmpty(reply.Text)), reply => Assert.True(IsAscii(reply.Text), reply.Text));
        Assert.Contains("source=NpcHandler.SuperGuildWar", result.Log);
        Assert.DoesNotContain("PARITY-GAP LegacyNpc CompetitionManagers", result.Log);
    }

    [Fact]
    public async Task DuplicateLeaderClaimDoesNotDoublePay()
    {
        WorldPacketHandlers.ResetLegacySuperGuildWarStateForTests();
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(guildId: 77, guildRank: 1, conquerPoints: 100, money: 200);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var worldData = CreateWorldDataCatalog(CreateSuperGuildWarInfo(winnerGuildId: 77, leaderReward: 1));

        var firstHandled = await ClickSuperGuildWarRewardAsync(connection, log, sessions, session, worldData);

        Assert.True(firstHandled);
        Assert.True(sessions.TryGet(connection, out var firstUpdated));
        Assert.NotNull(firstUpdated);
        Assert.Equal(100 + SuperGuildWarLeaderRewardConquerPoints, firstUpdated.Character.ConquerPoints);
        Assert.Equal(200 + SuperGuildWarLeaderRewardMoney, firstUpdated.Character.Money);
        Assert.Contains(firstUpdated.PlayerItems.InventoryItems, item => item.ItemId == SuperGuildWarRewardItemId);
        var firstItemCount = firstUpdated.PlayerItems.InventoryItems.Count(item => item.ItemId == SuperGuildWarRewardItemId);

        sent.Clear();
        var secondHandled = await ClickSuperGuildWarRewardAsync(connection, log, sessions, firstUpdated, worldData);

        Assert.True(secondHandled);
        Assert.True(sessions.TryGet(connection, out var secondUpdated));
        Assert.NotNull(secondUpdated);
        Assert.Equal(firstUpdated.Character.ConquerPoints, secondUpdated.Character.ConquerPoints);
        Assert.Equal(firstUpdated.Character.Money, secondUpdated.Character.Money);
        Assert.Equal(firstItemCount, secondUpdated.PlayerItems.InventoryItems.Count(item => item.ItemId == SuperGuildWarRewardItemId));
        Assert.DoesNotContain(
            ParseMsgUpdates(sent),
            packet => packet.Updates.Any(entry =>
                entry.Type is LegacyMsgUpdateDataType.Conquer or
                    LegacyMsgUpdateDataType.ConquerPoints or
                    LegacyMsgUpdateDataType.Money));
        Assert.DoesNotContain(ParseGameItems(sent), item => item.ItemId == SuperGuildWarRewardItemId);
        AssertSuperGuildWarFlagGranted(secondUpdated, TopSuperGuildWarFiveStarsFlag, "TopSuperGuildWarFiveStars");
    }

    private static async Task<SuperGuildWarRewardResult> ClaimSuperGuildWarRewardAsync(
        ulong guildId,
        ulong guildRank,
        long conquerPoints = 0,
        long money = 0)
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(guildId, guildRank, conquerPoints, money);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var worldData = CreateWorldDataCatalog(CreateSuperGuildWarInfo(winnerGuildId: 77, leaderReward: 1));

        var handled = await ClickSuperGuildWarRewardAsync(connection, log, sessions, session, worldData);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        return new SuperGuildWarRewardResult(handled, sent, log.ToString(), updatedSession);
    }

    private static async Task<bool> ClickSuperGuildWarRewardAsync(
        ConquerTcpConnection connection,
        StringWriter log,
        WorldSessionStore sessions,
        WorldSession session,
        WorldDataCatalog worldData)
    {
        return await WorldPacketHandlers.TryHandleLegacyCompetitionManagersNpcAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                Session: session,
                WorldData: worldData,
                Sessions: sessions),
            session,
            SuperGuildWarNpcId,
            optionId: 7,
            now: new DateTime(2026, 6, 23, 22, 5, 0),
            CancellationToken.None);
    }

    private static WorldSession CreateSession(
        ulong guildId,
        ulong guildRank,
        long conquerPoints,
        long money)
    {
        var attributes = CreateSuperGuildWarWinnerAttributes(guildId, guildRank);

        return new WorldSession(
            AccountId: 100,
            Username: "super-guild-war-reward-test",
            Character: new CharacterSnapshot(
                Uid: 1_293_800,
                Name: "SuperGuildHero",
                Body: 1003,
                Face: 0,
                Hair: 0,
                Class: 7051,
                FirstClass: 7051,
                SecondClass: 0,
                Reborn: 0,
                Level: 140,
                Experience: 0,
                MapId: 1_002,
                X: 430,
                Y: 378,
                PreviousMapId: 1_002,
                PreviousMapX: 430,
                PreviousMapY: 378,
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
                PkPoints: 0,
                Online: 0,
                SourceFile: "fixture",
                Attributes: attributes,
                PaidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)),
            PlayerItems: PlayerItemSnapshot.Empty,
            PlayerSpells: PlayerSpellSnapshot.Empty,
            PlayerProficiencies: PlayerProficiencySnapshot.Empty);
    }

    private static Dictionary<string, string> CreateSuperGuildWarWinnerAttributes(
        ulong guildId,
        ulong guildRank,
        ulong winnerGuildId = 77)
    {
        var guildIdText = guildId.ToString(CultureInfo.InvariantCulture);
        var guildRankText = guildRank.ToString(CultureInfo.InvariantCulture);
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["DynamicID"] = "0",
            ["GuildID"] = guildIdText,
            ["GuildId"] = guildIdText,
            ["MyGuildID"] = guildIdText,
            ["MyGuildId"] = guildIdText,
            ["GuildName"] = guildId == winnerGuildId ? "WinnerGuild" : "RivalGuild",
            ["MyGuildName"] = guildId == winnerGuildId ? "WinnerGuild" : "RivalGuild",
            ["GuildRank"] = guildRankText,
            ["GuildMemberRank"] = guildRankText,
        };

        SetSuperGuildWarAttributeTriplet(attributes, "Process", "Dead");
        SetSuperGuildWarAttributeTriplet(attributes, "Proces", "Dead");
        SetSuperGuildWarAttributeTriplet(attributes, "WinnerGuildID", winnerGuildId.ToString(CultureInfo.InvariantCulture));
        SetSuperGuildWarAttributeTriplet(attributes, "WinnerGuildId", winnerGuildId.ToString(CultureInfo.InvariantCulture));
        SetSuperGuildWarAttributeTriplet(attributes, "RoundOwnerGuildID", winnerGuildId.ToString(CultureInfo.InvariantCulture));
        SetSuperGuildWarAttributeTriplet(attributes, "RoundOwnerGuildId", winnerGuildId.ToString(CultureInfo.InvariantCulture));
        SetSuperGuildWarAttributeTriplet(attributes, "WinnerGuildName", "WinnerGuild");
        SetSuperGuildWarAttributeTriplet(attributes, "RoundOwnerGuildName", "WinnerGuild");
        return attributes;
    }

    private static void SetSuperGuildWarAttributeTriplet(
        Dictionary<string, string> attributes,
        string suffix,
        string value)
    {
        string[] aliases = ["LegacySuperGuildWar", "MsgSuperGuildWar", "SuperGuildWar", "MsgSchedulesSuperGuildWar"];
        foreach (var alias in aliases)
        {
            attributes[alias + suffix] = value;
        }
    }

    private static WorldDataCatalog CreateWorldDataCatalog(GuildWarData superGuildWarInfo)
    {
        return WorldDataCatalog.Create(
            new SeedManifest(
                DateTimeOffset.Parse("2026-06-20T00:00:00Z", CultureInfo.InvariantCulture),
                "fixture://legacy-super-guild-war-reward-npc",
                new Dictionary<string, int>()),
            clientConfigs: [],
            items: [],
            itemAddBonuses: [],
            levelExperience: [],
            runeLevelExperience: [],
            runeStorageAttributes: [],
            itemRefineEffects: [],
            itemRefineEffectEx: [],
            itemRefineUpgrades: [],
            transformInfo: [],
            furniture: [],
            hairColorTypes: [],
            hairFaceStorageTypes: [],
            coatStorageTypes: [],
            coatStorageAttributes: [],
            coatColorRules: [],
            xuanbaoAdditionAttributes: [],
            xuanbaoComposeAttributeLimits: [],
            xuanbaoRandomAttributes: [],
            christmasNpcs: [],
            tutorTypes: [],
            tutorBattleLimitTypes: [],
            beastAttributes: [],
            fateExperience: [],
            spiritRates: [],
            hundredWeapons: [],
            gouyuTypes: [],
            gouyuImmortals: [],
            specialEffectAttributes: [],
            daoqiDictTypes: [],
            syndicateFormationTypes: [],
            syndicateSkillTypes: [],
            disdainRules: [],
            jiangHuAttributes: [],
            jiangHuCultivateStatuses: [],
            refineryBoxes: [],
            refinerySouls: [],
            refineryMaterials: [],
            stateCountFiles: [],
            binarySeedFiles: [],
            emptySeedFiles: [],
            backupSeedFiles: [],
            pokerTables: [],
            rouletteTables: [],
            npcTimeEntries: [],
            activityTasks: new ActivityTaskCatalog(new ActivityTaskMetadata(0, "", 0, []), [], "fixture://activity"),
            meltingTypeRules: [],
            lotteryPrizes: [],
            arenaRecords: [],
            elitePkRecords: [],
            teamElitePkRecords: [],
            skillTeamPkRecords: [],
            classPkWarRecords: [],
            combatGearRanks: [],
            prestigeRanks: [],
            innerPowerRanks: [],
            runeRankings: [],
            staticStatues: [],
            newPlayerRewards: [],
            votes: [],
            couplesPkRecords: [],
            socialAssociations: [],
            guildWarInfo: CreateSuperGuildWarInfo(winnerGuildId: 0),
            superGuildWarInfo: superGuildWarInfo,
            classLevelStats: [],
            professionLevelUps: [],
            professionLevelBenefits: [],
            professionTitleBenefits: [],
            questInfo: new QuestInfoMetadata(ActionBase: null, RecommendInterval: null, TotalMission: null),
            questTaskTypes: [],
            questMissions: [],
            quizQuestions: [],
            magic: [],
            magicEffects: [],
            magicTypeOperations: [],
            titleTypes: [],
            combatGear: [],
            traps: [],
            npcs: [],
            sobNpcs: [],
            portals: [],
            maps: [],
            mapNames: [],
            monsters: [],
            monsterSpawns: [],
            lotteryDictionary: [],
            collectionRewardTiers: [],
            taskRewardTypes: [],
            taskRewardTypeIniEntries: [],
            neiGongHeader: new NeiGongHeader(0, [], "fixture://neigong", 0, "", new Dictionary<string, string>()),
            neiGongSecrets: [],
            neiGongTypes: [],
            subProfessions: [],
            syndicateLevels: [],
            stageGoalInfo: new StageGoalInfo(0),
            stageGoalStages: [],
            stageGoalTasks: [],
            shops: [],
            shopItems: [],
            exchangeShopGoods: [],
            exchangeShopGoodsEx: [],
            exchangeShopLevels: [],
            honorShopItems: [],
            honorShopRecommendations: [],
            racePointShopItems: [],
            racePointShopRecommendations: [],
            goldenLeagueShopItems: [],
            goldenLeagueShopRecommendations: [],
            goldenLeagueShopConfig: new Dictionary<string, string>(),
            booths: [],
            boothItems: []);
    }

    private static GuildWarData CreateSuperGuildWarInfo(
        int? winnerGuildId,
        int? leaderReward = 1,
        int? deputyReward = 7,
        IReadOnlyDictionary<int, int>? leaderTop = null,
        IReadOnlyDictionary<int, int>? deputyTop = null)
    {
        return new GuildWarData(
            "fixture://SuperGuildWarInfo.ini",
            [],
            [],
            [],
            new GuildWarInfoData(
                winnerGuildId,
                Name: winnerGuildId is > 0 ? "WinnerGuild" : null,
                LeaderReward: leaderReward,
                DeputiLeaderReward: deputyReward,
                LeaderTop: leaderTop ?? new Dictionary<int, int>(),
                DeputiTop: deputyTop ?? new Dictionary<int, int>()),
            pole: null,
            conductors: []);
    }

    private static void AssertSuperGuildWarRewardNotGranted(
        SuperGuildWarRewardResult result,
        long expectedConquerPoints,
        long expectedMoney)
    {
        Assert.Equal(expectedConquerPoints, result.UpdatedSession.Character.ConquerPoints);
        Assert.Equal(expectedMoney, result.UpdatedSession.Character.Money);
        Assert.DoesNotContain(result.UpdatedSession.PlayerItems.InventoryItems, item => item.ItemId == SuperGuildWarRewardItemId);
        Assert.DoesNotContain(ParseGameItems(result.Sent), item => item.ItemId == SuperGuildWarRewardItemId);
        Assert.DoesNotContain(
            ParseMsgUpdates(result.Sent),
            packet => packet.Updates.Any(entry =>
                entry.Type is LegacyMsgUpdateDataType.Conquer or
                    LegacyMsgUpdateDataType.ConquerPoints or
                    LegacyMsgUpdateDataType.Money));
        AssertSuperGuildWarFlagNotGranted(result.UpdatedSession, TopSuperGuildWarFiveStarsFlag, "TopSuperGuildWarFiveStars");
        AssertSuperGuildWarFlagNotGranted(result.UpdatedSession, TopSuperGuildWarThreeStarsFlag, "TopSuperGuildWarThreeStars");
        Assert.DoesNotContain(
            result.UpdatedSession.Character.Attributes,
            pair => IsSuperGuildWarClaimMarker(pair.Key, pair.Value, rankName: null));
    }

    private static void AssertSuperGuildWarClaimMarked(WorldSession updatedSession, string rankName)
    {
        Assert.Contains(
            updatedSession.Character.Attributes,
            pair => IsSuperGuildWarClaimMarker(pair.Key, pair.Value, rankName));
    }

    private static bool IsSuperGuildWarClaimMarker(string key, string value, string? rankName)
    {
        return key.Contains("SuperGuildWar", StringComparison.OrdinalIgnoreCase) &&
            key.Contains("Reward", StringComparison.OrdinalIgnoreCase) &&
            key.Contains("Claim", StringComparison.OrdinalIgnoreCase) &&
            (rankName is null || key.Contains(rankName, StringComparison.OrdinalIgnoreCase)) &&
            IsTrue(value);
    }

    private static void AssertSuperGuildWarFlagGranted(WorldSession updatedSession, int flag, string flagName)
    {
        Assert.True(
            HasLegacyStatusFlag(updatedSession.Character.Attributes, flag) ||
            HasNamedFlag(updatedSession.Character.Attributes, flagName),
            $"Expected SuperGuildWar title flag {flagName} ({flag}) to be present.");
    }

    private static void AssertSuperGuildWarFlagNotGranted(WorldSession updatedSession, int flag, string flagName)
    {
        Assert.False(
            HasLegacyStatusFlag(updatedSession.Character.Attributes, flag) ||
            HasNamedFlag(updatedSession.Character.Attributes, flagName),
            $"Expected SuperGuildWar title flag {flagName} ({flag}) to stay absent.");
    }

    private static bool HasLegacyStatusFlag(IReadOnlyDictionary<string, string> attributes, int flag)
    {
        return TryHasLegacyStatusFlag(attributes, "StatusFlags", flag, out var hasFlag) && hasFlag ||
            TryHasLegacyStatusFlag(attributes, "BitVector", flag, out hasFlag) && hasFlag ||
            TryHasLegacyStatusFlag(attributes, "LegacyStatusFlags", flag, out hasFlag) && hasFlag;
    }

    private static bool TryHasLegacyStatusFlag(
        IReadOnlyDictionary<string, string> attributes,
        string key,
        int flag,
        out bool hasFlag)
    {
        hasFlag = false;
        if (!attributes.TryGetValue(key, out var raw) || string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var wordIndex = flag / 32;
        var bitIndex = flag % 32;
        var words = raw.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (wordIndex >= words.Length || !ulong.TryParse(words[wordIndex], out var word))
        {
            return false;
        }

        hasFlag = (word & (1UL << bitIndex)) != 0;
        return true;
    }

    private static bool HasNamedFlag(IReadOnlyDictionary<string, string> attributes, string flagName)
    {
        return attributes.Any(pair =>
            pair.Key.Equals(flagName, StringComparison.OrdinalIgnoreCase) && IsTrue(pair.Value) ||
            pair.Key.Equals("Legacy" + flagName, StringComparison.OrdinalIgnoreCase) && IsTrue(pair.Value));
    }

    private static bool IsTrue(string value)
    {
        return value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("1", StringComparison.Ordinal);
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

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }

    private static bool IsAscii(string value)
    {
        return value.All(static character => character <= 127);
    }

    private sealed record SuperGuildWarRewardResult(
        bool Handled,
        IReadOnlyList<byte[]> Sent,
        string Log,
        WorldSession UpdatedSession);
}
