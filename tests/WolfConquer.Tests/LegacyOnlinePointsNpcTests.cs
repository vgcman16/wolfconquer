using System.Buffers;
using System.Globalization;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyOnlinePointsNpcTests : IDisposable
{
    private const uint OnlinePointsNpcId = 50_032;
    private const uint AccessoryItemId = 350_001;

    private readonly string _usersRoot = Path.Combine(Path.GetTempPath(), "wolf-online-points-users-" + Guid.NewGuid());
    private readonly string _itemsRoot = Path.Combine(Path.GetTempPath(), "wolf-online-points-items-" + Guid.NewGuid());

    [Fact]
    public async Task HandlerSendsTranslatedOnlinePointsDialog()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(onlinePoints: 777);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            ReadFrame(LegacyNpcServerRequestPacket.Script(OnlinePointsNpcId, optionId: 0, interactType: 1).ToFrameBytes()));

        Assert.Equal(11, sent.Count);
        var replies = ParseNpcReplies(sent);
        Assert.Equal("You can exchange online points for rewards.", replies[0].Text);
        Assert.Equal("Your current online points: [777]", replies[2].Text);
        Assert.All(replies.Where(reply => !string.IsNullOrEmpty(reply.Text)), reply => Assert.False(ContainsCjk(reply.Text), reply.Text));
        Assert.Equal("10 donation points [2500 points].", replies[3].Text);
        Assert.Equal(10, replies[3].OptionId);
        Assert.Equal("6 hours VIP 4 [120 points].", replies[4].Text);
        Assert.Equal(21, replies[4].OptionId);
        Assert.Equal("100,000 CPs [6 points].", replies[6].Text);
        Assert.Equal(2, replies[6].OptionId);
        Assert.Equal("Soul Gem x10 [1000 points].", replies[7].Text);
        Assert.Equal(4, replies[7].OptionId);
        Assert.Equal(LegacyNpcReplyInteractType.Avatar, replies[9].InteractType);
        Assert.Equal(63, replies[9].InputMaxLength);
        Assert.Contains("source=NpcHandler.OnlinePoints", log.ToString());
    }

    [Fact]
    public async Task HandlerPurchasesConquerPointsWithOnlineMinutesBalance()
    {
        Directory.CreateDirectory(_usersRoot);
        WriteSnapshot(onlinePoints: 9, donatePoints: 0, conquerPoints: 25, vipLevel: 0);
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(onlinePoints: 9, conquerPoints: 25, usersRoot: _usersRoot);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            ReadFrame(LegacyNpcServerRequestPacket.Script(OnlinePointsNpcId, optionId: 2, interactType: 1).ToFrameBytes()));

        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(100_025, updated.Character.ConquerPoints);
        Assert.Equal("3", updated.Character.Attributes["OnlineMinutes"]);
        var persisted = CharacterSnapshotParser.ParseText(File.ReadAllText(Path.Combine(_usersRoot, "100.ini")));
        Assert.Equal("3", persisted.Attributes["OnlineMinutes"]);
        Assert.Equal(100_025, persisted.ConquerPoints);
        Assert.Contains(ParseNpcReplies(sent), reply => reply.Text == "Congratulations! You received 100,000 CPs!");
        Assert.Contains("option=2 cost=6 onlinePoints=3 conquerPoints=100025", log.ToString());
    }

    [Fact]
    public async Task HandlerPurchasesDonationPointsWithSourceCost()
    {
        Directory.CreateDirectory(_usersRoot);
        WriteSnapshot(onlinePoints: 2_500, donatePoints: 4, conquerPoints: 0, vipLevel: 0);
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(onlinePoints: 2_500, donatePoints: 4, usersRoot: _usersRoot);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            ReadFrame(LegacyNpcServerRequestPacket.Script(OnlinePointsNpcId, optionId: 10, interactType: 1).ToFrameBytes()));

        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal("0", updated.Character.Attributes["OnlineMinutes"]);
        Assert.Equal("14", updated.Character.Attributes["DonatePoints"]);
        var persisted = CharacterSnapshotParser.ParseText(File.ReadAllText(Path.Combine(_usersRoot, "100.ini")));
        Assert.Equal("0", persisted.Attributes["OnlineMinutes"]);
        Assert.Equal("14", persisted.Attributes["DonatePoints"]);
        Assert.Contains(ParseNpcReplies(sent), reply => reply.Text.Contains("10 donation points", StringComparison.Ordinal));
    }

    [Fact]
    public async Task HandlerPurchasesVipFourForSixHoursWhenVipBelowFour()
    {
        Directory.CreateDirectory(_usersRoot);
        WriteSnapshot(onlinePoints: 120, donatePoints: 0, conquerPoints: 0, vipLevel: 0);
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(onlinePoints: 120, vipLevel: 0, usersRoot: _usersRoot);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            ReadFrame(LegacyNpcServerRequestPacket.Script(OnlinePointsNpcId, optionId: 21, interactType: 1).ToFrameBytes()));

        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal("0", updated.Character.Attributes["OnlineMinutes"]);
        Assert.Equal("4", updated.Character.Attributes["VipLevel"]);
        Assert.True(long.TryParse(updated.Character.Attributes["VipTime"], NumberStyles.Integer, CultureInfo.InvariantCulture, out _));
        Assert.Contains(ParseMsgUpdates(sent), update => update.Updates.Contains(new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.VIPLevel, 4)));
        Assert.Contains(ParseNpcReplies(sent), reply => reply.Text == "Congratulations! You received 6 hours of VIP 4 service!");
    }

    [Fact]
    public async Task HandlerPurchasesSourceSoulGemItemAndPersistsOnlineMinutes()
    {
        Directory.CreateDirectory(_usersRoot);
        Directory.CreateDirectory(_itemsRoot);
        WriteSnapshot(onlinePoints: 1_000, donatePoints: 0, conquerPoints: 0, vipLevel: 0);
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(onlinePoints: 1_000, usersRoot: _usersRoot);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                Session: session,
                Sessions: sessions,
                PlayerItemsRoot: _itemsRoot),
            ReadFrame(LegacyNpcServerRequestPacket.Script(OnlinePointsNpcId, optionId: 4, interactType: 1).ToFrameBytes()));

        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal("0", updated.Character.Attributes["OnlineMinutes"]);
        var item = Assert.Single(updated.PlayerItems.InventoryItems);
        Assert.Equal(4_200_010U, item.ItemId);
        Assert.Equal(1, item.StackSize);
        Assert.Equal(4_200_010U, Assert.Single(ParseGameItems(sent)).ItemId);
        var persisted = CharacterSnapshotParser.ParseText(File.ReadAllText(Path.Combine(_usersRoot, "100.ini")));
        Assert.Equal("0", persisted.Attributes["OnlineMinutes"]);
        Assert.Equal(4_200_010U, Assert.Single(new PlayerItemSnapshotLoader(_itemsRoot).LoadByCharacterUid(1_293_600).InventoryItems).ItemId);
        Assert.Contains("PARITY-GAP LegacyNpc OnlinePoints item purchase missing item type", log.ToString());
    }

    [Fact]
    public async Task HandlerPurchasesSourceRandomAccessoryAndPersistsBalanceAndItem()
    {
        Directory.CreateDirectory(_usersRoot);
        Directory.CreateDirectory(_itemsRoot);
        WriteSnapshot(onlinePoints: 125, donatePoints: 0, conquerPoints: 0, vipLevel: 0);
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(onlinePoints: 125, usersRoot: _usersRoot);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                Session: session,
                WorldData: CreateWorldData(CreateItemType(AccessoryItemId, "FixtureAccessory", durability: 1_234)),
                Sessions: sessions,
                PlayerItemsRoot: _itemsRoot),
            ReadFrame(LegacyNpcServerRequestPacket.Script(OnlinePointsNpcId, optionId: 14, interactType: 1).ToFrameBytes()));

        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal("25", updated.Character.Attributes["OnlineMinutes"]);
        var item = Assert.Single(updated.PlayerItems.InventoryItems);
        Assert.Equal(AccessoryItemId, item.ItemId);
        Assert.Equal(1_234, item.Durability);
        Assert.Equal(0, item.Bound);
        Assert.Equal(AccessoryItemId, Assert.Single(ParseGameItems(sent)).ItemId);

        var persistedCharacter = CharacterSnapshotParser.ParseText(File.ReadAllText(Path.Combine(_usersRoot, "100.ini")));
        Assert.Equal("25", persistedCharacter.Attributes["OnlineMinutes"]);
        var persistedItem = Assert.Single(
            new PlayerItemSnapshotLoader(_itemsRoot).LoadByCharacterUid(1_293_600).InventoryItems);
        Assert.Equal(AccessoryItemId, persistedItem.ItemId);
        Assert.Equal(1_234, persistedItem.Durability);
        Assert.Contains(
            ParseNpcReplies(sent),
            reply => reply.Text == "Congratulations! You received a FixtureAccessory!");
        Assert.Contains(
            $"option=14 itemId={AccessoryItemId} amount=1 cost=100 onlinePoints=25",
            log.ToString());
    }

    [Fact]
    public async Task HandlerDoesNotSelectOrGrantRandomAccessoryWithoutSourceCost()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(onlinePoints: 99);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                Session: session,
                WorldData: CreateWorldData(CreateItemType(AccessoryItemId, "FixtureAccessory", durability: 1_234)),
                Sessions: sessions),
            ReadFrame(LegacyNpcServerRequestPacket.Script(OnlinePointsNpcId, optionId: 14, interactType: 1).ToFrameBytes()));

        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal("99", updated.Character.Attributes["OnlineMinutes"]);
        Assert.Empty(updated.PlayerItems.InventoryItems);
        Assert.Empty(ParseGameItems(sent));
        Assert.Contains(ParseNpcReplies(sent), reply => reply.Text == "You do not have enough online points!");
        Assert.Contains("option=14 cost=100 onlinePoints=99", log.ToString());
    }

    private static WorldSession CreateSession(
        ulong onlinePoints,
        ulong donatePoints = 0,
        long conquerPoints = 0,
        ulong vipLevel = 0,
        string? usersRoot = null)
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["OnlineMinutes"] = onlinePoints.ToString(CultureInfo.InvariantCulture),
            ["DonatePoints"] = donatePoints.ToString(CultureInfo.InvariantCulture),
            ["VipLevel"] = vipLevel.ToString(CultureInfo.InvariantCulture),
            ["DynamicID"] = "0",
        };

        return new WorldSession(
            AccountId: 100,
            Username: "online-points-test",
            Character: new CharacterSnapshot(
                Uid: 1_293_600,
                Name: "Rewarded",
                Body: 1003,
                Face: 0,
                Hair: 0,
                Class: 7051,
                FirstClass: 7051,
                SecondClass: 0,
                Reborn: 0,
                Level: 140,
                Experience: 0,
                MapId: 1002,
                X: 430,
                Y: 378,
                PreviousMapId: 1002,
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
                ConquerPoints: conquerPoints,
                BoundConquerPoints: 0,
                BankConquerPoints: 0,
                PkPoints: 0,
                Online: 0,
                SourceFile: "fixture",
                Attributes: attributes,
                PaidNobility: new Dictionary<string, string>()),
            PlayerItems: PlayerItemSnapshot.Empty,
            PlayerSpells: PlayerSpellSnapshot.Empty,
            PlayerProficiencies: PlayerProficiencySnapshot.Empty,
            UsersRoot: usersRoot);
    }

    private void WriteSnapshot(ulong onlinePoints, ulong donatePoints, long conquerPoints, ulong vipLevel)
    {
        File.WriteAllText(
            Path.Combine(_usersRoot, "100.ini"),
            $$"""
            [Character]
            UID=1293600
            Body=1003
            Face=0
            Name=Rewarded
            Class=7051
            Map=1002
            X=430
            Y=378
            FirstClass=7051
            SecoundeClass=0
            Conquer=0
            Reborn=0
            Level=140
            Haire=0
            Experience=0
            PMap=1002
            PMapX=430
            PMapY=378
            Agility=0
            Strength=0
            Vitaliti=0
            Spirit=0
            Atributes=0
            MinHitPoints=1
            MinMana=0
            ConquerPoints={{conquerPoints}}
            BankCps=0
            BoundConquerPoints=0
            Money=0
            PkPoints=0
            Online=0
            OnlineMinutes={{onlinePoints}}
            DonatePoints={{donatePoints}}
            VipLevel={{vipLevel}}
            VipTime=0
            DynamicID=0

            [PaidNobility]
            """);
    }

    private static ItemType CreateItemType(uint itemId, string name, ushort durability)
    {
        var fields = Enumerable.Repeat("0", 66).ToArray();
        fields[ItemType.DurabilityFieldIndex] = durability.ToString(CultureInfo.InvariantCulture);
        return new ItemType(checked((int)itemId), name, fields);
    }

    private static WorldDataCatalog CreateWorldData(params ItemType[] items)
    {
        return WorldDataCatalog.Create(
            new SeedManifest(
                DateTimeOffset.Parse("2026-07-10T00:00:00Z", CultureInfo.InvariantCulture),
                "fixture://online-points",
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
            npcs: [new Npc(50_032, 0, 0, 1_002, 430, 378, null)],
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

    private static GuildWarData EmptyGuildWarData(string name)
    {
        return new GuildWarData($"fixture://{name}", [], [], [], info: null, pole: null, conductors: []);
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

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }

    private static bool ContainsCjk(string value)
    {
        return value.Any(character => character is >= '\u3400' and <= '\u9FFF');
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
