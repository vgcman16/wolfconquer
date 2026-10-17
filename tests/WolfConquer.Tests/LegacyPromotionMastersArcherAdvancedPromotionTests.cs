using System.Buffers;
using System.Globalization;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyPromotionMastersArcherAdvancedPromotionTests : IDisposable
{
    private const uint ArcherPromotionNpcId = 400;
    private const byte AdvancedPromotionOption = 33;
    private const long CharacterUid = 1_294_633;
    private const uint EuxeniteOreItemId = 1_072_031;
    private const uint ScarletBowItemId = 500_069;
    private const uint EmeraldItemId = 1_080_001;
    private const uint MoonBoxItemId = 721_080;
    private const uint DragonBallItemId = 1_088_000;

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "WolfConquer-LegacyPromotionMastersArcherAdvancedPromotionTests-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Option33RejectsLowLevelArcherWithLegacyTextAndAvatar()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(classCode: 4_001, level: 20, playerItems: InventoryItem(EuxeniteOreItemId, 5));
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacyPromotionMastersNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            session,
            ArcherPromotionNpcId,
            AdvancedPromotionOption,
            CancellationToken.None);

        Assert.True(handled);
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(4_001, updated.Character.Class);
        Assert.Equal([EuxeniteOreItemId], updated.PlayerItems.InventoryItems.Select(item => item.ItemId).ToArray());

        var replies = ParseNpcReplies(sent);
        Assert.Contains(
            replies,
            reply => reply.Text == "Sorry,~you~cannot~get~promoted~before~you~reach~level~40.~Please~train~harder.");
        Assert.Contains(
            replies,
            reply => reply.InteractType == LegacyNpcReplyInteractType.Avatar && reply.InputMaxLength == 234);
        Assert.DoesNotContain("PARITY-GAP", log.ToString());
    }

    [Fact]
    public async Task Option33ConsumesEuxeniteOreGrantsScarletBowAndPromotesArcher()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var usersRoot = Path.Combine(_root, "users-euxenite");
        var itemsRoot = Path.Combine(_root, "items-euxenite");
        CreateMinimalCharacterSnapshot(usersRoot, classCode: 4_001);
        var session = CreateSession(
            classCode: 4_001,
            level: 40,
            usersRoot: usersRoot,
            playerItems: Inventory(
                Item(uid: 2_000, itemId: 725_016),
                Item(uid: 2_001, itemId: EuxeniteOreItemId, stackSize: 5)));
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacyPromotionMastersNpcAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                Session: session,
                WorldData: CreateWorldDataCatalog(CreateItemType(ScarletBowItemId, "ScarletBow", durability: 999)),
                Sessions: sessions,
                PlayerItemsRoot: itemsRoot),
            session,
            ArcherPromotionNpcId,
            AdvancedPromotionOption,
            CancellationToken.None);

        Assert.True(handled);
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(4_002, updated.Character.Class);
        Assert.DoesNotContain(updated.PlayerItems.InventoryItems, item => item.ItemId == EuxeniteOreItemId);

        var bow = Assert.Single(updated.PlayerItems.InventoryItems, item => item.ItemId == ScarletBowItemId);
        Assert.Equal(2_001U, bow.Uid);
        Assert.Equal((ushort)999, bow.Durability);
        Assert.Equal((ushort)999, bow.MaximumDurability);
        Assert.Equal((ushort)1, bow.StackSize);

        var persistedItems = new PlayerItemSnapshotLoader(itemsRoot).LoadByCharacterUid(CharacterUid);
        Assert.DoesNotContain(persistedItems.InventoryItems, item => item.ItemId == EuxeniteOreItemId);
        Assert.Contains(persistedItems.InventoryItems, item => item.ItemId == ScarletBowItemId);

        var persistedCharacter = File.ReadAllText(Path.Combine(usersRoot, CharacterUid + ".ini"));
        Assert.Contains("Class=4002", persistedCharacter, StringComparison.Ordinal);

        var itemPacket = Assert.Single(ParseGameItemPackets(sent), entry => entry.Packet.ItemId == ScarletBowItemId);
        Assert.Equal(LegacyGameItemPacket.AddItemMode, itemPacket.Packet.Mode);
        Assert.Equal("You received 1 (Super)ScarletBow.", Assert.Single(ParseSystemTalks(sent)));
        Assert.Contains(
            ParseNpcReplies(sent),
            reply => reply.Text == "Excellent! You`ve learned ways of the Archer. Do you feel a different energy in your body? That`s stronger power and battle prowess!");

        var logText = log.ToString();
        Assert.Contains("WorldServer handled LegacyNpc PromotionArcher advanced promotion", logText);
        Assert.Contains("sourceRange=NpcHandler.cs:48780-48982", logText);
        Assert.Contains("requiredItemId=1072031", logText);
        Assert.Contains("rewardItemId=500069", logText);
        Assert.DoesNotContain("PARITY-GAP", logText);
    }

    [Fact]
    public async Task Option33ConsumesEmeraldAndPromotesEagleArcher()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var usersRoot = Path.Combine(_root, "users-emerald");
        var itemsRoot = Path.Combine(_root, "items-emerald");
        CreateMinimalCharacterSnapshot(usersRoot, classCode: 4_002);
        var session = CreateSession(
            classCode: 4_002,
            level: 70,
            usersRoot: usersRoot,
            playerItems: Inventory(Item(uid: 3_000, itemId: EmeraldItemId)));
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
            ArcherPromotionNpcId,
            AdvancedPromotionOption,
            CancellationToken.None);

        Assert.True(handled);
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(4_003, updated.Character.Class);
        Assert.Empty(updated.PlayerItems.InventoryItems);
        Assert.Empty(new PlayerItemSnapshotLoader(itemsRoot).LoadByCharacterUid(CharacterUid).InventoryItems);

        var persistedCharacter = File.ReadAllText(Path.Combine(usersRoot, CharacterUid + ".ini"));
        Assert.Contains("Class=4003", persistedCharacter, StringComparison.Ordinal);
        Assert.Empty(ParseGameItemPackets(sent));
        Assert.Empty(ParseSystemTalks(sent));
        Assert.Contains(
            ParseNpcReplies(sent),
            reply => reply.Text == "Excellent! You`ve learned ways of the Taoist. Do you feel a different energy in your body? That`s stronger power and battle prowess!");

        var logText = log.ToString();
        Assert.Contains("requiredItemId=1080001", logText);
        Assert.Contains("rewardItemId=none", logText);
        Assert.DoesNotContain("PARITY-GAP", logText);
    }

    [Fact]
    public async Task Option33ConsumesMoonBoxGrantsDragonBallAndPromotesDragonArcher()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var usersRoot = Path.Combine(_root, "users-moonbox");
        var itemsRoot = Path.Combine(_root, "items-moonbox");
        CreateMinimalCharacterSnapshot(usersRoot, classCode: 4_004);
        var session = CreateSession(
            classCode: 4_004,
            level: 110,
            usersRoot: usersRoot,
            playerItems: Inventory(
                Item(uid: 4_000, itemId: 725_016),
                Item(uid: 4_001, itemId: MoonBoxItemId)));
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacyPromotionMastersNpcAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                Session: session,
                WorldData: CreateWorldDataCatalog(CreateItemType(DragonBallItemId, "DragonBall", durability: 1)),
                Sessions: sessions,
                PlayerItemsRoot: itemsRoot),
            session,
            ArcherPromotionNpcId,
            AdvancedPromotionOption,
            CancellationToken.None);

        Assert.True(handled);
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(4_005, updated.Character.Class);
        Assert.DoesNotContain(updated.PlayerItems.InventoryItems, item => item.ItemId == MoonBoxItemId);

        var dragonBall = Assert.Single(updated.PlayerItems.InventoryItems, item => item.ItemId == DragonBallItemId);
        Assert.Equal(4_001U, dragonBall.Uid);
        Assert.Equal((ushort)1, dragonBall.Durability);

        var persistedItems = new PlayerItemSnapshotLoader(itemsRoot).LoadByCharacterUid(CharacterUid);
        Assert.DoesNotContain(persistedItems.InventoryItems, item => item.ItemId == MoonBoxItemId);
        Assert.Contains(persistedItems.InventoryItems, item => item.ItemId == DragonBallItemId);

        var persistedCharacter = File.ReadAllText(Path.Combine(usersRoot, CharacterUid + ".ini"));
        Assert.Contains("Class=4005", persistedCharacter, StringComparison.Ordinal);

        var itemPacket = Assert.Single(ParseGameItemPackets(sent), entry => entry.Packet.ItemId == DragonBallItemId);
        Assert.Equal(LegacyGameItemPacket.AddItemMode, itemPacket.Packet.Mode);
        Assert.Equal("You received DragonBall.", Assert.Single(ParseSystemTalks(sent)));
        Assert.Contains(
            ParseNpcReplies(sent),
            reply => reply.Text == "Excellent!Do you feel a different energy in your body? That`s stronger power and battle prowess!");

        var logText = log.ToString();
        Assert.Contains("requiredItemId=721080", logText);
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
        PlayerItemSnapshot? playerItems = null,
        long reborn = 0)
    {
        return new WorldSession(
            AccountId: 100,
            Username: "archer-advanced-promotion-test",
            Character: new CharacterSnapshot(
                Uid: CharacterUid,
                Name: "ArcherHero",
                Body: 1003,
                Face: 0,
                Hair: 0,
                Class: classCode,
                FirstClass: classCode,
                SecondClass: 0,
                Reborn: reborn,
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

    private static void CreateMinimalCharacterSnapshot(string usersRoot, long classCode)
    {
        Directory.CreateDirectory(usersRoot);
        File.WriteAllText(
            Path.Combine(usersRoot, CharacterUid + ".ini"),
            "[Character]" + Environment.NewLine +
            "UID=" + CharacterUid + Environment.NewLine +
            "Class=" + classCode + Environment.NewLine);
    }

    private static PlayerItemSnapshot Inventory(params PlayerItemSnapshotItem[] items)
    {
        return PlayerItemSnapshot.Empty with
        {
            InventoryItems = items,
        };
    }

    private static PlayerItemSnapshot InventoryItem(uint itemId, ushort stackSize = 1)
    {
        return Inventory(Item(uid: 2_000, itemId: itemId, stackSize: stackSize));
    }

    private static PlayerItemSnapshotItem Item(uint uid, uint itemId, ushort stackSize = 1)
    {
        return new PlayerItemSnapshotItem
        {
            Uid = uid,
            ItemId = itemId,
            Durability = 1,
            MaximumDurability = 1,
            Position = 0,
            StackSize = stackSize,
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

    private static IReadOnlyList<string> ParseSystemTalks(IReadOnlyList<byte[]> sent)
    {
        var messages = new List<string>();
        foreach (var bytes in sent)
        {
            if (LegacyMsgMessagePacket.TryParse(ReadFrame(bytes), out var packet) &&
                packet.ChatType == LegacyMsgMessagePacket.TalkChatType)
            {
                messages.Add(packet.Content[3]);
            }
        }

        return messages;
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
                "fixture://archer-advanced-promotion",
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

    private static ItemType CreateItemType(uint itemTypeId, string name, ushort durability)
    {
        var fields = Enumerable.Repeat("0", 66).ToArray();
        fields[0] = itemTypeId.ToString(CultureInfo.InvariantCulture);
        fields[1] = name;
        fields[ItemType.DurabilityFieldIndex] = durability.ToString(CultureInfo.InvariantCulture);
        return new ItemType(checked((int)itemTypeId), name, fields);
    }

    private static GuildWarData EmptyGuildWarData(string name)
    {
        return new GuildWarData("fixture://" + name, [], [], diagnostics: [], info: null, pole: null, conductors: []);
    }
}
