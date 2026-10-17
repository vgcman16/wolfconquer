using System.Buffers;
using System.Globalization;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyPromotionMastersNinjaQuestFinishOptionTests : IDisposable
{
    private const uint NinjaPromotionNpcId = 4_972;
    private const byte QuestFinishOption = 111;
    private const long CharacterUid = 1_293_680;
    private const uint FinishQuestId = 2_001;
    private const uint RewardItemId = 3_005_160;

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "WolfConquer-LegacyPromotionMastersNinjaQuestFinishOptionTests-" + Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData(5_000, 1)]
    [InlineData(5_001, 20)]
    [InlineData(5_002, 20)]
    public async Task Option111WithoutLocalFinishQuestSendsFollowupDialogWithoutMutation(
        long classCode,
        byte expectedNextOption)
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(classCode);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacyPromotionMastersNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            session,
            NinjaPromotionNpcId,
            QuestFinishOption,
            CancellationToken.None);

        Assert.True(handled);
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(classCode, updated.Character.Class);
        Assert.Equal(321, updated.Character.ConquerPoints);
        Assert.Equal(987_654, updated.Character.Experience);
        Assert.Empty(updated.PlayerItems.InventoryItems);
        Assert.Empty(updated.PlayerSpells.Spells);
        Assert.Empty(updated.Character.PlayerQuests.Quests);

        var replies = ParseNpcReplies(sent);
        Assert.Contains(
            replies,
            reply => reply.InteractType == LegacyNpcReplyInteractType.Dialog &&
                reply.Text == "Wow, look at you, you`ll be another hero in the world of Conquer! Here, you can learn some skills and get promoted! Do you want to learn more?");
        Assert.Contains(
            replies,
            reply => reply.InteractType == LegacyNpcReplyInteractType.Dialog &&
                reply.Text == "~get promoted here to start your life. Would you like to learn something about your class promotion and quests?.");
        AssertOption(replies, "I~want~to~get~promoted", expectedNextOption);
        Assert.Contains(
            replies,
            reply => reply.InteractType == LegacyNpcReplyInteractType.Avatar &&
                reply.InputMaxLength == 259);
        Assert.DoesNotContain(sent, bytes => LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out _));
        Assert.DoesNotContain(sent, bytes => LegacyGameItemPacket.TryParse(ReadFrame(bytes), out _));
        Assert.DoesNotContain(sent, bytes => LegacyQuestListPacket.TryParse(ReadFrame(bytes), out _));

        var logText = log.ToString();
        Assert.Contains("PARITY-GAP LegacyNpc PromotionNinja quest-finish reward skipped", logText);
        Assert.Contains("source=NpcHandler.PromotionNinja", logText);
        Assert.Contains("sourceRange=NpcHandler.cs:44063-44102", logText);
        Assert.Contains("finishLookup=NpcHandler.cs:44019", logText);
        Assert.Contains("option=111", logText);
        Assert.Contains("GetFinishQuest(2001, class, 1004)", logText);
        Assert.Contains("item=3005160", logText);
        Assert.Contains("GainExpBall(300, angelwing)", logText);
        Assert.Contains("finishQuest.MissionId=2001 is not in the runtime quest snapshot", logText);
        Assert.DoesNotContain("PromotionMaster mutation unavailable", logText);
    }

    [Theory]
    [InlineData(5_000, 1)]
    [InlineData(5_001, 20)]
    public async Task Option111FinishesQuestAndGrantsSourceRewardWhenQuestIsLocal(
        long classCode,
        byte expectedNextOption)
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var itemsRoot = Path.Combine(_root, "items-" + classCode.ToString(CultureInfo.InvariantCulture));
        Directory.CreateDirectory(itemsRoot);
        var session = CreateSession(
            classCode,
            new PlayerQuestSnapshot([
                new PlayerQuestSnapshotEntry(FinishQuestId, PlayerQuestStatus.Accepted, Time: 123, Intentions: [1, 2, 3]),
            ]),
            new PlayerItemSnapshot(
                [CreateInventoryItem(uid: 2_000, itemId: 725_016)],
                [],
                [],
                [],
                []));
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacyPromotionMastersNpcAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                Session: session,
                Sessions: sessions,
                WorldData: CreateWorldDataCatalog(
                    [CreateItemType(RewardItemId, "SourceNinjaQuestReward", durability: 77)],
                    [CreateLevelExperience(level: 70, experience: 10_000_000, upLevTime: 10_000)]),
                PlayerItemsRoot: itemsRoot),
            session,
            NinjaPromotionNpcId,
            QuestFinishOption,
            CancellationToken.None);

        Assert.True(handled);
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(classCode, updated.Character.Class);
        Assert.Equal(321, updated.Character.ConquerPoints);
        Assert.Equal(1_887_654, updated.Character.Experience);
        Assert.Equal(70, updated.Character.Level);

        Assert.Equal([725_016U, RewardItemId], updated.PlayerItems.InventoryItems.Select(item => item.ItemId).ToArray());
        var rewardItem = updated.PlayerItems.InventoryItems[1];
        Assert.Equal(77, rewardItem.Durability);
        Assert.Equal(77, rewardItem.MaximumDurability);

        Assert.Equal([725_016U, RewardItemId], new PlayerItemSnapshotLoader(itemsRoot)
            .LoadByCharacterUid(CharacterUid)
            .InventoryItems
            .Select(item => item.ItemId)
            .ToArray());

        Assert.True(updated.Character.PlayerQuests.TryGetQuest(FinishQuestId, out var finishedQuest));
        Assert.Equal(PlayerQuestStatus.Finished, finishedQuest.Status);
        Assert.Equal(123u, finishedQuest.Time);
        Assert.Equal([1U, 2U, 3U], finishedQuest.Intentions);

        Assert.Contains(sent, bytes =>
            LegacyQuestListPacket.TryParse(ReadFrame(bytes), out var packet) &&
            packet.Mode == LegacyQuestListMode.FinishQuest &&
            packet.QuestId == FinishQuestId &&
            packet.Count == 1 &&
            packet.Status == LegacyQuestStatus.Finished);
        Assert.Contains(sent, bytes =>
            LegacyGameItemPacket.TryParse(ReadFrame(bytes), out var packet) &&
            packet.ItemId == RewardItemId &&
            packet.Durability == 77);
        Assert.Contains(sent, bytes =>
            LegacyStringPacket.TryParse(ReadFrame(bytes), out var packet) &&
            packet.Strings.SequenceEqual(["angelwing"]));
        Assert.Contains(sent, bytes =>
            LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var packet) &&
            packet.Updates.Any(update =>
                update.Type == LegacyMsgUpdateDataType.Experience &&
                update.Value == 1_887_654));

        var replies = ParseNpcReplies(sent);
        AssertOption(replies, "I~want~to~get~promoted", expectedNextOption);
        Assert.Contains(
            replies,
            reply => reply.InteractType == LegacyNpcReplyInteractType.Avatar &&
                reply.InputMaxLength == 259);

        var logText = log.ToString();
        Assert.Contains("WorldServer handled LegacyNpc PromotionNinja quest-finish reward", logText);
        Assert.Contains("source=NpcHandler.PromotionNinja", logText);
        Assert.Contains("sourceRange=NpcHandler.cs:44063-44102", logText);
        Assert.Contains("finishLookup=NpcHandler.cs:44019", logText);
        Assert.Contains("finishQuest.MissionId=2001", logText);
        Assert.Contains("rewardItemId=3005160", logText);
        Assert.Contains("GainExpBall(300, angelwing)", logText);
        Assert.Contains("experience=987654->1887654", logText);
        Assert.Contains("quest finish persistence is session-only", logText);
        Assert.DoesNotContain("quest-finish reward block not modeled", logText);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static WorldSession CreateSession(
        long classCode,
        PlayerQuestSnapshot? playerQuests = null,
        PlayerItemSnapshot? playerItems = null)
    {
        return new WorldSession(
            AccountId: 100,
            Username: "ninja-quest-finish-test",
            Character: new CharacterSnapshot(
                Uid: CharacterUid,
                Name: "NinjaHero",
                Body: 1003,
                Face: 0,
                Hair: 0,
                Class: classCode,
                FirstClass: classCode,
                SecondClass: 0,
                Reborn: 0,
                Level: 70,
                Experience: 987_654,
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
                ConquerPoints: 321,
                BoundConquerPoints: 0,
                BankConquerPoints: 0,
                PkPoints: 0,
                Online: 0,
                SourceFile: "fixture",
                Attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                PaidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase))
            {
                PlayerQuests = playerQuests ?? PlayerQuestSnapshot.Empty,
            },
            PlayerItems: playerItems ?? PlayerItemSnapshot.Empty,
            PlayerSpells: PlayerSpellSnapshot.Empty,
            PlayerProficiencies: PlayerProficiencySnapshot.Empty);
    }

    private static PlayerItemSnapshotItem CreateInventoryItem(uint uid, uint itemId)
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

    private static void AssertOption(IReadOnlyList<LegacyNpcReplyPacket> replies, string text, byte optionId)
    {
        Assert.Contains(
            replies,
            reply => reply.InteractType == LegacyNpcReplyInteractType.Option &&
                reply.Text == text &&
                reply.OptionId == optionId);
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
                "fixture://ninja-quest-finish",
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
}
