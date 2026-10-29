using System.Buffers;
using System.Globalization;
using System.Net;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacySecurityAnimaItemOperationTests : IDisposable
{
    private const long AccountId = 1_293_600;
    private const uint SellerNpcId = 20_499;
    private const uint FirstAnimaItemId = 4_200_001;
    private const string ActivationMessage = "Please Active Pincode";

    private static readonly PacketHandlerRegistry<WorldPacketHandlerContext> Handlers =
        PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "WolfConquer-LegacySecurityAnimaItemOperationTests-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task InactivePinBlocksProtectedSellWithoutMutation()
    {
        var item = Item(uid: 101, itemId: FirstAnimaItemId);
        var result = await InvokeAsync(
            new MsgItemUsagePacket(
                SellerNpcId,
                item.Uid,
                MsgItemUsageAction.SellItem,
                Timestamp: 0,
                DwParam2: 0,
                DwParam3: 0,
                DwParam4: 0).ToFrameBytes(),
            item,
            InactivePinAttributes(),
            CreateWorldData([ItemType(item.ItemId, goldWorth: 100)], [SellerNpc()]));

        AssertDenied(result, "MsgItemUsuagePacket.SellItem");
    }

    [Fact]
    public async Task InactivePinBlocksProtectedDropWithoutMutation()
    {
        var item = Item(uid: 102, itemId: FirstAnimaItemId);
        var result = await InvokeAsync(
            DropPacket(item.Uid),
            item,
            InactivePinAttributes(),
            CreateWorldData([ItemType(item.ItemId)]));

        AssertDenied(result, "MsgItemUsuagePacket.DropItem");
    }

    [Fact]
    public async Task InactivePinBlocksItemWithEmbeddedAnimaWithoutMutation()
    {
        var item = Item(uid: 103, itemId: 50_000, animaItemId: FirstAnimaItemId);
        var result = await InvokeAsync(
            DropPacket(item.Uid),
            item,
            InactivePinAttributes(),
            CreateWorldData([ItemType(item.ItemId)]));

        AssertDenied(result, "MsgItemUsuagePacket.DropItem");
    }

    [Fact]
    public async Task InactivePinBlocksInitialAnimaItemNpcBeforeActiveNpcMutation()
    {
        var item = Item(uid: 104, itemId: FirstAnimaItemId);
        var result = await InvokeAsync(
            new MsgItemUsagePacket(
                item.Uid,
                DwParam: 0,
                MsgItemUsageAction.Equip,
                Timestamp: 0,
                DwParam2: 0,
                DwParam3: 0,
                DwParam4: 0).ToFrameBytes(),
            item,
            InactivePinAttributes(),
            CreateWorldData([ItemType(item.ItemId)]));

        Assert.Null(result.FinalSession.ActiveNpcId);
        AssertDenied(result, "NpcHandler.Animas");
    }

    [Fact]
    public async Task InactivePinBlocksAnimaNpcMutationOptionBeforeSwitch()
    {
        var item = Item(uid: 105, itemId: FirstAnimaItemId);
        var result = await InvokeAsync(
            NpcOptionPacket(item.ItemId, optionId: 7),
            item,
            InactivePinAttributes(),
            CreateWorldData([ItemType(item.ItemId, conquerPointsWorth: 101)]),
            activeNpcId: item.ItemId);

        Assert.Equal(50, result.FinalSession.Character.ConquerPoints);
        AssertDenied(result, "NpcHandler.Animas");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProtectedDropContinuesForNoPinOrActivatedSession(bool activated)
    {
        var item = Item(uid: activated ? 107U : 106U, itemId: FirstAnimaItemId);
        var attributes = activated
            ? ActivatedPinAttributes()
            : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var result = await InvokeAsync(
            DropPacket(item.Uid),
            item,
            attributes,
            CreateWorldData([ItemType(item.ItemId)]));

        Assert.NotSame(result.InitialStoredSession, result.FinalSession);
        Assert.Empty(result.FinalSession.PlayerItems.InventoryItems);
        Assert.Equal(result.InitialStoredSession.Character.Money, result.FinalSession.Character.Money);
        Assert.Single(result.FloorItems.SnapshotVisible(DateTimeOffset.UtcNow));
        Assert.False(result.ItemsBefore.SequenceEqual(result.ItemsAfter));
        AssertNoActivationDenial(result.Sent);
    }

    [Fact]
    public async Task InactivePinAllowsNonAnimaDrop()
    {
        var item = Item(uid: 108, itemId: 50_000);
        var result = await InvokeAsync(
            DropPacket(item.Uid),
            item,
            InactivePinAttributes(),
            CreateWorldData([ItemType(item.ItemId)]));

        Assert.Empty(result.FinalSession.PlayerItems.InventoryItems);
        Assert.Single(result.FloorItems.SnapshotVisible(DateTimeOffset.UtcNow));
        AssertNoActivationDenial(result.Sent);
    }

    [Fact]
    public async Task AnimaNpcUsesLatestStoredActivationState()
    {
        var item = Item(uid: 109, itemId: FirstAnimaItemId);
        var result = await InvokeAsync(
            NpcOptionPacket(item.ItemId, optionId: 1),
            item,
            InactivePinAttributes(),
            CreateWorldData([ItemType(item.ItemId)]),
            activeNpcId: item.ItemId,
            storedAttributes: ActivatedPinAttributes());

        Assert.Same(result.InitialStoredSession, result.FinalSession);
        Assert.NotEmpty(result.Sent);
        Assert.Contains(
            result.Sent.Select(ParseNpcReply),
            reply => reply.InteractType == LegacyNpcReplyInteractType.Dialog &&
                reply.Text.Contains("everlasting vital power", StringComparison.Ordinal));
        AssertNoActivationDenial(result.Sent);
    }

    [Fact]
    public async Task AnimaNpcMutationPreservesLatestStoredActivationState()
    {
        var item = Item(uid: 110, itemId: FirstAnimaItemId);
        var result = await InvokeAsync(
            NpcOptionPacket(item.ItemId, optionId: 7),
            item,
            InactivePinAttributes(),
            CreateWorldData([ItemType(item.ItemId, conquerPointsWorth: 101)]),
            activeNpcId: item.ItemId,
            storedAttributes: ActivatedPinAttributes());

        Assert.Equal(150, result.FinalSession.Character.ConquerPoints);
        Assert.Empty(result.FinalSession.PlayerItems.InventoryItems);
        Assert.Equal("true", result.FinalSession.Character.Attributes["TREPIN2"]);
        Assert.False(result.ItemsBefore.SequenceEqual(result.ItemsAfter));
        AssertNoActivationDenial(result.Sent);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private async Task<InvokeResult> InvokeAsync(
        byte[] request,
        PlayerItemSnapshotItem item,
        IReadOnlyDictionary<string, string> attributes,
        WorldDataCatalog worldData,
        uint? activeNpcId = null,
        IReadOnlyDictionary<string, string>? storedAttributes = null)
    {
        var invocationRoot = Path.Combine(_root, Guid.NewGuid().ToString("N"));
        var usersRoot = Path.Combine(invocationRoot, "Users");
        var itemsRoot = Path.Combine(invocationRoot, "PlayersItems");
        Directory.CreateDirectory(usersRoot);

        var playerItems = new PlayerItemSnapshot([item], [], [], [], []);
        var itemLoader = new PlayerItemSnapshotLoader(itemsRoot);
        itemLoader.SaveByCharacterUid(AccountId, playerItems);
        var itemPath = Path.Combine(itemsRoot, AccountId.ToString(CultureInfo.InvariantCulture) + ".bin");

        var characterPath = Path.Combine(usersRoot, AccountId.ToString(CultureInfo.InvariantCulture) + ".ini");
        WriteCharacter(characterPath, attributes);
        var contextSession = Session(playerItems, attributes, usersRoot, activeNpcId);
        var initialStoredSession = storedAttributes is null
            ? contextSession
            : contextSession with
            {
                Character = contextSession.Character with
                {
                    Attributes = new Dictionary<string, string>(storedAttributes, StringComparer.OrdinalIgnoreCase),
                },
            };

        var sent = new List<byte[]>();
        var connection = new ConquerTcpConnection(
            null,
            new IPEndPoint(IPAddress.Loopback, 9_958),
            (frame, _) =>
            {
                sent.Add(frame.ToArray());
                return ValueTask.CompletedTask;
            });
        var sessions = new WorldSessionStore();
        sessions.Set(connection, initialStoredSession);
        var floorItems = new FloorItemRuntimeStateStore();
        using var log = new StringWriter();
        var recovery = new LegacyTradeRecoveryState(usersRoot, itemsRoot);
        Assert.True(recovery.TryEnsureInitialized(out var recoveryReason), recoveryReason);
        Assert.True(recovery.IsReadyFor(usersRoot, itemsRoot));
        var characterBefore = File.ReadAllBytes(characterPath);
        var itemsBefore = File.ReadAllBytes(itemPath);

        await Handlers.HandleAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                Session: contextSession,
                WorldData: worldData,
                Sessions: sessions,
                FloorItems: floorItems,
                PlayerItemsRoot: itemsRoot)
                { TradeOperations = new WorldOperationBarrier(), TradeRecovery = recovery },
            ReadFrame(request));

        Assert.True(sessions.TryGet(connection, out var stored));
        return new InvokeResult(
            contextSession,
            initialStoredSession,
            Assert.IsType<WorldSession>(stored),
            sent,
            log.ToString(),
            floorItems,
            characterBefore,
            File.ReadAllBytes(characterPath),
            itemsBefore,
            File.ReadAllBytes(itemPath));
    }

    private static void AssertDenied(InvokeResult result, string source)
    {
        var bytes = Assert.Single(result.Sent);
        var frame = ReadFrame(bytes);
        Assert.Equal((ushort)2255, frame.Header.Opcode);
        Assert.True(LegacyNpcReplyPacket.TryParse(frame, out var reply));
        Assert.Equal(LegacyNpcReplyInteractType.MessageBox, reply.InteractType);
        Assert.Equal(ActivationMessage, reply.Text);

        Assert.Same(result.InitialStoredSession, result.FinalSession);
        Assert.Equal(
            PlayerItemSnapshotLoader.ToLegacyBytes(result.InitialStoredSession.PlayerItems),
            PlayerItemSnapshotLoader.ToLegacyBytes(result.FinalSession.PlayerItems));
        Assert.Equal(result.InitialStoredSession.Character.Money, result.FinalSession.Character.Money);
        Assert.Empty(result.FloorItems.SnapshotVisible(DateTimeOffset.UtcNow));
        Assert.Equal(result.CharacterBefore, result.CharacterAfter);
        Assert.Equal(result.ItemsBefore, result.ItemsAfter);
        Assert.Contains("source=" + source, result.Log, StringComparison.Ordinal);
        Assert.Contains("wire=7899", result.Log, StringComparison.Ordinal);
        Assert.Contains("npcReplyOpcode=2255", result.Log, StringComparison.Ordinal);
    }

    private static void AssertNoActivationDenial(IEnumerable<byte[]> sent)
    {
        Assert.DoesNotContain(
            sent,
            bytes => LegacyNpcReplyPacket.TryParse(ReadFrame(bytes), out var reply) &&
                reply.InteractType == LegacyNpcReplyInteractType.MessageBox &&
                reply.Text == ActivationMessage);
    }

    private static byte[] DropPacket(uint itemUid)
    {
        return new MsgItemUsagePacket(
            itemUid,
            DwParam: 0,
            MsgItemUsageAction.DropItem,
            Timestamp: 0,
            DwParam2: 0,
            DwParam3: 0,
            DwParam4: 0).ToFrameBytes();
    }

    private static byte[] NpcOptionPacket(uint npcId, byte optionId)
    {
        return new LegacyNpcDialogRequestPacket(
            npcId,
            Mesh: 0,
            optionId,
            InteractType: (byte)LegacyNpcReplyInteractType.Option,
            Action: 0,
            Input: string.Empty).ToFrameBytes();
    }

    private static PlayerItemSnapshotItem Item(uint uid, uint itemId, uint animaItemId = 0)
    {
        return new PlayerItemSnapshotItem
        {
            Uid = uid,
            ItemId = itemId,
            Durability = 10,
            MaximumDurability = 10,
            Position = 0,
            StackSize = 1,
            Bound = 0,
            Color = 5,
            AnimaItemId = animaItemId,
        };
    }

    private static ItemType ItemType(
        uint itemId,
        uint goldWorth = 0,
        uint conquerPointsWorth = 0)
    {
        var fields = Enumerable.Repeat("0", 66).ToArray();
        fields[0] = itemId.ToString(CultureInfo.InvariantCulture);
        fields[1] = "SecurityAnimaTestItem";
        fields[WolfConquer.Core.Data.ItemType.GoldWorthFieldIndex] =
            goldWorth.ToString(CultureInfo.InvariantCulture);
        fields[WolfConquer.Core.Data.ItemType.DurabilityFieldIndex] = "10";
        fields[WolfConquer.Core.Data.ItemType.ConquerPointsWorthFieldIndex] =
            conquerPointsWorth.ToString(CultureInfo.InvariantCulture);
        return new ItemType(checked((int)itemId), fields[1], fields);
    }

    private static Npc SellerNpc()
    {
        return new Npc(
            checked((int)SellerNpcId),
            Kind: 0,
            Lookface: 0,
            MapId: 1_002,
            X: 430,
            Y: 378,
            OptionalFlag: null);
    }

    private static Dictionary<string, string> InactivePinAttributes()
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["PinCodeAnima"] = "abc123",
        };
    }

    private static Dictionary<string, string> ActivatedPinAttributes()
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["PinCodeAnima"] = "abc123",
            ["TREPIN2"] = "true",
        };
    }

    private static WorldSession Session(
        PlayerItemSnapshot playerItems,
        IReadOnlyDictionary<string, string> attributes,
        string usersRoot,
        uint? activeNpcId)
    {
        var characterAttributes = new Dictionary<string, string>(attributes, StringComparer.OrdinalIgnoreCase)
        {
            ["Money"] = "1000",
            ["ConquerPoints"] = "50",
        };
        return new WorldSession(
            AccountId,
            Username: "security-anima-item-operation-test",
            Character: new CharacterSnapshot(
                Uid: AccountId,
                Name: "SecurityAnimaItem",
                Body: 1003,
                Face: 0,
                Hair: 0,
                Class: 15,
                FirstClass: 15,
                SecondClass: 0,
                Reborn: 2,
                Level: 140,
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
                Money: 1_000,
                ConquerPoints: 50,
                BoundConquerPoints: 0,
                BankConquerPoints: 0,
                PkPoints: 0,
                Online: 0,
                SourceFile: Path.Combine(usersRoot, AccountId.ToString(CultureInfo.InvariantCulture) + ".ini"),
                Attributes: characterAttributes,
                PaidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)),
            PlayerItems: playerItems,
            PlayerSpells: PlayerSpellSnapshot.Empty,
            PlayerProficiencies: PlayerProficiencySnapshot.Empty,
            UsersRoot: usersRoot,
            ActiveNpcId: activeNpcId);
    }

    private static void WriteCharacter(
        string path,
        IReadOnlyDictionary<string, string> attributes)
    {
        var pin = attributes.TryGetValue("PinCodeAnima", out var pinValue) ? pinValue : string.Empty;
        var activated = attributes.TryGetValue("TREPIN2", out var activatedValue) ? activatedValue : "false";
        File.WriteAllText(
            path,
            "[Character]\r\n" +
            $"UID={AccountId.ToString(CultureInfo.InvariantCulture)}\r\n" +
            "Name=SecurityAnimaItem\r\nBody=1003\r\nFace=0\r\nHaire=0\r\nClass=15\r\n" +
            "FirstClass=15\r\nSecoundeClass=0\r\nReborn=2\r\nLevel=140\r\nExperience=0\r\n" +
            "Map=1002\r\nX=430\r\nY=378\r\nPMap=1002\r\nPMapX=430\r\nPMapY=378\r\n" +
            "Strength=0\r\nAgility=0\r\nVitaliti=0\r\nSpirit=0\r\nAtributes=0\r\n" +
            "MinHitPoints=1\r\nMinMana=0\r\nMoney=1000\r\nConquerPoints=50\r\n" +
            "BoundConquerPoints=0\r\nBankCps=0\r\nPkPoints=0\r\nOnline=0\r\n" +
            $"PinCodeAnima={pin}\r\nTREPIN2={activated}\r\nSentinel=keep-me\r\n",
            Encoding.Latin1);
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(
            new ReadOnlySequence<byte>(bytes),
            out var frame,
            out _,
            out _));
        return frame;
    }

    private static LegacyNpcReplyPacket ParseNpcReply(byte[] bytes)
    {
        Assert.True(LegacyNpcReplyPacket.TryParse(ReadFrame(bytes), out var reply));
        return reply;
    }

    private static WorldDataCatalog CreateWorldData(
        IReadOnlyList<ItemType> items,
        IReadOnlyList<Npc>? npcs = null)
    {
        return WorldDataCatalog.Create(
            new SeedManifest(
                DateTimeOffset.UnixEpoch,
                "fixture://security-anima-item-operation",
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
            npcs: npcs ?? [],
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

    private static GuildWarData EmptyGuildWarData(string name)
    {
        return new GuildWarData("fixture://" + name, [], [], [], info: null, pole: null, conductors: []);
    }

    private sealed record InvokeResult(
        WorldSession ContextSession,
        WorldSession InitialStoredSession,
        WorldSession FinalSession,
        IReadOnlyList<byte[]> Sent,
        string Log,
        FloorItemRuntimeStateStore FloorItems,
        byte[] CharacterBefore,
        byte[] CharacterAfter,
        byte[] ItemsBefore,
        byte[] ItemsAfter);
}
