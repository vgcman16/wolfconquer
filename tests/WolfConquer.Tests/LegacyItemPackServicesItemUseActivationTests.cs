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

public sealed class LegacyItemPackServicesItemUseActivationTests : IDisposable
{
    private const long AccountId = 1_293_600;
    private const uint PackItemUid = 5_390_001;
    private const uint FirstAccessoryItemId = 360_071;
    private const uint FirstMountItemId = 200_610;
    private const uint FirstGarmentItemId = 181_610;

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "wolf-item-pack-use-activation-" + Guid.NewGuid());
    private readonly string _usersRoot;
    private readonly string _itemsRoot;

    public LegacyItemPackServicesItemUseActivationTests()
    {
        _usersRoot = Path.Combine(_root, "Users");
        _itemsRoot = Path.Combine(_root, "PlayersItems");
        Directory.CreateDirectory(_usersRoot);
        Directory.CreateDirectory(_itemsRoot);
    }

    public static TheoryData<uint, byte, uint, byte, string, string, byte, string> ActivationCases => new()
    {
        { 728_525, 0, 4_294_967_291, 0, "NpcHandler.Steed1", "Hello. You can choose a black, brown, or white horse.", 1, "Black~horse" },
        { 728_526, 0, 4_294_967_290, 0, "NpcHandler.Steed3", "Hello. You can choose a black, brown, or white horse.", 1, "Black~horse" },
        { 728_527, 0, 4_294_967_289, 0, "NpcHandler.Steed6", "Hello. You can choose a black, brown, or white horse.", 1, "Black~horse" },
        { 729_990, 0, 4_294_967_281, 0, "NpcHandler.NobleSteedPack", "Hello. You can choose a rare +6 horse.", 1, "+6~Spitfire~Steed" },
        { 3_001_406, 0, 4_294_967_280, 0, "NpcHandler.RareSteedPack6", "Hello. You can choose a rare +6 horse.", 1, "+6~Spitfire~Steed" },
        { 3_004_247, 0, 4_294_967_294, 0, "NpcHandler.SelectP7WeaponSoulPack", "Select the p7 weapon you need.", 1, "MonsterSaber" },
        { 3_004_249, 0, 4_294_967_292, 0, "NpcHandler.SelectSacredMaterial", "Choose what Refineries with level 6 you need .", 32, "Detoxication[Bag]. " },
        { 3_004_248, 0, 4_294_967_293, 40, "NpcHandler.SelectP7EquipmentSoulPack", "Select the p7 weapon you need.", 1, "MoonHeadgear" },
        { 3_008_732, 0, 4_294_967_279, 0, "NpcHandler.DazzlingDiamondBox", "Hello. You can chose a 30 pieces Diamond: Sun~Diamond, Moon~Diamond, Star~Diamond or Cloud~Diamond.", 1, "Sun~Diamond" },
        { 3_100_055, 0, 4_294_967_268, 0, "NpcHandler.BlackFridayGarmentPack", "Select the Garment you need.", 100, "GarmentFixture(4Stars)" },
        { 3_100_054, 0, 4_294_967_267, 0, "NpcHandler.BlackFridayMountPack", "Select the Mount you need.", 100, "MountFixture(5Stars)" },
        { 3_100_053, 0, 4_294_967_266, 0, "NpcHandler.BlackFridayAccesory", "Select the Accesory you need.", 100, "AccessoryFixture" },
        { 3_007_287, 1, 4_294_967_265, 0, "NpcHandler.Steed1Pack", "You can claim one of three +1 bound steeds from this pack. Which one do you want?", 1, "+1~White~Steed(B)" },
        { 3_007_283, 1, 4_294_967_264, 0, "NpcHandler.Steed3Pack", "You can claim one of three +3 bound steeds from this pack. Which one do you want?", 1, "+3~White~Steed(B)" },
    };

    [Theory]
    [MemberData(nameof(ActivationCases))]
    public async Task HandlerItemUsageEquipZeroActivatesExactItemPackServiceWithoutMutatingInventory(
        uint itemId,
        byte bound,
        uint expectedNpcId,
        byte expectedInitialOption,
        string expectedSource,
        string expectedDialog,
        byte expectedFirstOptionId,
        string expectedFirstOptionText)
    {
        WriteSnapshot();
        WritePackItem(itemId, bound);
        using var log = new StringWriter();
        var (handler, connection, sent) = await EnterWorldAsync(log, CreateCatalog());
        var sessionBefore = GetSession(handler, connection);
        var itemBefore = Assert.Single(sessionBefore.PlayerItems.InventoryItems);
        var itemPath = GetItemPath();
        var persistedBytesBefore = File.ReadAllBytes(itemPath);
        var persistedWriteTimeBefore = File.GetLastWriteTimeUtc(itemPath);

        var countBeforeUse = sent.Count;
        await handler.HandleFrameAsync(
            connection,
            ReadFrame(CreateEquipZeroUse(PackItemUid).ToFrameBytes()));

        var replies = ParseNpcReplies(sent.Skip(countBeforeUse));
        Assert.NotEmpty(replies);
        Assert.Equal(LegacyNpcReplyInteractType.Dialog, replies[0].InteractType);
        Assert.Equal(expectedDialog, replies[0].Text);
        var firstOption = replies.First(reply => reply.InteractType == LegacyNpcReplyInteractType.Option);
        Assert.Equal(expectedFirstOptionId, firstOption.OptionId);
        Assert.Equal(expectedFirstOptionText, firstOption.Text);

        var sessionAfter = GetSession(handler, connection);
        Assert.Equal(expectedNpcId, sessionAfter.ActiveNpcId);
        Assert.Same(sessionBefore.PlayerItems, sessionAfter.PlayerItems);
        Assert.Equal(itemBefore, Assert.Single(sessionAfter.PlayerItems.InventoryItems));
        Assert.Equal(persistedBytesBefore, File.ReadAllBytes(itemPath));
        Assert.Equal(persistedWriteTimeBefore, File.GetLastWriteTimeUtc(itemPath));

        var output = log.ToString();
        Assert.Contains("source=" + expectedSource, output);
        Assert.Contains($"itemId={itemId}", output);
        Assert.Contains($"activeNpcId={expectedNpcId}", output);
        Assert.Contains($"initialOption={expectedInitialOption}", output);
        Assert.Contains("handled=True", output);
        Assert.DoesNotContain("non-equipment item effects are not modeled", output);
    }

    [Fact]
    public async Task HandlerLegacyNpcReplyContinuesThroughStoredSyntheticItemPackNpc()
    {
        const uint packItemId = 3_007_287;
        const uint syntheticNpcId = 4_294_967_265;
        WriteSnapshot();
        WritePackItem(packItemId, bound: 1);
        using var log = new StringWriter();
        var (handler, connection, sent) = await EnterWorldAsync(log, CreateCatalog());
        var itemPath = GetItemPath();
        var persistedBytesBefore = File.ReadAllBytes(itemPath);
        var persistedWriteTimeBefore = File.GetLastWriteTimeUtc(itemPath);

        await handler.HandleFrameAsync(
            connection,
            ReadFrame(CreateEquipZeroUse(PackItemUid).ToFrameBytes()));
        var countBeforeReply = sent.Count;
        await handler.HandleFrameAsync(
            connection,
            ReadFrame(new LegacyNpcDialogRequestPacket(
                    NpcId: 0,
                    Mesh: 0,
                    OptionId: 1,
                    InteractType: (byte)LegacyNpcReplyInteractType.Option,
                    Action: 0,
                    Input: string.Empty)
                .ToFrameBytes()));

        var replies = ParseNpcReplies(sent.Skip(countBeforeReply));
        Assert.NotEmpty(replies);
        Assert.Equal(LegacyNpcReplyInteractType.Dialog, replies[0].InteractType);
        Assert.Equal(
            "Are you sure you want to claim a +1 White Steed (B) from this pack?",
            replies[0].Text);
        var firstOption = replies.First(reply => reply.InteractType == LegacyNpcReplyInteractType.Option);
        Assert.Equal(2, firstOption.OptionId);
        Assert.Equal("Yes.", firstOption.Text);

        var session = GetSession(handler, connection);
        Assert.Equal(syntheticNpcId, session.ActiveNpcId);
        var pack = Assert.Single(session.PlayerItems.InventoryItems);
        Assert.Equal(packItemId, pack.ItemId);
        Assert.Equal((byte)1, pack.Bound);
        Assert.Equal((ushort)1, pack.StackSize);
        Assert.Equal(persistedBytesBefore, File.ReadAllBytes(itemPath));
        Assert.Equal(persistedWriteTimeBefore, File.GetLastWriteTimeUtc(itemPath));

        var output = log.ToString();
        Assert.Contains("source=NpcHandler.Steed1Pack", output);
        Assert.DoesNotContain("clicked NPC is not currently visible", output);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private async Task<(WorldServerFrameHandler Handler, ConquerTcpConnection Connection, List<byte[]> Sent)>
        EnterWorldAsync(TextWriter log, WorldDataCatalog worldData)
    {
        var handoff = new AccountHandoffService(new InMemoryAccountHandoffTicketStore());
        var ticket = handoff.Issue(AccountId, "user02", ConquerRuntimeDefaults.WorldServer);
        var handler = new WorldServerFrameHandler(
            new CharacterSnapshotLoader(_usersRoot, _itemsRoot),
            handoff,
            worldData,
            log);
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        await handler.HandleFrameAsync(
            connection,
            ReadFrame(new InternalWorldEnterRequestPacket(ticket.Token).ToFrameBytes()));
        return (handler, connection, sent);
    }

    private void WriteSnapshot()
    {
        File.WriteAllText(
            Path.Combine(_usersRoot, AccountId.ToString(CultureInfo.InvariantCulture) + ".ini"),
            $$"""
            [Character]
            UID={{AccountId}}
            Body=1003
            Face=154
            Name=HeroOne
            Class=7051
            Map=1015
            X=826
            Y=565
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
            Agility=100
            Strength=100
            Vitaliti=447
            Spirit=0
            Atributes=12
            MinHitPoints=10000
            MinMana=5000
            ConquerPoints=0
            BankCps=123
            BoundConquerPoints=0
            Money=50000
            PkPoints=0
            Online=7

            [PaidNobility]
            Rank=King
            Identifier={{AccountId}}
            """);
    }

    private void WritePackItem(uint itemId, byte bound)
    {
        File.WriteAllBytes(
            GetItemPath(),
            PlayerItemSnapshotLoaderTests.BuildPlayerItems(
                [PlayerItemSnapshotLoaderTests.DbItem(
                    PackItemUid,
                    itemId,
                    position: 0,
                    stackSize: 1,
                    bound: bound)],
                []));
    }

    private string GetItemPath()
    {
        return Path.Combine(_itemsRoot, AccountId.ToString(CultureInfo.InvariantCulture) + ".bin");
    }

    private static MsgItemUsagePacket CreateEquipZeroUse(uint itemUid)
    {
        return new MsgItemUsagePacket(
            itemUid,
            DwParam: 0,
            MsgItemUsageAction.Equip,
            Timestamp: 511_043_495,
            DwParam2: 0,
            DwParam3: 0,
            DwParam4: 0);
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
        return Assert.IsType<WorldSession>(session);
    }

    private static IReadOnlyList<LegacyNpcReplyPacket> ParseNpcReplies(IEnumerable<byte[]> frames)
    {
        var replies = new List<LegacyNpcReplyPacket>();
        foreach (var bytes in frames)
        {
            Assert.True(LegacyNpcReplyPacket.TryParse(ReadFrame(bytes), out var reply));
            replies.Add(reply);
        }

        return replies;
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }

    private static WorldDataCatalog CreateCatalog()
    {
        var items = new[]
        {
            CreateItemType(FirstAccessoryItemId, "AccessoryFixture"),
            CreateItemType(FirstMountItemId, "MountFixture"),
            CreateItemType(FirstGarmentItemId, "GarmentFixture"),
        };
        var coatStorageTypes = new[]
        {
            CreateCoatStorageType(FirstMountItemId, garment: false, stars: 5, lineNumber: 1),
            CreateCoatStorageType(FirstGarmentItemId, garment: true, stars: 4, lineNumber: 2),
        };
        return WorldDataCatalog.Create(
            new SeedManifest(
                DateTimeOffset.Parse("2026-07-11T00:00:00Z", CultureInfo.InvariantCulture),
                "fixture://item-pack-use-activation",
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
            coatStorageTypes: coatStorageTypes,
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
                new ActivityTaskMetadata(0, string.Empty, 0, []),
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
            neiGongHeader: new NeiGongHeader(
                0,
                [],
                "fixture://neigong",
                0,
                string.Empty,
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

    private static ItemType CreateItemType(uint itemId, string name)
    {
        var fields = Enumerable.Repeat("0", 66).ToArray();
        fields[0] = itemId.ToString(CultureInfo.InvariantCulture);
        fields[1] = name;
        fields[ItemType.DurabilityFieldIndex] = "321";
        return new ItemType(checked((int)itemId), name, fields);
    }

    private static CoatStorageType CreateCoatStorageType(
        uint itemId,
        bool garment,
        int stars,
        int lineNumber)
    {
        var kind = garment ? 1 : 2;
        var fields = new[]
        {
            itemId.ToString(CultureInfo.InvariantCulture),
            itemId.ToString(CultureInfo.InvariantCulture),
            kind.ToString(CultureInfo.InvariantCulture),
            "0",
            "0",
            "0",
            "0",
            stars.ToString(CultureInfo.InvariantCulture),
            string.Empty,
        };
        return new CoatStorageType(
            checked((int)itemId),
            StorageType: itemId,
            Parameters: [kind, 0, 0, 0, 0, stars],
            SourceFile: "coat_storage_type.txt",
            LineNumber: lineNumber,
            RawLine: string.Join("@@", fields),
            Fields: fields);
    }

    private static GuildWarData EmptyGuildWarData(string name)
    {
        return new GuildWarData("fixture://" + name, [], [], [], info: null, pole: null, conductors: []);
    }
}
