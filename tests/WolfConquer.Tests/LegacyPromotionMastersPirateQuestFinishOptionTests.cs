using System.Buffers;
using System.Globalization;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyPromotionMastersPirateQuestFinishOptionTests : IDisposable
{
    private const uint PiratePromotionNpcId = 9_391;
    private const byte QuestFinishOption = 111;
    private const long CharacterUid = 1_293_722;
    private const uint QuestFinishMissionId = 2_001;
    private const uint QuestFinishRewardItemId = 3_005_160;

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "WolfConquer-LegacyPromotionMastersPirateQuestFinishOptionTests-" + Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData(7_000, 1)]
    [InlineData(7_001, 4)]
    [InlineData(7_002, 4)]
    public async Task Option111WithoutActiveQuestSendsLegacyFollowupDialogWithoutReward(
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
            PiratePromotionNpcId,
            QuestFinishOption,
            CancellationToken.None);

        Assert.True(handled);
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(classCode, updated.Character.Class);
        Assert.Equal(456, updated.Character.ConquerPoints);
        Assert.Equal(789, updated.Character.Experience);
        Assert.Empty(updated.PlayerItems.InventoryItems);
        Assert.Empty(updated.PlayerSpells.Spells);

        AssertLegacyQuestFinishDialog(sent, expectedNextOption);
        Assert.DoesNotContain(sent, bytes => LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out _));
        Assert.DoesNotContain(sent, bytes => LegacyGameItemPacket.TryParse(ReadFrame(bytes), out _));
        Assert.DoesNotContain(sent, bytes => LegacyQuestListPacket.TryParse(ReadFrame(bytes), out _));

        var logText = log.ToString();
        Assert.Contains("PARITY-GAP LegacyNpc PromotionPirate quest-finish reward skipped", logText);
        Assert.Contains("source=NpcHandler.PromotionPirate", logText);
        Assert.Contains("sourceRange=NpcHandler.cs:51318-51355", logText);
        Assert.Contains("finishLookup=NpcHandler.cs:51269", logText);
        Assert.Contains("option=111", logText);
        Assert.Contains("GetFinishQuest(2001, class, 1004)", logText);
        Assert.Contains("item=3005160", logText);
        Assert.Contains("GainExpBall(300, angelwing)", logText);
        Assert.Contains("source FinishQuest returned false because quest is not active", logText);
        Assert.DoesNotContain("PromotionMaster mutation unavailable", logText);
    }

    [Fact]
    public async Task Option111FinishesActiveQuestGrantsRewardItemAndExpBall()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var usersRoot = Path.Combine(_root, "Users-success");
        var itemsRoot = Path.Combine(_root, "Items-success");
        var questsRoot = Path.Combine(_root, "Quests-success");
        CreateMinimalCharacterSnapshot(usersRoot, classCode: 7_002);
        var quest = new PlayerQuestSnapshotEntry(QuestFinishMissionId, PlayerQuestStatus.Accepted, 12, [3U]);
        var session = CreateSession(
            classCode: 7_002,
            experience: 0,
            usersRoot: usersRoot,
            playerQuests: new PlayerQuestSnapshot([quest]),
            playerItems: Inventory(Item(4_000, 725_016)));
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacyPromotionMastersNpcAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                Session: session,
                Sessions: sessions,
                WorldData: CreateWorldDataCatalog(
                    CreateItemType(QuestFinishRewardItemId, "PromotionToken", durability: 10),
                    LevelExperienceRow(level: 70, experience: 1_000, upLevTime: 1_000)),
                PlayerItemsRoot: itemsRoot,
                PlayerQuestsRoot: questsRoot),
            session,
            PiratePromotionNpcId,
            QuestFinishOption,
            CancellationToken.None);

        Assert.True(handled);
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(7_002, updated.Character.Class);
        Assert.Equal(900, updated.Character.Experience);
        var finishedQuest = Assert.Single(updated.Character.PlayerQuests.Quests, entry => entry.Uid == QuestFinishMissionId);
        Assert.Equal(PlayerQuestStatus.Finished, finishedQuest.Status);
        Assert.Contains(updated.PlayerItems.InventoryItems, item => item.ItemId == QuestFinishRewardItemId);

        var persistedItems = new PlayerItemSnapshotLoader(itemsRoot).LoadByCharacterUid(CharacterUid);
        var reward = Assert.Single(persistedItems.InventoryItems, item => item.ItemId == QuestFinishRewardItemId);
        Assert.Equal((ushort)1, reward.StackSize);
        var persistedQuest = Assert.Single(
            new PlayerQuestSnapshotLoader(questsRoot).LoadByCharacterUid(CharacterUid).Quests,
            entry => entry.Uid == QuestFinishMissionId);
        Assert.Equal(PlayerQuestStatus.Finished, persistedQuest.Status);
        Assert.Equal([3U], persistedQuest.Intentions);

        var persistedCharacter = File.ReadAllText(Path.Combine(usersRoot, CharacterUid + ".ini"));
        Assert.Contains("Experience=900", persistedCharacter, StringComparison.Ordinal);

        var questFrame = Assert.Single(ParseQuestListPackets(sent));
        Assert.Equal(LegacyQuestListMode.FinishQuest, questFrame.Mode);
        Assert.Equal(QuestFinishMissionId, questFrame.QuestId);
        Assert.Equal(LegacyQuestStatus.Finished, questFrame.Status);

        var rewardPacket = Assert.Single(ParseGameItemPackets(sent), packet => packet.ItemId == QuestFinishRewardItemId);
        Assert.Equal(LegacyGameItemPacket.AddItemMode, rewardPacket.Mode);
        var talk = Assert.Single(ParseMessagePackets(sent), packet => packet.Content[3] == "You have gained experience worth 0.5 exp ball(s).");
        Assert.Equal(LegacyMsgMessagePacket.TalkChatType, talk.ChatType);
        var effect = Assert.Single(ParseStringPackets(sent), packet => packet.Strings.Contains("angelwing"));
        Assert.Equal((uint)CharacterUid, effect.Uid);
        Assert.Contains(
            ParseUpdatePackets(sent),
            packet => packet.Updates.Any(update =>
                update.Type == LegacyMsgUpdateDataType.Experience &&
                update.Value == 900UL));
        AssertLegacyQuestFinishDialog(sent, expectedNextOption: 4);

        var logText = log.ToString();
        Assert.Contains("WorldServer handled LegacyNpc PromotionPirate quest-finish reward", logText);
        Assert.Contains("finishQuest.MissionId=2001", logText);
        Assert.Contains("rewardItemId=3005160", logText);
        Assert.Contains("GainExpBall(600 hidden, 300 angelwing)", logText);
        Assert.Contains("experience=0->900 level=70->70", logText);
        Assert.Contains("WorldServer persisted LegacyNpc PromotionMaster item mutation", logText);
        Assert.Contains("WorldServer persisted LegacyNpc PromotionPirate quest finish", logText);
        Assert.Contains("quest persistence is durable", logText);
    }

    [Fact]
    public async Task Option111ActiveQuestMissingPlayerItemsRootLeavesRewardSessionOnly()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var quest = new PlayerQuestSnapshotEntry(QuestFinishMissionId, PlayerQuestStatus.Accepted, 12, [3U]);
        var session = CreateSession(
            classCode: 7_002,
            experience: 0,
            playerQuests: new PlayerQuestSnapshot([quest]));
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacyPromotionMastersNpcAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                Session: session,
                Sessions: sessions,
                WorldData: CreateWorldDataCatalog(
                    CreateItemType(QuestFinishRewardItemId, "PromotionToken", durability: 10),
                    LevelExperienceRow(level: 70, experience: 1_000, upLevTime: 1_000))),
            session,
            PiratePromotionNpcId,
            QuestFinishOption,
            CancellationToken.None);

        Assert.True(handled);
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Contains(updated.PlayerItems.InventoryItems, item => item.ItemId == QuestFinishRewardItemId);
        Assert.Contains("PlayerItemsRoot", log.ToString(), StringComparison.Ordinal);
        Assert.Contains("WorldServer handled LegacyNpc PromotionPirate quest-finish reward", log.ToString(), StringComparison.Ordinal);
        AssertLegacyQuestFinishDialog(sent, expectedNextOption: 4);
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
        long experience = 789,
        string? usersRoot = null,
        PlayerQuestSnapshot? playerQuests = null,
        PlayerItemSnapshot? playerItems = null)
    {
        return new WorldSession(
            AccountId: 100,
            Username: "pirate-quest-finish-option-test",
            Character: new CharacterSnapshot(
                Uid: CharacterUid,
                Name: "PirateQuestHero",
                Body: 1003,
                Face: 0,
                Hair: 0,
                Class: classCode,
                FirstClass: classCode,
                SecondClass: 0,
                Reborn: 0,
                Level: 70,
                Experience: experience,
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
                Money: 0,
                ConquerPoints: 456,
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
            PlayerProficiencies: PlayerProficiencySnapshot.Empty,
            UsersRoot: usersRoot);
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

    private static void CreateMinimalCharacterSnapshot(string usersRoot, long classCode)
    {
        Directory.CreateDirectory(usersRoot);
        File.WriteAllText(
            Path.Combine(usersRoot, CharacterUid + ".ini"),
            "[Character]" + Environment.NewLine +
            "UID=" + CharacterUid + Environment.NewLine +
            "Class=" + classCode + Environment.NewLine +
            "Level=70" + Environment.NewLine +
            "Experience=0" + Environment.NewLine);
    }

    private static ConquerTcpConnection CreateSendingConnection(List<byte[]> sent)
    {
        return new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958), (frame, _) =>
        {
            sent.Add(frame.ToArray());
            return ValueTask.CompletedTask;
        });
    }

    private static void AssertLegacyQuestFinishDialog(IEnumerable<byte[]> sent, byte expectedNextOption)
    {
        var replies = ParseNpcReplies(sent);
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
                reply.OptionId == expectedNextOption &&
                reply.Text == "I~want~to~get~promoted");
        Assert.Contains(
            replies,
            reply => reply.InteractType == LegacyNpcReplyInteractType.Avatar &&
                reply.InputMaxLength == 234);
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

    private static IReadOnlyList<LegacyGameItemPacket> ParseGameItemPackets(IEnumerable<byte[]> sent)
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

    private static IReadOnlyList<LegacyMsgMessagePacket> ParseMessagePackets(IEnumerable<byte[]> sent)
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

    private static IReadOnlyList<LegacyMsgUpdatePacket> ParseUpdatePackets(IEnumerable<byte[]> sent)
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

    private static IReadOnlyList<LegacyQuestListPacket> ParseQuestListPackets(IEnumerable<byte[]> sent)
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

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }

    private static WorldDataCatalog CreateWorldDataCatalog(ItemType item, LevelExperience levelExperience)
    {
        return WorldDataCatalog.Create(
            new SeedManifest(
                DateTimeOffset.Parse("2026-06-05T00:00:00Z", CultureInfo.InvariantCulture),
                "fixture://pirate-quest-finish",
                new Dictionary<string, int>()),
            clientConfigs: [],
            items: [item],
            itemAddBonuses: [],
            levelExperience: [levelExperience],
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

    private static LevelExperience LevelExperienceRow(int level, long experience, int upLevTime)
    {
        return new LevelExperience(
            Type: 0,
            Level: level,
            Experience: experience,
            UpLevTime: upLevTime,
            MentorUpLevTime: upLevTime,
            SourceLine: 1,
            RawLine: $"0 {level.ToString(CultureInfo.InvariantCulture)} {experience.ToString(CultureInfo.InvariantCulture)} {upLevTime.ToString(CultureInfo.InvariantCulture)} {upLevTime.ToString(CultureInfo.InvariantCulture)}",
            Fields:
            [
                "0",
                level.ToString(CultureInfo.InvariantCulture),
                experience.ToString(CultureInfo.InvariantCulture),
                upLevTime.ToString(CultureInfo.InvariantCulture),
                upLevTime.ToString(CultureInfo.InvariantCulture),
            ]);
    }

    private static GuildWarData EmptyGuildWarData(string name)
    {
        return new GuildWarData("fixture://" + name, [], [], [], info: null, pole: null, conductors: []);
    }
}
