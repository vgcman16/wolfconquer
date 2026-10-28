using System.Buffers;
using System.Globalization;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyLabyrinthSimonDiamondRewardParityTests : IDisposable
{
    private const long CharacterUid = 1_293_600;
    private const uint SimonNpcId = 1_152;
    private const uint SunDiamondItemId = 721_533;
    private const uint MoonDiamondItemId = 721_534;
    private const uint StarDiamondItemId = 721_535;
    private const uint CloudDiamondItemId = 721_536;
    private const uint AncestorBoxItemId = 721_540;
    private const uint MeteorItemId = 1_088_001;
    private const string SuccessText = "Well done. Here is the reward I promised you.";
    private const string SuccessOptionText = "Thanks.";
    private const string MissingOptionText = "Wait.~I~will~get~more.";

    private static readonly uint[] NormalGemItemIds =
    [
        700_011,
        700_021,
        700_121,
        700_041,
        700_061,
        700_001,
        700_031,
        700_101,
        700_071,
        700_051,
    ];

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "WolfConquer-LegacyLabyrinthSimonDiamondRewardParityTests-" + Guid.NewGuid().ToString("N"));

    public static TheoryData<byte> RewardOptions => new()
    {
        { 12 },
        { 13 },
        { 14 },
        { 15 },
    };

    public static TheoryData<byte, bool> MissingGateCases => new()
    {
        { 12, false },
        { 12, true },
        { 13, false },
        { 13, true },
        { 14, false },
        { 14, true },
        { 15, false },
        { 15, true },
    };

    [Theory]
    [MemberData(nameof(RewardOptions))]
    public async Task Options12Through15PersistExactRewardMappingsAndSourcePacketOrder(byte optionId)
    {
        var route = RewardFor(optionId);
        var originalItems = Items(
            InventoryItem(10, route.DiamondItemId, stackSize: 17, seed: 1),
            InventoryItem(50, 410_001, stackSize: 7, seed: 2));
        using var harness = CreateHarness(optionId, originalItems);

        Assert.True(await harness.InvokeAsync());

        var updated = harness.CurrentSession;
        Assert.NotSame(harness.OriginalSession, updated);
        Assert.Null(updated.ActiveNpcId);
        Assert.Equal(0, StackUnits(updated.PlayerItems, route.DiamondItemId));
        AssertInventoryItemEqual(
            Assert.Single(harness.OriginalSession.PlayerItems.InventoryItems, item => item.Uid == 50),
            Assert.Single(updated.PlayerItems.InventoryItems, item => item.Uid == 50));

        var rewards = route.FixedRewardItemId.HasValue
            ? updated.PlayerItems.InventoryItems
                .Where(item => item.ItemId == route.FixedRewardItemId.Value)
                .ToArray()
            : updated.PlayerItems.InventoryItems
                .Where(item => NormalGemItemIds.Contains(item.ItemId))
                .ToArray();
        Assert.Equal(route.RewardCount, rewards.Length);
        Assert.All(rewards, reward =>
        {
            Assert.Equal((byte)0, reward.Bound);
            Assert.Equal((ushort)0, reward.Position);
            Assert.Equal((ushort)1, reward.StackSize);
        });

        var rewardItemId = Assert.Single(rewards.Select(item => item.ItemId).Distinct());
        if (route.FixedRewardItemId.HasValue)
        {
            Assert.Equal(route.FixedRewardItemId.Value, rewardItemId);
        }
        else
        {
            Assert.Contains(rewardItemId, NormalGemItemIds);
        }

        Assert.Equal(1, harness.CommitCount);
        Assert.True(harness.PersistenceMatchedAtCommit);
        Assert.True(harness.SessionCommittedBeforeFirstSend);
        AssertItemsEqual(updated.PlayerItems, harness.ReloadItems());
        AssertSourceOrderedSuccessPackets(
            harness.ClientSent,
            consumptionFrameCount: 1,
            Enumerable.Repeat(rewardItemId, route.RewardCount).ToArray());
        AssertRemoveInventory(harness.ClientSent[0], uid: 10);
    }

    [Fact]
    public async Task RewardConsumesExactlySeventeenStackUnitsAndPreservesUnrelatedFields()
    {
        var originalItems = Items(
            InventoryItem(20, CloudDiamondItemId, stackSize: 9, seed: 3),
            InventoryItem(30, 410_002, stackSize: 6, seed: 4),
            InventoryItem(40, CloudDiamondItemId, stackSize: 10, seed: 5)) with
        {
            EquipmentItems = [InventoryItem(8_000, 130_009, seed: 6).WithPosition(4)],
            WarehouseItems = [InventoryItem(9_000, 410_301, stackSize: 3, seed: 7).WithWarehouseId(12)],
        };
        using var harness = CreateHarness(optionId: 15, originalItems);

        Assert.True(await harness.InvokeAsync());

        var sourceItems = harness.OriginalSession.PlayerItems;
        var updatedItems = harness.CurrentSession.PlayerItems;
        Assert.Equal(17, StackUnits(sourceItems, CloudDiamondItemId) - StackUnits(updatedItems, CloudDiamondItemId));
        Assert.DoesNotContain(updatedItems.InventoryItems, item => item.Uid == 20);
        var remainder = Assert.Single(updatedItems.InventoryItems, item => item.Uid == 40);
        var sourceRemainder = Assert.Single(sourceItems.InventoryItems, item => item.Uid == 40);
        AssertInventoryItemEqual(sourceRemainder.WithStackSize(2), remainder);
        AssertInventoryItemEqual(
            Assert.Single(sourceItems.InventoryItems, item => item.Uid == 30),
            Assert.Single(updatedItems.InventoryItems, item => item.Uid == 30));
        AssertSideCollectionsEqual(sourceItems, updatedItems);
        AssertItemsEqual(updatedItems, harness.ReloadItems());

        AssertRemoveInventory(harness.ClientSent[0], uid: 20);
        var partialUpdate = AssertGameItem(harness.ClientSent[1]);
        Assert.Equal(40U, partialUpdate.Uid);
        Assert.Equal(CloudDiamondItemId, partialUpdate.ItemId);
        Assert.Equal(LegacyGameItemPacket.UpdateItemMode, partialUpdate.Mode);
        Assert.Equal((ushort)2, partialUpdate.StackSize);
        AssertSourceOrderedSuccessPackets(
            harness.ClientSent,
            consumptionFrameCount: 2,
            [AncestorBoxItemId]);
    }

    [Fact]
    public async Task RewardConsumesOnlyEligibleUnboundUnitsAndPreservesBoundStock()
    {
        var originalItems = Items(
            InventoryItem(10, CloudDiamondItemId, stackSize: 12, bound: 1, seed: 8),
            InventoryItem(20, CloudDiamondItemId, stackSize: 17, bound: 0, seed: 9),
            InventoryItem(30, 410_003, stackSize: 2, seed: 10));
        await RunHarnessAsync(optionId: 15, originalItems, async harness =>
        {

            Assert.True(await harness.InvokeAsync());

            var updated = harness.CurrentSession.PlayerItems;
            Assert.Equal(17, StackUnits(harness.OriginalSession.PlayerItems, CloudDiamondItemId) -
                StackUnits(updated, CloudDiamondItemId));
            AssertInventoryItemEqual(
                Assert.Single(harness.OriginalSession.PlayerItems.InventoryItems, item => item.Uid == 10),
                Assert.Single(updated.InventoryItems, item => item.Uid == 10));
            Assert.DoesNotContain(updated.InventoryItems, item => item.Uid == 20);
            AssertItemsEqual(updated, harness.ReloadItems());
            AssertRemoveInventory(harness.ClientSent[0], uid: 20);
            AssertSourceOrderedSuccessPackets(
                harness.ClientSent,
                consumptionFrameCount: 1,
                [AncestorBoxItemId]);
        });
    }

    [Theory]
    [MemberData(nameof(MissingGateCases))]
    public async Task MissingOrBoundOnlyDiamondsUseExactPerDiamondDialogWithoutMutation(
        byte optionId,
        bool boundOnly)
    {
        var route = RewardFor(optionId);
        var originalItems = boundOnly
            ? Items(
                InventoryItem(10, route.DiamondItemId, stackSize: 17, bound: 1, seed: 11),
                InventoryItem(30, 410_004, stackSize: 2, seed: 12))
            : Items(InventoryItem(30, 410_004, stackSize: 2, seed: 12));
        using var harness = CreateHarness(optionId, originalItems);

        Assert.True(await harness.InvokeAsync());

        Assert.Same(harness.OriginalSession, harness.CurrentSession);
        Assert.Equal(SimonNpcId, harness.CurrentSession.ActiveNpcId);
        Assert.Equal(0, harness.CommitCount);
        Assert.Equal(harness.OriginalItemBytes, File.ReadAllBytes(harness.ItemPath));
        AssertItemsEqual(harness.OriginalSession.PlayerItems, harness.ReloadItems());
        AssertExactMissingDialog(harness.ClientSent, route.DiamondName);
        AssertNoItemMutationPackets(harness.ClientSent);
    }

    [Fact]
    public async Task ItemPersistenceFailureRollsBackAndSendsNoSuccessPackets()
    {
        var originalItems = Items(
            InventoryItem(10, SunDiamondItemId, stackSize: 17, seed: 13),
            InventoryItem(30, 410_005, stackSize: 2, seed: 14));
        using var harness = CreateHarness(optionId: 12, originalItems);
        using var locked = new FileStream(
            harness.ItemPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read);

        Assert.True(await harness.InvokeAsync());

        Assert.Same(harness.OriginalSession, harness.CurrentSession);
        Assert.Equal(SimonNpcId, harness.CurrentSession.ActiveNpcId);
        Assert.Equal(0, harness.CommitCount);
        Assert.Equal(harness.OriginalItemBytes, File.ReadAllBytes(harness.ItemPath));
        AssertItemsEqual(harness.OriginalSession.PlayerItems, harness.ReloadItems());
        AssertNoSuccessPackets(harness.ClientSent);
        Assert.Contains("item-persistence-", harness.Log.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("rollback=", harness.Log.ToString(), StringComparison.OrdinalIgnoreCase);
        AssertNoTransactionArtifacts(harness.PlayerItemsRoot);
    }

    [Fact]
    public async Task ObserverFailureRetainsPublishedFilesWithoutSuccessPackets()
    {
        var originalItems = Items(
            InventoryItem(10, MoonDiamondItemId, stackSize: 17, seed: 15),
            InventoryItem(30, 410_006, stackSize: 2, seed: 16));
        using var harness = CreateHarness(optionId: 13, originalItems);

        var race = await harness.InvokeWithObserverFailureAsync();

        Assert.True(race.Handled);
        Assert.False(harness.HasCurrentSession);
        Assert.Null(race.PublishedSession.ActiveNpcId);
        AssertItemsEqual(race.PublishedSession.PlayerItems, harness.ReloadItems());
        Assert.Empty(harness.ClientSent);
        Assert.False(harness.OriginalItemBytes.AsSpan().SequenceEqual(File.ReadAllBytes(harness.ItemPath)));
        Assert.Contains("publicationStage=Observer", harness.Log.ToString(), StringComparison.Ordinal);
        Assert.Contains("compensationSuppressed=true", harness.Log.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("rollback=complete", harness.Log.ToString(), StringComparison.OrdinalIgnoreCase);
        var retainedItems = File.ReadAllBytes(harness.ItemPath);
        Assert.True(await harness.InvokeAsync(harness.OriginalSession));
        Assert.Equal(retainedItems, File.ReadAllBytes(harness.ItemPath));
        Assert.Empty(harness.ClientSent);
    }

    [Fact]
    public async Task ReplayAfterSuccessCannotConsumeOrRewardAgainEvenWithStaleSession()
    {
        var originalItems = Items(
            InventoryItem(10, CloudDiamondItemId, stackSize: 34, seed: 17),
            InventoryItem(30, 410_007, stackSize: 2, seed: 18));
        using var harness = CreateHarness(optionId: 15, originalItems);

        Assert.True(await harness.InvokeAsync());
        var committedSession = harness.CurrentSession;
        var committedFrames = harness.ClientSent.Select(bytes => bytes.ToArray()).ToArray();
        var committedBytes = File.ReadAllBytes(harness.ItemPath);

        Assert.True(await harness.InvokeAsync());
        Assert.True(await harness.InvokeAsync(harness.OriginalSession));

        Assert.Same(committedSession, harness.CurrentSession);
        Assert.Null(harness.CurrentSession.ActiveNpcId);
        Assert.Equal(1, harness.CommitCount);
        Assert.Equal((ushort)17, Assert.Single(
            harness.CurrentSession.PlayerItems.InventoryItems,
            item => item.ItemId == CloudDiamondItemId).StackSize);
        Assert.Single(
            harness.CurrentSession.PlayerItems.InventoryItems,
            item => item.ItemId == AncestorBoxItemId);
        Assert.Equal(committedFrames.Length, harness.ClientSent.Count);
        for (var index = 0; index < committedFrames.Length; index++)
        {
            Assert.Equal(committedFrames[index], harness.ClientSent[index]);
        }

        Assert.Equal(committedBytes, File.ReadAllBytes(harness.ItemPath));
        AssertItemsEqual(harness.CurrentSession.PlayerItems, harness.ReloadItems());
        Assert.Equal(1, harness.ClientSent.Count(IsRewardAddPacket));
        AssertNoTransactionArtifacts(harness.PlayerItemsRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private SimonHarness CreateHarness(byte optionId, PlayerItemSnapshot originalItems)
    {
        var caseRoot = Path.Combine(_root, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(caseRoot);
        return new SimonHarness(caseRoot, optionId, originalItems);
    }

    private static void AssertSourceOrderedSuccessPackets(
        IReadOnlyList<byte[]> sent,
        int consumptionFrameCount,
        IReadOnlyList<uint> expectedRewardItemIds)
    {
        var dialogStart = consumptionFrameCount + expectedRewardItemIds.Count;
        Assert.Equal(dialogStart + 4, sent.Count);

        for (var index = 0; index < expectedRewardItemIds.Count; index++)
        {
            var reward = AssertGameItem(sent[consumptionFrameCount + index]);
            Assert.Equal(expectedRewardItemIds[index], reward.ItemId);
            Assert.Equal(LegacyGameItemPacket.AddItemMode, reward.Mode);
            Assert.Equal((byte)0, reward.Bound);
            Assert.Equal((ushort)0, reward.Position);
            Assert.Equal((ushort)1, reward.StackSize);
        }

        var dialog = AssertNpcReply(sent[dialogStart], LegacyNpcReplyInteractType.Dialog);
        Assert.Equal(SuccessText, dialog.Text);

        var option = AssertNpcReply(sent[dialogStart + 1], LegacyNpcReplyInteractType.Option);
        Assert.Equal(LegacyNpcReplyPacket.ClosedOptionId, option.OptionId);
        Assert.Equal(SuccessOptionText, option.Text);

        var avatar = AssertNpcReply(sent[dialogStart + 2], LegacyNpcReplyInteractType.Avatar);
        Assert.Equal((ushort)63, avatar.InputMaxLength);

        AssertNpcReply(sent[dialogStart + 3], LegacyNpcReplyInteractType.Finish);
    }

    private static void AssertExactMissingDialog(IReadOnlyList<byte[]> sent, string diamondName)
    {
        Assert.Equal(4, sent.Count);

        var dialog = AssertNpcReply(sent[0], LegacyNpcReplyInteractType.Dialog);
        Assert.Equal(
            $"You are kidding me. How dare you come here to claim a prize with so few {diamondName}s?",
            dialog.Text);

        var option = AssertNpcReply(sent[1], LegacyNpcReplyInteractType.Option);
        Assert.Equal(LegacyNpcReplyPacket.ClosedOptionId, option.OptionId);
        Assert.Equal(MissingOptionText, option.Text);

        var avatar = AssertNpcReply(sent[2], LegacyNpcReplyInteractType.Avatar);
        Assert.Equal((ushort)63, avatar.InputMaxLength);

        AssertNpcReply(sent[3], LegacyNpcReplyInteractType.Finish);
    }

    private static void AssertNoItemMutationPackets(IEnumerable<byte[]> sent)
    {
        Assert.DoesNotContain(sent, bytes =>
            MsgItemUsagePacket.TryParse(ReadFrame(bytes), out _) ||
            LegacyGameItemPacket.TryParse(ReadFrame(bytes), out _));
    }

    private static void AssertNoSuccessPackets(IEnumerable<byte[]> sent)
    {
        AssertNoItemMutationPackets(sent);
        Assert.DoesNotContain(sent, bytes =>
            LegacyNpcReplyPacket.TryParse(ReadFrame(bytes), out var packet) &&
            packet.InteractType == LegacyNpcReplyInteractType.Dialog &&
            packet.Text == SuccessText);
    }

    private static void AssertNoTransactionArtifacts(string playerItemsRoot)
    {
        Assert.Empty(Directory.EnumerateFiles(playerItemsRoot, "*.player-item-file-*"));
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

    private static LegacyNpcReplyPacket AssertNpcReply(
        byte[] bytes,
        LegacyNpcReplyInteractType interactType)
    {
        Assert.True(LegacyNpcReplyPacket.TryParse(ReadFrame(bytes), out var packet));
        Assert.Equal(interactType, packet.InteractType);
        return packet;
    }

    private static bool IsRewardAddPacket(byte[] bytes)
    {
        return LegacyGameItemPacket.TryParse(ReadFrame(bytes), out var packet) &&
            packet.Mode == LegacyGameItemPacket.AddItemMode &&
            (packet.ItemId == MeteorItemId ||
                packet.ItemId == AncestorBoxItemId ||
                NormalGemItemIds.Contains(packet.ItemId));
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

    private static int StackUnits(PlayerItemSnapshot items, uint itemId)
    {
        return items.InventoryItems
            .Where(item => item.ItemId == itemId)
            .Sum(item => item.StackSize);
    }

    private static void AssertInventoryItemEqual(
        PlayerItemSnapshotItem expected,
        PlayerItemSnapshotItem actual)
    {
        AssertItemsEqual(Items(expected), Items(actual));
    }

    private static void AssertSideCollectionsEqual(PlayerItemSnapshot expected, PlayerItemSnapshot actual)
    {
        AssertItemsEqual(
            expected with { InventoryItems = [] },
            actual with { InventoryItems = [] });
    }

    private static void AssertItemsEqual(PlayerItemSnapshot expected, PlayerItemSnapshot actual)
    {
        Assert.Equal(
            PlayerItemSnapshotLoader.ToLegacyBytes(expected),
            PlayerItemSnapshotLoader.ToLegacyBytes(actual));
    }

    private static PlayerItemSnapshot Items(params PlayerItemSnapshotItem[] inventoryItems)
    {
        return new PlayerItemSnapshot(inventoryItems, [], [], [], [], []);
    }

    private static PlayerItemSnapshotItem InventoryItem(
        uint uid,
        uint itemId,
        ushort stackSize = 1,
        byte bound = 0,
        byte seed = 0)
    {
        return new PlayerItemSnapshotItem
        {
            Uid = uid,
            ItemId = itemId,
            Durability = checked((ushort)(70 + seed)),
            MaximumDurability = checked((ushort)(90 + seed)),
            Position = 0,
            SocketProgress = checked((uint)(120 + seed)),
            SocketOne = 1,
            SocketTwo = 2,
            Effect = checked((uint)(10 + seed)),
            Plus = checked((byte)(seed % 12)),
            Bless = checked((byte)(seed % 7)),
            Bound = bound,
            Enchant = checked((byte)(seed % 20)),
            Suspicious = checked((byte)(seed % 2)),
            Locked = checked((byte)((seed + 1) % 2)),
            PlusProgress = checked((uint)(200 + seed)),
            Inscribed = checked((ushort)(seed % 2)),
            Activate = checked((uint)(seed % 2)),
            TimeLeftInMinutes = checked((uint)(300 + seed)),
            StackSize = stackSize,
            Color = checked((uint)(3 + seed % 6)),
            UnlockTimer = 400 + seed,
            ItemPoints = checked((uint)(500 + seed)),
            PurificationItemId = checked((uint)(600 + seed)),
            PurificationLevel = checked((uint)(1 + seed % 5)),
            PurificationDuration = checked((uint)(700 + seed)),
            PurificationAddedOn = 800 + seed,
            RefineryEffectId = checked((uint)(900 + seed)),
            RefineryEffectLevel = checked((uint)(1 + seed % 5)),
            RefineryEffectPercent = checked((uint)(10 + seed)),
            RefineryEffectPercent2 = checked((uint)(20 + seed)),
            RefineryEffectDuration = checked((uint)(1_000 + seed)),
            RefineryEffectAddedOn = 1_100 + seed,
            AnimaItemId = checked((uint)(1_200 + seed)),
            RuneExperience = checked((uint)(1_300 + seed)),
            Attribute1 = checked((uint)(1_400 + seed)),
            Attribute2 = checked((uint)(1_500 + seed)),
            Attribute3 = checked((uint)(1_600 + seed)),
            Attribute4 = checked((uint)(1_700 + seed)),
            Attribute5 = checked((uint)(1_800 + seed)),
            Expiration = 1_900 + seed,
            MythSoul = checked((uint)(2_000 + seed)),
            MythSoulPoints = checked((uint)(2_100 + seed)),
            PerfectionLevel = checked((uint)(seed % 10)),
            PerfectionProgress = checked((uint)(2_200 + seed)),
            OwnerUid = checked((uint)(3_000 + seed)),
            OwnerName = "Owner" + seed.ToString(CultureInfo.InvariantCulture),
            Signature = "Signature" + seed.ToString(CultureInfo.InvariantCulture),
        };
    }

    private static SimonRewardRoute RewardFor(byte optionId)
    {
        return optionId switch
        {
            12 => new(optionId, SunDiamondItemId, "SunDiamond", MeteorItemId, 2),
            13 => new(optionId, MoonDiamondItemId, "MoonDiamond", MeteorItemId, 4),
            14 => new(optionId, StarDiamondItemId, "StarDiamond", null, 1),
            15 => new(optionId, CloudDiamondItemId, "CloudDiamond", AncestorBoxItemId, 1),
            _ => throw new ArgumentOutOfRangeException(nameof(optionId), optionId, "Unknown Simon reward option."),
        };
    }

    private static WorldSession CreateSession(PlayerItemSnapshot playerItems)
    {
        return new WorldSession(
            AccountId: 100,
            Username: "labyrinth-simon-diamond-test",
            Character: new CharacterSnapshot(
                Uid: CharacterUid,
                Name: "SimonRewardHero",
                Body: 1003,
                Face: 154,
                Hair: 420,
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
                Strength: 123,
                Agility: 124,
                Vitality: 125,
                Spirit: 126,
                AttributePoints: 17,
                MinHitPoints: 5_432,
                MinMana: 654,
                Money: 1_234_567,
                ConquerPoints: 2_345,
                BoundConquerPoints: 3_456,
                BankConquerPoints: 4_567,
                PkPoints: 12,
                Online: 1,
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
            ActiveNpcId: SimonNpcId);
    }

    private static WorldDataCatalog CreateRewardWorldData()
    {
        var itemTypes = new List<ItemType>
        {
            CreateItemType(MeteorItemId, "Meteor"),
            CreateItemType(AncestorBoxItemId, "AncestorBox"),
        };
        itemTypes.AddRange(NormalGemItemIds.Select(itemId => CreateItemType(itemId, "NormalGem")));
        return CreateWorldDataCatalog(itemTypes.ToArray());
    }

    private static ItemType CreateItemType(uint itemTypeId, string name)
    {
        var fields = Enumerable.Repeat("0", 66).ToArray();
        fields[0] = itemTypeId.ToString(CultureInfo.InvariantCulture);
        fields[1] = name;
        fields[ItemType.DurabilityFieldIndex] = "321";
        fields[46] = "0";
        return new ItemType(checked((int)itemTypeId), name, fields);
    }

    private static WorldDataCatalog CreateWorldDataCatalog(params ItemType[] items)
    {
        return WorldDataCatalog.Create(
            new SeedManifest(
                DateTimeOffset.Parse("2026-07-14T00:00:00Z", CultureInfo.InvariantCulture),
                "fixture://labyrinth-simon-diamond-rewards",
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

    private sealed class SimonHarness : IDisposable
    {
        private readonly byte _optionId;
        private readonly ConquerTcpConnection _connection;
        private readonly StringWriter _log = new();
        private bool? _sessionCommittedBeforeFirstSend;
        private bool _replaceSessionDuringCommit;
        private WorldSession? _replacementSession;
        private WorldSession? _publishedAtFailure;

        public SimonHarness(string caseRoot, byte optionId, PlayerItemSnapshot originalItems)
        {
            _optionId = optionId;
            PlayerItemsRoot = Path.Combine(caseRoot, "PlayersItems");
            var loader = new PlayerItemSnapshotLoader(PlayerItemsRoot);
            loader.SaveByCharacterUid(CharacterUid, originalItems);
            ItemPath = Path.Combine(
                PlayerItemsRoot,
                CharacterUid.ToString(CultureInfo.InvariantCulture) + ".bin");
            OriginalSession = CreateSession(loader.LoadByCharacterUid(CharacterUid));
            OriginalItemBytes = File.ReadAllBytes(ItemPath);

            ConquerTcpConnection? connection = null;
            connection = new ConquerTcpConnection(
                null,
                new IPEndPoint(IPAddress.Loopback, 9_958),
                (frame, _) =>
                {
                    _sessionCommittedBeforeFirstSend ??=
                        Sessions!.TryGet(connection!, out var current) &&
                        !ReferenceEquals(current, OriginalSession);
                    ClientSent.Add(frame.ToArray());
                    return ValueTask.CompletedTask;
                });
            _connection = connection;

            Sessions = new WorldSessionStore((changedConnection, previous, current) =>
            {
                if (!ReferenceEquals(changedConnection, _connection) ||
                    previous is null ||
                    ReferenceEquals(previous, current))
                {
                    return;
                }

                if (_replaceSessionDuringCommit && current.ActiveNpcId is null)
                {
                    _replaceSessionDuringCommit = false;
                    _publishedAtFailure = current;
                    _replacementSession = OriginalSession with
                    {
                        Username = "labyrinth-simon-cas-replacement",
                    };
                    Sessions!.Set(_connection, _replacementSession);
                    throw new InvalidOperationException("Injected observer failure after actual publication.");
                }

                CommitCount++;
                try
                {
                    var persisted = ReloadItems();
                    PersistenceMatchedAtCommit = PlayerItemSnapshotLoader
                        .ToLegacyBytes(current.PlayerItems)
                        .SequenceEqual(PlayerItemSnapshotLoader.ToLegacyBytes(persisted));
                }
                catch (Exception)
                {
                    PersistenceMatchedAtCommit = false;
                }
            });
            Sessions.Set(_connection, OriginalSession);

            Context = new WorldPacketHandlerContext(
                _connection,
                _log,
                Session: OriginalSession,
                WorldData: CreateRewardWorldData(),
                Sessions: Sessions,
                PlayerItemsRoot: PlayerItemsRoot,
                ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13);
        }

        public string PlayerItemsRoot { get; }

        public string ItemPath { get; }

        public byte[] OriginalItemBytes { get; }

        public WorldSession OriginalSession { get; }

        public WorldSessionStore Sessions { get; }

        public WorldPacketHandlerContext Context { get; }

        public List<byte[]> ClientSent { get; } = [];

        public int CommitCount { get; private set; }

        public bool PersistenceMatchedAtCommit { get; private set; }

        public bool SessionCommittedBeforeFirstSend => _sessionCommittedBeforeFirstSend == true;

        public StringWriter Log => _log;

        public bool HasCurrentSession => Sessions.TryGet(_connection, out _);

        public WorldSession CurrentSession
        {
            get
            {
                Assert.True(Sessions.TryGet(_connection, out var session));
                return Assert.IsType<WorldSession>(session);
            }
        }

        public void SetBeforeAction(WorldSession replacement) => Sessions.Set(_connection, replacement);

        public ValueTask<bool> InvokeAsync(WorldSession? suppliedSession = null)
        {
            var session = suppliedSession ?? CurrentSession;
            return WorldPacketHandlers.TryHandleLegacyLabyrinthNpcAsync(
                Context with { Session = session },
                session,
                SimonNpcId,
                _optionId,
                CancellationToken.None);
        }

        public async Task<PublicationFailureResult> InvokeWithObserverFailureAsync()
        {
            _replaceSessionDuringCommit = true;
            var handled = await InvokeAsync().ConfigureAwait(false);
            return new PublicationFailureResult(handled, Assert.IsType<WorldSession>(_publishedAtFailure), Assert.IsType<WorldSession>(_replacementSession));
        }

        public PlayerItemSnapshot ReloadItems()
        {
            return new PlayerItemSnapshotLoader(PlayerItemsRoot).LoadByCharacterUid(CharacterUid);
        }

        public void Dispose()
        {
            _log.Dispose();
        }
    }

    private readonly record struct SimonRewardRoute(
        byte OptionId,
        uint DiamondItemId,
        string DiamondName,
        uint? FixedRewardItemId,
        int RewardCount);

    private sealed record PublicationFailureResult(bool Handled, WorldSession PublishedSession, WorldSession ReplacementSession);
}
