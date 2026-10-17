using System.Buffers;
using System.Globalization;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyPromotionMastersDragonWarriorSkillTests : IDisposable
{
    private const uint DragonWarriorPromotionNpcId = 17_126;
    private const long CharacterUid = 1_293_720;
    private const byte QuestFinishOption = 111;
    private const uint FinishQuestId = 5;
    private const uint QuestFinishRewardItemId = 3_005_160;

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "WolfConquer-LegacyPromotionMastersDragonWarriorSkillTests-" + Guid.NewGuid().ToString("N"));

    public static TheoryData<byte, ushort, string, string> DragonWarriorSkillOptions => new()
    {
        { 6, 12_240, "DragonPunch", "NpcHandler.cs:50665-50703" },
        { 7, 12_290, "DragonCyclone", "NpcHandler.cs:50626-50664" },
        { 8, 12_320, "AirKick", "NpcHandler.cs:50587-50625" },
        { 9, 12_340, "AirRaid", "NpcHandler.cs:50548-50586" },
        { 10, 12_330, "AirSweep", "NpcHandler.cs:50509-50547" },
        { 11, 12_270, "DragonFlow", "NpcHandler.cs:50470-50508" },
        { 12, 12_160, "CrackingSwipe", "NpcHandler.cs:50431-50469" },
        { 13, 12_170, "SplittingSwipe", "NpcHandler.cs:50392-50430" },
        { 14, 12_350, "DragonSlash", "NpcHandler.cs:50353-50391" },
        { 16, 12_280, "DragonRoar", "NpcHandler.cs:50314-50352" },
        { 17, 12_200, "DragonSwing", "NpcHandler.cs:50275-50313" },
        { 18, 12_120, "SpeedKick", "NpcHandler.cs:50236-50274" },
        { 19, 12_130, "ViolentKick", "NpcHandler.cs:50197-50235" },
        { 20, 12_140, "StormKick", "NpcHandler.cs:50158-50196" },
        { 121, 12_210, "AirStrike", "NpcHandler.cs:50041-50079" },
        { 122, 12_220, "EarthSweep", "NpcHandler.cs:50080-50118" },
        { 123, 12_230, "Kick", "NpcHandler.cs:50119-50157" },
    };

    [Theory]
    [MemberData(nameof(DragonWarriorSkillOptions))]
    public async Task DragonWarriorSkillOptionLearnsSpellAndPersists(
        byte optionId,
        ushort spellId,
        string spellName,
        string sourceRange)
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var spellsRoot = Path.Combine(_root, "success-" + optionId);
        Directory.CreateDirectory(spellsRoot);
        var session = CreateSession(level: 100);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacyPromotionMastersNpcAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                Session: session,
                Sessions: sessions,
                PlayerSpellsRoot: spellsRoot),
            session,
            DragonWarriorPromotionNpcId,
            optionId,
            CancellationToken.None);

        Assert.True(handled);
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal([(uint)spellId], updated.PlayerSpells.SpellIds.ToArray());
        Assert.Equal([(uint)spellId], new PlayerSpellSnapshotLoader(spellsRoot)
            .LoadByCharacterUid(CharacterUid)
            .SpellIds
            .ToArray());

        Assert.Equal([spellId], ParseSpellPackets(sent).Select(packet => packet.Id).ToArray());
        Assert.Contains(
            ParseNpcReplies(sent),
            reply => reply.Text == "Congratulation! You just have learned the skill " + spellName);

        var logText = log.ToString();
        Assert.Contains("WorldServer handled LegacyNpc PromotionMaster skill", logText);
        Assert.Contains("source=NpcHandler.PromotionLeeLong", logText);
        Assert.Contains("sourceRange=" + sourceRange, logText);
        Assert.Contains("option=" + optionId, logText);
        Assert.Contains("spellId=" + spellId, logText);
        Assert.DoesNotContain("PARITY-GAP", logText);
    }

    [Fact]
    public async Task DragonWarriorSkillOptionAlreadyLearnedDoesNotDuplicate()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var spellsRoot = Path.Combine(_root, "duplicate-dragon-roar");
        Directory.CreateDirectory(spellsRoot);
        var learned = new PlayerSpellSnapshot([Entry(12_280)]);
        new PlayerSpellSnapshotLoader(spellsRoot).SaveByCharacterUid(CharacterUid, learned);
        var session = CreateSession(level: 100, playerSpells: learned);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacyPromotionMastersNpcAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                Session: session,
                Sessions: sessions,
                PlayerSpellsRoot: spellsRoot),
            session,
            DragonWarriorPromotionNpcId,
            optionId: 16,
            CancellationToken.None);

        Assert.True(handled);
        Assert.Empty(ParseSpellPackets(sent));
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal([12_280U], updated.PlayerSpells.SpellIds.ToArray());
        Assert.Equal([12_280U], new PlayerSpellSnapshotLoader(spellsRoot)
            .LoadByCharacterUid(CharacterUid)
            .SpellIds
            .ToArray());
        Assert.Contains(ParseNpcReplies(sent), reply => reply.Text == "You`ve learned DragonRoar!");

        var logText = log.ToString();
        Assert.Contains("sourceRange=NpcHandler.cs:50314-50352", logText);
        Assert.Contains("spellId=12280; already learned", logText);
    }

    [Fact]
    public async Task DragonWarriorSkillOptionRejectsBelowSourceLevelGate()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var spellsRoot = Path.Combine(_root, "level-gate-air-strike");
        Directory.CreateDirectory(spellsRoot);
        new PlayerSpellSnapshotLoader(spellsRoot).SaveByCharacterUid(CharacterUid, PlayerSpellSnapshot.Empty);
        var session = CreateSession(level: 99);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacyPromotionMastersNpcAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                Session: session,
                Sessions: sessions,
                PlayerSpellsRoot: spellsRoot),
            session,
            DragonWarriorPromotionNpcId,
            optionId: 121,
            CancellationToken.None);

        Assert.True(handled);
        Assert.Empty(ParseSpellPackets(sent));
        Assert.Empty(new PlayerSpellSnapshotLoader(spellsRoot)
            .LoadByCharacterUid(CharacterUid)
            .SpellIds);
        Assert.Contains(
            ParseNpcReplies(sent),
            reply => reply.Text == "You`re not strong enough to learn this skill. Keep practicing, and come back when you`re qualified.");

        var logText = log.ToString();
        Assert.Contains("sourceRange=NpcHandler.cs:50041-50079", logText);
        Assert.Contains("option=121", logText);
        Assert.Contains("level=99 requiredLevel=100; source level gate rejected", logText);
    }

    [Fact]
    public async Task DragonWarriorPromotionMasterRejectsNonDragonWarriorClass()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var spellsRoot = Path.Combine(_root, "class-gate");
        Directory.CreateDirectory(spellsRoot);
        new PlayerSpellSnapshotLoader(spellsRoot).SaveByCharacterUid(CharacterUid, PlayerSpellSnapshot.Empty);
        var session = CreateSession(level: 100, classCode: 7_005);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacyPromotionMastersNpcAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                Session: session,
                Sessions: sessions,
                PlayerSpellsRoot: spellsRoot),
            session,
            DragonWarriorPromotionNpcId,
            optionId: 6,
            CancellationToken.None);

        Assert.True(handled);
        Assert.Empty(ParseSpellPackets(sent));
        Assert.Empty(new PlayerSpellSnapshotLoader(spellsRoot)
            .LoadByCharacterUid(CharacterUid)
            .SpellIds);
        Assert.Contains(
            ParseNpcReplies(sent),
            reply => reply.Text == "Sorry, but you are not a member of the Dragon-Warrior class.");
    }

    [Fact]
    public async Task DragonWarriorOption111WithoutLocalFinishQuestSendsFollowupDialogWithoutMutation()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(level: 70, classCode: 8_001);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacyPromotionMastersNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            session,
            DragonWarriorPromotionNpcId,
            QuestFinishOption,
            CancellationToken.None);

        Assert.True(handled);
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(8_001, updated.Character.Class);
        Assert.Equal(session.Character.Experience, updated.Character.Experience);
        Assert.Empty(updated.PlayerItems.InventoryItems);
        Assert.Empty(updated.Character.PlayerQuests.Quests);
        Assert.DoesNotContain(sent, bytes => LegacyGameItemPacket.TryParse(ReadFrame(bytes), out _));
        Assert.DoesNotContain(sent, bytes => LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out _));

        AssertDragonWarriorQuestFinishFollowup(sent);

        var logText = log.ToString();
        Assert.Contains("PARITY-GAP LegacyNpc PromotionLeeLong quest-finish reward skipped", logText);
        Assert.Contains("source=NpcHandler.PromotionLeeLong", logText);
        Assert.Contains("sourceRange=NpcHandler.cs:49693-49728", logText);
        Assert.Contains("finishLookup=NpcHandler.cs:49655", logText);
        Assert.Contains("GetFinishQuest(5, class, 6426)", logText);
        Assert.Contains("item=3005160", logText);
        Assert.Contains("GainExpBall(300, angelwing)", logText);
        Assert.Contains("finishQuest.MissionId=5 is not in the runtime quest snapshot", logText);
        Assert.DoesNotContain("PromotionMaster mutation unavailable", logText);
    }

    [Fact]
    public async Task DragonWarriorOption111FinishesQuestAndGrantsSourceRewardWhenQuestIsLocal()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var itemsRoot = Path.Combine(_root, "quest-finish-items");
        Directory.CreateDirectory(itemsRoot);
        var session = CreateSession(
            level: 70,
            classCode: 8_057,
            playerQuests: new PlayerQuestSnapshot([
                new PlayerQuestSnapshotEntry(FinishQuestId, PlayerQuestStatus.Accepted, Time: 123, Intentions: [1, 2]),
            ]));
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacyPromotionMastersNpcAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                Session: session,
                Sessions: sessions,
                PlayerItemsRoot: itemsRoot,
                WorldData: CreateWorldDataCatalog(
                    [CreateItemType(QuestFinishRewardItemId, "SourceDragonWarriorQuestReward", durability: 77)],
                    [CreateLevelExperience(level: 70, experience: 1_200, upLevTime: 1_200)])),
            session,
            DragonWarriorPromotionNpcId,
            QuestFinishOption,
            CancellationToken.None);

        Assert.True(handled);
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(8_057, updated.Character.Class);
        Assert.Equal(900, updated.Character.Experience);
        Assert.Equal(70, updated.Character.Level);

        var item = Assert.Single(updated.PlayerItems.InventoryItems);
        Assert.Equal(QuestFinishRewardItemId, item.ItemId);
        Assert.Equal(77, item.Durability);
        Assert.Equal(77, item.MaximumDurability);
        Assert.Equal(
            [QuestFinishRewardItemId],
            new PlayerItemSnapshotLoader(itemsRoot)
                .LoadByCharacterUid(CharacterUid)
                .InventoryItems
                .Select(savedItem => savedItem.ItemId)
                .ToArray());

        Assert.True(updated.Character.PlayerQuests.TryGetQuest(FinishQuestId, out var finishedQuest));
        Assert.Equal(PlayerQuestStatus.Finished, finishedQuest.Status);
        Assert.Equal(123u, finishedQuest.Time);

        Assert.Contains(sent, bytes =>
            LegacyQuestListPacket.TryParse(ReadFrame(bytes), out var packet) &&
            packet.Mode == LegacyQuestListMode.FinishQuest &&
            packet.QuestId == FinishQuestId &&
            packet.Status == LegacyQuestStatus.Finished);
        Assert.Contains(sent, bytes =>
            LegacyGameItemPacket.TryParse(ReadFrame(bytes), out var packet) &&
            packet.ItemId == QuestFinishRewardItemId &&
            packet.Durability == 77);
        Assert.Contains(sent, bytes =>
            LegacyStringPacket.TryParse(ReadFrame(bytes), out var packet) &&
            packet.Strings.SequenceEqual(["angelwing"]));
        Assert.Contains(sent, bytes =>
            LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var packet) &&
            packet.Updates.Any(update =>
                update.Type == LegacyMsgUpdateDataType.Experience &&
                update.Value == 900));

        AssertDragonWarriorQuestFinishFollowup(sent);

        var logText = log.ToString();
        Assert.Contains("WorldServer handled LegacyNpc PromotionLeeLong quest-finish reward", logText);
        Assert.Contains("source=NpcHandler.PromotionLeeLong", logText);
        Assert.Contains("sourceRange=NpcHandler.cs:49693-49728", logText);
        Assert.Contains("finishLookup=NpcHandler.cs:49655", logText);
        Assert.Contains("finishQuest.MissionId=5", logText);
        Assert.Contains("rewardItemId=3005160", logText);
        Assert.Contains("GainExpBall(300, angelwing)", logText);
        Assert.Contains("experience=0->900", logText);
        Assert.Contains("persisted LegacyNpc PromotionMaster item mutation", logText);
        Assert.Contains("quest finish persistence is session-only", logText);
        Assert.DoesNotContain("quest-finish reward block not modeled yet", logText);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static WorldSession CreateSession(
        long level,
        long classCode = 8_005,
        PlayerSpellSnapshot? playerSpells = null,
        PlayerQuestSnapshot? playerQuests = null)
    {
        return new WorldSession(
            AccountId: 100,
            Username: "dragon-warrior-promotion-skill-test",
            Character: new CharacterSnapshot(
                Uid: CharacterUid,
                Name: "DragonHero",
                Body: 1003,
                Face: 0,
                Hair: 0,
                Class: classCode,
                FirstClass: classCode,
                SecondClass: 0,
                Reborn: 0,
                Level: level,
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
                Money: 0,
                ConquerPoints: 0,
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
            PlayerItems: PlayerItemSnapshot.Empty,
            PlayerSpells: playerSpells ?? PlayerSpellSnapshot.Empty,
            PlayerProficiencies: PlayerProficiencySnapshot.Empty);
    }

    private static PlayerSpellSnapshotEntry Entry(ushort id)
    {
        return new PlayerSpellSnapshotEntry(
            id,
            Level: 0,
            Experience: 0,
            PreviousLevel: 0,
            SoulLevel: 0,
            UseSpellSoul: 0);
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

    private static IReadOnlyList<LegacySpellPacket> ParseSpellPackets(IEnumerable<byte[]> sent)
    {
        var packets = new List<LegacySpellPacket>();
        foreach (var bytes in sent)
        {
            if (LegacySpellPacket.TryParse(ReadFrame(bytes), out var packet))
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

    private static void AssertDragonWarriorQuestFinishFollowup(IEnumerable<byte[]> sent)
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
                reply.Text == "Job~Promotion~2." &&
                reply.OptionId == 3);
        Assert.Contains(
            replies,
            reply => reply.InteractType == LegacyNpcReplyInteractType.Avatar &&
                reply.InputMaxLength == 259);
    }

    private static WorldDataCatalog CreateWorldDataCatalog(
        IReadOnlyList<ItemType> items,
        IReadOnlyList<LevelExperience> levelExperience)
    {
        return WorldDataCatalog.Create(
            new SeedManifest(
                DateTimeOffset.Parse("2026-06-05T00:00:00Z", CultureInfo.InvariantCulture),
                "fixture://dragon-warrior-quest-finish",
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
