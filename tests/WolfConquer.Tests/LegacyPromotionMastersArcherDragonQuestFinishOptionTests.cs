using System.Buffers;
using System.Globalization;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyPromotionMastersArcherDragonQuestFinishOptionTests : IDisposable
{
    private const uint ArcherPromotionNpcId = 400;
    private const uint DragonWarriorPromotionNpcId = 17_126;
    private const byte QuestFinishOption = 111;
    private const long CharacterUid = 1_294_111;
    private const uint ArcherQuestFinishRewardItemId = 3_005_160;

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "WolfConquer-LegacyPromotionMastersArcherDragonQuestFinishOptionTests-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task ArcherOption111Class4000FinishesQuestRewardsItemAndRoutesToStarterPromotion()
    {
        var result = await HandleQuestFinishOptionAsync(ArcherPromotionNpcId, 4_000);

        AssertLegacyQuestFinishDialog(result, "I~want~to~get~promoted", expectedPromotionOption: 1, expectedAvatarId: 259);
        AssertArcherQuestFinishReward(result, expectedClass: 4_000);
        Assert.Contains("source=NpcHandler.PromotionArcher", result.Log);
        Assert.Contains("sourceRange=NpcHandler.cs:48729-48779", result.Log);
        Assert.Contains("finishLookup=NpcHandler.cs:48693", result.Log);
        Assert.Contains("GetFinishQuest(2001, class, map 1004)", result.Log);
        Assert.Contains("WorldServer handled LegacyNpc PromotionArcher quest-finish reward", result.Log);
    }

    [Theory]
    [InlineData(4_001)]
    [InlineData(4_002)]
    public async Task ArcherOption111PromotedClassesFinishQuestRewardItemAndRouteToAdvancedPromotion(long classCode)
    {
        var result = await HandleQuestFinishOptionAsync(ArcherPromotionNpcId, classCode);

        AssertLegacyQuestFinishDialog(result, "I~want~to~get~promoted", expectedPromotionOption: 33, expectedAvatarId: 259);
        AssertArcherQuestFinishReward(result, expectedClass: classCode);
        Assert.Contains("sourceRange=NpcHandler.cs:48729-48779", result.Log);
        Assert.Contains("GetFinishQuest(2001, class, map 1004)", result.Log);
    }

    [Fact]
    public async Task ArcherOption111AlreadyFinishedQuestRoutesWithoutDuplicateReward()
    {
        var result = await HandleQuestFinishOptionAsync(
            ArcherPromotionNpcId,
            4_001,
            questStatus: PlayerQuestStatus.Finished);

        AssertLegacyQuestFinishDialog(result, "I~want~to~get~promoted", expectedPromotionOption: 33, expectedAvatarId: 259);
        AssertNoMutation(result, expectedClass: 4_001);
        Assert.Contains("source FinishQuest returned false because quest is already finished", result.Log);
        Assert.DoesNotContain("WorldServer handled LegacyNpc PromotionArcher quest-finish reward", result.Log);
    }

    [Theory]
    [InlineData(8_000)]
    [InlineData(8_001)]
    [InlineData(8_002)]
    public async Task DragonWarriorOption111AlwaysRoutesToStarterPromotionWithoutMutation(long classCode)
    {
        var result = await HandleQuestFinishOptionAsync(DragonWarriorPromotionNpcId, classCode);

        AssertLegacyQuestFinishDialog(result, "Job~Promotion~2.", expectedPromotionOption: 3, expectedAvatarId: 259);
        AssertNoMutation(result, expectedClass: classCode);
        Assert.Contains("PARITY-GAP LegacyNpc PromotionLeeLong quest-finish reward skipped", result.Log);
        Assert.Contains("source=NpcHandler.PromotionLeeLong", result.Log);
        Assert.Contains("sourceRange=NpcHandler.cs:49693-49728", result.Log);
        Assert.Contains("finishLookup=NpcHandler.cs:49655", result.Log);
        Assert.Contains("GetFinishQuest(5, class, 6426)", result.Log);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private async Task<QuestFinishResult> HandleQuestFinishOptionAsync(
        uint npcId,
        long classCode,
        PlayerQuestStatus questStatus = PlayerQuestStatus.Accepted)
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(classCode, questStatus);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var playerItemsRoot = Path.Combine(_root, "items-" + npcId + "-" + classCode + "-" + questStatus);
        Directory.CreateDirectory(playerItemsRoot);

        var handled = await WorldPacketHandlers.TryHandleLegacyPromotionMastersNpcAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                Session: session,
                WorldData: CreateWorldDataCatalog(
                    CreateItemType(ArcherQuestFinishRewardItemId, "ArcherQuestPack", durability: 456),
                    CreateLevelExperience(level: 70, experience: 10_000_000, upLevTime: 1_000)),
                Sessions: sessions,
                PlayerItemsRoot: playerItemsRoot),
            session,
            npcId,
            QuestFinishOption,
            CancellationToken.None);

        Assert.True(handled);
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);

        return new QuestFinishResult(session, updated, sent, log.ToString(), playerItemsRoot);
    }

    private static void AssertLegacyQuestFinishDialog(
        QuestFinishResult result,
        string expectedOptionText,
        byte expectedPromotionOption,
        ushort expectedAvatarId)
    {
        var replies = ParseNpcReplies(result.Sent);

        Assert.Contains(
            replies,
            reply => reply.InteractType == LegacyNpcReplyInteractType.Dialog &&
                reply.Text == "Wow, look at you, you`ll be another hero in the world of Conquer! Here, you can learn some skills and get promoted! Do you want to learn more?");
        Assert.Contains(
            replies,
            reply => reply.InteractType == LegacyNpcReplyInteractType.Dialog &&
                reply.Text == "~get promoted here to start your life. Would you like to learn something about your class promotion and quests?.");
        Assert.Contains(
            replies,
            reply => reply.InteractType == LegacyNpcReplyInteractType.Option &&
                reply.Text == expectedOptionText &&
                reply.OptionId == expectedPromotionOption);
        Assert.Contains(
            replies,
            reply => reply.InteractType == LegacyNpcReplyInteractType.Avatar &&
                reply.InputMaxLength == expectedAvatarId);
        Assert.Contains("option=111", result.Log);
        Assert.Contains("item=3005160", result.Log);
        Assert.Contains("GainExpBall(300, angelwing)", result.Log);
        Assert.DoesNotContain("PromotionMaster mutation unavailable", result.Log);
    }

    private static void AssertArcherQuestFinishReward(QuestFinishResult result, long expectedClass)
    {
        Assert.Equal(expectedClass, result.UpdatedSession.Character.Class);
        Assert.Equal(9_123_456, result.UpdatedSession.Character.Experience);
        Assert.Equal(result.OriginalSession.Character.ConquerPoints, result.UpdatedSession.Character.ConquerPoints);

        var quest = Assert.Single(result.UpdatedSession.Character.PlayerQuests.Quests, entry => entry.Uid == 2_001U);
        Assert.Equal(PlayerQuestStatus.Finished, quest.Status);

        var rewardItem = Assert.Single(
            result.UpdatedSession.PlayerItems.InventoryItems,
            item => item.ItemId == ArcherQuestFinishRewardItemId);
        Assert.Equal((ushort)456, rewardItem.Durability);
        Assert.Equal((ushort)456, rewardItem.MaximumDurability);
        Assert.Contains(
            new PlayerItemSnapshotLoader(result.PlayerItemsRoot)
                .LoadByCharacterUid(CharacterUid)
                .InventoryItems,
            item => item.ItemId == ArcherQuestFinishRewardItemId);

        var gameItem = Assert.Single(ParseGameItems(result.Sent));
        Assert.Equal(ArcherQuestFinishRewardItemId, gameItem.ItemId);

        var questFinish = Assert.Single(ParseQuestListFrames(result.Sent), packet => packet.Mode == LegacyQuestListMode.FinishQuest);
        Assert.Equal(2_001U, questFinish.QuestId);
        Assert.Equal(LegacyQuestStatus.Finished, questFinish.Status);

        Assert.Contains(
            ParseMsgUpdates(result.Sent),
            packet => packet.Updates.Any(entry =>
                entry.Type == LegacyMsgUpdateDataType.Experience &&
                entry.Value == 9_123_456UL));
        Assert.Contains(
            ParseStringPackets(result.Sent),
            packet => packet.Id == LegacyStringId.Effect &&
                packet.Uid == CharacterUid &&
                packet.Strings.Contains("angelwing"));

        Assert.DoesNotContain("quest-finish reward block not modeled yet", result.Log);
        Assert.Contains("rewardItemId=3005160", result.Log);
        Assert.Contains("expBall=300", result.Log);
        Assert.Contains("quest finish persistence", result.Log);
    }

    private static void AssertNoMutation(QuestFinishResult result, long expectedClass)
    {
        Assert.Equal(expectedClass, result.UpdatedSession.Character.Class);
        Assert.Equal(result.OriginalSession.Character.Experience, result.UpdatedSession.Character.Experience);
        Assert.Equal(result.OriginalSession.Character.ConquerPoints, result.UpdatedSession.Character.ConquerPoints);
        Assert.Equal(result.OriginalSession.Character.PlayerQuests.Quests, result.UpdatedSession.Character.PlayerQuests.Quests);
        Assert.Equal(
            result.OriginalSession.PlayerItems.InventoryItems.Select(item => item.ItemId).ToArray(),
            result.UpdatedSession.PlayerItems.InventoryItems.Select(item => item.ItemId).ToArray());

        Assert.DoesNotContain(result.Sent, bytes => LegacyGameItemPacket.TryParse(ReadFrame(bytes), out _));
        Assert.DoesNotContain(result.Sent, bytes => LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out _));
        Assert.DoesNotContain(result.Sent, bytes => LegacyQuestListPacket.TryParse(ReadFrame(bytes), out _));
    }

    private static WorldSession CreateSession(long classCode, PlayerQuestStatus questStatus)
    {
        var quests = new PlayerQuestSnapshot(
            [
                new PlayerQuestSnapshotEntry(2_001, questStatus, 12, [3U]),
            ]);

        return new WorldSession(
            AccountId: 100,
            Username: "archer-dragon-quest-finish-option-test",
            Character: new CharacterSnapshot(
                Uid: CharacterUid,
                Name: "QuestFinishHero",
                Body: 1003,
                Face: 0,
                Hair: 0,
                Class: classCode,
                FirstClass: classCode,
                SecondClass: 0,
                Reborn: 0,
                Level: 70,
                Experience: 123_456,
                MapId: 1_004,
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
                Money: 0,
                ConquerPoints: 5_000,
                BoundConquerPoints: 0,
                BankConquerPoints: 0,
                PkPoints: 0,
                Online: 0,
                SourceFile: "fixture",
                Attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                PaidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase))
            {
                PlayerQuests = quests,
            },
            PlayerItems: Inventory(Item(2_000, 725_016)),
            PlayerSpells: PlayerSpellSnapshot.Empty,
            PlayerProficiencies: PlayerProficiencySnapshot.Empty);
    }

    private static PlayerItemSnapshot Inventory(params PlayerItemSnapshotItem[] items)
    {
        return PlayerItemSnapshot.Empty with
        {
            InventoryItems = items,
        };
    }

    private static PlayerItemSnapshotItem Item(uint uid, uint itemId)
    {
        return new PlayerItemSnapshotItem
        {
            Uid = uid,
            ItemId = itemId,
            Durability = 1,
            MaximumDurability = 1,
            Position = 0,
            StackSize = 1,
        };
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

    private static IReadOnlyList<LegacyQuestListPacket> ParseQuestListFrames(IEnumerable<byte[]> sent)
    {
        var packets = new List<LegacyQuestListPacket>();
        foreach (var bytes in sent)
        {
            if (LegacyQuestListPacket.TryParse(ReadFrame(bytes), out var packet))
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

    private static IReadOnlyList<LegacyStringPacket> ParseStringPackets(IEnumerable<byte[]> sent)
    {
        var packets = new List<LegacyStringPacket>();
        foreach (var bytes in sent)
        {
            if (LegacyStringPacket.TryParse(ReadFrame(bytes), out var packet))
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

    private static WorldDataCatalog CreateWorldDataCatalog(
        ItemType item,
        params LevelExperience[] levelExperience)
    {
        return WorldDataCatalog.Create(
            new SeedManifest(
                DateTimeOffset.Parse("2026-06-05T00:00:00Z", CultureInfo.InvariantCulture),
                "fixture://archer-quest-finish",
                new Dictionary<string, int>()),
            clientConfigs: [],
            items: [item],
            itemAddBonuses: [],
            levelExperience: levelExperience,
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
            guildWarInfo: EmptyGuildWarData("guildwar"),
            superGuildWarInfo: EmptyGuildWarData("superguildwar"),
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

    private static ItemType CreateItemType(uint itemTypeId, string name, ushort durability)
    {
        var fields = Enumerable.Repeat("0", 66).ToArray();
        fields[0] = itemTypeId.ToString(CultureInfo.InvariantCulture);
        fields[1] = name;
        fields[ItemType.DurabilityFieldIndex] = durability.ToString(CultureInfo.InvariantCulture);
        return new ItemType(checked((int)itemTypeId), name, fields);
    }

    private static LevelExperience CreateLevelExperience(int level, long experience, int upLevTime)
    {
        var fields = new[]
        {
            "0",
            level.ToString(CultureInfo.InvariantCulture),
            experience.ToString(CultureInfo.InvariantCulture),
            upLevTime.ToString(CultureInfo.InvariantCulture),
            "0",
        };
        return new LevelExperience(
            0,
            level,
            experience,
            upLevTime,
            MentorUpLevTime: 0,
            SourceLine: level,
            RawLine: string.Join(' ', fields),
            Fields: fields);
    }

    private static GuildWarData EmptyGuildWarData(string name)
    {
        return new GuildWarData("fixture://" + name, [], [], diagnostics: [], info: null, pole: null, conductors: []);
    }

    private sealed record QuestFinishResult(
        WorldSession OriginalSession,
        WorldSession UpdatedSession,
        List<byte[]> Sent,
        string Log,
        string PlayerItemsRoot);
}
