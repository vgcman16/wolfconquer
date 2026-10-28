using System.Buffers;
using System.Globalization;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyDisCityDarkHornParityTests : IDisposable
{
    private const uint DicCityMap4NpcId = 4_026_316;
    private const uint TwinDisCityMainNpcId = 3_215;
    private const byte RedeemOptionId = 20;
    private const uint DarkHornItemId = 790_001;
    private const uint PowerExpBallItemId = 722_057;
    private const uint DragonBallScrollItemId = 720_028;
    private const uint StarDrillItemId = 1_200_006;
    private const uint ChiRewardItemId = 3_005_129;
    private const long CharacterUid = 1_293_600;
    private const string CharacterName = "DarkHornHero";
    private const string RewardBroadcast =
        "DarkHornHero has claimed the reward from DisCity.[5PowerExpBalls, 2DBScrolls, 1StarDrill and 3000ChiPoints.]";

    private static readonly uint[] RewardItemIds =
    [
        PowerExpBallItemId,
        PowerExpBallItemId,
        PowerExpBallItemId,
        PowerExpBallItemId,
        PowerExpBallItemId,
        DragonBallScrollItemId,
        DragonBallScrollItemId,
        StarDrillItemId,
        ChiRewardItemId,
    ];

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "WolfConquer-LegacyDisCityDarkHornParityTests-" + Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData(DicCityMap4NpcId, 154)]
    [InlineData(TwinDisCityMainNpcId, 67)]
    public async Task MissingUnboundHornUsesExactSourceDialogAndDoesNotMutate(
        uint npcId,
        ushort expectedAvatar)
    {
        var originalItems = CreatePlayerItems(
            CreateInventoryItem(10, DarkHornItemId, bound: 1),
            CreateInventoryItem(20, 410_001));
        using var harness = CreateHarness(npcId, originalItems, worldData: null);

        Assert.True(await harness.InvokeAsync());

        Assert.Same(harness.OriginalSession, harness.CurrentSession);
        AssertExactDialog(
            harness.ClientSent,
            "Sorry you not have DarkHorn.",
            "Ah. ok",
            expectedAvatar);
        AssertNoMutationOrBroadcastFrames(harness.ClientSent);
        AssertPersistedEquals(harness.PlayerItemsRoot, originalItems);
        Assert.Equal(npcId, harness.OriginalSession.ActiveNpcId);
    }

    [Theory]
    [InlineData(DicCityMap4NpcId, 8)]
    [InlineData(TwinDisCityMainNpcId, 67)]
    public async Task SevenFreeSlotsUsesExactSourceDialogAndDoesNotConsumeHorn(
        uint npcId,
        ushort expectedAvatar)
    {
        var originalItems = CreateCapacityInventory(usedSlots: 33);
        using var harness = CreateHarness(npcId, originalItems, worldData: null);

        Assert.True(await harness.InvokeAsync());

        Assert.Same(harness.OriginalSession, harness.CurrentSession);
        AssertExactDialog(
            harness.ClientSent,
            "Please make 8 more spaces in your inventory",
            "Let me check.",
            expectedAvatar);
        AssertNoMutationOrBroadcastFrames(harness.ClientSent);
        AssertPersistedEquals(harness.PlayerItemsRoot, originalItems);
    }

    [Theory]
    [InlineData(DicCityMap4NpcId)]
    [InlineData(TwinDisCityMainNpcId)]
    public async Task EightFreeSlotsRedeemsIntoNineIndividualRewardsInSourcePacketOrder(uint npcId)
    {
        var originalItems = CreateCapacityInventory(usedSlots: 32);
        using var harness = CreateHarness(npcId, originalItems, CreateCompleteWorldData(), includeObserver: true);

        Assert.True(await harness.InvokeAsync());

        var updated = harness.CurrentSession;
        Assert.NotSame(harness.OriginalSession, updated);
        Assert.DoesNotContain(updated.PlayerItems.InventoryItems, item => item.ItemId == DarkHornItemId);
        Assert.Equal(40, updated.PlayerItems.InventoryItems.Count);
        AssertUnrelatedItemsPreserved(originalItems, updated.PlayerItems);

        var rewards = updated.PlayerItems.InventoryItems
            .Where(item => RewardItemIds.Contains(item.ItemId))
            .ToArray();
        Assert.Equal(RewardItemIds, rewards.Select(item => item.ItemId).ToArray());
        Assert.Equal(9, rewards.Select(item => item.Uid).Distinct().Count());
        Assert.All(rewards, reward =>
        {
            Assert.Equal(0, reward.Bound);
            Assert.Equal(0, reward.Position);
            Assert.Equal(1, reward.StackSize);
            Assert.InRange(reward.Color, 3U, 8U);
        });

        Assert.True(harness.PersistenceMatchedAtCommit);
        Assert.True(harness.SessionCommittedBeforeFirstSend);
        Assert.Equal(1, harness.CommitCount);
        AssertPersistedEquals(harness.PlayerItemsRoot, updated.PlayerItems);
        AssertSourceOrderedSuccessFrames(harness, hornUid: 10);
    }

    [Theory]
    [InlineData(0U)]
    [InlineData(PowerExpBallItemId)]
    [InlineData(DragonBallScrollItemId)]
    [InlineData(StarDrillItemId)]
    [InlineData(ChiRewardItemId)]
    public async Task MissingCatalogOrRewardTypeFailsClosedWithoutConsumingHorn(uint missingItemId)
    {
        var originalItems = CreatePlayerItems(
            CreateInventoryItem(10, DarkHornItemId),
            CreateInventoryItem(20, 410_001));
        var worldData = missingItemId == 0
            ? null
            : CreateWorldDataCatalog(
                CreateRewardItemTypes().Where(item => checked((uint)item.Id) != missingItemId).ToArray());
        using var harness = CreateHarness(TwinDisCityMainNpcId, originalItems, worldData, includeObserver: true);

        Assert.True(await harness.InvokeAsync());

        Assert.Same(harness.OriginalSession, harness.CurrentSession);
        AssertNoMutationOrBroadcastFrames(harness.ClientSent);
        Assert.Empty(harness.ObserverSent);
        AssertPersistedEquals(harness.PlayerItemsRoot, originalItems);
    }

    [Fact]
    public async Task ItemPersistenceFailureFailsClosedWithoutExposingRewardFrames()
    {
        var originalItems = CreatePlayerItems(
            CreateInventoryItem(10, DarkHornItemId),
            CreateInventoryItem(20, 410_001));
        using var harness = CreateHarness(
            DicCityMap4NpcId,
            originalItems,
            CreateCompleteWorldData(),
            blockPlayerItemsRoot: true,
            includeObserver: true);

        Assert.True(await harness.InvokeAsync());

        Assert.Same(harness.OriginalSession, harness.CurrentSession);
        AssertNoMutationOrBroadcastFrames(harness.ClientSent);
        Assert.Empty(harness.ObserverSent);
        Assert.Equal("blocked", File.ReadAllText(harness.PlayerItemsRoot));
    }

    [Theory]
    [InlineData(DicCityMap4NpcId)]
    [InlineData(TwinDisCityMainNpcId)]
    public async Task StaleOptionReplayCannotRedeemTheSameHornTwice(uint npcId)
    {
        var originalItems = CreatePlayerItems(
            CreateInventoryItem(10, DarkHornItemId),
            CreateInventoryItem(20, 410_001));
        using var harness = CreateHarness(npcId, originalItems, CreateCompleteWorldData(), includeObserver: true);

        Assert.True(await harness.InvokeAsync());
        var firstFrameCount = harness.ClientSent.Count;
        Assert.True(await harness.InvokeAsync(harness.OriginalSession));

        Assert.Equal(1, harness.CommitCount);
        Assert.Equal(1, harness.ClientSent.Count(IsHornRemoval));
        Assert.Equal(9, harness.ClientSent.Count(IsRewardAdd));
        Assert.Equal(1, harness.ClientSent.Count(IsRewardBroadcast));
        Assert.Single(harness.ObserverSent, IsRewardBroadcast);
        AssertNoMutationOrBroadcastFrames(harness.ClientSent.Skip(firstFrameCount));

        var updated = harness.CurrentSession;
        Assert.DoesNotContain(updated.PlayerItems.InventoryItems, item => item.ItemId == DarkHornItemId);
        Assert.Equal(9, updated.PlayerItems.InventoryItems.Count(item => RewardItemIds.Contains(item.ItemId)));
        AssertPersistedEquals(harness.PlayerItemsRoot, updated.PlayerItems);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private DarkHornHarness CreateHarness(
        uint npcId,
        PlayerItemSnapshot originalItems,
        WorldDataCatalog? worldData,
        bool blockPlayerItemsRoot = false,
        bool includeObserver = false)
    {
        var invocationRoot = Path.Combine(_root, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(invocationRoot);
        var playerItemsRoot = Path.Combine(invocationRoot, "PlayersItems");
        if (blockPlayerItemsRoot)
        {
            File.WriteAllText(playerItemsRoot, "blocked");
        }
        else
        {
            new PlayerItemSnapshotLoader(playerItemsRoot).SaveByCharacterUid(CharacterUid, originalItems);
        }

        return new DarkHornHarness(
            npcId,
            originalItems,
            worldData,
            playerItemsRoot,
            includeObserver);
    }

    private static PlayerItemSnapshot CreateCapacityInventory(int usedSlots)
    {
        Assert.InRange(usedSlots, 1, 40);
        var inventory = new List<PlayerItemSnapshotItem>
        {
            CreateInventoryItem(10, DarkHornItemId),
        };
        inventory.AddRange(
            Enumerable.Range(0, usedSlots - 1)
                .Select(index => CreateInventoryItem(
                    checked((uint)(100 + index)),
                    checked((uint)(410_000 + index)))));
        return CreatePlayerItems(inventory.ToArray());
    }

    private static void AssertSourceOrderedSuccessFrames(DarkHornHarness harness, uint hornUid)
    {
        Assert.Equal(11, harness.ClientSent.Count);

        var removal = AssertItemUsage(harness.ClientSent[0]);
        Assert.Equal(hornUid, removal.Id);
        Assert.Equal(MsgItemUsageAction.RemoveInventory, removal.Action);

        for (var index = 0; index < RewardItemIds.Length; index++)
        {
            var add = AssertGameItem(harness.ClientSent[index + 1]);
            Assert.Equal(RewardItemIds[index], add.ItemId);
            Assert.Equal(LegacyGameItemPacket.AddItemMode, add.Mode);
            Assert.Equal(0, add.Bound);
            Assert.Equal(0, add.Position);
            Assert.Equal(1, add.StackSize);
        }

        var broadcast = AssertRewardBroadcast(harness.ClientSent[10]);
        var observerBroadcast = AssertRewardBroadcast(Assert.Single(harness.ObserverSent));
        Assert.Equal(broadcast.Color, observerBroadcast.Color);
        Assert.Equal(broadcast.ChatType, observerBroadcast.ChatType);
        Assert.Equal(broadcast.Content.ToArray(), observerBroadcast.Content.ToArray());
    }

    private static void AssertExactDialog(
        IReadOnlyList<byte[]> sent,
        string expectedText,
        string expectedOption,
        ushort expectedAvatar)
    {
        Assert.Equal(4, sent.Count);

        var dialog = AssertNpcReply(sent[0]);
        Assert.Equal(LegacyNpcReplyInteractType.Dialog, dialog.InteractType);
        Assert.Equal(expectedText, dialog.Text);

        var option = AssertNpcReply(sent[1]);
        Assert.Equal(LegacyNpcReplyInteractType.Option, option.InteractType);
        Assert.Equal(LegacyNpcReplyPacket.ClosedOptionId, option.OptionId);
        Assert.Equal(expectedOption, option.Text);

        var avatar = AssertNpcReply(sent[2]);
        Assert.Equal(LegacyNpcReplyInteractType.Avatar, avatar.InteractType);
        Assert.Equal(expectedAvatar, avatar.InputMaxLength);

        Assert.Equal(LegacyNpcReplyInteractType.Finish, AssertNpcReply(sent[3]).InteractType);
    }

    private static void AssertUnrelatedItemsPreserved(
        PlayerItemSnapshot original,
        PlayerItemSnapshot updated)
    {
        foreach (var expected in original.InventoryItems.Where(item => item.ItemId != DarkHornItemId))
        {
            var actual = Assert.Single(updated.InventoryItems, item => item.Uid == expected.Uid);
            Assert.Equal(
                PlayerItemSnapshotLoader.ToLegacyBytes(CreatePlayerItems(expected)),
                PlayerItemSnapshotLoader.ToLegacyBytes(CreatePlayerItems(actual)));
        }
    }

    private static void AssertNoMutationOrBroadcastFrames(IEnumerable<byte[]> sent)
    {
        Assert.DoesNotContain(sent, IsItemMutation);
        Assert.DoesNotContain(sent, IsRewardBroadcast);
    }

    private static void AssertPersistedEquals(string playerItemsRoot, PlayerItemSnapshot expected)
    {
        var persisted = new PlayerItemSnapshotLoader(playerItemsRoot).LoadByCharacterUid(CharacterUid);
        Assert.Equal(
            PlayerItemSnapshotLoader.ToLegacyBytes(expected),
            PlayerItemSnapshotLoader.ToLegacyBytes(persisted));
    }

    private static LegacyNpcReplyPacket AssertNpcReply(byte[] bytes)
    {
        Assert.True(LegacyNpcReplyPacket.TryParse(ReadFrame(bytes), out var packet));
        return packet;
    }

    private static MsgItemUsagePacket AssertItemUsage(byte[] bytes)
    {
        Assert.True(MsgItemUsagePacket.TryParse(ReadFrame(bytes), out var packet));
        return packet;
    }

    private static LegacyGameItemPacket AssertGameItem(byte[] bytes)
    {
        Assert.True(LegacyGameItemPacket.TryParse(ReadFrame(bytes), out var packet));
        return packet;
    }

    private static LegacyMsgMessagePacket AssertRewardBroadcast(byte[] bytes)
    {
        Assert.True(LegacyMsgMessagePacket.TryParse(ReadFrame(bytes), out var packet));
        Assert.Equal(LegacyMsgMessagePacket.TalkChatType, packet.ChatType);
        Assert.Equal(LegacyMsgMessagePacket.RedColor, packet.Color);
        Assert.Equal(RewardBroadcast, packet.Content[3]);
        return packet;
    }

    private static bool IsItemMutation(byte[] bytes)
    {
        var frame = ReadFrame(bytes);
        return MsgItemUsagePacket.TryParse(frame, out _) || LegacyGameItemPacket.TryParse(frame, out _);
    }

    private static bool IsHornRemoval(byte[] bytes)
    {
        return MsgItemUsagePacket.TryParse(ReadFrame(bytes), out var packet) &&
            packet.Id == 10 &&
            packet.Action == MsgItemUsageAction.RemoveInventory;
    }

    private static bool IsRewardAdd(byte[] bytes)
    {
        return LegacyGameItemPacket.TryParse(ReadFrame(bytes), out var packet) &&
            packet.Mode == LegacyGameItemPacket.AddItemMode &&
            RewardItemIds.Contains(packet.ItemId);
    }

    private static bool IsRewardBroadcast(byte[] bytes)
    {
        return LegacyMsgMessagePacket.TryParse(ReadFrame(bytes), out var packet) &&
            packet.ChatType == LegacyMsgMessagePacket.TalkChatType &&
            packet.Content.Count > 3 &&
            packet.Content[3] == RewardBroadcast;
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }

    private static PlayerItemSnapshot CreatePlayerItems(params PlayerItemSnapshotItem[] inventoryItems)
    {
        return new PlayerItemSnapshot(inventoryItems, [], [], [], []);
    }

    private static PlayerItemSnapshotItem CreateInventoryItem(
        uint uid,
        uint itemId,
        ushort stackSize = 1,
        byte bound = 0)
    {
        return new PlayerItemSnapshotItem
        {
            Uid = uid,
            ItemId = itemId,
            Durability = 77,
            MaximumDurability = 99,
            Position = 0,
            StackSize = stackSize,
            Bound = bound,
            Color = 5,
            Plus = 2,
            Bless = 1,
        };
    }

    private static WorldSession CreateSession(
        PlayerItemSnapshot playerItems,
        long characterUid,
        string characterName,
        uint activeNpcId)
    {
        var mapId = activeNpcId == TwinDisCityMainNpcId ? 1_002 : 2_024;
        return new WorldSession(
            AccountId: 100,
            Username: "dark-horn-test",
            Character: new CharacterSnapshot(
                Uid: characterUid,
                Name: characterName,
                Body: 1003,
                Face: 0,
                Hair: 0,
                Class: 7_051,
                FirstClass: 7_051,
                SecondClass: 0,
                Reborn: 0,
                Level: 140,
                Experience: 0,
                MapId: mapId,
                X: 430,
                Y: 378,
                PreviousMapId: mapId,
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
                Attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["DynamicID"] = "0",
                    ["Sentinel"] = "keep-me",
                },
                PaidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)),
            PlayerItems: playerItems,
            PlayerSpells: PlayerSpellSnapshot.Empty,
            PlayerProficiencies: PlayerProficiencySnapshot.Empty,
            ActiveNpcId: activeNpcId);
    }

    private static WorldDataCatalog CreateCompleteWorldData()
    {
        return CreateWorldDataCatalog(CreateRewardItemTypes());
    }

    private static ItemType[] CreateRewardItemTypes()
    {
        return
        [
            CreateItemType(PowerExpBallItemId, "PowerExpBall"),
            CreateItemType(DragonBallScrollItemId, "DBScroll"),
            CreateItemType(StarDrillItemId, "StarDrill"),
            CreateItemType(ChiRewardItemId, "ChiReward"),
        ];
    }

    private static ItemType CreateItemType(uint itemTypeId, string name)
    {
        var fields = Enumerable.Repeat("0", 66).ToArray();
        fields[0] = itemTypeId.ToString(CultureInfo.InvariantCulture);
        fields[1] = name;
        fields[ItemType.DurabilityFieldIndex] = "321";
        fields[46] = "100";
        return new ItemType(checked((int)itemTypeId), name, fields);
    }

    private static WorldDataCatalog CreateWorldDataCatalog(params ItemType[] items)
    {
        return WorldDataCatalog.Create(
            new SeedManifest(
                DateTimeOffset.Parse("2026-07-14T00:00:00Z", CultureInfo.InvariantCulture),
                "fixture://dis-city-dark-horn",
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

    private static GuildWarData EmptyGuildWarData(string name)
    {
        return new GuildWarData("fixture://" + name, [], [], [], info: null, pole: null, conductors: []);
    }

    private sealed class DarkHornHarness : IDisposable
    {
        private readonly WorldPacketHandlerContext _context;
        private readonly WorldSessionStore _sessions;
        private readonly ConquerTcpConnection _connection;
        private readonly ConquerTcpConnection? _observerConnection;
        private bool? _sessionCommittedBeforeFirstSend;
        public bool FailObserver;
        public WorldSession? PublishedAtFailure;

        public DarkHornHarness(
            uint npcId,
            PlayerItemSnapshot originalItems,
            WorldDataCatalog? worldData,
            string playerItemsRoot,
            bool includeObserver)
        {
            NpcId = npcId;
            PlayerItemsRoot = playerItemsRoot;
            OriginalSession = CreateSession(originalItems, CharacterUid, CharacterName, npcId);
            Log = new StringWriter();
            _sessions = new WorldSessionStore((_, previous, current) =>
            {
                if (previous is null || ReferenceEquals(previous, current))
                {
                    return;
                }

                if (FailObserver && current.ActiveNpcId is null)
                {
                    FailObserver = false; PublishedAtFailure = current;
                    throw new IOException("Actual Dark Horn observer failed after publication.");
                }
                CommitCount++;
                try
                {
                    var persisted = new PlayerItemSnapshotLoader(PlayerItemsRoot)
                        .LoadByCharacterUid(CharacterUid);
                    PersistenceMatchedAtCommit = PlayerItemSnapshotLoader
                        .ToLegacyBytes(current.PlayerItems)
                        .SequenceEqual(PlayerItemSnapshotLoader.ToLegacyBytes(persisted));
                }
                catch (Exception)
                {
                    PersistenceMatchedAtCommit = false;
                }
            });

            ConquerTcpConnection? connection = null;
            connection = new ConquerTcpConnection(
                null,
                new IPEndPoint(IPAddress.Loopback, 9958),
                (frame, _) =>
                {
                    _sessionCommittedBeforeFirstSend ??= _sessions.TryGet(connection!, out var current) &&
                        !ReferenceEquals(current, OriginalSession);
                    ClientSent.Add(frame.ToArray());
                    return ValueTask.CompletedTask;
                });
            _connection = connection;
            _sessions.Set(_connection, OriginalSession);

            if (includeObserver)
            {
                _observerConnection = new ConquerTcpConnection(
                    null,
                    new IPEndPoint(IPAddress.Loopback, 9959),
                    (frame, _) =>
                    {
                        ObserverSent.Add(frame.ToArray());
                        return ValueTask.CompletedTask;
                    });
                _sessions.Set(
                    _observerConnection,
                    CreateSession(PlayerItemSnapshot.Empty, CharacterUid + 1, "Observer", npcId));
            }

            _context = new WorldPacketHandlerContext(
                _connection,
                Log,
                Session: OriginalSession,
                WorldData: worldData,
                Sessions: _sessions,
                PlayerItemsRoot: PlayerItemsRoot,
                ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13);
        }

        public uint NpcId { get; }

        public string PlayerItemsRoot { get; }

        public WorldSession OriginalSession { get; }

        public StringWriter Log { get; }

        public List<byte[]> ClientSent { get; } = [];

        public List<byte[]> ObserverSent { get; } = [];

        public int CommitCount { get; private set; }

        public bool PersistenceMatchedAtCommit { get; private set; }

        public bool SessionCommittedBeforeFirstSend => _sessionCommittedBeforeFirstSend == true;

        public bool HasCurrentSession => _sessions.TryGet(_connection, out _);

        public WorldSession CurrentSession
        {
            get
            {
                Assert.True(_sessions.TryGet(_connection, out var current));
                return Assert.IsType<WorldSession>(current);
            }
        }

        public void SetBeforeAction(WorldSession replacement) => _sessions.Set(_connection, replacement);

        public ValueTask<bool> InvokeAsync(WorldSession? suppliedSession = null)
        {
            var session = suppliedSession ?? CurrentSession;
            return WorldPacketHandlers.TryHandleLegacyDisCityBattlefieldServicesNpcAsync(
                _context with { Session = session },
                session,
                NpcId,
                RedeemOptionId,
                CancellationToken.None);
        }

        public void Dispose()
        {
            Log.Dispose();
        }
    }
}
