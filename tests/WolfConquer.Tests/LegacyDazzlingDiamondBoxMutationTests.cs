using System.Buffers;
using System.Globalization;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyDazzlingDiamondBoxMutationTests : IDisposable
{
    private const uint DazzlingDiamondBoxNpcId = 4_294_967_279;
    private const uint DazzlingDiamondBoxItemId = 3_008_732;
    private const long CharacterUid = 1_293_600;

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "WolfConquer-LegacyDazzlingDiamondBoxMutationTests-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task RootDialogUsesExactDecompiledTextAndOptions()
    {
        var result = await InvokeAsync(
            optionId: 0,
            CreatePlayerItems(CreateInventoryItem(10, DazzlingDiamondBoxItemId)),
            worldData: null,
            playerItemsRoot: null,
            seedPersistence: false);

        Assert.True(result.Handled);
        Assert.Same(result.OriginalSession, result.UpdatedSession);
        var replies = result.Sent.Select(AssertNpcReply).ToArray();
        Assert.Equal(
            "Hello. You can chose a 30 pieces Diamond: Sun~Diamond, Moon~Diamond, Star~Diamond or Cloud~Diamond.",
            replies[0].Text);
        Assert.Equal(
            [1, 2, 3, 4, byte.MaxValue],
            replies
                .Where(packet => packet.InteractType == LegacyNpcReplyInteractType.Option)
                .Select(packet => packet.OptionId)
                .ToArray());
    }

    [Theory]
    [InlineData(1, 721_533U, "Sun")]
    [InlineData(2, 721_534U, "Moon")]
    [InlineData(3, 721_535U, "Star")]
    [InlineData(4, 721_536U, "Cloud")]
    public async Task SelectionConsumesOneBoxGrantsThirtyDiamondsAndPersistsReload(
        byte optionId,
        uint rewardItemId,
        string diamondName)
    {
        var playerItemsRoot = CreatePlayerItemsRoot();
        var originalItems = CreatePlayerItems(
            CreateInventoryItem(10, DazzlingDiamondBoxItemId, stackSize: 2));
        var result = await InvokeAsync(
            optionId,
            originalItems,
            CreateWorldDataCatalog(CreateItemType(rewardItemId, diamondName + "Diamond", stackLimit: 100, durability: 321)),
            playerItemsRoot);

        Assert.True(result.Handled);
        Assert.NotSame(result.OriginalSession, result.UpdatedSession);
        var remainingBox = Assert.Single(
            result.UpdatedSession.PlayerItems.InventoryItems,
            item => item.ItemId == DazzlingDiamondBoxItemId);
        Assert.Equal(1, remainingBox.StackSize);

        var reward = Assert.Single(
            result.UpdatedSession.PlayerItems.InventoryItems,
            item => item.ItemId == rewardItemId);
        Assert.Equal(30, reward.StackSize);
        Assert.Equal(321, reward.Durability);
        Assert.Equal(321, reward.MaximumDurability);
        Assert.Equal(0, reward.Bound);
        Assert.InRange(reward.Color, 3U, 8U);

        Assert.Equal(3, result.Sent.Count);
        var boxUpdate = AssertGameItem(result.Sent[0]);
        Assert.Equal(DazzlingDiamondBoxItemId, boxUpdate.ItemId);
        Assert.Equal(LegacyGameItemPacket.UpdateItemMode, boxUpdate.Mode);
        Assert.Equal(1, boxUpdate.StackSize);

        var rewardPacket = AssertGameItem(result.Sent[1]);
        Assert.Equal(rewardItemId, rewardPacket.ItemId);
        Assert.Equal(LegacyGameItemPacket.AddItemMode, rewardPacket.Mode);
        Assert.Equal(30, rewardPacket.StackSize);
        Assert.Equal(0, rewardPacket.Bound);
        Assert.Equal(
            $"You receive 30 pieces of the {diamondName}~Diamond.",
            AssertMessageBox(result.Sent[2]).Text);

        var persisted = new PlayerItemSnapshotLoader(playerItemsRoot).LoadByCharacterUid(CharacterUid);
        AssertPlayerItemsEqual(result.UpdatedSession.PlayerItems, persisted);
        Assert.Contains($"optionId={optionId}", result.Log);
        Assert.Contains($"rewardItemId={rewardItemId}", result.Log);
        Assert.Contains("rewardCount=30", result.Log);
        Assert.DoesNotContain("PARITY-GAP LegacyNpc ItemPackServices Dazzling Diamond Box exchange", result.Log);
    }

    [Fact]
    public async Task ExistingCompatibleDiamondStackReceivesAllThirtyAsOneUpdate()
    {
        var playerItemsRoot = CreatePlayerItemsRoot();
        var originalItems = CreatePlayerItems(
            CreateInventoryItem(10, DazzlingDiamondBoxItemId),
            CreateInventoryItem(20, 721_533, stackSize: 50));
        var result = await InvokeAsync(
            optionId: 1,
            originalItems,
            CreateWorldDataCatalog(CreateItemType(721_533, "SunDiamond", stackLimit: 100)),
            playerItemsRoot);

        Assert.True(result.Handled);
        var reward = Assert.Single(result.UpdatedSession.PlayerItems.InventoryItems);
        Assert.Equal(721_533U, reward.ItemId);
        Assert.Equal(80, reward.StackSize);

        var removal = AssertItemUsage(result.Sent[0]);
        Assert.Equal(10U, removal.Id);
        Assert.Equal(MsgItemUsageAction.RemoveInventory, removal.Action);
        var rewardUpdate = AssertGameItem(result.Sent[1]);
        Assert.Equal(20U, rewardUpdate.Uid);
        Assert.Equal(LegacyGameItemPacket.UpdateItemMode, rewardUpdate.Mode);
        Assert.Equal(80, rewardUpdate.StackSize);
        Assert.Equal("You receive 30 pieces of the Sun~Diamond.", AssertMessageBox(result.Sent[2]).Text);

        var persisted = new PlayerItemSnapshotLoader(playerItemsRoot).LoadByCharacterUid(CharacterUid);
        AssertPlayerItemsEqual(result.UpdatedSession.PlayerItems, persisted);
    }

    [Fact]
    public async Task UnstackableDiamondTypeCreatesThirtyIndividualItems()
    {
        var playerItemsRoot = CreatePlayerItemsRoot();
        var originalItems = CreatePlayerItems(CreateInventoryItem(10, DazzlingDiamondBoxItemId));
        var result = await InvokeAsync(
            optionId: 2,
            originalItems,
            CreateWorldDataCatalog(CreateItemType(721_534, "MoonDiamond", stackLimit: 0)),
            playerItemsRoot);

        Assert.True(result.Handled);
        Assert.Equal(30, result.UpdatedSession.PlayerItems.InventoryItems.Count);
        Assert.All(result.UpdatedSession.PlayerItems.InventoryItems, item =>
        {
            Assert.Equal(721_534U, item.ItemId);
            Assert.Equal(1, item.StackSize);
            Assert.Equal(0, item.Bound);
            Assert.InRange(item.Color, 3U, 8U);
        });

        var itemPackets = result.Sent
            .Where(bytes => LegacyGameItemPacket.TryParse(ReadFrame(bytes), out _))
            .Select(AssertGameItem)
            .ToArray();
        Assert.Equal(30, itemPackets.Length);
        Assert.All(itemPackets, packet =>
        {
            Assert.Equal(721_534U, packet.ItemId);
            Assert.Equal(LegacyGameItemPacket.AddItemMode, packet.Mode);
            Assert.Equal(1, packet.StackSize);
        });
        Assert.Equal("You receive 30 pieces of the Moon~Diamond.", AssertMessageBox(result.Sent[^1]).Text);

        var persisted = new PlayerItemSnapshotLoader(playerItemsRoot).LoadByCharacterUid(CharacterUid);
        AssertPlayerItemsEqual(result.UpdatedSession.PlayerItems, persisted);
    }

    [Fact]
    public async Task BoundFirstBoxIsConsumedAfterUnboundEligibilityGate()
    {
        var playerItemsRoot = CreatePlayerItemsRoot();
        var originalItems = CreatePlayerItems(
            CreateInventoryItem(10, DazzlingDiamondBoxItemId, stackSize: 2, bound: 1),
            CreateInventoryItem(11, DazzlingDiamondBoxItemId));
        var result = await InvokeAsync(
            optionId: 3,
            originalItems,
            CreateWorldDataCatalog(CreateItemType(721_535, "StarDiamond", stackLimit: 100)),
            playerItemsRoot);

        Assert.True(result.Handled);
        var boxes = result.UpdatedSession.PlayerItems.InventoryItems
            .Where(item => item.ItemId == DazzlingDiamondBoxItemId)
            .OrderBy(item => item.Uid)
            .ToArray();
        Assert.Equal(2, boxes.Length);
        Assert.Equal(10U, boxes[0].Uid);
        Assert.Equal(1, boxes[0].Bound);
        Assert.Equal(1, boxes[0].StackSize);
        Assert.Equal(11U, boxes[1].Uid);
        Assert.Equal(0, boxes[1].Bound);
        Assert.Equal(1, boxes[1].StackSize);

        var consumption = AssertGameItem(result.Sent[0]);
        Assert.Equal(10U, consumption.Uid);
        Assert.Equal(1, consumption.Bound);
        Assert.Equal(1, consumption.StackSize);
        Assert.Equal(LegacyGameItemPacket.UpdateItemMode, consumption.Mode);
    }

    [Fact]
    public async Task BoundOnlyBoxUsesExactSourceMessageAndDoesNotMutate()
    {
        var originalItems = CreatePlayerItems(
            CreateInventoryItem(10, DazzlingDiamondBoxItemId, bound: 1));
        var result = await InvokeAsync(
            optionId: 1,
            originalItems,
            worldData: null,
            playerItemsRoot: null,
            seedPersistence: false);

        Assert.True(result.Handled);
        Assert.Same(result.OriginalSession, result.UpdatedSession);
        AssertPlayerItemsEqual(originalItems, result.UpdatedSession.PlayerItems);
        var message = Assert.Single(result.Sent);
        Assert.Equal(
            "Sorry, you don`t have a DazzlingDiamondBox.",
            AssertMessageBox(message).Text);
    }

    [Fact]
    public async Task FewerThanThirtyFreeSlotsUsesExactDialogAndDoesNotConsumeBox()
    {
        var inventory = new List<PlayerItemSnapshotItem>
        {
            CreateInventoryItem(10, DazzlingDiamondBoxItemId),
        };
        inventory.AddRange(
            Enumerable.Range(0, 10)
                .Select(index => CreateInventoryItem(
                    checked((uint)(100 + index)),
                    checked((uint)(410_000 + index)))));
        var originalItems = CreatePlayerItems(inventory.ToArray());
        var result = await InvokeAsync(
            optionId: 4,
            originalItems,
            worldData: null,
            playerItemsRoot: null,
            seedPersistence: false);

        Assert.True(result.Handled);
        Assert.Same(result.OriginalSession, result.UpdatedSession);
        AssertPlayerItemsEqual(originalItems, result.UpdatedSession.PlayerItems);
        AssertExactDialog(result.Sent, "Please make 30 spaces in your inventory.", "Let~me~check.");
        Assert.DoesNotContain(result.Sent, IsGameItem);
        Assert.DoesNotContain(result.Sent, IsItemUsage);
    }

    [Fact]
    public async Task MissingRewardItemTypeIsMutationFree()
    {
        var playerItemsRoot = CreatePlayerItemsRoot();
        var originalItems = CreatePlayerItems(CreateInventoryItem(10, DazzlingDiamondBoxItemId));
        var result = await InvokeAsync(
            optionId: 1,
            originalItems,
            CreateWorldDataCatalog(),
            playerItemsRoot);

        Assert.True(result.Handled);
        Assert.Same(result.OriginalSession, result.UpdatedSession);
        Assert.Empty(result.Sent);
        Assert.Contains("reason=reward-itemtype-missing", result.Log);
        AssertPlayerItemsEqual(
            originalItems,
            new PlayerItemSnapshotLoader(playerItemsRoot).LoadByCharacterUid(CharacterUid));
    }

    [Theory]
    [InlineData(false, true, "session-store-missing")]
    [InlineData(true, false, "player-items-root-missing")]
    public async Task MissingPersistencePrerequisiteDoesNotCommitExchange(
        bool includeSessionStore,
        bool includePlayerItemsRoot,
        string expectedReason)
    {
        var playerItemsRoot = includePlayerItemsRoot ? CreatePlayerItemsRoot() : null;
        var originalItems = CreatePlayerItems(CreateInventoryItem(10, DazzlingDiamondBoxItemId));
        var result = await InvokeAsync(
            optionId: 1,
            originalItems,
            CreateWorldDataCatalog(CreateItemType(721_533, "SunDiamond", stackLimit: 100)),
            playerItemsRoot,
            seedPersistence: includePlayerItemsRoot,
            includeSessionStore: includeSessionStore);

        Assert.True(result.Handled);
        Assert.Same(result.OriginalSession, result.UpdatedSession);
        AssertPlayerItemsEqual(originalItems, result.UpdatedSession.PlayerItems);
        Assert.Empty(result.Sent);
        Assert.Contains("reason=" + expectedReason, result.Log);
        if (playerItemsRoot is not null)
        {
            AssertPlayerItemsEqual(
                originalItems,
                new PlayerItemSnapshotLoader(playerItemsRoot).LoadByCharacterUid(CharacterUid));
        }
    }

    [Fact]
    public async Task PersistenceFailureDoesNotCommitSessionOrSendExchangePackets()
    {
        Directory.CreateDirectory(_root);
        var blockedPlayerItemsRoot = Path.Combine(_root, "blocked-player-items-root");
        File.WriteAllText(blockedPlayerItemsRoot, "not a directory");
        var originalItems = CreatePlayerItems(CreateInventoryItem(10, DazzlingDiamondBoxItemId));
        var result = await InvokeAsync(
            optionId: 1,
            originalItems,
            CreateWorldDataCatalog(CreateItemType(721_533, "SunDiamond", stackLimit: 100)),
            blockedPlayerItemsRoot,
            seedPersistence: false);

        Assert.True(result.Handled);
        Assert.Same(result.OriginalSession, result.UpdatedSession);
        AssertPlayerItemsEqual(originalItems, result.UpdatedSession.PlayerItems);
        Assert.Empty(result.Sent);
        Assert.Contains("reason=persistence-", result.Log);
        Assert.Equal("not a directory", File.ReadAllText(blockedPlayerItemsRoot));
    }

    [Fact]
    public async Task ExhaustedItemUidDoesNotConsumeBoxOrPersistPartialReward()
    {
        var playerItemsRoot = CreatePlayerItemsRoot();
        var originalItems = CreatePlayerItems(
            CreateInventoryItem(uint.MaxValue, DazzlingDiamondBoxItemId));
        var result = await InvokeAsync(
            optionId: 1,
            originalItems,
            CreateWorldDataCatalog(CreateItemType(721_533, "SunDiamond", stackLimit: 100)),
            playerItemsRoot);

        Assert.True(result.Handled);
        Assert.Same(result.OriginalSession, result.UpdatedSession);
        Assert.Empty(result.Sent);
        Assert.Contains("reason=item-uid-exhausted", result.Log);
        AssertPlayerItemsEqual(
            originalItems,
            new PlayerItemSnapshotLoader(playerItemsRoot).LoadByCharacterUid(CharacterUid));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private async Task<InvokeResult> InvokeAsync(
        byte optionId,
        PlayerItemSnapshot originalItems,
        WorldDataCatalog? worldData,
        string? playerItemsRoot,
        bool seedPersistence = true,
        bool includeSessionStore = true)
    {
        if (seedPersistence)
        {
            Assert.False(string.IsNullOrWhiteSpace(playerItemsRoot));
            new PlayerItemSnapshotLoader(playerItemsRoot!).SaveByCharacterUid(CharacterUid, originalItems);
        }

        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(originalItems);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var handled = await WorldPacketHandlers.TryHandleLegacyItemPackServicesNpcAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                Session: session,
                WorldData: worldData,
                Sessions: includeSessionStore ? sessions : null,
                PlayerItemsRoot: playerItemsRoot),
            session,
            DazzlingDiamondBoxNpcId,
            optionId,
            CancellationToken.None);

        var updatedSession = session;
        if (includeSessionStore)
        {
            Assert.True(sessions.TryGet(connection, out var stored));
            Assert.NotNull(stored);
            updatedSession = stored;
        }

        return new InvokeResult(handled, sent, log.ToString(), session, updatedSession);
    }

    private string CreatePlayerItemsRoot()
    {
        return Path.Combine(_root, "PlayersItems-" + Guid.NewGuid().ToString("N"));
    }

    private static WorldSession CreateSession(PlayerItemSnapshot playerItems)
    {
        return new WorldSession(
            AccountId: 100,
            Username: "dazzling-diamond-box-test",
            Character: new CharacterSnapshot(
                Uid: CharacterUid,
                Name: "DiamondHero",
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
                    ["DynamicID"] = "0",
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

    private static void AssertExactDialog(
        IReadOnlyList<byte[]> sent,
        string expectedText,
        string expectedOption)
    {
        Assert.Equal(3, sent.Count);
        var dialog = AssertNpcReply(sent[0]);
        Assert.Equal(LegacyNpcReplyInteractType.Dialog, dialog.InteractType);
        Assert.Equal(expectedText, dialog.Text);

        var option = AssertNpcReply(sent[1]);
        Assert.Equal(LegacyNpcReplyInteractType.Option, option.InteractType);
        Assert.Equal(LegacyNpcReplyPacket.ClosedOptionId, option.OptionId);
        Assert.Equal(expectedOption, option.Text);

        Assert.Equal(LegacyNpcReplyInteractType.Finish, AssertNpcReply(sent[2]).InteractType);
    }

    private static LegacyNpcReplyPacket AssertNpcReply(byte[] bytes)
    {
        Assert.True(LegacyNpcReplyPacket.TryParse(ReadFrame(bytes), out var packet));
        return packet;
    }

    private static LegacyNpcReplyPacket AssertMessageBox(byte[] bytes)
    {
        var packet = AssertNpcReply(bytes);
        Assert.Equal(LegacyNpcReplyInteractType.MessageBox, packet.InteractType);
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
                "fixture://dazzling-diamond-box",
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
        ushort stackLimit,
        ushort durability = 1)
    {
        var fields = Enumerable.Repeat("0", 66).ToArray();
        fields[0] = itemTypeId.ToString(CultureInfo.InvariantCulture);
        fields[1] = name;
        fields[ItemType.DurabilityFieldIndex] = durability.ToString(CultureInfo.InvariantCulture);
        fields[46] = stackLimit.ToString(CultureInfo.InvariantCulture);
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
