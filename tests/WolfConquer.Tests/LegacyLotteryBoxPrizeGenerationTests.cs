using System.Buffers;
using System.Globalization;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyLotteryBoxPrizeGenerationTests : IDisposable
{
    private const long AccountId = 100;
    private const long CharacterUid = 1_293_600;
    private const uint LotteryBoxNpcId = 925;
    private const uint SmallLotteryTicketItemId = 711_504;
    private const uint PrizeItemId = 151_159;

    private readonly string _usersRoot = Path.Combine(
        Path.GetTempPath(),
        "wolf-lottery-prize-users-" + Guid.NewGuid());
    private readonly string _itemsRoot = Path.Combine(
        Path.GetTempPath(),
        "wolf-lottery-prize-items-" + Guid.NewGuid());

    [Fact]
    public async Task HandlerGeneratesSourceShapedPrizePersistsWholeDrawAndReloadsIt()
    {
        var initialItems = PrepareSnapshots(
            [Item(uid: 50, SmallLotteryTicketItemId, stackSize: 3)],
            heroRewards: "1/101/1/");
        var worldData = CreateWorldDataCatalog(
            items: [ItemTypeWithDurability(PrizeItemId, durability: 900)],
            lotteryPrizes: [Prize(socketCount: 2, plus: 8)]);
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(initialItems.InventoryItems, heroRewards: "1/101/1/");
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        await DispatchDrawAsync(connection, log, session, sessions, worldData);

        var updated = GetRequiredSession(sessions, connection);
        Assert.True(
            updated.Character.Attributes["LotteryEntries"] == "3",
            log.ToString());
        Assert.Equal("0", updated.Character.Attributes["AddJade"]);
        Assert.Equal("2/101/1/306/0/", updated.Character.Attributes["HeroRewards"]);

        Assert.DoesNotContain(
            updated.PlayerItems.InventoryItems,
            item => item.ItemId == SmallLotteryTicketItemId);
        var prize = Assert.Single(updated.PlayerItems.InventoryItems, item => item.ItemId == PrizeItemId);
        Assert.Equal(51U, prize.Uid);
        Assert.Equal((ushort)900, prize.Durability);
        Assert.Equal((ushort)900, prize.MaximumDurability);
        Assert.Equal(byte.MaxValue, prize.SocketOne);
        Assert.Equal(byte.MaxValue, prize.SocketTwo);
        Assert.Equal((byte)8, prize.Plus);
        Assert.Equal(6U, prize.Color);
        Assert.Equal((ushort)1, prize.StackSize);

        var reloadedCharacter = CharacterSnapshotParser.ParseText(
            File.ReadAllText(CharacterPath(), LegacyCharacterEncoding()));
        Assert.Equal("3", reloadedCharacter.Attributes["LotteryEntries"]);
        Assert.Equal("0", reloadedCharacter.Attributes["AddJade"]);
        Assert.Equal("2/101/1/306/0/", reloadedCharacter.Attributes["HeroRewards"]);
        var reloadedItems = new PlayerItemSnapshotLoader(_itemsRoot).LoadByCharacterUid(CharacterUid);
        var reloadedPrize = Assert.Single(reloadedItems.InventoryItems, item => item.ItemId == PrizeItemId);
        Assert.Equal(prize.Uid, reloadedPrize.Uid);
        Assert.Equal(prize.Durability, reloadedPrize.Durability);
        Assert.Equal(prize.MaximumDurability, reloadedPrize.MaximumDurability);
        Assert.Equal(prize.SocketOne, reloadedPrize.SocketOne);
        Assert.Equal(prize.SocketTwo, reloadedPrize.SocketTwo);
        Assert.Equal(prize.Plus, reloadedPrize.Plus);
        Assert.Equal(prize.Color, reloadedPrize.Color);

        Assert.Equal(3, sent.Count);
        Assert.True(MsgItemUsagePacket.TryParse(ReadFrame(sent[0]), out var ticketRemoval));
        Assert.Equal(MsgItemUsageAction.RemoveInventory, ticketRemoval.Action);
        Assert.Equal(50U, ticketRemoval.Id);
        Assert.True(LegacyGameItemPacket.TryParse(ReadFrame(sent[1]), out var prizeAdd));
        Assert.Equal(LegacyGameItemPacket.AddItemMode, prizeAdd.Mode);
        Assert.Equal(PrizeItemId, prizeAdd.ItemId);
        Assert.Equal(byte.MaxValue, prizeAdd.SocketOne);
        Assert.Equal(byte.MaxValue, prizeAdd.SocketTwo);
        Assert.Equal((byte)8, prizeAdd.Plus);
        Assert.True(LegacyStringPacket.TryParse(ReadFrame(sent[2]), out var effect));
        Assert.Equal(LegacyStringId.Effect, effect.Id);
        Assert.Equal(LotteryBoxNpcId, effect.Uid);
        Assert.Equal(["lottery"], effect.Strings);

        Assert.Contains("rank=1 sourcePoolIndex=0", log.ToString(), StringComparison.Ordinal);
        Assert.Contains("sourceFile=lottery.ini sourceLine=2", log.ToString(), StringComparison.Ordinal);
        Assert.Contains("materialization=bounded-local-immediate-grant", log.ToString(), StringComparison.Ordinal);
        Assert.Contains("only assigns Player.LotteryItem", log.ToString(), StringComparison.Ordinal);
        Assert.Contains("LotteryTable.CreateGameItem is otherwise unused", log.ToString(), StringComparison.Ordinal);
        Assert.Contains("rather than reproducing an exact observed source call", log.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("reward generation fenced", log.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task HandlerMissingAuthoritativePrizeItemDataPreservesWholeDraw()
    {
        var initialItems = PrepareSnapshots(
            [Item(uid: 60, SmallLotteryTicketItemId, stackSize: 3)],
            heroRewards: "1/101/1/");
        var characterBefore = File.ReadAllBytes(CharacterPath());
        var itemsBefore = File.ReadAllBytes(ItemsPath());
        var worldData = CreateWorldDataCatalog(
            items: [],
            lotteryPrizes: [Prize(socketCount: 2, plus: 8)]);
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(initialItems.InventoryItems, heroRewards: "1/101/1/");
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        await DispatchDrawAsync(connection, log, session, sessions, worldData);

        var unchanged = GetRequiredSession(sessions, connection);
        Assert.Equal("2", unchanged.Character.Attributes["LotteryEntries"]);
        Assert.Equal("7", unchanged.Character.Attributes["AddJade"]);
        Assert.Equal("1/101/1/", unchanged.Character.Attributes["HeroRewards"]);
        Assert.Equal((ushort)3, Assert.Single(unchanged.PlayerItems.InventoryItems).StackSize);
        Assert.Empty(sent);
        Assert.Equal(characterBefore, File.ReadAllBytes(CharacterPath()));
        Assert.Equal(itemsBefore, File.ReadAllBytes(ItemsPath()));
        Assert.Contains("reason=lotteryRank1HasNoEligibleItemType", log.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task HandlerFullInventoryPreservesTicketsPrizeAndLotteryState()
    {
        var inventory = Enumerable.Range(0, 40)
            .Select(index => Item(
                uid: checked((uint)(100 + index)),
                itemId: index == 0 ? SmallLotteryTicketItemId : checked((uint)(700_000 + index)),
                stackSize: index == 0 ? (ushort)3 : (ushort)1))
            .ToArray();
        var initialItems = PrepareSnapshots(inventory, heroRewards: "1/101/1/");
        var characterBefore = File.ReadAllBytes(CharacterPath());
        var itemsBefore = File.ReadAllBytes(ItemsPath());
        var worldData = CreateWorldDataCatalog(
            items: [ItemTypeWithDurability(PrizeItemId, durability: 900)],
            lotteryPrizes: [Prize(socketCount: 2, plus: 8)]);
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(initialItems.InventoryItems, heroRewards: "1/101/1/");
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        await DispatchDrawAsync(connection, log, session, sessions, worldData);

        var unchanged = GetRequiredSession(sessions, connection);
        Assert.Equal(40, unchanged.PlayerItems.InventoryItems.Count);
        Assert.Equal(
            (ushort)3,
            Assert.Single(
                unchanged.PlayerItems.InventoryItems,
                item => item.ItemId == SmallLotteryTicketItemId).StackSize);
        Assert.Equal("2", unchanged.Character.Attributes["LotteryEntries"]);
        Assert.Equal("7", unchanged.Character.Attributes["AddJade"]);
        Assert.Equal("1/101/1/", unchanged.Character.Attributes["HeroRewards"]);
        Assert.Empty(sent);
        Assert.Equal(characterBefore, File.ReadAllBytes(CharacterPath()));
        Assert.Equal(itemsBefore, File.ReadAllBytes(ItemsPath()));
        Assert.Contains("inventory is full", log.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task HandlerMissingSessionStorePreservesWholeDrawBeforePersistence()
    {
        var initialItems = PrepareSnapshots(
            [Item(uid: 70, SmallLotteryTicketItemId, stackSize: 3)],
            heroRewards: "1/101/1/");
        var characterBefore = File.ReadAllBytes(CharacterPath());
        var itemsBefore = File.ReadAllBytes(ItemsPath());
        var worldData = CreateWorldDataCatalog(
            items: [ItemTypeWithDurability(PrizeItemId, durability: 900)],
            lotteryPrizes: [Prize(socketCount: 2, plus: 8)]);
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(initialItems.InventoryItems, heroRewards: "1/101/1/");

        await DispatchDrawAsync(connection, log, session, sessions: null, worldData: worldData);

        Assert.Equal("2", session.Character.Attributes["LotteryEntries"]);
        Assert.Equal("7", session.Character.Attributes["AddJade"]);
        Assert.Equal("1/101/1/", session.Character.Attributes["HeroRewards"]);
        Assert.Equal((ushort)3, Assert.Single(session.PlayerItems.InventoryItems).StackSize);
        Assert.Empty(sent);
        Assert.Equal(characterBefore, File.ReadAllBytes(CharacterPath()));
        Assert.Equal(itemsBefore, File.ReadAllBytes(ItemsPath()));
        Assert.Contains("reason=sessionStoreMissing", log.ToString(), StringComparison.Ordinal);
        Assert.Contains("without persistence, session mutation, or packets", log.ToString(), StringComparison.Ordinal);
    }

    private PlayerItemSnapshot PrepareSnapshots(
        IReadOnlyList<PlayerItemSnapshotItem> inventoryItems,
        string heroRewards)
    {
        Directory.CreateDirectory(_usersRoot);
        Directory.CreateDirectory(_itemsRoot);
        WriteCharacterSnapshot(heroRewards);
        var snapshot = new PlayerItemSnapshot(inventoryItems, [], [], [], []);
        new PlayerItemSnapshotLoader(_itemsRoot).SaveByCharacterUid(CharacterUid, snapshot);
        return snapshot;
    }

    private async Task DispatchDrawAsync(
        ConquerTcpConnection connection,
        TextWriter log,
        WorldSession session,
        WorldSessionStore? sessions,
        WorldDataCatalog worldData)
    {
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);
        await registry.HandleAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                Session: session,
                WorldData: worldData,
                Sessions: sessions,
                PlayerItemsRoot: _itemsRoot),
            ReadFrame(LegacyNpcServerRequestPacket.Script(LotteryBoxNpcId, optionId: 1, interactType: 1).ToFrameBytes()));
    }

    private static WorldSession GetRequiredSession(
        WorldSessionStore sessions,
        ConquerTcpConnection connection)
    {
        Assert.True(sessions.TryGet(connection, out var session));
        return Assert.IsType<WorldSession>(session);
    }

    private WorldSession CreateSession(
        IReadOnlyList<PlayerItemSnapshotItem> inventoryItems,
        string heroRewards)
    {
        return new WorldSession(
            AccountId,
            Username: "lottery-prize-test",
            Character: new CharacterSnapshot(
                Uid: CharacterUid,
                Name: "Lucky",
                Body: 1003,
                Face: 0,
                Hair: 0,
                Class: 7051,
                FirstClass: 7051,
                SecondClass: 0,
                Reborn: 0,
                Level: 140,
                Experience: 0,
                MapId: 700,
                X: 50,
                Y: 50,
                PreviousMapId: 700,
                PreviousMapX: 50,
                PreviousMapY: 50,
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
                Attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["LotteryEntries"] = "2",
                    ["AddJade"] = "7",
                    ["HeroRewards"] = heroRewards,
                    ["DynamicID"] = "0",
                    ["LegacyLotteryRank"] = "1",
                    ["LegacyLotteryPrizeIndex"] = "0",
                    ["LegacyLotteryColor"] = "6",
                },
                PaidNobility: new Dictionary<string, string>()),
            PlayerItems: new PlayerItemSnapshot(inventoryItems, [], [], [], []),
            PlayerSpells: PlayerSpellSnapshot.Empty,
            PlayerProficiencies: PlayerProficiencySnapshot.Empty,
            UsersRoot: _usersRoot);
    }

    private static PlayerItemSnapshotItem Item(uint uid, uint itemId, ushort stackSize)
    {
        return new PlayerItemSnapshotItem
        {
            Uid = uid,
            ItemId = itemId,
            Position = 0,
            StackSize = stackSize,
        };
    }

    private static LotteryPrize Prize(int socketCount, int plus)
    {
        return new LotteryPrize(
            Type: 1,
            Level: 70,
            ItemName: "WavyHeavyRing+8",
            ItemTypeRaw: PrizeItemId.ToString(CultureInfo.InvariantCulture),
            ItemTypeId: checked((int)PrizeItemId),
            SocketOne: 2,
            SocketTwo: socketCount,
            PlusLevel: plus,
            SourceFile: "lottery.ini",
            LineNumber: 2,
            RawLine: "1 70 WavyHeavyRing+8 151159 2 2 8",
            Fields: ["1", "70", "WavyHeavyRing+8", "151159", "2", "2", "8"]);
    }

    private static ItemType ItemTypeWithDurability(uint itemId, ushort durability)
    {
        var fields = Enumerable.Repeat("0", 66).ToArray();
        fields[ItemType.DurabilityFieldIndex] = durability.ToString(CultureInfo.InvariantCulture);
        return new ItemType(checked((int)itemId), "WavyHeavyRing", fields);
    }

    private static WorldDataCatalog CreateWorldDataCatalog(
        IReadOnlyList<ItemType> items,
        IReadOnlyList<LotteryPrize> lotteryPrizes)
    {
        return WorldDataCatalog.Create(
            new SeedManifest(
                DateTimeOffset.Parse("2026-07-10T00:00:00Z", CultureInfo.InvariantCulture),
                "fixture://legacy-lottery-prize",
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
            activityTasks: new ActivityTaskCatalog(
                new ActivityTaskMetadata(0, "", 0, []),
                [],
                "fixture://activity"),
            meltingTypeRules: [],
            lotteryPrizes: lotteryPrizes,
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
            npcs: [new Npc(checked((int)LotteryBoxNpcId), 0, 0, 700, 50, 50, null)],
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
            neiGongHeader: new NeiGongHeader(
                0,
                [],
                "fixture://neigong",
                0,
                "",
                new Dictionary<string, string>()),
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

    private static GuildWarData EmptyGuildWarData(string name)
    {
        return new GuildWarData(
            "fixture://" + name,
            [],
            [],
            [],
            info: null,
            pole: null,
            conductors: []);
    }

    private void WriteCharacterSnapshot(string heroRewards)
    {
        File.WriteAllText(
            CharacterPath(),
            $$"""
            [Character]
            UID={{CharacterUid}}
            Body=1003
            Face=0
            Name=Lucky
            Class=7051
            Map=700
            X=50
            Y=50
            FirstClass=7051
            SecoundeClass=0
            Conquer=0
            Reborn=0
            Level=140
            Haire=0
            Experience=0
            PMap=700
            PMapX=50
            PMapY=50
            Agility=0
            Strength=0
            Vitaliti=0
            Spirit=0
            Atributes=0
            MinHitPoints=1
            MinMana=0
            ConquerPoints=0
            BankCps=0
            BoundConquerPoints=0
            Money=0
            PkPoints=0
            Online=0
            LotteryEntries=2
            AddJade=7
            HeroRewards={{heroRewards}}
            DynamicID=0

            [PaidNobility]
            """);
    }

    private string CharacterPath()
    {
        return Path.Combine(_usersRoot, AccountId.ToString(CultureInfo.InvariantCulture) + ".ini");
    }

    private string ItemsPath()
    {
        return Path.Combine(_itemsRoot, CharacterUid.ToString(CultureInfo.InvariantCulture) + ".bin");
    }

    private static System.Text.Encoding LegacyCharacterEncoding()
    {
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        return System.Text.Encoding.GetEncoding(1252);
    }

    private static ConquerTcpConnection CreateSendingConnection(List<byte[]> sent)
    {
        return new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958), (frame, _) =>
        {
            sent.Add(frame.ToArray());
            return ValueTask.CompletedTask;
        });
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }

    public void Dispose()
    {
        if (Directory.Exists(_usersRoot))
        {
            Directory.Delete(_usersRoot, recursive: true);
        }

        if (Directory.Exists(_itemsRoot))
        {
            Directory.Delete(_itemsRoot, recursive: true);
        }
    }
}
