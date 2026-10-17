using System.Buffers;
using System.Globalization;
using System.Net;
using System.Reflection;
using WolfConquer.Core.Accounts;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyRelicFuseHandlerTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "wolf-relic-fuse-handler-" + Guid.NewGuid());
    private readonly string _usersRoot;
    private readonly string _itemsRoot;

    public LegacyRelicFuseHandlerTests()
    {
        _usersRoot = Path.Combine(_root, "Users");
        _itemsRoot = Path.Combine(_root, "PlayersItems");
        Directory.CreateDirectory(_usersRoot);
        Directory.CreateDirectory(_itemsRoot);
    }

    [Fact]
    public async Task HandlerComposesRelicFuseResultMovesSourceItemsAndPersists()
    {
        const uint relicUid1 = 6_140_001;
        const uint relicUid2 = 6_140_002;
        const uint expectedGeneratedUid = 6_140_003;
        WriteSnapshot();
        WriteItems(
            PlayerItemSnapshotLoaderTests.DbItem(
                relicUid1,
                4_100_001,
                position: 0,
                attribute1: EncodeRelicAttribute(LegacyChiAttributeType.Immunity, 100),
                attribute2: EncodeRelicAttribute(LegacyChiAttributeType.Breakthrough, 110),
                attribute3: EncodeRelicAttribute(LegacyChiAttributeType.Counteraction, 120),
                attribute4: EncodeRelicAttribute(LegacyChiAttributeType.AddMagicAttack, 130),
                attribute5: EncodeRelicAttribute(LegacyChiAttributeType.AddMagicDefense, 140)),
            PlayerItemSnapshotLoaderTests.DbItem(
                relicUid2,
                4_100_001,
                position: 0,
                attribute1: EncodeRelicAttribute(LegacyChiAttributeType.Immunity, 80),
                attribute2: EncodeRelicAttribute(LegacyChiAttributeType.Breakthrough, 90),
                attribute3: EncodeRelicAttribute(LegacyChiAttributeType.Counteraction, 100),
                attribute4: EncodeRelicAttribute(LegacyChiAttributeType.AddMagicAttack, 110),
                attribute5: EncodeRelicAttribute(LegacyChiAttributeType.AddMagicDefense, 120)));
        using var log = new StringWriter();
        var worldData = CreateWorldDataCatalog(CreateComposeAttributeLimits());
        var (handler, connection, sent) = await EnterWorldAsync(log, worldData);

        var countBeforeRequest = sent.Count;
        await handler.HandleFrameAsync(
            connection,
            ReadFrame(new LegacyRelicFusePacket(0, relicUid1, relicUid2, 0).ToFrameBytes()));

        Assert.Equal(countBeforeRequest + 2, sent.Count);
        var generated = AssertGameItemAdd(sent[countBeforeRequest], expectedGeneratedUid, 4_100_001);
        AssertRelicFuse(sent[countBeforeRequest + 1], type: 0, relicUid1, relicUid2, expectedGeneratedUid);
        AssertRelicFuseAttributesAllowed(generated);

        var session = GetSession(handler, connection);
        Assert.Empty(session.PlayerItems.InventoryItems);
        Assert.Contains(session.PlayerItems.RelicFuseItems, item => item.Uid == relicUid1 && item.Position == 213 && item.StackSize == 1);
        Assert.Contains(session.PlayerItems.RelicFuseItems, item => item.Uid == relicUid2 && item.Position == 214 && item.StackSize == 1);
        Assert.Contains(session.PlayerItems.RelicFuseItems, item => item.Uid == expectedGeneratedUid && item.Position == 215);

        var persisted = new PlayerItemSnapshotLoader(_itemsRoot).LoadByCharacterUid(1_293_600);
        Assert.Empty(persisted.InventoryItems);
        var persistedGenerated = Assert.Single(persisted.RelicFuseItems, item => item.Uid == expectedGeneratedUid);
        Assert.Equal(215, persistedGenerated.Position);
        AssertRelicFuseAttributesAllowed(persistedGenerated);

        var output = log.ToString();
        Assert.Contains("handled LegacyRelicFuse compose", output);
        Assert.Contains("persisted MsgItemUsage relic fuse compose items", output);
    }

    [Fact]
    public async Task HandlerReturnsSelectedRelicFuseItemClearsFuseSlotsAndPersists()
    {
        const uint relicUid1 = 6_150_001;
        const uint relicUid2 = 6_150_002;
        const uint relicUid3 = 6_150_003;
        WriteSnapshot();
        WriteItems(
            PlayerItemSnapshotLoaderTests.DbItem(relicUid1, 3_306_801, position: 213, durability: 8000, maximumDurability: 10000),
            PlayerItemSnapshotLoaderTests.DbItem(relicUid2, 3_306_801, position: 214),
            PlayerItemSnapshotLoaderTests.DbItem(relicUid3, 3_306_801, position: 215));
        using var log = new StringWriter();
        var (handler, connection, sent) = await EnterWorldAsync(log);

        var countBeforeRequest = sent.Count;
        await handler.HandleFrameAsync(
            connection,
            ReadFrame(new LegacyRelicFusePacket(1, relicUid1, relicUid2, relicUid3).ToFrameBytes()));

        Assert.Equal(countBeforeRequest + 2, sent.Count);
        AssertGameItemUpdate(sent[countBeforeRequest], relicUid1, position: 0);
        AssertRelicFuse(sent[countBeforeRequest + 1], type: 1, relicUid1, relicUid2, relicUid3);

        var session = GetSession(handler, connection);
        Assert.Contains(session.PlayerItems.InventoryItems, item => item.Uid == relicUid1 && item.Position == 0);
        Assert.Empty(session.PlayerItems.RelicFuseItems);

        var persisted = new PlayerItemSnapshotLoader(_itemsRoot).LoadByCharacterUid(1_293_600);
        Assert.Contains(persisted.InventoryItems, item => item.Uid == relicUid1 && item.Position == 0);
        Assert.Empty(persisted.RelicFuseItems);

        var output = log.ToString();
        Assert.Contains("handled LegacyRelicFuse return", output);
        Assert.Contains("changed=True", output);
        Assert.Contains("persisted MsgItemUsage relic fuse return items", output);
    }

    [Fact]
    public async Task HandlerEchoesRelicFuseReturnWhenSelectedItemIsMissing()
    {
        const uint missingRelicUid = 6_160_001;
        const uint relicUid2 = 6_160_002;
        const uint relicUid3 = 6_160_003;
        WriteSnapshot();
        WriteItems(
            PlayerItemSnapshotLoaderTests.DbItem(relicUid2, 3_306_801, position: 214),
            PlayerItemSnapshotLoaderTests.DbItem(relicUid3, 3_306_801, position: 215));
        using var log = new StringWriter();
        var (handler, connection, sent) = await EnterWorldAsync(log);

        var countBeforeRequest = sent.Count;
        await handler.HandleFrameAsync(
            connection,
            ReadFrame(new LegacyRelicFusePacket(1, missingRelicUid, relicUid2, relicUid3).ToFrameBytes()));

        Assert.Equal(countBeforeRequest + 1, sent.Count);
        AssertRelicFuse(sent[countBeforeRequest], type: 1, missingRelicUid, relicUid2, relicUid3);

        var session = GetSession(handler, connection);
        Assert.Empty(session.PlayerItems.InventoryItems);
        Assert.Equal(2, session.PlayerItems.RelicFuseItems.Count);

        var output = log.ToString();
        Assert.Contains("handled LegacyRelicFuse return", output);
        Assert.Contains("changed=False", output);
        Assert.DoesNotContain("persisted MsgItemUsage relic fuse return items", output);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private async Task<(WorldServerFrameHandler Handler, ConquerTcpConnection Connection, List<byte[]> Sent)> EnterWorldAsync(
        TextWriter log,
        WorldDataCatalog? worldData = null)
    {
        var handoff = new AccountHandoffService(new InMemoryAccountHandoffTicketStore());
        var ticket = handoff.Issue(1_293_600, "user02", ConquerRuntimeDefaults.WorldServer);
        var handler = worldData is null
            ? new WorldServerFrameHandler(new CharacterSnapshotLoader(_usersRoot), handoff, log)
            : new WorldServerFrameHandler(new CharacterSnapshotLoader(_usersRoot), handoff, worldData, log);
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);

        await handler.HandleFrameAsync(
            connection,
            ReadFrame(new InternalWorldEnterRequestPacket(ticket.Token).ToFrameBytes()));

        return (handler, connection, sent);
    }

    private void WriteItems(params PlayerItemSnapshotLoaderTests.TestDbItem[] items)
    {
        File.WriteAllBytes(
            Path.Combine(_itemsRoot, "1293600.bin"),
            PlayerItemSnapshotLoaderTests.BuildPlayerItems(items, []));
    }

    private void WriteSnapshot()
    {
        File.WriteAllText(
            Path.Combine(_usersRoot, "1293600.ini"),
            """
            [Character]
            UID=1293600
            Body=1008
            Face=154
            Name=HeroOne
            Class=7051
            Map=1015
            X=830
            Y=584
            FirstClass=0
            SecoundeClass=7050
            Conquer=0
            Reborn=2
            Level=140
            Haire=27
            Experience=0
            PMap=1000
            PMapX=500
            PMapY=650
            Agility=36
            Strength=0
            Vitaliti=447
            Spirit=0
            Atributes=12
            MinHitPoints=61258
            MinMana=5000
            ConquerPoints=0
            BankCps=123
            BoundConquerPoints=0
            Money=50000
            PkPoints=0
            Online=7

            [PaidNobility]
            Rank=King
            Identifier=1293600
            """);
    }

    private static LegacyGameItemPacket AssertGameItemAdd(byte[] bytes, uint uid, uint itemId)
    {
        Assert.True(LegacyGameItemPacket.TryParse(ReadFrame(bytes), out var packet));
        Assert.Equal(uid, packet.Uid);
        Assert.Equal(itemId, packet.ItemId);
        Assert.Equal(LegacyGameItemPacket.AddItemMode, packet.Mode);
        Assert.Equal(215, packet.Position);
        Assert.Equal(1, packet.StackSize);
        Assert.InRange(packet.Durability, (ushort)3_000, (ushort)10_000);
        Assert.Equal(0, packet.Durability % 100);
        Assert.Equal(packet.Durability, packet.MaximumDurability);
        return packet;
    }

    private static void AssertGameItemUpdate(byte[] bytes, uint uid, byte position)
    {
        Assert.True(LegacyGameItemPacket.TryParse(ReadFrame(bytes), out var packet));
        Assert.Equal(uid, packet.Uid);
        Assert.Equal(position, packet.Position);
        Assert.Equal(LegacyGameItemPacket.UpdateItemMode, packet.Mode);
    }

    private static void AssertRelicFuse(
        byte[] bytes,
        uint type,
        uint relicUid1,
        uint relicUid2,
        uint relicUid3)
    {
        Assert.True(LegacyRelicFusePacket.TryParse(ReadFrame(bytes), out var packet));
        Assert.Equal(type, packet.Type);
        Assert.Equal(relicUid1, packet.RelicID1);
        Assert.Equal(relicUid2, packet.RelicID2);
        Assert.Equal(relicUid3, packet.RelicID3);
    }

    private static void AssertRelicFuseAttributesAllowed(LegacyGameItemPacket item)
    {
        AssertRelicFuseAttributesAllowed(
            item.Attribute1,
            item.Attribute2,
            item.Attribute3,
            item.Attribute4,
            item.Attribute5);
    }

    private static void AssertRelicFuseAttributesAllowed(PlayerItemSnapshotItem item)
    {
        AssertRelicFuseAttributesAllowed(
            item.Attribute1,
            item.Attribute2,
            item.Attribute3,
            item.Attribute4,
            item.Attribute5);
    }

    private static void AssertRelicFuseAttributesAllowed(params uint[] encodedAttributes)
    {
        Assert.All(encodedAttributes, encoded =>
        {
            var type = (LegacyChiAttributeType)(encoded % 100);
            var value = encoded / 1_000;
            var epic = encoded % 1_000 - encoded % 100 == 100;

            Assert.NotEqual(LegacyChiAttributeType.None, type);
            Assert.DoesNotContain(
                type,
                new[]
                {
                    LegacyChiAttributeType.SkillCriticalStrike,
                    LegacyChiAttributeType.CriticalStrike,
                    LegacyChiAttributeType.HPAdd,
                    LegacyChiAttributeType.AddAttack,
                });

            if (type == LegacyChiAttributeType.LukyStrike)
            {
                Assert.Equal(100U, value);
                Assert.True(epic);
            }
            else
            {
                Assert.InRange(value, 30U, 400U);
            }
        });
    }

    private static uint EncodeRelicAttribute(LegacyChiAttributeType type, ushort value, bool epic = false)
    {
        return checked(((uint)value * 1_000) + (byte)type + (epic ? 100U : 0U));
    }

    private static IReadOnlyList<XuanbaoComposeAttributeLimit> CreateComposeAttributeLimits()
    {
        return Enumerable
            .Range(1, 14)
            .Select(attributeType => new XuanbaoComposeAttributeLimit(
                attributeType,
                Values: [0, 4, 400],
                SourceFile: "xuanbao_compose_attr_limit.txt",
                LineNumber: attributeType,
                RawLine: $"{attributeType.ToString(CultureInfo.InvariantCulture)}@@0@@4@@400@@",
                Fields:
                [
                    attributeType.ToString(CultureInfo.InvariantCulture),
                    "0",
                    "4",
                    "400",
                    "",
                ]))
            .ToArray();
    }

    private static WorldDataCatalog CreateWorldDataCatalog(
        IReadOnlyList<XuanbaoComposeAttributeLimit> xuanbaoComposeAttributeLimits)
    {
        return WorldDataCatalog.Create(
            new SeedManifest(
                DateTimeOffset.Parse("2026-07-04T00:00:00Z", CultureInfo.InvariantCulture),
                "fixture://legacy-relic-fuse",
                new Dictionary<string, int>()),
            clientConfigs: [],
            items: [],
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
            xuanbaoComposeAttributeLimits: xuanbaoComposeAttributeLimits,
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

    private static WorldSession GetSession(WorldServerFrameHandler handler, ConquerTcpConnection connection)
    {
        var field = typeof(WorldServerFrameHandler).GetField("_sessions", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        var sessions = Assert.IsType<WorldSessionStore>(field.GetValue(handler));
        Assert.True(sessions.TryGet(connection, out var session));
        Assert.NotNull(session);
        return session;
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }
}
