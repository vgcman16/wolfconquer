using System.Buffers;
using System.Globalization;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyPromotionMastersWarriorAdvancedPromotionTests : IDisposable
{
    private const uint WarriorPromotionNpcId = 10_001;
    private const long CharacterUid = 1_293_600;
    private const byte AdvancedPromotionOption = 3;
    private const uint EmeraldItemId = 1_080_001;
    private const uint MeteorItemId = 1_088_001;
    private const uint MoonBoxItemId = 721_080;
    private const uint DragonBallItemId = 1_088_000;

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "WolfConquer-LegacyPromotionMastersWarriorAdvancedPromotionTests-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Option3RejectsBelowLevel40ThenPromotesClass2001WithoutItem()
    {
        using var lowLog = new StringWriter();
        var lowSent = new List<byte[]>();
        var lowConnection = CreateSendingConnection(lowSent);
        var lowUsersRoot = Path.Combine(_root, "users-rank-one-low");
        CreateMinimalCharacterSnapshot(lowUsersRoot, classCode: 2_001);
        var lowSession = CreateSession(classCode: 2_001, level: 39, usersRoot: lowUsersRoot);
        var lowSessions = new WorldSessionStore();
        lowSessions.Set(lowConnection, lowSession);

        var handled = await WorldPacketHandlers.TryHandleLegacyPromotionMastersNpcAsync(
            new WorldPacketHandlerContext(lowConnection, lowLog, Session: lowSession, Sessions: lowSessions),
            lowSession,
            WarriorPromotionNpcId,
            AdvancedPromotionOption,
            CancellationToken.None);

        Assert.True(handled);
        Assert.True(lowSessions.TryGet(lowConnection, out var lowUpdated));
        Assert.NotNull(lowUpdated);
        Assert.Equal(2_001, lowUpdated.Character.Class);
        Assert.Empty(ParseItemUsagePackets(lowSent));
        Assert.Empty(ParseGameItemPackets(lowSent));
        var lowReplies = ParseNpcReplies(lowSent);
        Assert.Contains(
            lowReplies,
            reply => reply.Text == "There is much that you must learn before you can accept my training. Slaughter a legion of monsters first! [Level 40+]");
        Assert.Contains(lowReplies, reply => reply.Text == "I~see." && reply.OptionId == LegacyNpcReplyPacket.ClosedOptionId);
        Assert.Contains(
            lowReplies,
            reply => reply.InteractType == LegacyNpcReplyInteractType.Avatar && reply.InputMaxLength == 8);
        Assert.Contains(
            "Class=2001",
            File.ReadAllText(Path.Combine(lowUsersRoot, CharacterUid + ".ini")),
            StringComparison.Ordinal);

        using var successLog = new StringWriter();
        var successSent = new List<byte[]>();
        var successConnection = CreateSendingConnection(successSent);
        var successUsersRoot = Path.Combine(_root, "users-rank-one-success");
        CreateMinimalCharacterSnapshot(successUsersRoot, classCode: 2_001);
        var successSession = CreateSession(classCode: 2_001, level: 40, usersRoot: successUsersRoot);
        var successSessions = new WorldSessionStore();
        successSessions.Set(successConnection, successSession);

        handled = await WorldPacketHandlers.TryHandleLegacyPromotionMastersNpcAsync(
            new WorldPacketHandlerContext(
                successConnection,
                successLog,
                Session: successSession,
                Sessions: successSessions),
            successSession,
            WarriorPromotionNpcId,
            AdvancedPromotionOption,
            CancellationToken.None);

        Assert.True(handled);
        Assert.True(successSessions.TryGet(successConnection, out var successUpdated));
        Assert.NotNull(successUpdated);
        Assert.Equal(2_002, successUpdated.Character.Class);
        Assert.Empty(ParseItemUsagePackets(successSent));
        Assert.Empty(ParseGameItemPackets(successSent));
        var successReplies = ParseNpcReplies(successSent);
        Assert.Contains(
            successReplies,
            reply => reply.Text == "Excellent! You`ve learned ways of the Warrior. Do you feel a different energy in your body? That`s stronger power and battle prowess!");
        Assert.Contains(
            successReplies,
            reply => reply.InteractType == LegacyNpcReplyInteractType.Avatar && reply.InputMaxLength == 8);
        Assert.Contains(
            "Class=2002",
            File.ReadAllText(Path.Combine(successUsersRoot, CharacterUid + ".ini")),
            StringComparison.Ordinal);
        Assert.DoesNotContain("PARITY-GAP", successLog.ToString());
    }

    [Fact]
    public async Task Option3Class2002ConsumesEmeraldPersistsAndPromotes()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var usersRoot = Path.Combine(_root, "users-emerald");
        var itemsRoot = Path.Combine(_root, "items-emerald");
        CreateMinimalCharacterSnapshot(usersRoot, classCode: 2_002);
        var session = CreateSession(
            classCode: 2_002,
            level: 70,
            usersRoot: usersRoot,
            playerItems: Inventory(
                InventoryItem(2_000, 725_016),
                InventoryItem(2_001, EmeraldItemId)));
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
            WarriorPromotionNpcId,
            AdvancedPromotionOption,
            CancellationToken.None);

        Assert.True(handled);
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(2_003, updated.Character.Class);
        Assert.Equal([725_016U], updated.PlayerItems.InventoryItems.Select(item => item.ItemId).ToArray());
        Assert.Equal([725_016U], new PlayerItemSnapshotLoader(itemsRoot)
            .LoadByCharacterUid(CharacterUid)
            .InventoryItems
            .Select(item => item.ItemId)
            .ToArray());
        Assert.Contains(
            "Class=2003",
            File.ReadAllText(Path.Combine(usersRoot, CharacterUid + ".ini")),
            StringComparison.Ordinal);

        var removes = ParseItemUsagePackets(sent);
        Assert.Single(removes);
        Assert.Equal(2_001U, removes[0].Packet.Id);
        Assert.Equal(MsgItemUsageAction.RemoveInventory, removes[0].Packet.Action);
        Assert.Empty(ParseGameItemPackets(sent));
        var replies = ParseNpcReplies(sent);
        Assert.Contains(
            replies,
            reply => reply.Text == "Excellent! You`ve learned ways of the Taoist. Do you feel a different energy in your body? That`s stronger power and battle prowess!");
        Assert.Contains(
            replies,
            reply => reply.InteractType == LegacyNpcReplyInteractType.Avatar && reply.InputMaxLength == 6);

        var logText = log.ToString();
        Assert.Contains("WorldServer persisted LegacyNpc PromotionMaster item mutation", logText);
        Assert.Contains("WorldServer handled LegacyNpc PromotionWarrior advanced promotion", logText);
        Assert.Contains("consumedItemId=1080001", logText);
        Assert.DoesNotContain("PARITY-GAP", logText);
    }

    [Fact]
    public async Task Option3Class2003WithoutMeteorRejectsAndDoesNotMutate()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var usersRoot = Path.Combine(_root, "users-missing-meteor");
        CreateMinimalCharacterSnapshot(usersRoot, classCode: 2_003);
        var session = CreateSession(
            classCode: 2_003,
            level: 100,
            usersRoot: usersRoot,
            playerItems: Inventory(InventoryItem(2_000, EmeraldItemId)));
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacyPromotionMastersNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            session,
            WarriorPromotionNpcId,
            AdvancedPromotionOption,
            CancellationToken.None);

        Assert.True(handled);
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(2_003, updated.Character.Class);
        Assert.Equal([EmeraldItemId], updated.PlayerItems.InventoryItems.Select(item => item.ItemId).ToArray());
        Assert.Empty(ParseItemUsagePackets(sent));
        Assert.Empty(ParseGameItemPackets(sent));
        var replies = ParseNpcReplies(sent);
        Assert.Contains(replies, reply => reply.Text == "I'm sorry but you need to have 1 Meteor at your inventory.");
        Assert.Contains(replies, reply => reply.Text == "I~see." && reply.OptionId == LegacyNpcReplyPacket.ClosedOptionId);
        Assert.Contains(
            replies,
            reply => reply.InteractType == LegacyNpcReplyInteractType.Avatar && reply.InputMaxLength == 6);
        Assert.Contains(
            "Class=2003",
            File.ReadAllText(Path.Combine(usersRoot, CharacterUid + ".ini")),
            StringComparison.Ordinal);

        var logText = log.ToString();
        Assert.Contains("required inventory item missing", logText);
        Assert.Contains("itemId=1088001", logText);
        Assert.DoesNotContain("PARITY-GAP", logText);
    }

    [Fact]
    public async Task Option3Class2004ConsumesMoonBoxGrantsDragonBallTalksAndPromotes()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var usersRoot = Path.Combine(_root, "users-moonbox");
        var itemsRoot = Path.Combine(_root, "items-moonbox");
        CreateMinimalCharacterSnapshot(usersRoot, classCode: 2_004);
        var session = CreateSession(
            classCode: 2_004,
            level: 110,
            usersRoot: usersRoot,
            playerItems: Inventory(
                InventoryItem(2_000, 725_016),
                InventoryItem(3_000, MoonBoxItemId)));
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacyPromotionMastersNpcAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                Session: session,
                WorldData: CreateWorldDataCatalog(CreateItemType(DragonBallItemId, durability: 125)),
                Sessions: sessions,
                PlayerItemsRoot: itemsRoot),
            session,
            WarriorPromotionNpcId,
            AdvancedPromotionOption,
            CancellationToken.None);

        Assert.True(handled);
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(2_005, updated.Character.Class);
        Assert.Equal([725_016U, DragonBallItemId], updated.PlayerItems.InventoryItems.Select(item => item.ItemId).ToArray());
        Assert.DoesNotContain(updated.PlayerItems.InventoryItems, item => item.ItemId == MoonBoxItemId);
        var dragonBall = Assert.Single(updated.PlayerItems.InventoryItems, item => item.ItemId == DragonBallItemId);
        Assert.Equal((ushort)125, dragonBall.Durability);
        Assert.Equal((ushort)125, dragonBall.MaximumDurability);

        var persistedItems = new PlayerItemSnapshotLoader(itemsRoot).LoadByCharacterUid(CharacterUid);
        Assert.Equal([725_016U, DragonBallItemId], persistedItems.InventoryItems.Select(item => item.ItemId).ToArray());
        Assert.Contains(
            "Class=2005",
            File.ReadAllText(Path.Combine(usersRoot, CharacterUid + ".ini")),
            StringComparison.Ordinal);

        var removes = ParseItemUsagePackets(sent);
        Assert.Single(removes);
        Assert.Equal(3_000U, removes[0].Packet.Id);
        Assert.Equal(MsgItemUsageAction.RemoveInventory, removes[0].Packet.Action);
        var grantedItems = ParseGameItemPackets(sent);
        var granted = Assert.Single(grantedItems);
        Assert.Equal(DragonBallItemId, granted.Packet.ItemId);
        Assert.Equal(LegacyGameItemPacket.AddItemMode, granted.Packet.Mode);
        Assert.Equal((ushort)125, granted.Packet.Durability);
        Assert.True(removes[0].Index < granted.Index);

        var systemTalks = ParseSystemTalkMessages(sent);
        var talk = Assert.Single(systemTalks);
        Assert.Equal("You received DragonBall.", talk.Packet.Content[3]);
        Assert.True(granted.Index < talk.Index);

        var replies = ParseNpcReplies(sent);
        Assert.Contains(
            replies,
            reply => reply.Text == "Excellent!Do you feel a different energy in your body? That`s stronger power and battle prowess!");
        Assert.Contains(
            replies,
            reply => reply.InteractType == LegacyNpcReplyInteractType.Avatar && reply.InputMaxLength == 6);

        var logText = log.ToString();
        Assert.Contains("consumedItemId=721080", logText);
        Assert.Contains("grantedItemId=1088000", logText);
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
            Username: "warrior-advanced-promotion-test",
            Character: new CharacterSnapshot(
                Uid: CharacterUid,
                Name: "WarriorHero",
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

    private static PlayerItemSnapshot Inventory(params PlayerItemSnapshotItem[] items)
    {
        return PlayerItemSnapshot.Empty with
        {
            InventoryItems = items,
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

    private static IReadOnlyList<(int Index, MsgItemUsagePacket Packet)> ParseItemUsagePackets(
        IReadOnlyList<byte[]> sent)
    {
        var packets = new List<(int Index, MsgItemUsagePacket Packet)>();
        for (var index = 0; index < sent.Count; index++)
        {
            if (MsgItemUsagePacket.TryParse(ReadFrame(sent[index]), out var packet))
            {
                packets.Add((index, packet));
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

    private static IReadOnlyList<(int Index, LegacyMsgMessagePacket Packet)> ParseSystemTalkMessages(
        IReadOnlyList<byte[]> sent)
    {
        var packets = new List<(int Index, LegacyMsgMessagePacket Packet)>();
        for (var index = 0; index < sent.Count; index++)
        {
            if (LegacyMsgMessagePacket.TryParse(ReadFrame(sent[index]), out var packet) &&
                packet.ChatType == LegacyMsgMessagePacket.TalkChatType)
            {
                packets.Add((index, packet));
            }
        }

        return packets;
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
                "fixture://warrior-advanced-promotion",
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
