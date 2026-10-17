using System.Buffers;
using System.Globalization;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyDeitylandSupportItemPersistenceTests : IDisposable
{
    private const uint RelicSpiritItemId = 3_307_142;
    private const uint RelicCrystalItemId = 3_306_885;
    private const uint DragonBallItemId = 1_088_000;
    private const uint GeneratedRelicItemId = 4_100_001;
    private const long AccountId = 1_293_600;
    private const long CharacterUid = 1_293_600;

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "wolf-deityland-support-persistence-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task RelicSpiritPersistsItemsAndTrueFlagBeforeCommitThenSendsSourceMutationOrder()
    {
        var initialItems = Items(
            inventory:
            [
                InventoryItem(10, RelicSpiritItemId),
                InventoryItem(20, DragonBallItemId),
                InventoryItem(21, 720_001),
            ],
            equipment: [EquipmentItem(30, 4_100_005, durability: 1_099, maximumDurability: 1_099)]);

        var result = await InvokeAsync(
            SupportMutation.RelicSpirit,
            initialItems,
            CreateRelicWorldData());

        Assert.NotSame(result.InitialSession, result.UpdatedSession);
        Assert.Equal(bool.TrueString, result.UpdatedSession.Character.Attributes["RelicSpirit"]);
        Assert.DoesNotContain(result.UpdatedSession.PlayerItems.InventoryItems, item =>
            item.ItemId is RelicSpiritItemId or DragonBallItemId);

        var reward = Assert.Single(
            result.UpdatedSession.PlayerItems.InventoryItems,
            item => item.ItemId == GeneratedRelicItemId);
        Assert.Equal(31U, reward.Uid);
        Assert.Equal((ushort)1_099, reward.Durability);
        Assert.Equal((ushort)1_099, reward.MaximumDurability);
        Assert.Equal(10_003U, reward.Attribute1);
        Assert.Equal(20_004U, reward.Attribute2);
        Assert.Equal(30_005U, reward.Attribute3);

        Assert.Equal(1, result.CommitCount);
        AssertItemsEqual(result.UpdatedSession.PlayerItems, Assert.IsType<PlayerItemSnapshot>(result.ItemsAtCommit));
        Assert.Equal(
            bool.TrueString,
            Assert.IsType<CharacterSnapshot>(result.CharacterAtCommit).Attributes["RelicSpirit"]);
        AssertItemsEqual(result.UpdatedSession.PlayerItems, Assert.IsType<PlayerItemSnapshot>(result.PersistedItems));
        Assert.Equal(
            bool.TrueString,
            Assert.IsType<CharacterSnapshot>(result.PersistedCharacter).Attributes["RelicSpirit"]);

        Assert.Equal(3, result.Sent.Count);
        AssertRemoveInventory(result.Sent[0], uid: 10);
        AssertRemoveInventory(result.Sent[1], uid: 20);
        var rewardPacket = AssertGameItem(result.Sent[2]);
        Assert.Equal(LegacyGameItemPacket.AddItemMode, rewardPacket.Mode);
        Assert.Equal(reward.Uid, rewardPacket.Uid);
        Assert.Equal(reward.ItemId, rewardPacket.ItemId);
        Assert.Contains("persisted LegacyNpc RelicSpirit shape", result.Log);
        Assert.Contains("source=NpcHandler.RelicSpirit", result.Log);
    }

    [Fact]
    public async Task RelicCrystalPersistsConsumedCrystalsAndRepairedEquipmentBeforeCommit()
    {
        var initialItems = Items(
            inventory:
            [
                InventoryItem(40, RelicCrystalItemId),
                InventoryItem(41, RelicCrystalItemId),
                InventoryItem(42, 720_001),
            ],
            equipment: [EquipmentItem(50, GeneratedRelicItemId, durability: 900, maximumDurability: 1_100)]);

        var result = await InvokeAsync(
            SupportMutation.RelicCrystal,
            initialItems,
            worldData: null);

        Assert.NotSame(result.InitialSession, result.UpdatedSession);
        Assert.DoesNotContain(
            result.UpdatedSession.PlayerItems.InventoryItems,
            item => item.ItemId == RelicCrystalItemId);
        var repaired = Assert.Single(result.UpdatedSession.PlayerItems.EquipmentItems);
        Assert.Equal((ushort)1_100, repaired.Durability);
        Assert.Equal((ushort)1_100, repaired.MaximumDurability);

        Assert.Equal(1, result.CommitCount);
        AssertItemsEqual(result.UpdatedSession.PlayerItems, Assert.IsType<PlayerItemSnapshot>(result.ItemsAtCommit));
        AssertItemsEqual(result.UpdatedSession.PlayerItems, Assert.IsType<PlayerItemSnapshot>(result.PersistedItems));

        Assert.Equal(3, result.Sent.Count);
        AssertRemoveInventory(result.Sent[0], uid: 40);
        AssertRemoveInventory(result.Sent[1], uid: 41);
        var repairedPacket = AssertGameItem(result.Sent[2]);
        Assert.Equal(LegacyGameItemPacket.UpdateItemMode, repairedPacket.Mode);
        Assert.Equal(repaired.Uid, repairedPacket.Uid);
        Assert.Equal(repaired.MaximumDurability, repairedPacket.Durability);
        Assert.Contains("persisted LegacyNpc RelicCrystal repair", result.Log);
        Assert.Contains("crystals=2", result.Log);
    }

    [Theory]
    [InlineData(SupportMutation.RelicSpirit)]
    [InlineData(SupportMutation.RelicCrystal)]
    public async Task MissingSessionStoreLeavesLiveAndPersistedStateAndPacketsUnchanged(
        SupportMutation mutation)
    {
        var initialItems = ValidItems(mutation);
        var result = await InvokeAsync(
            mutation,
            initialItems,
            mutation == SupportMutation.RelicSpirit ? CreateRelicWorldData() : null,
            includeSessionStore: false);

        AssertMutationFree(result, initialItems);
        Assert.Contains("reason=session-store-missing", result.Log);
    }

    [Theory]
    [InlineData(SupportMutation.RelicSpirit)]
    [InlineData(SupportMutation.RelicCrystal)]
    public async Task MissingPlayerItemsRootLeavesLiveStateAndPacketsUnchanged(
        SupportMutation mutation)
    {
        var initialItems = ValidItems(mutation);
        var result = await InvokeAsync(
            mutation,
            initialItems,
            mutation == SupportMutation.RelicSpirit ? CreateRelicWorldData() : null,
            includePlayerItemsRoot: false);

        AssertMutationFree(result, initialItems, expectPersistedItems: false);
        Assert.Contains("reason=player-items-root-missing", result.Log);
    }

    [Fact]
    public async Task RelicSpiritRequiresCharacterRootBeforeGeneratingOrConsuming()
    {
        var initialItems = ValidItems(SupportMutation.RelicSpirit);
        var result = await InvokeAsync(
            SupportMutation.RelicSpirit,
            initialItems,
            CreateRelicWorldData(),
            includeUsersRoot: false);

        AssertMutationFree(result, initialItems, expectPersistedCharacter: false);
        Assert.Contains("reason=users-root-missing", result.Log);
    }

    [Fact]
    public async Task RelicSpiritMissingRandomRelicDataDoesNotConsumeMaterials()
    {
        var initialItems = ValidItems(SupportMutation.RelicSpirit);
        var result = await InvokeAsync(
            SupportMutation.RelicSpirit,
            initialItems,
            worldData: null);

        AssertMutationFree(result, initialItems);
        Assert.Contains("reason=world-data-missing", result.Log);
        Assert.Contains("no materials were consumed", result.Log);
    }

    [Fact]
    public async Task RelicSpiritMissingSelectedMaterialDoesNotConsumeTheSpirit()
    {
        var initialItems = Items(
            inventory: [InventoryItem(10, RelicSpiritItemId)],
            equipment: []);
        var result = await InvokeAsync(
            SupportMutation.RelicSpirit,
            initialItems,
            CreateRelicWorldData());

        AssertMutationFree(result, initialItems);
        Assert.Contains("materials missing", result.Log);
    }

    [Fact]
    public async Task RelicCrystalMissingEquipmentOrEnoughCrystalsDoesNotMutate()
    {
        var noRelicItems = Items(
            inventory: [InventoryItem(40, RelicCrystalItemId, stackSize: 2)],
            equipment: []);
        var noRelic = await InvokeAsync(
            SupportMutation.RelicCrystal,
            noRelicItems,
            worldData: null);
        AssertMutationFree(noRelic, noRelicItems);
        Assert.Contains("equipped relic missing", noRelic.Log);

        var insufficientItems = Items(
            inventory: [InventoryItem(40, RelicCrystalItemId)],
            equipment: [EquipmentItem(50, GeneratedRelicItemId, durability: 900, maximumDurability: 1_100)]);
        var insufficient = await InvokeAsync(
            SupportMutation.RelicCrystal,
            insufficientItems,
            worldData: null);
        AssertMutationFree(insufficient, insufficientItems);
        Assert.Contains("requiredCrystals=2", insufficient.Log);
        Assert.Contains("crystals missing", insufficient.Log);

        var invalidRelicItems = Items(
            inventory: [InventoryItem(40, RelicCrystalItemId)],
            equipment: [EquipmentItem(50, GeneratedRelicItemId, durability: 1_200, maximumDurability: 1_100)]);
        var invalidRelic = await InvokeAsync(
            SupportMutation.RelicCrystal,
            invalidRelicItems,
            worldData: null);
        AssertMutationFree(invalidRelic, invalidRelicItems);
        Assert.Contains("reason=relic-durability-invalid", invalidRelic.Log);
    }

    [Theory]
    [InlineData(SupportMutation.RelicSpirit)]
    [InlineData(SupportMutation.RelicCrystal)]
    public async Task ItemSnapshotWriteFailureLeavesLiveStateAndPacketsUnchanged(
        SupportMutation mutation)
    {
        var initialItems = ValidItems(mutation);
        var result = await InvokeAsync(
            mutation,
            initialItems,
            mutation == SupportMutation.RelicSpirit ? CreateRelicWorldData() : null,
            blockPlayerItemsRoot: true);

        AssertMutationFree(result, initialItems, expectPersistedItems: false);
        Assert.Equal("blocked", File.ReadAllText(result.PlayerItemsRoot));
        Assert.Contains("item persistence", result.Log);
        Assert.Contains("no session mutation was committed", result.Log);
    }

    [Fact]
    public async Task RelicSpiritCharacterPersistenceFailureRollsBackItemSidecarBeforeReturning()
    {
        var initialItems = ValidItems(SupportMutation.RelicSpirit);
        var result = await InvokeAsync(
            SupportMutation.RelicSpirit,
            initialItems,
            CreateRelicWorldData(),
            seedCharacterSnapshot: false);

        AssertMutationFree(result, initialItems, expectPersistedCharacter: false);
        Assert.Contains("character persistence", result.Log);
        Assert.Contains("reason=snapshotMissing", result.Log);
        Assert.Contains("itemRollback=none", result.Log);
    }

    [Fact]
    public async Task RelicSpiritExhaustedRewardUidDoesNotConsumeOrPersistMaterials()
    {
        var initialItems = Items(
            inventory:
            [
                InventoryItem(10, RelicSpiritItemId),
                InventoryItem(20, DragonBallItemId),
            ],
            equipment:
            [
                EquipmentItem(
                    uint.MaxValue,
                    4_100_005,
                    durability: 1_099,
                    maximumDurability: 1_099),
            ]);
        var result = await InvokeAsync(
            SupportMutation.RelicSpirit,
            initialItems,
            CreateRelicWorldData());

        AssertMutationFree(result, initialItems);
        Assert.Contains("reason=item-uid-exhausted", result.Log);
        Assert.Contains("no materials were consumed", result.Log);
    }

    [Fact]
    public async Task RelicCrystalRepairPreservesMaxUidBecauseTheSourcePathAllocatesNoItem()
    {
        var initialItems = Items(
            inventory: [InventoryItem(40, RelicCrystalItemId)],
            equipment:
            [
                EquipmentItem(
                    uint.MaxValue,
                    GeneratedRelicItemId,
                    durability: 1_000,
                    maximumDurability: 1_100),
            ]);
        var result = await InvokeAsync(
            SupportMutation.RelicCrystal,
            initialItems,
            worldData: null);

        Assert.Equal(1, result.CommitCount);
        var repaired = Assert.Single(result.UpdatedSession.PlayerItems.EquipmentItems);
        Assert.Equal(uint.MaxValue, repaired.Uid);
        Assert.Equal((ushort)1_100, repaired.Durability);
        AssertItemsEqual(result.UpdatedSession.PlayerItems, Assert.IsType<PlayerItemSnapshot>(result.PersistedItems));
    }

    [Fact]
    public async Task RelicCrystalDoesNotCombineBoundAndUnboundStacksToSatisfyOneRemoval()
    {
        var initialItems = Items(
            inventory:
            [
                InventoryItem(40, RelicCrystalItemId, bound: 0),
                InventoryItem(41, RelicCrystalItemId, bound: 1),
            ],
            equipment: [EquipmentItem(50, GeneratedRelicItemId, durability: 900, maximumDurability: 1_100)]);
        var result = await InvokeAsync(
            SupportMutation.RelicCrystal,
            initialItems,
            worldData: null);

        AssertMutationFree(result, initialItems);
        Assert.Contains("requiredCrystals=2", result.Log);
        Assert.Contains("crystals missing", result.Log);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private async Task<InvokeResult> InvokeAsync(
        SupportMutation mutation,
        PlayerItemSnapshot initialItems,
        WorldDataCatalog? worldData,
        bool includeSessionStore = true,
        bool includePlayerItemsRoot = true,
        bool includeUsersRoot = true,
        bool blockPlayerItemsRoot = false,
        bool seedCharacterSnapshot = true)
    {
        var caseRoot = Path.Combine(_root, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(caseRoot);
        var usersRoot = Path.Combine(caseRoot, "Users");
        var playerItemsRoot = Path.Combine(caseRoot, "PlayersItems");

        if (includeUsersRoot)
        {
            Directory.CreateDirectory(usersRoot);
            if (seedCharacterSnapshot)
            {
                WriteCharacterSnapshot(usersRoot);
            }
        }

        PlayerItemSnapshotLoader? itemLoader = null;
        if (includePlayerItemsRoot)
        {
            if (blockPlayerItemsRoot)
            {
                File.WriteAllText(playerItemsRoot, "blocked");
            }
            else
            {
                itemLoader = new PlayerItemSnapshotLoader(playerItemsRoot);
                itemLoader.SaveByCharacterUid(CharacterUid, initialItems);
            }
        }

        var characterLoader = includeUsersRoot && seedCharacterSnapshot
            ? new CharacterSnapshotLoader(usersRoot, includePlayerItemsRoot && !blockPlayerItemsRoot ? playerItemsRoot : null)
            : null;
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            initialItems,
            includeUsersRoot ? usersRoot : null,
            mutation == SupportMutation.RelicSpirit ? RelicSpiritItemId : RelicCrystalItemId);

        var commitCount = 0;
        PlayerItemSnapshot? itemsAtCommit = null;
        CharacterSnapshot? characterAtCommit = null;
        var sessions = new WorldSessionStore((_, previous, _) =>
        {
            if (previous is null)
            {
                return;
            }

            commitCount++;
            if (itemLoader is not null)
            {
                itemsAtCommit = itemLoader.LoadByCharacterUid(CharacterUid);
            }

            if (characterLoader is not null)
            {
                characterAtCommit = characterLoader.LoadByAccountId(AccountId);
            }
        });
        sessions.Set(connection, session);

        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);
        await registry.HandleAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                Session: session,
                WorldData: worldData,
                Sessions: includeSessionStore ? sessions : null,
                PlayerItemsRoot: includePlayerItemsRoot ? playerItemsRoot : null),
            ReadFrame(BuildLegacyNpcDialogReply(
                optionId: mutation == SupportMutation.RelicSpirit ? (byte)2 : (byte)3)));

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        var persistedItems = itemLoader?.LoadByCharacterUid(CharacterUid);
        var persistedCharacter = characterLoader?.LoadByAccountId(AccountId);
        return new InvokeResult(
            session,
            updatedSession,
            commitCount,
            itemsAtCommit,
            characterAtCommit,
            persistedItems,
            persistedCharacter,
            sent,
            log.ToString(),
            playerItemsRoot);
    }

    private static PlayerItemSnapshot ValidItems(SupportMutation mutation)
    {
        return mutation switch
        {
            SupportMutation.RelicSpirit => Items(
                inventory:
                [
                    InventoryItem(10, RelicSpiritItemId),
                    InventoryItem(20, DragonBallItemId),
                ],
                equipment: []),
            SupportMutation.RelicCrystal => Items(
                inventory:
                [
                    InventoryItem(40, RelicCrystalItemId),
                    InventoryItem(41, RelicCrystalItemId),
                ],
                equipment: [EquipmentItem(50, GeneratedRelicItemId, durability: 900, maximumDurability: 1_100)]),
            _ => throw new ArgumentOutOfRangeException(nameof(mutation), mutation, null),
        };
    }

    private static WorldSession CreateSession(
        PlayerItemSnapshot items,
        string? usersRoot,
        uint activeNpcId)
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["LegacyBoss10250RelicItemIndex"] = "0",
            ["LegacyBoss10250RelicDurabilityRoll"] = "10",
            ["LegacyBoss10250RelicAttributeCount"] = "3",
            ["LegacyBoss10250RelicAttributeIndexes"] = "0,0,0",
            ["LegacyBoss10250RelicAttributeValues"] = "10,20,30",
            ["RelicSpirit"] = bool.FalseString,
        };
        return new WorldSession(
            AccountId: AccountId,
            Username: "deityland-support-persistence-test",
            Character: new CharacterSnapshot(
                Uid: CharacterUid,
                Name: "DeitylandTester",
                Body: 1003,
                Face: 0,
                Hair: 0,
                Class: 7_051,
                FirstClass: 7_051,
                SecondClass: 0,
                Reborn: 0,
                Level: 140,
                Experience: 0,
                MapId: 10_250,
                X: 1_014,
                Y: 1_282,
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
                Attributes: attributes,
                PaidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)),
            PlayerItems: items,
            PlayerSpells: PlayerSpellSnapshot.Empty,
            PlayerProficiencies: PlayerProficiencySnapshot.Empty,
            UsersRoot: usersRoot,
            ActiveNpcId: activeNpcId);
    }

    private static PlayerItemSnapshot Items(
        IReadOnlyList<PlayerItemSnapshotItem> inventory,
        IReadOnlyList<PlayerItemSnapshotItem> equipment)
    {
        return new PlayerItemSnapshot(inventory, [], [], equipment, [], [])
        {
            WarehouseItems = [],
        };
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

    private static PlayerItemSnapshotItem EquipmentItem(
        uint uid,
        uint itemId,
        ushort durability,
        ushort maximumDurability)
    {
        return new PlayerItemSnapshotItem
        {
            Uid = uid,
            ItemId = itemId,
            Durability = durability,
            MaximumDurability = maximumDurability,
            Position = 13,
            StackSize = 1,
        };
    }

    private static byte[] BuildLegacyNpcDialogReply(byte optionId)
    {
        var writer = new PacketWriter(WorldPacketOpcodes.LegacyNpcReply, payloadLength: 11);
        writer.WriteUInt32(0, 0);
        writer.WriteUInt16(4, 0);
        writer.Payload[6] = optionId;
        writer.Payload[7] = 1;
        writer.Payload[8] = 0;
        writer.WriteUInt16(9, 0);
        return writer.ToArray();
    }

    private static ConquerTcpConnection CreateSendingConnection(List<byte[]> sent)
    {
        return new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958), (frame, _) =>
        {
            sent.Add(frame.ToArray());
            return ValueTask.CompletedTask;
        });
    }

    private static void AssertMutationFree(
        InvokeResult result,
        PlayerItemSnapshot initialItems,
        bool expectPersistedItems = true,
        bool expectPersistedCharacter = true)
    {
        Assert.Same(result.InitialSession, result.UpdatedSession);
        Assert.Equal(0, result.CommitCount);
        Assert.Empty(result.Sent);
        AssertItemsEqual(initialItems, result.UpdatedSession.PlayerItems);
        if (expectPersistedItems)
        {
            AssertItemsEqual(initialItems, Assert.IsType<PlayerItemSnapshot>(result.PersistedItems));
        }
        else
        {
            Assert.Null(result.PersistedItems);
        }

        if (expectPersistedCharacter)
        {
            Assert.Equal(
                bool.FalseString,
                Assert.IsType<CharacterSnapshot>(result.PersistedCharacter).Attributes["RelicSpirit"]);
        }
        else
        {
            Assert.Null(result.PersistedCharacter);
        }
    }

    private static void AssertRemoveInventory(byte[] bytes, uint uid)
    {
        Assert.True(MsgItemUsagePacket.TryParse(ReadFrame(bytes), out var packet));
        Assert.Equal(MsgItemUsageAction.RemoveInventory, packet.Action);
        Assert.Equal(uid, packet.Id);
    }

    private static LegacyGameItemPacket AssertGameItem(byte[] bytes)
    {
        Assert.True(LegacyGameItemPacket.TryParse(ReadFrame(bytes), out var packet));
        return packet;
    }

    private static void AssertItemsEqual(PlayerItemSnapshot expected, PlayerItemSnapshot actual)
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

    private static void WriteCharacterSnapshot(string usersRoot)
    {
        File.WriteAllText(
            Path.Combine(usersRoot, AccountId.ToString(CultureInfo.InvariantCulture) + ".ini"),
            $$"""
            [Character]
            UID={{CharacterUid}}
            Body=1003
            Face=0
            Name=DeitylandTester
            Class=7051
            Map=10250
            X=1014
            Y=1282
            FirstClass=7051
            SecoundeClass=0
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
            Money=0
            ConquerPoints=0
            BankCps=0
            BoundConquerPoints=0
            PkPoints=0
            Online=0
            LegacyBoss10250RelicItemIndex=0
            LegacyBoss10250RelicDurabilityRoll=10
            LegacyBoss10250RelicAttributeCount=3
            LegacyBoss10250RelicAttributeIndexes=0,0,0
            LegacyBoss10250RelicAttributeValues=10,20,30
            RelicSpirit=False

            [PaidNobility]
            Identifier=0
            LastNobilityDonation=0
            PaidRank=0
            PeriodTime=0
            IsActive=False
            Rank=Serf
            Position=-1
            """);
    }

    private static WorldDataCatalog CreateRelicWorldData()
    {
        var randomAttributes = new[]
        {
            RandomRelicAttribute(LegacyChiAttributeType.Immunity, minimum: 1, maximum: 100),
            RandomRelicAttribute(LegacyChiAttributeType.Breakthrough, minimum: 1, maximum: 100),
            RandomRelicAttribute(LegacyChiAttributeType.Counteraction, minimum: 1, maximum: 100),
        };
        return WorldDataCatalog.Create(
            new SeedManifest(
                DateTimeOffset.Parse("2026-07-10T00:00:00Z", CultureInfo.InvariantCulture),
                "fixture://deityland-support-persistence",
                new Dictionary<string, int>()),
            clientConfigs: [],
            items: [CreateItemType(GeneratedRelicItemId, "Relic")],
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
            xuanbaoRandomAttributes: randomAttributes,
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

    private static XuanbaoRandomAttribute RandomRelicAttribute(
        LegacyChiAttributeType type,
        int minimum,
        int maximum)
    {
        return new XuanbaoRandomAttribute(
            checked((int)GeneratedRelicItemId),
            (int)type,
            minimum,
            maximum,
            "fixture://xuanbao_rand_attr.txt",
            1,
            string.Empty,
            []);
    }

    private static ItemType CreateItemType(uint itemId, string name)
    {
        var fields = Enumerable.Repeat("0", 66).ToArray();
        fields[0] = itemId.ToString(CultureInfo.InvariantCulture);
        fields[1] = name;
        fields[ItemType.DurabilityFieldIndex] = "1099";
        return new ItemType(checked((int)itemId), name, fields);
    }

    private static GuildWarData EmptyGuildWarData(string name)
    {
        return new GuildWarData("fixture://" + name, [], [], [], info: null, pole: null, conductors: []);
    }

    public enum SupportMutation : byte
    {
        RelicSpirit,
        RelicCrystal,
    }

    private sealed record InvokeResult(
        WorldSession InitialSession,
        WorldSession UpdatedSession,
        int CommitCount,
        PlayerItemSnapshot? ItemsAtCommit,
        CharacterSnapshot? CharacterAtCommit,
        PlayerItemSnapshot? PersistedItems,
        CharacterSnapshot? PersistedCharacter,
        IReadOnlyList<byte[]> Sent,
        string Log,
        string PlayerItemsRoot);
}
