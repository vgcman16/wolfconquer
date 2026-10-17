using System.Buffers;
using System.Globalization;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyPromotionMastersMonkQuestFinishOptionTests : IDisposable
{
    private const uint MonkPromotionNpcId = 8_314;
    private const byte QuestFinishOption = 111;
    private const long CharacterUid = 1_293_602;
    private const uint FinishQuestId = 2_001;
    private const uint RewardItemId = 3_005_160;

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "WolfConquer-LegacyPromotionMastersMonkQuestFinishOptionTests-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Option111Class6000FinishesQuestGrantsRewardAndRoutesToStarterPromotion()
    {
        var result = await HandleQuestFinishOptionAsync(6_000, AcceptedQuest());

        AssertLegacyQuestFinishDialog(result, expectedPromotionOption: 1);
        AssertSourceQuestFinishReward(result, expectedClass: 6_000);
    }

    [Theory]
    [InlineData(6_001)]
    [InlineData(6_002)]
    public async Task Option111PromotedMonkFinishesQuestGrantsRewardAndRoutesToAdvancedPromotion(long classCode)
    {
        var result = await HandleQuestFinishOptionAsync(classCode, AcceptedQuest());

        AssertLegacyQuestFinishDialog(result, expectedPromotionOption: 22);
        AssertSourceQuestFinishReward(result, expectedClass: classCode);
    }

    [Fact]
    public async Task Option111AlreadyFinishedQuestSendsDialogWithoutReward()
    {
        var result = await HandleQuestFinishOptionAsync(
            6_001,
            new PlayerQuestSnapshot([
                new PlayerQuestSnapshotEntry(FinishQuestId, PlayerQuestStatus.Finished, Time: 12, Intentions: [3U]),
            ]));

        AssertLegacyQuestFinishDialog(result, expectedPromotionOption: 22);
        AssertNoRewardMutation(result, expectedClass: 6_001);
        Assert.Contains("source FinishQuest returned false because quest is already finished", result.Log);
    }

    private async Task<QuestFinishResult> HandleQuestFinishOptionAsync(
        long classCode,
        PlayerQuestSnapshot quests)
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var itemsRoot = Path.Combine(_root, "items-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(itemsRoot);
        var session = CreateSession(classCode, quests);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacyPromotionMastersNpcAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                Session: session,
                Sessions: sessions,
                WorldData: CreateWorldDataCatalog(
                    [CreateItemType(RewardItemId, "MonkQuestReward", durability: 77)],
                    [CreateLevelExperience(level: 70, experience: 1_200, upLevTime: 1_200)]),
                PlayerItemsRoot: itemsRoot),
            session,
            MonkPromotionNpcId,
            QuestFinishOption,
            CancellationToken.None);

        Assert.True(handled);
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        return new QuestFinishResult(session, updated, sent, log.ToString(), itemsRoot);
    }

    private static void AssertLegacyQuestFinishDialog(QuestFinishResult result, byte expectedPromotionOption)
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
                reply.Text == "I~want~to~get~promoted" &&
                reply.OptionId == expectedPromotionOption);
        Assert.Contains(
            replies,
            reply => reply.InteractType == LegacyNpcReplyInteractType.Avatar &&
                reply.InputMaxLength == 259);
    }

    private static void AssertSourceQuestFinishReward(QuestFinishResult result, long expectedClass)
    {
        Assert.Equal(expectedClass, result.UpdatedSession.Character.Class);
        Assert.Equal(0, result.OriginalSession.Character.Experience);
        Assert.Equal(900, result.UpdatedSession.Character.Experience);
        Assert.Equal(result.OriginalSession.Character.ConquerPoints, result.UpdatedSession.Character.ConquerPoints);

        Assert.True(result.UpdatedSession.Character.PlayerQuests.TryGetQuest(FinishQuestId, out var finishedQuest));
        Assert.Equal(PlayerQuestStatus.Finished, finishedQuest.Status);
        Assert.Equal(12u, finishedQuest.Time);

        var item = Assert.Single(result.UpdatedSession.PlayerItems.InventoryItems);
        Assert.Equal(RewardItemId, item.ItemId);
        Assert.Equal(77, item.Durability);
        Assert.Equal([RewardItemId], new PlayerItemSnapshotLoader(result.ItemsRoot)
            .LoadByCharacterUid(CharacterUid)
            .InventoryItems
            .Select(saved => saved.ItemId)
            .ToArray());

        Assert.Contains(result.Sent, bytes =>
            LegacyQuestListPacket.TryParse(ReadFrame(bytes), out var packet) &&
            packet.Mode == LegacyQuestListMode.FinishQuest &&
            packet.QuestId == FinishQuestId &&
            packet.Status == LegacyQuestStatus.Finished);
        Assert.Contains(result.Sent, bytes =>
            LegacyGameItemPacket.TryParse(ReadFrame(bytes), out var packet) &&
            packet.ItemId == RewardItemId &&
            packet.Durability == 77);
        Assert.Contains(result.Sent, bytes =>
            LegacyStringPacket.TryParse(ReadFrame(bytes), out var packet) &&
            packet.Strings.SequenceEqual(["angelwing"]));
        Assert.Contains(result.Sent, bytes =>
            LegacyMsgMessagePacket.TryParse(ReadFrame(bytes), out var packet) &&
            packet.Content[3] == "You have gained experience worth 0.5 exp ball(s).");
        Assert.Contains(result.Sent, bytes =>
            LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var packet) &&
            packet.Updates.Any(update =>
                update.Type == LegacyMsgUpdateDataType.Experience &&
                update.Value == 900));

        var logText = result.Log;
        Assert.Contains("WorldServer handled LegacyNpc PromotionMonk quest-finish reward", logText);
        Assert.Contains("source=NpcHandler.PromotionMonk", logText);
        Assert.Contains("sourceRange=NpcHandler.cs:43215-43247", logText);
        Assert.Contains("questId=2001", logText);
        Assert.Contains("rewardItemId=3005160", logText);
        Assert.Contains("QuestGUI.SendSinglePacket FinishQuest GainExpBall(600, sendMsg:false, None)", logText);
        Assert.Contains("PromotionMonk option 111 GainExpBall(300, sendMsg:true, angelwing)", logText);
        Assert.Contains("quest finish persistence", logText);
        Assert.DoesNotContain("quest-finish reward block unavailable", logText);
    }

    private static void AssertNoRewardMutation(QuestFinishResult result, long expectedClass)
    {
        Assert.Equal(expectedClass, result.UpdatedSession.Character.Class);
        Assert.Equal(result.OriginalSession.Character.Experience, result.UpdatedSession.Character.Experience);
        Assert.Equal(result.OriginalSession.Character.ConquerPoints, result.UpdatedSession.Character.ConquerPoints);
        Assert.Equal(PlayerQuestStatus.Finished, result.UpdatedSession.Character.PlayerQuests.Quests.Single().Status);
        Assert.Empty(result.UpdatedSession.PlayerItems.InventoryItems);

        Assert.DoesNotContain(result.Sent, bytes => LegacyGameItemPacket.TryParse(ReadFrame(bytes), out _));
        Assert.DoesNotContain(result.Sent, bytes => LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out _));
        Assert.DoesNotContain(result.Sent, bytes => LegacyQuestListPacket.TryParse(ReadFrame(bytes), out _));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static PlayerQuestSnapshot AcceptedQuest()
    {
        return new PlayerQuestSnapshot([
            new PlayerQuestSnapshotEntry(FinishQuestId, PlayerQuestStatus.Accepted, Time: 12, Intentions: [3U]),
        ]);
    }

    private static WorldSession CreateSession(long classCode, PlayerQuestSnapshot quests)
    {
        return new WorldSession(
            AccountId: 100,
            Username: "monk-quest-finish-option-test",
            Character: new CharacterSnapshot(
                Uid: CharacterUid,
                Name: "MonkQuestHero",
                Body: 1003,
                Face: 0,
                Hair: 0,
                Class: classCode,
                FirstClass: classCode,
                SecondClass: 0,
                Reborn: 0,
                Level: 70,
                Experience: 0,
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

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }

    private static WorldDataCatalog CreateWorldDataCatalog(
        IReadOnlyList<ItemType> items,
        IReadOnlyList<LevelExperience> levelExperience)
    {
        return WorldDataCatalog.Create(
            new SeedManifest(
                DateTimeOffset.Parse("2026-06-05T00:00:00Z", CultureInfo.InvariantCulture),
                "fixture://monk-quest-finish",
                new Dictionary<string, int>()),
            clientConfigs: [],
            items: items,
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
        return new LevelExperience(
            Type: 0,
            Level: level,
            Experience: experience,
            UpLevTime: upLevTime,
            MentorUpLevTime: upLevTime,
            SourceLine: 0,
            RawLine: "fixture",
            Fields: []);
    }

    private static GuildWarData EmptyGuildWarData(string name)
    {
        return new GuildWarData("fixture://" + name, [], [], [], info: null, pole: null, conductors: []);
    }

    private sealed record QuestFinishResult(
        WorldSession OriginalSession,
        WorldSession UpdatedSession,
        List<byte[]> Sent,
        string Log,
        string ItemsRoot);
}
