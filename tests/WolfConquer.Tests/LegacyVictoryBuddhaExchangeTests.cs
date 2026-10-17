using System.Buffers;
using System.Globalization;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyVictoryBuddhaExchangeTests : IDisposable
{
    private const uint VicotryBuddhaNpcId = 25_003;
    private const uint BuddhaNecklaceItemId = 3_007_563;
    private const uint BoundlessHeartItemId = 3_007_564;
    private const uint RosewoodIncenseItemId = 3_007_565;
    private const uint BuddhaNecklaceUid = 10;
    private const long CharacterUid = 1_293_600;

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "WolfConquer-LegacyVictoryBuddhaExchangeTests-" + Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData(29U, BoundlessHeartItemId, "You received BoundlessHeart.", true)]
    [InlineData(30U, RosewoodIncenseItemId, "You received RosewoodIncense.", false)]
    public async Task Option2UsesExactRate30BoundaryAndSourcePacketOrder(
        uint rateRoll,
        uint expectedRewardItemId,
        string expectedMessage,
        bool expectedRateHit)
    {
        var playerItemsRoot = CreatePlayerItemsRoot();
        var originalItems = Items(InventoryItem(BuddhaNecklaceUid, BuddhaNecklaceItemId));
        var result = await InvokeAsync(
            originalItems,
            CreateWorldDataCatalog(
                ItemType(BoundlessHeartItemId, "BoundlessHeart", stackLimit: 10),
                ItemType(RosewoodIncenseItemId, "RosewoodIncense", stackLimit: 10)),
            playerItemsRoot,
            rateRoll);

        Assert.True(result.Handled);
        Assert.NotSame(result.OriginalSession, result.UpdatedSession);
        Assert.DoesNotContain(
            result.UpdatedSession.PlayerItems.InventoryItems,
            item => item.ItemId == BuddhaNecklaceItemId);
        var reward = Assert.Single(
            result.UpdatedSession.PlayerItems.InventoryItems,
            item => item.ItemId == expectedRewardItemId);
        Assert.Equal((byte)0, reward.Bound);
        Assert.Equal((ushort)1, reward.StackSize);

        Assert.Equal(3, result.Sent.Count);
        var removal = AssertItemRemoval(result.Sent[0]);
        Assert.Equal(BuddhaNecklaceUid, removal.Id);

        var messageBox = AssertNpcReply(result.Sent[1]);
        Assert.Equal(LegacyNpcReplyInteractType.MessageBox, messageBox.InteractType);
        Assert.Equal(ushort.MaxValue, messageBox.InputMaxLength);
        Assert.Equal(expectedMessage, messageBox.Text);

        var grant = AssertGameItem(result.Sent[2]);
        Assert.Equal(expectedRewardItemId, grant.ItemId);
        Assert.Equal(LegacyGameItemPacket.AddItemMode, grant.Mode);
        Assert.Equal((byte)0, grant.Bound);
        Assert.Equal((ushort)1, grant.StackSize);

        var persisted = new PlayerItemSnapshotLoader(playerItemsRoot).LoadByCharacterUid(CharacterUid);
        AssertPlayerItemsEqual(result.UpdatedSession.PlayerItems, persisted);
        Assert.Contains($"Rate(30)={expectedRateHit.ToString().ToLowerInvariant()}", result.Log);
        Assert.Contains($"rateRoll={rateRoll}", result.Log);
        Assert.Contains($"rewardItemId={expectedRewardItemId}", result.Log);
        Assert.DoesNotContain("PARITY-GAP LegacyNpc VicotryBuddha necklace exchange", result.Log);
    }

    [Fact]
    public async Task Option2StacksCompatibleRewardAfterSourceMessageBox()
    {
        var playerItemsRoot = CreatePlayerItemsRoot();
        var originalItems = Items(
            InventoryItem(BuddhaNecklaceUid, BuddhaNecklaceItemId),
            InventoryItem(uid: 20, itemId: BoundlessHeartItemId, stackSize: 3));
        var result = await InvokeAsync(
            originalItems,
            CreateWorldDataCatalog(ItemType(BoundlessHeartItemId, "BoundlessHeart", stackLimit: 10)),
            playerItemsRoot,
            rateRoll: 29);

        Assert.True(result.Handled);
        var reward = Assert.Single(
            result.UpdatedSession.PlayerItems.InventoryItems,
            item => item.ItemId == BoundlessHeartItemId);
        Assert.Equal((uint)20, reward.Uid);
        Assert.Equal((ushort)4, reward.StackSize);

        Assert.Equal(3, result.Sent.Count);
        AssertItemRemoval(result.Sent[0]);
        Assert.Equal(LegacyNpcReplyInteractType.MessageBox, AssertNpcReply(result.Sent[1]).InteractType);
        var stackUpdate = AssertGameItem(result.Sent[2]);
        Assert.Equal((uint)20, stackUpdate.Uid);
        Assert.Equal(BoundlessHeartItemId, stackUpdate.ItemId);
        Assert.Equal(LegacyGameItemPacket.UpdateItemMode, stackUpdate.Mode);
        Assert.Equal((ushort)4, stackUpdate.StackSize);
        Assert.Contains("itemMode=update", result.Log);

        var persisted = new PlayerItemSnapshotLoader(playerItemsRoot).LoadByCharacterUid(CharacterUid);
        AssertPlayerItemsEqual(result.UpdatedSession.PlayerItems, persisted);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Option2RequiresAnUnboundNecklaceAndOtherwiseUsesSourceDialog(bool includeBoundNecklace)
    {
        var playerItemsRoot = CreatePlayerItemsRoot();
        var originalItems = includeBoundNecklace
            ? Items(InventoryItem(BuddhaNecklaceUid, BuddhaNecklaceItemId, bound: 1))
            : PlayerItemSnapshot.Empty;
        var result = await InvokeAsync(
            originalItems,
            CreateWorldDataCatalog(ItemType(BoundlessHeartItemId, "BoundlessHeart", stackLimit: 10)),
            playerItemsRoot,
            rateRoll: 29);

        Assert.True(result.Handled);
        Assert.Same(result.OriginalSession, result.UpdatedSession);
        AssertPlayerItemsEqual(originalItems, result.UpdatedSession.PlayerItems);
        AssertExactDialog(
            result.Sent,
            "Please come back when you will have 1 Buddha`sNecklace in your inventory.",
            "Oh.");
        Assert.DoesNotContain(result.Sent, IsInventoryMutation);
        Assert.Contains("reason=unbound-necklace-missing", result.Log);
        Assert.DoesNotContain("PARITY-GAP LegacyNpc VicotryBuddha necklace exchange", result.Log);

        var persisted = new PlayerItemSnapshotLoader(playerItemsRoot).LoadByCharacterUid(CharacterUid);
        AssertPlayerItemsEqual(originalItems, persisted);
    }

    [Fact]
    public async Task MissingSelectedRewardItemTypeDoesNotConsumeOrPersist()
    {
        var playerItemsRoot = CreatePlayerItemsRoot();
        var originalItems = Items(InventoryItem(BuddhaNecklaceUid, BuddhaNecklaceItemId));
        var result = await InvokeAsync(
            originalItems,
            CreateWorldDataCatalog(ItemType(RosewoodIncenseItemId, "RosewoodIncense", stackLimit: 10)),
            playerItemsRoot,
            rateRoll: 29);

        Assert.True(result.Handled);
        Assert.Same(result.OriginalSession, result.UpdatedSession);
        Assert.Empty(result.Sent);
        Assert.Contains($"rewardItemId={BoundlessHeartItemId}", result.Log);
        Assert.Contains("reason=itemtype-missing", result.Log);

        var persisted = new PlayerItemSnapshotLoader(playerItemsRoot).LoadByCharacterUid(CharacterUid);
        AssertPlayerItemsEqual(originalItems, persisted);
    }

    [Theory]
    [InlineData(false, true, "session-store-missing")]
    [InlineData(true, false, "playerItemsRoot-missing")]
    public async Task MissingCommitInfrastructureDoesNotMutate(
        bool includeSessionStore,
        bool includePlayerItemsRoot,
        string expectedReason)
    {
        var playerItemsRoot = includePlayerItemsRoot ? CreatePlayerItemsRoot() : null;
        var originalItems = Items(InventoryItem(BuddhaNecklaceUid, BuddhaNecklaceItemId));
        var result = await InvokeAsync(
            originalItems,
            CreateWorldDataCatalog(ItemType(BoundlessHeartItemId, "BoundlessHeart", stackLimit: 10)),
            playerItemsRoot,
            rateRoll: 29,
            includeSessionStore: includeSessionStore);

        Assert.True(result.Handled);
        Assert.Same(result.OriginalSession, result.UpdatedSession);
        AssertPlayerItemsEqual(originalItems, result.UpdatedSession.PlayerItems);
        Assert.Empty(result.Sent);
        Assert.Contains($"reason={expectedReason}", result.Log);

        if (playerItemsRoot is not null)
        {
            var persisted = new PlayerItemSnapshotLoader(playerItemsRoot).LoadByCharacterUid(CharacterUid);
            AssertPlayerItemsEqual(originalItems, persisted);
        }
    }

    [Fact]
    public async Task FullInventoryThatCannotStackRewardDoesNotConsumeNecklace()
    {
        var playerItemsRoot = CreatePlayerItemsRoot();
        var inventory = new List<PlayerItemSnapshotItem>
        {
            InventoryItem(BuddhaNecklaceUid, BuddhaNecklaceItemId, stackSize: 2),
        };
        inventory.AddRange(
            Enumerable.Range(0, 39)
                .Select(index => InventoryItem(
                    checked((uint)(100 + index)),
                    checked((uint)(410_000 + index)))));
        var originalItems = Items(inventory.ToArray());
        var result = await InvokeAsync(
            originalItems,
            CreateWorldDataCatalog(ItemType(BoundlessHeartItemId, "BoundlessHeart", stackLimit: 10)),
            playerItemsRoot,
            rateRoll: 29);

        Assert.True(result.Handled);
        Assert.Same(result.OriginalSession, result.UpdatedSession);
        AssertPlayerItemsEqual(originalItems, result.UpdatedSession.PlayerItems);
        Assert.Empty(result.Sent);
        Assert.Contains("reason=inventory-full", result.Log);

        var persisted = new PlayerItemSnapshotLoader(playerItemsRoot).LoadByCharacterUid(CharacterUid);
        AssertPlayerItemsEqual(originalItems, persisted);
    }

    [Fact]
    public async Task PersistenceFailureDoesNotCommitSessionOrSendMutationPackets()
    {
        Directory.CreateDirectory(_root);
        var blockedPlayerItemsRoot = Path.Combine(_root, "blocked-player-items-root");
        File.WriteAllText(blockedPlayerItemsRoot, "not a directory");
        var originalItems = Items(InventoryItem(BuddhaNecklaceUid, BuddhaNecklaceItemId));
        var result = await InvokeAsync(
            originalItems,
            CreateWorldDataCatalog(ItemType(BoundlessHeartItemId, "BoundlessHeart", stackLimit: 10)),
            blockedPlayerItemsRoot,
            rateRoll: 29,
            seedPersistence: false);

        Assert.True(result.Handled);
        Assert.Same(result.OriginalSession, result.UpdatedSession);
        AssertPlayerItemsEqual(originalItems, result.UpdatedSession.PlayerItems);
        Assert.Empty(result.Sent);
        Assert.Contains("necklace exchange persistence", result.Log);
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
        WorldDataCatalog worldData,
        string? playerItemsRoot,
        uint rateRoll,
        bool seedPersistence = true,
        bool includeSessionStore = true)
    {
        if (seedPersistence && playerItemsRoot is not null)
        {
            new PlayerItemSnapshotLoader(playerItemsRoot).SaveByCharacterUid(CharacterUid, originalItems);
        }

        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(originalItems, rateRoll);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var handled = await WorldPacketHandlers.TryHandleLegacyEventUtilityNpcAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                Session: session,
                WorldData: worldData,
                Sessions: includeSessionStore ? sessions : null,
                PlayerItemsRoot: playerItemsRoot),
            session,
            VicotryBuddhaNpcId,
            optionId: 2,
            CancellationToken.None);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        return new InvokeResult(handled, sent, log.ToString(), session, updatedSession);
    }

    private string CreatePlayerItemsRoot()
    {
        return Path.Combine(_root, "PlayersItems-" + Guid.NewGuid().ToString("N"));
    }

    private static WorldSession CreateSession(PlayerItemSnapshot playerItems, uint rateRoll)
    {
        return new WorldSession(
            AccountId: 100,
            Username: "victory-buddha-test",
            Character: new CharacterSnapshot(
                Uid: CharacterUid,
                Name: "BuddhaHero",
                Body: 1003,
                Face: 0,
                Hair: 0,
                Class: 7_051,
                FirstClass: 7_051,
                SecondClass: 0,
                Reborn: 0,
                Level: 140,
                Experience: 0,
                MapId: 3_850,
                X: 40,
                Y: 40,
                PreviousMapId: 3_850,
                PreviousMapX: 40,
                PreviousMapY: 40,
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
                    ["LegacyVicotryBuddhaRateRoll"] = rateRoll.ToString(CultureInfo.InvariantCulture),
                },
                PaidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)),
            PlayerItems: playerItems,
            PlayerSpells: PlayerSpellSnapshot.Empty,
            PlayerProficiencies: PlayerProficiencySnapshot.Empty);
    }

    private static PlayerItemSnapshot Items(params PlayerItemSnapshotItem[] inventoryItems)
    {
        return new PlayerItemSnapshot(inventoryItems, [], [], [], []);
    }

    private static PlayerItemSnapshotItem InventoryItem(
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
        Assert.Equal((ushort)226, avatar.InputMaxLength);

        var finish = AssertNpcReply(sent[3]);
        Assert.Equal(LegacyNpcReplyInteractType.Finish, finish.InteractType);
    }

    private static MsgItemUsagePacket AssertItemRemoval(byte[] bytes)
    {
        Assert.True(MsgItemUsagePacket.TryParse(ReadFrame(bytes), out var packet));
        Assert.Equal(MsgItemUsageAction.RemoveInventory, packet.Action);
        return packet;
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

    private static bool IsInventoryMutation(byte[] bytes)
    {
        var frame = ReadFrame(bytes);
        return MsgItemUsagePacket.TryParse(frame, out _) || LegacyGameItemPacket.TryParse(frame, out _);
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
                "fixture://victory-buddha-exchange",
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

    private static ItemType ItemType(uint itemTypeId, string name, ushort stackLimit)
    {
        var fields = Enumerable.Repeat("0", 66).ToArray();
        fields[0] = itemTypeId.ToString(CultureInfo.InvariantCulture);
        fields[1] = name;
        fields[WolfConquer.Core.Data.ItemType.DurabilityFieldIndex] = "1";
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
