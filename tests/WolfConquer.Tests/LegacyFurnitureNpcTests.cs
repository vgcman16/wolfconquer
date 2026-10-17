using System.Buffers;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyFurnitureNpcTests
{
    private const uint FurnitureNpcId = 8_924;
    private const int FurnitureItemId = 726_083;

    [Fact]
    public async Task HandlerMap1511FurnitureOptionZeroSendsSourceBuyDialog()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession();
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                Session: session,
                Sessions: sessions,
                WorldData: CreateCatalog()),
            ReadFrame(LegacyNpcServerRequestPacket.Script(FurnitureNpcId, optionId: 0, interactType: 1).ToFrameBytes()));

        Assert.Equal(5, sent.Count);
        Assert.Equal(
            "Do you want this FlowerDressingTable ? It costs only 23000 silvers.",
            AssertNpcReply(sent[0], LegacyNpcReplyInteractType.Dialog).Text);
        Assert.Equal("Yes.", AssertNpcReply(sent[1], LegacyNpcReplyInteractType.Option).Text);
        Assert.Equal(1, AssertNpcReply(sent[1], LegacyNpcReplyInteractType.Option).OptionId);
        Assert.Equal("No.", AssertNpcReply(sent[2], LegacyNpcReplyInteractType.Option).Text);
        Assert.Equal(byte.MaxValue, AssertNpcReply(sent[2], LegacyNpcReplyInteractType.Option).OptionId);
        AssertNpcReply(sent[3], LegacyNpcReplyInteractType.Avatar);
        AssertNpcReply(sent[4], LegacyNpcReplyInteractType.Finish);
        Assert.Contains("source=NpcHandler.Furnitures", log.ToString());
    }

    [Fact]
    public async Task HandlerMap1511FurnitureOptionOneChargesSilverAndAddsItem()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(money: 30_000);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                Session: session,
                Sessions: sessions,
                WorldData: CreateCatalog()),
            ReadFrame(LegacyNpcServerRequestPacket.Script(FurnitureNpcId, optionId: 1, interactType: 1).ToFrameBytes()));

        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(7_000, updated.Character.Money);
        var item = Assert.Single(updated.PlayerItems.InventoryItems);
        Assert.Equal((uint)FurnitureItemId, item.ItemId);
        Assert.Equal(1U, item.Uid);

        AssertSingleUpdate(sent[0], LegacyMsgUpdateDataType.Money, 7_000);
        Assert.True(LegacyGameItemPacket.TryParse(ReadFrame(sent[1]), out var gameItem));
        Assert.Equal((uint)FurnitureItemId, gameItem.ItemId);
        Assert.True(LegacyMsgMessagePacket.TryParse(ReadFrame(sent[2]), out var message));
        Assert.Contains("You got a FlowerDressingTable!", message.Content);
        Assert.Contains("WorldServer handled LegacyNpc furniture purchase", log.ToString());
    }

    [Fact]
    public async Task HandlerMap1511FurnitureOptionOneRejectsMissingSilverWithoutMutation()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(money: 22_999);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                Session: session,
                Sessions: sessions,
                WorldData: CreateCatalog()),
            ReadFrame(LegacyNpcServerRequestPacket.Script(FurnitureNpcId, optionId: 1, interactType: 1).ToFrameBytes()));

        Assert.Equal("You do not have 23000 silvers with you.", AssertNpcReply(sent[0], LegacyNpcReplyInteractType.Dialog).Text);
        Assert.Equal("I~see.", AssertNpcReply(sent[1], LegacyNpcReplyInteractType.Option).Text);
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(22_999, updated.Character.Money);
        Assert.Empty(updated.PlayerItems.InventoryItems);
    }

    [Fact]
    public async Task HandlerMap1511FurnitureOptionOneRejectsFullInventoryWithoutMutation()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(money: 30_000, inventoryItems: FullInventory());
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                Session: session,
                Sessions: sessions,
                WorldData: CreateCatalog()),
            ReadFrame(LegacyNpcServerRequestPacket.Script(FurnitureNpcId, optionId: 1, interactType: 1).ToFrameBytes()));

        Assert.Equal("There is not enough room in your inventory.", AssertNpcReply(sent[0], LegacyNpcReplyInteractType.Dialog).Text);
        Assert.Equal("Ah~yes.", AssertNpcReply(sent[1], LegacyNpcReplyInteractType.Option).Text);
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(30_000, updated.Character.Money);
        Assert.Equal(40, updated.PlayerItems.InventoryItems.Count);
    }

    [Fact]
    public async Task HandlerDoesNotRouteFurnitureNpcOutsideMap1511()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(mapId: 1_002, x: 143, y: 130);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                Session: session,
                Sessions: sessions,
                WorldData: CreateCatalog(furnitureMapId: 1_002)),
            ReadFrame(LegacyNpcServerRequestPacket.Script(FurnitureNpcId, optionId: 0, interactType: 1).ToFrameBytes()));

        Assert.Single(sent);
        Assert.Equal(
            "This NPC is present, but its legacy script is not modeled yet.",
            AssertNpcReply(sent[0], LegacyNpcReplyInteractType.MessageBox).Text);
        Assert.DoesNotContain("source=NpcHandler.Furnitures", log.ToString());
    }

    private static WorldSession CreateSession(
        long money = 0,
        int mapId = 1_511,
        int x = 143,
        int y = 130,
        IReadOnlyList<PlayerItemSnapshotItem>? inventoryItems = null)
    {
        return new WorldSession(
            AccountId: 100,
            Username: "furniture-test",
            Character: new CharacterSnapshot(
                Uid: 1_293_600,
                Name: "HouseBuyer",
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
                Money: money,
                ConquerPoints: 0,
                BoundConquerPoints: 0,
                BankConquerPoints: 0,
                PkPoints: 0,
                Online: 0,
                SourceFile: "fixture",
                Attributes: new Dictionary<string, string>(),
                PaidNobility: new Dictionary<string, string>()),
            PlayerItems: new PlayerItemSnapshot(inventoryItems ?? [], [], [], [], []),
            PlayerSpells: PlayerSpellSnapshot.Empty,
            PlayerProficiencies: PlayerProficiencySnapshot.Empty);
    }

    private static IReadOnlyList<PlayerItemSnapshotItem> FullInventory()
    {
        return Enumerable.Range(1, 40)
            .Select(index => new PlayerItemSnapshotItem
            {
                Uid = (uint)index,
                ItemId = 100_000,
                StackSize = 1,
            })
            .ToArray();
    }

    private static WorldDataCatalog CreateCatalog(int furnitureMapId = 1_511)
    {
        return WorldDataCatalog.Create(
            new SeedManifest(
                DateTimeOffset.Parse("2026-06-05T00:00:00Z"),
                "fixture://legacy-furniture",
                new Dictionary<string, int>()),
            clientConfigs: [],
            items: [CreateItemType()],
            itemAddBonuses: [],
            levelExperience: [],
            runeLevelExperience: [],
            runeStorageAttributes: [],
            itemRefineEffects: [],
            itemRefineEffectEx: [],
            itemRefineUpgrades: [],
            transformInfo: [],
            furniture: [CreateFurniture(furnitureMapId)],
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

    private static Furniture CreateFurniture(int mapId)
    {
        return new Furniture(
            (int)FurnitureNpcId,
            Kind: 26,
            Lookface: 837,
            MapId: mapId,
            X: 143,
            Y: 130,
            Name: "FlowerDressingTable",
            ItemTypeId: FurnitureItemId,
            Price: 23_000,
            SourceFile: "furnitures.txt",
            LineNumber: 1,
            RawLine: "8924,26,837,1511,143,130,FlowerDressingTable,726083,23000",
            Fields: ["8924", "26", "837", mapId.ToString(), "143", "130", "FlowerDressingTable", "726083", "23000"]);
    }

    private static ItemType CreateItemType()
    {
        var fields = Enumerable.Repeat("0", 66).ToArray();
        fields[0] = FurnitureItemId.ToString();
        fields[1] = "FlowerDressingTable";
        fields[ItemType.DurabilityFieldIndex] = "1200";
        return new ItemType(FurnitureItemId, "FlowerDressingTable", fields);
    }

    private static GuildWarData EmptyGuildWarData(string name)
    {
        return new GuildWarData("fixture://" + name, [], [], [], info: null, pole: null, conductors: []);
    }

    private static ConquerTcpConnection CreateSendingConnection(List<byte[]> sent)
    {
        return new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958), (frame, _) =>
        {
            sent.Add(frame.ToArray());
            return ValueTask.CompletedTask;
        });
    }

    private static LegacyNpcReplyPacket AssertNpcReply(byte[] bytes, LegacyNpcReplyInteractType expectedType)
    {
        Assert.True(LegacyNpcReplyPacket.TryParse(ReadFrame(bytes), out var packet));
        Assert.Equal(expectedType, packet.InteractType);
        return packet;
    }

    private static void AssertSingleUpdate(byte[] bytes, LegacyMsgUpdateDataType type, ulong value)
    {
        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var update));
        var entry = Assert.Single(update.Updates);
        Assert.Equal(type, entry.Type);
        Assert.Equal(value, entry.Value);
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }
}
