using System.Buffers;
using System.Globalization;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyPromotionMastersDragonWarriorAdvancedPromotionTests : IDisposable
{
    private const uint DragonWarriorPromotionNpcId = 17_126;
    private const long CharacterUid = 1_293_731;
    private const uint EmeraldItemId = 1_080_001;
    private const uint MeteorItemId = 1_088_001;
    private const uint MoonBoxItemId = 721_080;
    private const uint DragonBallItemId = 1_088_000;

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "WolfConquer-LegacyPromotionMastersDragonWarriorAdvancedPromotionTests-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Option4RejectsLevel15To39ExpertGateWithoutMutation()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var usersRoot = Path.Combine(_root, "Users-level-gate");
        CreateMinimalCharacterSnapshot(usersRoot, classCode: 8_001);
        var session = CreateSession(classCode: 8_001, level: 39, usersRoot: usersRoot);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacyPromotionMastersNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            session,
            DragonWarriorPromotionNpcId,
            optionId: 4,
            CancellationToken.None);

        Assert.True(handled);
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(8_001, updated.Character.Class);
        Assert.Empty(updated.PlayerItems.InventoryItems);
        Assert.Empty(ParseGameItemPackets(sent));
        Assert.Empty(ParseItemUsagePackets(sent));

        var replies = ParseNpcReplies(sent);
        Assert.Contains(replies, reply =>
            reply.InteractType == LegacyNpcReplyInteractType.Dialog &&
            reply.Text == "`Dragon-Warrior` can get promoted to be `Expert Dragon-Warrior` at Level 40.");
        Assert.Contains(replies, reply =>
            reply.InteractType == LegacyNpcReplyInteractType.Dialog &&
            reply.Text == "You should have reached Level 40 to get promoted. Keep practicing!.");
        Assert.Contains(replies, reply =>
            reply.InteractType == LegacyNpcReplyInteractType.Option &&
            reply.Text == "I`ll~think~about~it.");

        var persistedCharacter = File.ReadAllText(Path.Combine(usersRoot, CharacterUid + ".ini"));
        Assert.Contains("Class=8001", persistedCharacter, StringComparison.Ordinal);

        var logText = log.ToString();
        Assert.Contains("WorldServer skipped LegacyNpc PromotionLeeLong advanced promotion", logText);
        Assert.Contains("sourceRange=NpcHandler.cs:49729-50040", logText);
        Assert.Contains("option=4", logText);
        Assert.DoesNotContain("PARITY-GAP", logText);
    }

    [Fact]
    public async Task Option4PromotesClass8001AtLevel40WithoutItem()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var usersRoot = Path.Combine(_root, "Users-no-item");
        CreateMinimalCharacterSnapshot(usersRoot, classCode: 8_001);
        var session = CreateSession(classCode: 8_001, level: 40, usersRoot: usersRoot);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacyPromotionMastersNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            session,
            DragonWarriorPromotionNpcId,
            optionId: 4,
            CancellationToken.None);

        Assert.True(handled);
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(8_002, updated.Character.Class);
        Assert.Empty(updated.PlayerItems.InventoryItems);
        Assert.Empty(ParseGameItemPackets(sent));
        Assert.Empty(ParseItemUsagePackets(sent));

        Assert.Contains(ParseNpcReplies(sent), reply =>
            reply.InteractType == LegacyNpcReplyInteractType.Dialog &&
            reply.Text == "Excellent! You`ve learned ways of the Dragon-Warrior. Do you feel a different energy in your body? That`s stronger power and battle prowess!");

        var persistedCharacter = File.ReadAllText(Path.Combine(usersRoot, CharacterUid + ".ini"));
        Assert.Contains("Class=8002", persistedCharacter, StringComparison.Ordinal);

        var logText = log.ToString();
        Assert.Contains("WorldServer handled LegacyNpc PromotionLeeLong advanced promotion", logText);
        Assert.Contains("previousClass=8001", logText);
        Assert.Contains("newClass=8002", logText);
        Assert.Contains("noItem", logText);
        Assert.DoesNotContain("PARITY-GAP", logText);
    }

    [Fact]
    public async Task Option4ConsumesEmeraldAndPromotesClass8002()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var usersRoot = Path.Combine(_root, "Users-emerald");
        var itemsRoot = Path.Combine(_root, "PlayersItems-emerald");
        CreateMinimalCharacterSnapshot(usersRoot, classCode: 8_002);
        var emerald = InventoryItem(uid: 2_001, itemId: EmeraldItemId);
        var session = CreateSession(
            classCode: 8_002,
            level: 70,
            usersRoot: usersRoot,
            playerItems: Inventory(InventoryItem(uid: 2_000, itemId: 725_016), emerald));
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacyPromotionMastersNpcAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                Session: session,
                Sessions: sessions,
                PlayerItemsRoot: itemsRoot),
            session,
            DragonWarriorPromotionNpcId,
            optionId: 4,
            CancellationToken.None);

        Assert.True(handled);
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(8_003, updated.Character.Class);
        Assert.DoesNotContain(updated.PlayerItems.InventoryItems, item => item.ItemId == EmeraldItemId);
        Assert.Single(updated.PlayerItems.InventoryItems);

        var persistedItems = new PlayerItemSnapshotLoader(itemsRoot).LoadByCharacterUid(CharacterUid);
        Assert.DoesNotContain(persistedItems.InventoryItems, item => item.ItemId == EmeraldItemId);
        Assert.Contains(ParseItemUsagePackets(sent), packet =>
            packet.Id == emerald.Uid &&
            packet.Action == MsgItemUsageAction.RemoveInventory);
        Assert.Empty(ParseGameItemPackets(sent));
        Assert.Contains(ParseNpcReplies(sent), reply =>
            reply.Text == "Excellent! You`ve learned ways of the Taoist. Do you feel a different energy in your body? That`s stronger power and battle prowess!");

        var persistedCharacter = File.ReadAllText(Path.Combine(usersRoot, CharacterUid + ".ini"));
        Assert.Contains("Class=8003", persistedCharacter, StringComparison.Ordinal);

        var logText = log.ToString();
        Assert.Contains("consumeItemId=1080001", logText);
        Assert.Contains("WorldServer persisted LegacyNpc PromotionMaster item mutation", logText);
        Assert.DoesNotContain("PARITY-GAP", logText);
    }

    [Fact]
    public async Task Option4MissingMeteorRejectsClass8003WithoutConsumption()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var usersRoot = Path.Combine(_root, "Users-missing-meteor");
        CreateMinimalCharacterSnapshot(usersRoot, classCode: 8_003);
        var session = CreateSession(
            classCode: 8_003,
            level: 100,
            usersRoot: usersRoot,
            playerItems: Inventory(InventoryItem(uid: 2_000, itemId: 725_016)));
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacyPromotionMastersNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            session,
            DragonWarriorPromotionNpcId,
            optionId: 4,
            CancellationToken.None);

        Assert.True(handled);
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(8_003, updated.Character.Class);
        Assert.DoesNotContain(updated.PlayerItems.InventoryItems, item => item.ItemId == MeteorItemId);
        Assert.Empty(ParseItemUsagePackets(sent));
        Assert.Empty(ParseGameItemPackets(sent));
        Assert.Contains(ParseNpcReplies(sent), reply =>
            reply.InteractType == LegacyNpcReplyInteractType.Dialog &&
            reply.Text == "I'm sorry but you need to have 1 Meteor at your inventory.");
        Assert.Contains(ParseNpcReplies(sent), reply =>
            reply.InteractType == LegacyNpcReplyInteractType.Option &&
            reply.Text == "I~see.");

        var persistedCharacter = File.ReadAllText(Path.Combine(usersRoot, CharacterUid + ".ini"));
        Assert.Contains("Class=8003", persistedCharacter, StringComparison.Ordinal);

        var logText = log.ToString();
        Assert.Contains("itemId=1088001", logText);
        Assert.Contains("source item missing", logText);
        Assert.DoesNotContain("PARITY-GAP", logText);
    }

    [Fact]
    public async Task Option4ConsumesMoonBoxGrantsDragonBallTalksAndPromotesClass8004()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var usersRoot = Path.Combine(_root, "Users-moonbox");
        var itemsRoot = Path.Combine(_root, "PlayersItems-moonbox");
        CreateMinimalCharacterSnapshot(usersRoot, classCode: 8_004);
        var moonBox = InventoryItem(uid: 2_001, itemId: MoonBoxItemId);
        var session = CreateSession(
            classCode: 8_004,
            level: 110,
            usersRoot: usersRoot,
            playerItems: Inventory(InventoryItem(uid: 2_000, itemId: 725_016), moonBox));
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacyPromotionMastersNpcAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                Session: session,
                WorldData: CreateWorldDataCatalog(CreateItemType(DragonBallItemId, durability: 99)),
                Sessions: sessions,
                PlayerItemsRoot: itemsRoot),
            session,
            DragonWarriorPromotionNpcId,
            optionId: 4,
            CancellationToken.None);

        Assert.True(handled);
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(8_005, updated.Character.Class);
        Assert.DoesNotContain(updated.PlayerItems.InventoryItems, item => item.ItemId == MoonBoxItemId);
        var dragonBall = Assert.Single(updated.PlayerItems.InventoryItems, item => item.ItemId == DragonBallItemId);
        Assert.Equal((ushort)99, dragonBall.Durability);
        Assert.Equal((ushort)99, dragonBall.MaximumDurability);
        Assert.Equal((ushort)1, dragonBall.StackSize);

        var persistedItems = new PlayerItemSnapshotLoader(itemsRoot).LoadByCharacterUid(CharacterUid);
        Assert.DoesNotContain(persistedItems.InventoryItems, item => item.ItemId == MoonBoxItemId);
        Assert.Contains(persistedItems.InventoryItems, item => item.ItemId == DragonBallItemId);

        var removeIndex = FindItemUsagePacketIndex(sent, moonBox.Uid, MsgItemUsageAction.RemoveInventory);
        var addIndex = FindGameItemPacketIndex(sent, DragonBallItemId);
        var talkIndex = FindSystemTalkIndex(sent, "You received DragonBall.");
        Assert.True(removeIndex >= 0);
        Assert.True(addIndex > removeIndex);
        Assert.True(talkIndex > addIndex);

        var itemPacket = ParseGameItemPackets(sent).Single(packet => packet.Packet.ItemId == DragonBallItemId).Packet;
        Assert.Equal(LegacyGameItemPacket.AddItemMode, itemPacket.Mode);
        Assert.Equal((ushort)99, itemPacket.Durability);
        Assert.Equal((ushort)99, itemPacket.MaximumDurability);
        Assert.Contains(ParseNpcReplies(sent), reply =>
            reply.Text == "Excellent!Do you feel a different energy in your body? That`s stronger power and battle prowess!");

        var persistedCharacter = File.ReadAllText(Path.Combine(usersRoot, CharacterUid + ".ini"));
        Assert.Contains("Class=8005", persistedCharacter, StringComparison.Ordinal);

        var logText = log.ToString();
        Assert.Contains("consumeItemId=721080", logText);
        Assert.Contains("rewardItemId=1088000", logText);
        Assert.DoesNotContain("PARITY-GAP", logText);
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
        long level,
        string? usersRoot = null,
        PlayerItemSnapshot? playerItems = null)
    {
        return new WorldSession(
            AccountId: 100,
            Username: "dragon-warrior-advanced-promotion-test",
            Character: new CharacterSnapshot(
                Uid: CharacterUid,
                Name: "DragonAdvanced",
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
                PaidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)),
            PlayerItems: playerItems ?? PlayerItemSnapshot.Empty,
            PlayerSpells: PlayerSpellSnapshot.Empty,
            PlayerProficiencies: PlayerProficiencySnapshot.Empty,
            UsersRoot: usersRoot);
    }

    private static PlayerItemSnapshot Inventory(params PlayerItemSnapshotItem[] inventoryItems)
    {
        return PlayerItemSnapshot.Empty with
        {
            InventoryItems = inventoryItems,
        };
    }

    private static PlayerItemSnapshotItem InventoryItem(uint uid, uint itemId)
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
            "Class=" + classCode + Environment.NewLine);
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

    private static IReadOnlyList<(int Index, LegacyGameItemPacket Packet)> ParseGameItemPackets(
        IReadOnlyList<byte[]> sent)
    {
        var packets = new List<(int Index, LegacyGameItemPacket Packet)>();
        for (var index = 0; index < sent.Count; index++)
        {
            if (LegacyGameItemPacket.TryParse(ReadFrame(sent[index]), out var packet))
            {
                packets.Add((index, packet));
            }
        }

        return packets;
    }

    private static IReadOnlyList<MsgItemUsagePacket> ParseItemUsagePackets(IEnumerable<byte[]> sent)
    {
        var packets = new List<MsgItemUsagePacket>();
        foreach (var bytes in sent)
        {
            if (MsgItemUsagePacket.TryParse(ReadFrame(bytes), out var packet))
            {
                packets.Add(packet);
            }
        }

        return packets;
    }

    private static int FindItemUsagePacketIndex(
        IReadOnlyList<byte[]> sent,
        uint itemUid,
        MsgItemUsageAction action)
    {
        for (var index = 0; index < sent.Count; index++)
        {
            if (MsgItemUsagePacket.TryParse(ReadFrame(sent[index]), out var packet) &&
                packet.Id == itemUid &&
                packet.Action == action)
            {
                return index;
            }
        }

        return -1;
    }

    private static int FindGameItemPacketIndex(IReadOnlyList<byte[]> sent, uint itemId)
    {
        for (var index = 0; index < sent.Count; index++)
        {
            if (LegacyGameItemPacket.TryParse(ReadFrame(sent[index]), out var packet) &&
                packet.ItemId == itemId)
            {
                return index;
            }
        }

        return -1;
    }

    private static int FindSystemTalkIndex(IReadOnlyList<byte[]> sent, string expectedMessage)
    {
        for (var index = 0; index < sent.Count; index++)
        {
            if (LegacyMsgMessagePacket.TryParse(ReadFrame(sent[index]), out var packet) &&
                packet.ChatType == LegacyMsgMessagePacket.TalkChatType &&
                packet.Content.Contains(expectedMessage, StringComparer.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }

    private static WorldDataCatalog CreateWorldDataCatalog(params ItemType[] items)
    {
        return WorldDataCatalog.Create(
            new SeedManifest(
                DateTimeOffset.Parse("2026-06-05T00:00:00Z", CultureInfo.InvariantCulture),
                "fixture://dragon-warrior-advanced",
                new Dictionary<string, int>()),
            clientConfigs: [],
            items: items,
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

    private static ItemType CreateItemType(uint itemTypeId, ushort durability)
    {
        var fields = Enumerable.Repeat("0", 66).ToArray();
        fields[0] = itemTypeId.ToString(CultureInfo.InvariantCulture);
        fields[1] = "DragonBall";
        fields[ItemType.DurabilityFieldIndex] = durability.ToString(CultureInfo.InvariantCulture);
        return new ItemType(checked((int)itemTypeId), "DragonBall", fields);
    }

    private static GuildWarData EmptyGuildWarData(string name)
    {
        return new GuildWarData("fixture://" + name, [], [], [], info: null, pole: null, conductors: []);
    }
}
