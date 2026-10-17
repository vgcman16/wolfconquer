using System.Buffers;
using System.Globalization;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyRealmSashFragmentChanceTests : IDisposable
{
    private const uint SashFragmentRealmNpcId = 4_294_967_277;
    private const uint SashFragmentRealmItemId = 3_600_024;
    private const uint SmallSashItemId = 1_100_003;
    private const long CharacterUid = 1_293_601;
    private const string ForcedRateRollAttribute = "LegacyRealmTransferServicesSashFragmentRateRoll";

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "WolfConquer-LegacyRealmSashFragmentChanceTests-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task RollNineteenConsumesFirstUnboundSourceUnitGrantsUnboundSashPersistsAndPreservesSourceOrder()
    {
        var playerItemsRoot = CreatePlayerItemsRoot();
        var originalItems = CreatePlayerItems(
            CreateInventoryItem(10, SashFragmentRealmItemId, stackSize: 2, bound: 1),
            CreateInventoryItem(20, SashFragmentRealmItemId, stackSize: 2),
            CreateInventoryItem(30, 720_001));
        var result = await InvokeAsync(
            originalItems,
            forcedRoll: 19,
            CreateWorldDataCatalog(CreateItemType(SmallSashItemId, "Sash(S)", durability: 321)),
            playerItemsRoot);

        Assert.True(result.Handled);
        Assert.NotSame(result.OriginalSession, result.UpdatedSession);

        var sourceFragment = Assert.Single(
            result.UpdatedSession.PlayerItems.InventoryItems,
            item => item.Uid == 10);
        Assert.Equal(SashFragmentRealmItemId, sourceFragment.ItemId);
        Assert.Equal(2, sourceFragment.StackSize);
        Assert.Equal(1, sourceFragment.Bound);

        var unboundFragment = Assert.Single(
            result.UpdatedSession.PlayerItems.InventoryItems,
            item => item.Uid == 20);
        Assert.Equal(SashFragmentRealmItemId, unboundFragment.ItemId);
        Assert.Equal(1, unboundFragment.StackSize);
        Assert.Equal(0, unboundFragment.Bound);

        var sash = Assert.Single(
            result.UpdatedSession.PlayerItems.InventoryItems,
            item => item.ItemId == SmallSashItemId);
        Assert.Equal(31U, sash.Uid);
        Assert.Equal(321, sash.Durability);
        Assert.Equal(321, sash.MaximumDurability);
        Assert.Equal(0, sash.Position);
        Assert.Equal(0, sash.Bound);
        Assert.Equal(1, sash.StackSize);

        Assert.Equal(5, result.Sent.Count);
        AssertFragmentUpdate(result.Sent[0], uid: 20, stackSize: 1);

        var grantedPacket = AssertGameItem(result.Sent[1]);
        Assert.Equal(31U, grantedPacket.Uid);
        Assert.Equal(SmallSashItemId, grantedPacket.ItemId);
        Assert.Equal(LegacyGameItemPacket.AddItemMode, grantedPacket.Mode);
        Assert.Equal(321, grantedPacket.Durability);
        Assert.Equal(321, grantedPacket.MaximumDurability);
        Assert.Equal(0, grantedPacket.Bound);
        Assert.Equal(1, grantedPacket.StackSize);

        AssertExactDialog(
            result.Sent,
            offset: 2,
            "Wow, it must be your lucky day! You successfully made a Sash (S)!",
            "Great!");

        AssertPersistedEquals(playerItemsRoot, result.UpdatedSession.PlayerItems);
        Assert.Contains("source=NpcHandler.SashFragment_Realm", result.Log);
        Assert.Contains("sourceLines=24230-24252", result.Log);
        Assert.Contains("Rate(20)=true", result.Log);
        Assert.Contains("rateRoll=19", result.Log);
        Assert.Contains("rewardItemId=1100003", result.Log);
    }

    [Fact]
    public async Task RollTwentyConsumesFragmentGrantsNothingPersistsAndPreservesSourceOrder()
    {
        var playerItemsRoot = CreatePlayerItemsRoot();
        var originalItems = CreatePlayerItems(
            CreateInventoryItem(10, SashFragmentRealmItemId, bound: 1),
            CreateInventoryItem(20, SashFragmentRealmItemId));
        var result = await InvokeAsync(
            originalItems,
            forcedRoll: 20,
            CreateWorldDataCatalog(CreateItemType(SmallSashItemId, "Sash(S)")),
            playerItemsRoot);

        Assert.True(result.Handled);
        Assert.NotSame(result.OriginalSession, result.UpdatedSession);
        var remaining = Assert.Single(result.UpdatedSession.PlayerItems.InventoryItems);
        Assert.Equal(10U, remaining.Uid);
        Assert.Equal(SashFragmentRealmItemId, remaining.ItemId);
        Assert.Equal(1, remaining.Bound);
        Assert.DoesNotContain(
            result.UpdatedSession.PlayerItems.InventoryItems,
            item => item.ItemId == SmallSashItemId);

        Assert.Equal(4, result.Sent.Count);
        AssertFragmentRemoval(result.Sent[0], uid: 20);
        AssertExactDialog(
            result.Sent,
            offset: 1,
            "The fragment vanished since you failed to make a Sash (S) with it.",
            "Great!");
        Assert.DoesNotContain(result.Sent, IsGameItem);

        AssertPersistedEquals(playerItemsRoot, result.UpdatedSession.PlayerItems);
        Assert.Contains("sourceLines=24230-24252", result.Log);
        Assert.Contains("Rate(20)=false", result.Log);
        Assert.Contains("rateRoll=20", result.Log);
    }

    [Fact]
    public async Task BoundFragmentsDoNotSatisfyOuterUnboundContainGate()
    {
        var playerItemsRoot = CreatePlayerItemsRoot();
        var originalItems = CreatePlayerItems(
            CreateInventoryItem(10, SashFragmentRealmItemId, stackSize: 2, bound: 1));
        var result = await InvokeAsync(
            originalItems,
            forcedRoll: 19,
            CreateWorldDataCatalog(CreateItemType(SmallSashItemId, "Sash(S)")),
            playerItemsRoot);

        AssertMutationFree(result, originalItems);
        AssertExactDialog(
            result.Sent,
            offset: 0,
            "Sorry, you don`t have a SashFragment(Realm).",
            "I~see.");
        AssertNoItemMutationFrames(result.Sent);
        AssertPersistedEquals(playerItemsRoot, originalItems);
    }

    [Fact]
    public async Task FullInventoryGatePrecedesRemovalAndRoll()
    {
        var playerItemsRoot = CreatePlayerItemsRoot();
        var inventory = new List<PlayerItemSnapshotItem>
        {
            CreateInventoryItem(10, SashFragmentRealmItemId),
        };
        inventory.AddRange(
            Enumerable.Range(0, 39)
                .Select(index => CreateInventoryItem(
                    checked((uint)(100 + index)),
                    checked((uint)(410_000 + index)))));
        var originalItems = CreatePlayerItems(inventory.ToArray());
        var result = await InvokeAsync(
            originalItems,
            forcedRoll: 19,
            CreateWorldDataCatalog(),
            playerItemsRoot);

        AssertMutationFree(result, originalItems);
        Assert.Equal(40, result.UpdatedSession.PlayerItems.InventoryItems.Count);
        AssertExactDialog(
            result.Sent,
            offset: 0,
            "Please make one more space in your inventory.",
            "I~see.");
        AssertNoItemMutationFrames(result.Sent);
        Assert.DoesNotContain("reason=itemtype-missing", result.Log);
        AssertPersistedEquals(playerItemsRoot, originalItems);
    }

    [Fact]
    public async Task WinningRollMissingAuthoritativeSashItemTypeDoesNotConsumeFragment()
    {
        var playerItemsRoot = CreatePlayerItemsRoot();
        var originalItems = CreatePlayerItems(
            CreateInventoryItem(10, SashFragmentRealmItemId));
        var result = await InvokeAsync(
            originalItems,
            forcedRoll: 19,
            CreateWorldDataCatalog(),
            playerItemsRoot);

        AssertMutationFree(result, originalItems);
        Assert.Empty(result.Sent);
        Assert.Contains("rewardItemId=1100003", result.Log);
        Assert.Contains("rateRoll=19", result.Log);
        Assert.Contains("reason=itemtype-missing", result.Log);
        Assert.Contains("no fragment was consumed", result.Log);
        AssertPersistedEquals(playerItemsRoot, originalItems);
    }

    [Fact]
    public async Task WinningRollExhaustedItemUidDoesNotConsumeFragment()
    {
        var playerItemsRoot = CreatePlayerItemsRoot();
        var originalItems = CreatePlayerItems(
            CreateInventoryItem(uint.MaxValue, SashFragmentRealmItemId));
        var result = await InvokeAsync(
            originalItems,
            forcedRoll: 19,
            CreateWorldDataCatalog(CreateItemType(SmallSashItemId, "Sash(S)")),
            playerItemsRoot);

        AssertMutationFree(result, originalItems);
        Assert.Empty(result.Sent);
        Assert.Contains("rateRoll=19", result.Log);
        Assert.Contains("reason=item-uid-exhausted", result.Log);
        Assert.Contains("no fragment was consumed", result.Log);
        AssertPersistedEquals(playerItemsRoot, originalItems);
    }

    [Fact]
    public async Task MissingPlayerItemsRootKeepsCompatibilityGapMutationFree()
    {
        var originalItems = CreatePlayerItems(
            CreateInventoryItem(10, SashFragmentRealmItemId));
        var result = await InvokeAsync(
            originalItems,
            forcedRoll: 19,
            CreateWorldDataCatalog(CreateItemType(SmallSashItemId, "Sash(S)")),
            playerItemsRoot: null,
            seedPersistence: false);

        AssertMutationFree(result, originalItems);
        AssertExactDialog(
            result.Sent,
            offset: 0,
            "Single Sash Fragment exchange is unavailable here until random reward rolls and inventory mutation are modeled safely.",
            "I~see.");
        AssertNoItemMutationFrames(result.Sent);
        Assert.Contains("PARITY-GAP LegacyNpc RealmTransferServices", result.Log);
        Assert.Contains("sourceLines=24192-24255", result.Log);
    }

    [Fact]
    public async Task MissingSessionStoreDoesNotPersistOrMutate()
    {
        var playerItemsRoot = CreatePlayerItemsRoot();
        var originalItems = CreatePlayerItems(
            CreateInventoryItem(10, SashFragmentRealmItemId));
        var result = await InvokeAsync(
            originalItems,
            forcedRoll: 19,
            CreateWorldDataCatalog(CreateItemType(SmallSashItemId, "Sash(S)")),
            playerItemsRoot,
            includeSessionStore: false);

        AssertMutationFree(result, originalItems);
        Assert.Empty(result.Sent);
        Assert.Contains("reason=session-store-missing", result.Log);
        Assert.Contains("no item persistence or fragment mutation occurred", result.Log);
        AssertPersistedEquals(playerItemsRoot, originalItems);
    }

    [Theory]
    [InlineData(19U)]
    [InlineData(20U)]
    public async Task PersistenceFailureDoesNotCommitEitherRollOutcomeOrSendMutationPackets(uint forcedRoll)
    {
        Directory.CreateDirectory(_root);
        var blockedPlayerItemsRoot = Path.Combine(_root, "blocked-player-items-root-" + forcedRoll);
        File.WriteAllText(blockedPlayerItemsRoot, "not a directory");
        var originalItems = CreatePlayerItems(
            CreateInventoryItem(10, SashFragmentRealmItemId));
        var result = await InvokeAsync(
            originalItems,
            forcedRoll,
            CreateWorldDataCatalog(CreateItemType(SmallSashItemId, "Sash(S)")),
            blockedPlayerItemsRoot,
            seedPersistence: false);

        AssertMutationFree(result, originalItems);
        Assert.Empty(result.Sent);
        Assert.Contains("Sash Fragment chance persistence", result.Log);
        Assert.Contains("rateRoll=" + forcedRoll.ToString(CultureInfo.InvariantCulture), result.Log);
        Assert.Contains("no session mutation was committed", result.Log);
        Assert.Equal("not a directory", File.ReadAllText(blockedPlayerItemsRoot));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private async Task<InvokeResult> InvokeAsync(
        PlayerItemSnapshot originalItems,
        uint forcedRoll,
        WorldDataCatalog? worldData,
        string? playerItemsRoot,
        bool seedPersistence = true,
        bool includeSessionStore = true)
    {
        if (seedPersistence)
        {
            new PlayerItemSnapshotLoader(playerItemsRoot!).SaveByCharacterUid(CharacterUid, originalItems);
        }

        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(originalItems, forcedRoll);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var handled = await WorldPacketHandlers.TryHandleLegacyRealmTransferServicesNpcAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                Session: session,
                WorldData: worldData,
                Sessions: includeSessionStore ? sessions : null,
                PlayerItemsRoot: playerItemsRoot),
            session,
            SashFragmentRealmNpcId,
            optionId: 1,
            CancellationToken.None);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        return new InvokeResult(handled, sent, log.ToString(), session, updatedSession);
    }

    private string CreatePlayerItemsRoot()
    {
        return Path.Combine(_root, "PlayersItems-" + Guid.NewGuid().ToString("N"));
    }

    private static WorldSession CreateSession(PlayerItemSnapshot playerItems, uint forcedRoll)
    {
        return new WorldSession(
            AccountId: 100,
            Username: "realm-sash-fragment-chance-test",
            Character: new CharacterSnapshot(
                Uid: CharacterUid,
                Name: "RealmChanceTester",
                Body: 1003,
                Face: 0,
                Hair: 0,
                Class: 7_051,
                FirstClass: 7_051,
                SecondClass: 0,
                Reborn: 0,
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
                Money: 0,
                ConquerPoints: 0,
                BoundConquerPoints: 0,
                BankConquerPoints: 0,
                PkPoints: 0,
                Online: 0,
                SourceFile: "fixture",
                Attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    [ForcedRateRollAttribute] = forcedRoll.ToString(CultureInfo.InvariantCulture),
                },
                PaidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)),
            PlayerItems: playerItems,
            PlayerSpells: PlayerSpellSnapshot.Empty,
            PlayerProficiencies: PlayerProficiencySnapshot.Empty);
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
            Durability = 1,
            MaximumDurability = 1,
            Position = 0,
            StackSize = stackSize,
            Bound = bound,
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

    private static void AssertMutationFree(InvokeResult result, PlayerItemSnapshot originalItems)
    {
        Assert.True(result.Handled);
        Assert.Same(result.OriginalSession, result.UpdatedSession);
        AssertPlayerItemsEqual(originalItems, result.UpdatedSession.PlayerItems);
    }

    private static void AssertPersistedEquals(string playerItemsRoot, PlayerItemSnapshot expected)
    {
        var persisted = new PlayerItemSnapshotLoader(playerItemsRoot).LoadByCharacterUid(CharacterUid);
        AssertPlayerItemsEqual(expected, persisted);
    }

    private static void AssertFragmentUpdate(byte[] bytes, uint uid, ushort stackSize)
    {
        var packet = AssertGameItem(bytes);
        Assert.Equal(uid, packet.Uid);
        Assert.Equal(SashFragmentRealmItemId, packet.ItemId);
        Assert.Equal(LegacyGameItemPacket.UpdateItemMode, packet.Mode);
        Assert.Equal(stackSize, packet.StackSize);
    }

    private static void AssertFragmentRemoval(byte[] bytes, uint uid)
    {
        var packet = AssertItemUsage(bytes);
        Assert.Equal(uid, packet.Id);
        Assert.Equal(MsgItemUsageAction.RemoveInventory, packet.Action);
    }

    private static void AssertExactDialog(
        IReadOnlyList<byte[]> sent,
        int offset,
        string expectedText,
        string expectedOption)
    {
        Assert.Equal(offset + 3, sent.Count);

        var dialog = AssertNpcReply(sent[offset]);
        Assert.Equal(LegacyNpcReplyInteractType.Dialog, dialog.InteractType);
        Assert.Equal(expectedText, dialog.Text);

        var option = AssertNpcReply(sent[offset + 1]);
        Assert.Equal(LegacyNpcReplyInteractType.Option, option.InteractType);
        Assert.Equal(LegacyNpcReplyPacket.ClosedOptionId, option.OptionId);
        Assert.Equal(expectedOption, option.Text);

        var finish = AssertNpcReply(sent[offset + 2]);
        Assert.Equal(LegacyNpcReplyInteractType.Finish, finish.InteractType);
    }

    private static void AssertNoItemMutationFrames(IReadOnlyList<byte[]> sent)
    {
        Assert.DoesNotContain(sent, IsGameItem);
        Assert.DoesNotContain(sent, IsItemUsage);
    }

    private static LegacyNpcReplyPacket AssertNpcReply(byte[] bytes)
    {
        Assert.True(LegacyNpcReplyPacket.TryParse(ReadFrame(bytes), out var packet));
        return packet;
    }

    private static LegacyGameItemPacket AssertGameItem(byte[] bytes)
    {
        Assert.True(LegacyGameItemPacket.TryParse(ReadFrame(bytes), out var packet));
        return packet;
    }

    private static MsgItemUsagePacket AssertItemUsage(byte[] bytes)
    {
        Assert.True(MsgItemUsagePacket.TryParse(ReadFrame(bytes), out var packet));
        return packet;
    }

    private static bool IsGameItem(byte[] bytes)
    {
        return LegacyGameItemPacket.TryParse(ReadFrame(bytes), out _);
    }

    private static bool IsItemUsage(byte[] bytes)
    {
        return MsgItemUsagePacket.TryParse(ReadFrame(bytes), out _);
    }

    private static void AssertPlayerItemsEqual(PlayerItemSnapshot expected, PlayerItemSnapshot actual)
    {
        Assert.Equal(
            PlayerItemSnapshotLoader.ToLegacyBytes(expected),
            PlayerItemSnapshotLoader.ToLegacyBytes(actual));
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
                DateTimeOffset.Parse("2026-07-10T00:00:00Z", CultureInfo.InvariantCulture),
                "fixture://realm-sash-fragment-chance",
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

    private static ItemType CreateItemType(
        uint itemTypeId,
        string name,
        uint durability = 1)
    {
        var fields = Enumerable.Repeat("0", 66).ToArray();
        fields[0] = itemTypeId.ToString(CultureInfo.InvariantCulture);
        fields[1] = name;
        fields[ItemType.DurabilityFieldIndex] = durability.ToString(CultureInfo.InvariantCulture);
        return new ItemType(checked((int)itemTypeId), name, fields);
    }

    private static GuildWarData EmptyGuildWarData(string name)
    {
        return new GuildWarData("fixture://" + name, [], [], [], info: null, pole: null, conductors: []);
    }

    private sealed record InvokeResult(
        bool Handled,
        IReadOnlyList<byte[]> Sent,
        string Log,
        WorldSession OriginalSession,
        WorldSession UpdatedSession);
}
