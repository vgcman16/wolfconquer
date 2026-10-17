using System.Buffers;
using System.Globalization;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyNorbertBombCraftTests : IDisposable
{
    private const uint NorbertNpcId = 421;
    private const uint BombItemId = 721_261;
    private const uint SaltpeterItemId = 721_262;
    private const uint SulphurItemId = 721_263;
    private const long CharacterUid = 1_293_600;

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "WolfConquer-LegacyNorbertBombCraftTests-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task CraftConsumesUnboundMaterialsUnitWiseGrantsBombAndPreservesSourcePacketOrder()
    {
        var playerItemsRoot = CreatePlayerItemsRoot();
        var originalItems = CreatePlayerItems(
            CreateInventoryItem(10, SaltpeterItemId, stackSize: 3),
            CreateInventoryItem(20, SaltpeterItemId, stackSize: 2),
            CreateInventoryItem(25, SaltpeterItemId, stackSize: 7, bound: 1),
            CreateInventoryItem(30, SulphurItemId),
            CreateInventoryItem(35, SulphurItemId, stackSize: 2, bound: 1),
            CreateInventoryItem(40, 720_001));
        var result = await InvokeAsync(
            originalItems,
            CreateWorldDataCatalog(CreateItemType(BombItemId, "Bomb", durability: 321)),
            playerItemsRoot);

        Assert.True(result.Handled);
        Assert.NotSame(result.OriginalSession, result.UpdatedSession);
        Assert.DoesNotContain(
            result.UpdatedSession.PlayerItems.InventoryItems,
            item => item.ItemId == SaltpeterItemId && item.Bound == 0);
        Assert.DoesNotContain(
            result.UpdatedSession.PlayerItems.InventoryItems,
            item => item.ItemId == SulphurItemId && item.Bound == 0);
        Assert.Contains(
            result.UpdatedSession.PlayerItems.InventoryItems,
            item => item.Uid == 25 && item.ItemId == SaltpeterItemId && item.StackSize == 7 && item.Bound == 1);
        Assert.Contains(
            result.UpdatedSession.PlayerItems.InventoryItems,
            item => item.Uid == 35 && item.ItemId == SulphurItemId && item.StackSize == 2 && item.Bound == 1);

        var bomb = Assert.Single(
            result.UpdatedSession.PlayerItems.InventoryItems,
            item => item.ItemId == BombItemId);
        Assert.Equal(41U, bomb.Uid);
        Assert.Equal(321, bomb.Durability);
        Assert.Equal(321, bomb.MaximumDurability);
        Assert.Equal(0, bomb.Position);
        Assert.Equal(0, bomb.Bound);
        Assert.Equal(1, bomb.StackSize);

        Assert.Equal(11, result.Sent.Count);
        AssertMaterialUpdate(result.Sent[0], uid: 10, itemId: SaltpeterItemId, stackSize: 2);
        AssertMaterialUpdate(result.Sent[1], uid: 10, itemId: SaltpeterItemId, stackSize: 1);
        AssertMaterialRemoval(result.Sent[2], uid: 10);
        AssertMaterialUpdate(result.Sent[3], uid: 20, itemId: SaltpeterItemId, stackSize: 1);
        AssertMaterialRemoval(result.Sent[4], uid: 20);
        AssertMaterialRemoval(result.Sent[5], uid: 30);

        var grantedPacket = AssertGameItem(result.Sent[6]);
        Assert.Equal(41U, grantedPacket.Uid);
        Assert.Equal(BombItemId, grantedPacket.ItemId);
        Assert.Equal(LegacyGameItemPacket.AddItemMode, grantedPacket.Mode);
        Assert.Equal(321, grantedPacket.Durability);
        Assert.Equal(321, grantedPacket.MaximumDurability);
        Assert.Equal(0, grantedPacket.Bound);
        Assert.Equal(1, grantedPacket.StackSize);

        AssertExactDialog(
            result.Sent,
            offset: 7,
            "You can take the Bomb away. Remember that you`ll be killed once you use it to destroy the guild gate!",
            "I see. Thanks for the remind.");
        AssertPersistedEquals(playerItemsRoot, result.UpdatedSession.PlayerItems);
        Assert.Contains("source=NpcHandler.Norbert", result.Log);
        Assert.Contains("sourceLines=26352-26360", result.Log);
        Assert.Contains("rewardItemId=721261", result.Log);
        Assert.Contains("bound=false", result.Log);
    }

    [Fact]
    public async Task CraftDoesNotDebitAdvertisedThirtyThousandSilverFee()
    {
        var playerItemsRoot = CreatePlayerItemsRoot();
        var originalItems = CreatePlayerItems(
            CreateInventoryItem(10, SaltpeterItemId, stackSize: 5),
            CreateInventoryItem(20, SulphurItemId));
        const long silverBelowAdvertisedFee = 12_345;
        var result = await InvokeAsync(
            originalItems,
            CreateWorldDataCatalog(CreateItemType(BombItemId, "Bomb")),
            playerItemsRoot,
            money: silverBelowAdvertisedFee);

        Assert.True(result.Handled);
        Assert.NotSame(result.OriginalSession, result.UpdatedSession);
        Assert.Equal(silverBelowAdvertisedFee, result.OriginalSession.Character.Money);
        Assert.Equal(silverBelowAdvertisedFee, result.UpdatedSession.Character.Money);
        Assert.Single(result.UpdatedSession.PlayerItems.InventoryItems, item => item.ItemId == BombItemId);
        Assert.Contains("silverDebited=0", result.Log);
    }

    [Theory]
    [InlineData((byte)1, (byte)0)]
    [InlineData((byte)0, (byte)1)]
    public async Task BoundMaterialsDoNotSatisfyOptionTenAndRemainUntouched(
        byte saltpeterBound,
        byte sulphurBound)
    {
        var playerItemsRoot = CreatePlayerItemsRoot();
        var originalItems = CreatePlayerItems(
            CreateInventoryItem(10, SaltpeterItemId, stackSize: 5, bound: saltpeterBound),
            CreateInventoryItem(20, SulphurItemId, bound: sulphurBound));
        var result = await InvokeAsync(
            originalItems,
            CreateWorldDataCatalog(CreateItemType(BombItemId, "Bomb")),
            playerItemsRoot);

        AssertMutationFree(result, originalItems);
        Assert.Empty(result.Sent);
        Assert.Contains("reason=unbound-materials-missing", result.Log);
        AssertPersistedEquals(playerItemsRoot, originalItems);
    }

    [Fact]
    public async Task BoundMaterialsDoNotSatisfyOptionZeroMaterialPrompt()
    {
        var playerItemsRoot = CreatePlayerItemsRoot();
        var originalItems = CreatePlayerItems(
            CreateInventoryItem(10, SaltpeterItemId, stackSize: 5, bound: 1),
            CreateInventoryItem(20, SulphurItemId, bound: 1));
        var result = await InvokeAsync(
            originalItems,
            CreateWorldDataCatalog(CreateItemType(BombItemId, "Bomb")),
            playerItemsRoot,
            optionId: 0);

        AssertMutationFree(result, originalItems);
        var dialog = AssertNpcReply(result.Sent[0]);
        Assert.Equal(LegacyNpcReplyInteractType.Dialog, dialog.InteractType);
        Assert.Contains("Five pieces of Saltpeter", dialog.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("you have the materials", dialog.Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MissingBombItemDataDoesNotConsumeMaterials()
    {
        var playerItemsRoot = CreatePlayerItemsRoot();
        var originalItems = CreateCraftMaterials();
        var result = await InvokeAsync(
            originalItems,
            CreateWorldDataCatalog(),
            playerItemsRoot);

        AssertMutationFree(result, originalItems);
        Assert.Empty(result.Sent);
        Assert.Contains("rewardItemId=721261", result.Log);
        Assert.Contains("reason=itemtype-missing", result.Log);
        Assert.Contains("no materials were consumed", result.Log);
        AssertPersistedEquals(playerItemsRoot, originalItems);
    }

    [Fact]
    public async Task FullInventoryAfterVirtualConsumptionDoesNotConsumeMaterials()
    {
        var playerItemsRoot = CreatePlayerItemsRoot();
        var inventory = new List<PlayerItemSnapshotItem>
        {
            CreateInventoryItem(10, SaltpeterItemId, stackSize: 6),
            CreateInventoryItem(20, SulphurItemId, stackSize: 2),
        };
        inventory.AddRange(
            Enumerable.Range(0, 38)
                .Select(index => CreateInventoryItem(
                    checked((uint)(100 + index)),
                    checked((uint)(410_000 + index)))));
        var originalItems = CreatePlayerItems(inventory.ToArray());
        var result = await InvokeAsync(
            originalItems,
            CreateWorldDataCatalog(CreateItemType(BombItemId, "Bomb")),
            playerItemsRoot);

        AssertMutationFree(result, originalItems);
        Assert.Equal(40, result.UpdatedSession.PlayerItems.InventoryItems.Count);
        Assert.Empty(result.Sent);
        Assert.Contains("reason=inventory-full", result.Log);
        Assert.Contains("no materials were consumed", result.Log);
        AssertPersistedEquals(playerItemsRoot, originalItems);
    }

    [Fact]
    public async Task MissingSessionStoreDoesNotPersistOrMutate()
    {
        var playerItemsRoot = CreatePlayerItemsRoot();
        var originalItems = CreateCraftMaterials();
        var result = await InvokeAsync(
            originalItems,
            CreateWorldDataCatalog(CreateItemType(BombItemId, "Bomb")),
            playerItemsRoot,
            includeSessionStore: false);

        AssertMutationFree(result, originalItems);
        Assert.Empty(result.Sent);
        Assert.Contains("reason=session-store-missing", result.Log);
        Assert.Contains("no item persistence or inventory mutation occurred", result.Log);
        AssertPersistedEquals(playerItemsRoot, originalItems);
    }

    [Fact]
    public async Task MissingPlayerItemsRootKeepsCompatibilityGapMutationFree()
    {
        var originalItems = CreateCraftMaterials();
        var result = await InvokeAsync(
            originalItems,
            CreateWorldDataCatalog(CreateItemType(BombItemId, "Bomb")),
            playerItemsRoot: null,
            seedPersistence: false);

        AssertMutationFree(result, originalItems);
        AssertExactDialog(
            result.Sent,
            offset: 0,
            "Bomb crafting is unavailable here until inventory removal and item grants are modeled safely.",
            "I~see.");
        AssertNoItemMutationFrames(result.Sent);
        Assert.Contains("PARITY-GAP LegacyNpc RealmTransferServices", result.Log);
        Assert.Contains("sourceLines=26313-26369", result.Log);
    }

    [Fact]
    public async Task PersistenceFailureDoesNotCommitSessionOrSendMutationPackets()
    {
        Directory.CreateDirectory(_root);
        var blockedPlayerItemsRoot = Path.Combine(_root, "blocked-player-items-root");
        File.WriteAllText(blockedPlayerItemsRoot, "not a directory");
        var originalItems = CreateCraftMaterials();
        var result = await InvokeAsync(
            originalItems,
            CreateWorldDataCatalog(CreateItemType(BombItemId, "Bomb")),
            blockedPlayerItemsRoot,
            seedPersistence: false);

        AssertMutationFree(result, originalItems);
        Assert.Empty(result.Sent);
        Assert.Contains("Norbert Bomb craft persistence", result.Log);
        Assert.Contains("no session mutation was committed", result.Log);
        Assert.Equal("not a directory", File.ReadAllText(blockedPlayerItemsRoot));
    }

    [Fact]
    public async Task ExhaustedItemUidDoesNotConsumeMaterialsOrCommitSession()
    {
        var playerItemsRoot = CreatePlayerItemsRoot();
        var originalItems = CreatePlayerItems(
            CreateInventoryItem(10, SaltpeterItemId, stackSize: 5),
            CreateInventoryItem(uint.MaxValue, SulphurItemId));
        var result = await InvokeAsync(
            originalItems,
            CreateWorldDataCatalog(CreateItemType(BombItemId, "Bomb")),
            playerItemsRoot);

        AssertMutationFree(result, originalItems);
        Assert.Empty(result.Sent);
        Assert.Contains("reason=item-uid-exhausted", result.Log);
        Assert.Contains("no materials were consumed", result.Log);
        AssertPersistedEquals(playerItemsRoot, originalItems);
    }

    [Fact]
    public async Task PlayerItemsPersistBeforeSessionCommitAndCommitPrecedesFirstFrame()
    {
        var playerItemsRoot = CreatePlayerItemsRoot();
        var result = await InvokeAsync(
            CreateCraftMaterials(),
            CreateWorldDataCatalog(CreateItemType(BombItemId, "Bomb")),
            playerItemsRoot);

        Assert.True(result.Handled);
        Assert.True(result.PersistenceMatchedAtCommit);
        Assert.True(result.SessionCommittedBeforeFirstSend);
        AssertPersistedEquals(playerItemsRoot, result.UpdatedSession.PlayerItems);
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
        WorldDataCatalog? worldData,
        string? playerItemsRoot,
        long money = 50_000,
        bool seedPersistence = true,
        bool includeSessionStore = true,
        byte optionId = 10)
    {
        if (seedPersistence && playerItemsRoot is not null)
        {
            new PlayerItemSnapshotLoader(playerItemsRoot).SaveByCharacterUid(CharacterUid, originalItems);
        }

        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var session = CreateSession(originalItems, money);
        var persistenceMatchedAtCommit = false;
        var sessions = new WorldSessionStore((_, previous, current) =>
        {
            if (previous is null || ReferenceEquals(previous, current) || playerItemsRoot is null)
            {
                return;
            }

            try
            {
                var persisted = new PlayerItemSnapshotLoader(playerItemsRoot)
                    .LoadByCharacterUid(CharacterUid);
                persistenceMatchedAtCommit = PlayerItemSnapshotLoader
                    .ToLegacyBytes(current.PlayerItems)
                    .SequenceEqual(PlayerItemSnapshotLoader.ToLegacyBytes(persisted));
            }
            catch (Exception)
            {
                persistenceMatchedAtCommit = false;
            }
        });
        bool? sessionCommittedBeforeFirstSend = null;
        ConquerTcpConnection? connection = null;
        connection = new ConquerTcpConnection(
            null,
            new IPEndPoint(IPAddress.Loopback, 9958),
            (frame, _) =>
            {
                if (!sessionCommittedBeforeFirstSend.HasValue)
                {
                    sessionCommittedBeforeFirstSend = sessions.TryGet(connection!, out var current) &&
                        !ReferenceEquals(current, session);
                }

                sent.Add(frame.ToArray());
                return ValueTask.CompletedTask;
            });
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
            NorbertNpcId,
            optionId,
            CancellationToken.None);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        return new InvokeResult(
            handled,
            sent,
            log.ToString(),
            session,
            updatedSession,
            persistenceMatchedAtCommit,
            sessionCommittedBeforeFirstSend ?? false);
    }

    private string CreatePlayerItemsRoot()
    {
        return Path.Combine(_root, "PlayersItems-" + Guid.NewGuid().ToString("N"));
    }

    private static WorldSession CreateSession(PlayerItemSnapshot playerItems, long money)
    {
        return new WorldSession(
            AccountId: 100,
            Username: "norbert-bomb-craft-test",
            Character: new CharacterSnapshot(
                Uid: CharacterUid,
                Name: "NorbertTester",
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
                Money: money,
                ConquerPoints: 0,
                BoundConquerPoints: 0,
                BankConquerPoints: 0,
                PkPoints: 0,
                Online: 0,
                SourceFile: "fixture",
                Attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                PaidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)),
            PlayerItems: playerItems,
            PlayerSpells: PlayerSpellSnapshot.Empty,
            PlayerProficiencies: PlayerProficiencySnapshot.Empty);
    }

    private static PlayerItemSnapshot CreateCraftMaterials()
    {
        return CreatePlayerItems(
            CreateInventoryItem(10, SaltpeterItemId, stackSize: 5),
            CreateInventoryItem(20, SulphurItemId));
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

    private static void AssertMaterialUpdate(byte[] bytes, uint uid, uint itemId, ushort stackSize)
    {
        var packet = AssertGameItem(bytes);
        Assert.Equal(uid, packet.Uid);
        Assert.Equal(itemId, packet.ItemId);
        Assert.Equal(LegacyGameItemPacket.UpdateItemMode, packet.Mode);
        Assert.Equal(stackSize, packet.StackSize);
    }

    private static void AssertMaterialRemoval(byte[] bytes, uint uid)
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
        Assert.Equal(offset + 4, sent.Count);

        var dialog = AssertNpcReply(sent[offset]);
        Assert.Equal(LegacyNpcReplyInteractType.Dialog, dialog.InteractType);
        Assert.Equal(expectedText, dialog.Text);

        var option = AssertNpcReply(sent[offset + 1]);
        Assert.Equal(LegacyNpcReplyInteractType.Option, option.InteractType);
        Assert.Equal(LegacyNpcReplyPacket.ClosedOptionId, option.OptionId);
        Assert.Equal(expectedOption, option.Text);

        var avatar = AssertNpcReply(sent[offset + 2]);
        Assert.Equal(LegacyNpcReplyInteractType.Avatar, avatar.InteractType);
        Assert.Equal(67, avatar.InputMaxLength);

        var finish = AssertNpcReply(sent[offset + 3]);
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
                "fixture://norbert-bomb-craft",
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

    private static ItemType CreateItemType(uint itemTypeId, string name, uint durability = 1)
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
        WorldSession UpdatedSession,
        bool PersistenceMatchedAtCommit,
        bool SessionCommittedBeforeFirstSend);
}
