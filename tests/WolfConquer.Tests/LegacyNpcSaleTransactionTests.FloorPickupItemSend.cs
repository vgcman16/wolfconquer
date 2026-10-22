using System.Buffers.Binary;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNpcSaleTransactionTests
{
    [Theory]
    [InlineData(0, 1U)]
    [InlineData(0, 2U)]
    [InlineData(0, 7U)]
    [InlineData(1, 1U)]
    [InlineData(1, 2U)]
    [InlineData(1, 7U)]
    [InlineData(2, 1U)]
    [InlineData(2, 2U)]
    [InlineData(2, 7U)]
    public async Task ExistingActivationUsesNativeSavedMinutesGetterForSelectedAppendMergeOrWholeStackOnly(
        int selection, uint activation)
    {
        const uint uid = 1_300_191;
        var merge = selection == 1;
        var wholeStack = selection == 2;
        var deadline = DateTime.Now.AddMinutes(5).ToBinary();
        // Whole-stack rows retain an otherwise eligible zero-size destination
        // so selecting UpdateADD instead of a single-item merge is observable.
        SeedPickupSendInventory(uid, ItemId, merge || wholeStack, maximumDurability: 10);
        var loader = new PlayerItemSnapshotLoader(ItemsRoot);
        var seeded = loader.LoadByCharacterUid(uid);
        loader.SaveByCharacterUid(uid, seeded with
        {
            InventoryItems = seeded.InventoryItems.Select(item => item.Uid == SoldUid
                ? WithRawPickupSendActivation(item, activation, deadline) : item).ToArray(),
        });
        var owner = NewOwner(identity: new(42), stackLimit: selection == 0 ? (ushort)0 : (ushort)10);
        Player? player = null;
        WorldSession? before = null;
        byte[]? characterBytes = null;
        byte[]? expectedItems = null;
        var armed = false;
        var observed = 0;
        Exception? callbackFailure = null;
        player = await Login(owner, uid, bytes =>
        {
            if (!armed || observed != 0) return;
            observed++;
            callbackFailure = Record.Exception(() =>
            {
                Assert.NotNull(before);
                Assert.NotNull(expectedItems);
                Assert.NotNull(characterBytes);
                AssertPickupInventoryCommitted(owner, player!, before, expectedItems, characterBytes);
                Assert.True(LegacyGameItemPacket.TryParse(ReadFrame(bytes), out var item));
                Assert.Equal(merge ? SoldUid : AdmissionPickedItemUid, item.Uid);
                Assert.Equal(0U, item.TimeLeftInMinutes);
            });
        });
        before = Current(owner, player);
        var destination = Assert.Single(before.PlayerItems.InventoryItems, item => item.Uid == SoldUid);
        Assert.Equal(activation, destination.Activate);
        Assert.Equal(77U, destination.TimeLeftInMinutes);
        Assert.Equal(deadline, destination.Expiration);
        var incoming = WithRawPickupSendActivation(PickupSendRichItem(ItemId, wholeStack ? (ushort)3 : (ushort)1,
            maximumDurability: 10, deadline), activation, deadline);
        Assert.Equal(77U, incoming.TimeLeftInMinutes);
        var incomingBytes = PlayerItemSnapshotLoader.ToLegacyBytes(new PlayerItemSnapshot([incoming], [], [], [], []));
        var entry = SeedPickupInventoryFloor(owner, player, incoming);
        characterBytes = File.ReadAllBytes(UserPath(uid));
        expectedItems = ExpectedPickupInventoryBytes(before.PlayerItems, incoming, merge,
            stack: wholeStack ? (ushort)3 : (ushort)1, activate: activation);
        var first = player.Sent.Count;
        var lowerNow = DateTime.Now;
        armed = true;

        await PickupAdmissionFloor(owner, player, PersistencePickupFloorUid);

        var upperNow = DateTime.Now;
        Assert.Equal(1, observed);
        Assert.Null(callbackFailure);
        AssertPickupInventoryCommitted(owner, player, before, expectedItems, characterBytes);
        var selectedUid = merge ? SoldUid : incoming.Uid;
        var selected = Assert.Single(Current(owner, player).PlayerItems.InventoryItems, item => item.Uid == selectedUid);
        Assert.Equal(activation, selected.Activate);
        Assert.Equal(deadline, selected.Expiration);
        Assert.Equal(0U, selected.TimeLeftInMinutes);
        // The preimage/floor object keeps its raw value; only the selected
        // persisted output has the native getter projection.
        Assert.Same(incoming, entry.Item);
        Assert.Equal(incomingBytes, PlayerItemSnapshotLoader.ToLegacyBytes(new PlayerItemSnapshot([incoming], [], [], [], [])));
        Assert.Equal(77U, destination.TimeLeftInMinutes);
        if (!merge)
        {
            var untouched = Assert.Single(Current(owner, player).PlayerItems.InventoryItems, item => item.Uid == SoldUid);
            Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(new PlayerItemSnapshot([destination], [], [], [], [])),
                PlayerItemSnapshotLoader.ToLegacyBytes(new PlayerItemSnapshot([untouched], [], [], [], [])));
        }
        var frames = player.Sent.Skip(first).ToArray();
        var frame = Assert.Single(frames, bytes => LegacyGameItemPacket.TryParse(ReadFrame(bytes), out _));
        Assert.True(LegacyGameItemPacket.TryParse(ReadFrame(frame), out var packet));
        Assert.Equal(0U, packet.TimeLeftInMinutes);
        if (activation == 1)
            Assert.InRange(packet.RemainingTime, unchecked((uint)(DateTime.FromBinary(deadline) - upperNow).TotalSeconds),
                unchecked((uint)(DateTime.FromBinary(deadline) - lowerNow).TotalSeconds));
        else Assert.Equal(0U, packet.RemainingTime);
        AssertPickupInventoryFrames(frames, expectedItems, selectedUid,
            merge ? LegacyGameItemPacket.UpdateItemMode : LegacyGameItemPacket.AddItemMode,
            remainingSeconds: activation == 1 ? packet.RemainingTime : null);
        await AssertPersistencePickupReplayIsSilent(owner, player);
    }

    [Theory]
    [InlineData(50_000U, (ushort)1, (ushort)0, false, (byte)0, false)]
    [InlineData(50_000U, (ushort)1, (ushort)10, true, (byte)0, false)]
    [InlineData(50_000U, (ushort)3, (ushort)1, false, (byte)0, false)]
    [InlineData(410_003U, (ushort)1, (ushort)0, false, (byte)7, false)]
    [InlineData(350_001U, (ushort)1, (ushort)0, false, (byte)7, true)]
    [InlineData(370_001U, (ushort)3, (ushort)1, false, (byte)7, true)]
    [InlineData(730_001U, (ushort)1, (ushort)0, false, (byte)1, false)]
    [InlineData(730_008U, (ushort)3, (ushort)10, false, (byte)8, false)]
    [InlineData(730_009U, (ushort)1, (ushort)0, false, (byte)0, false)]
    [InlineData(730_000U, (ushort)1, (ushort)0, false, (byte)0, false)]
    public async Task NativeItemSendPrefixPersistsSelectedFullItemBeforeFirstD13FrameWithoutReplacingPhysicalHolder(
        uint catalogItemId, ushort groundStack, ushort stackLimit, bool merge, byte expectedPlus, bool accessory)
    {
        const uint uid = 1_300_201;
        var deadline = DateTime.Now.AddMinutes(5);
        SeedPickupSendInventory(uid, catalogItemId, merge, maximumDurability: 0);
        var owner = NewOwner(identity: new(42), stackLimit: stackLimit, catalogItemId: catalogItemId, catalogDurability: "47");
        Player? player = null;
        WorldSession? before = null;
        byte[]? characterBytes = null;
        byte[]? expectedItems = null;
        var armed = false;
        var observed = 0;
        Exception? callbackFailure = null;
        player = await Login(owner, uid, bytes =>
        {
            if (!armed || observed != 0) return;
            observed++;
            callbackFailure = Record.Exception(() =>
            {
                Assert.NotNull(before);
                Assert.NotNull(expectedItems);
                Assert.NotNull(characterBytes);
                AssertPickupInventoryCommitted(owner, player!, before, expectedItems, characterBytes);
                Assert.True(LegacyGameItemPacket.TryParse(ReadFrame(bytes), out var sent));
                Assert.Equal((ushort)47, sent.MaximumDurability);
                Assert.Equal(expectedPlus, sent.Plus);
                Assert.Equal(merge ? SoldUid : AdmissionPickedItemUid, sent.Uid);
                Assert.Equal(merge ? LegacyGameItemPacket.UpdateItemMode : LegacyGameItemPacket.AddItemMode, sent.Mode);
            });
        });
        before = Current(owner, player);
        var incoming = PickupSendRichItem(catalogItemId, groundStack, maximumDurability: 0, deadline.ToBinary());
        SeedPickupInventoryFloor(owner, player, incoming);
        characterBytes = File.ReadAllBytes(UserPath(uid));
        var selected = merge ? Assert.Single(before.PlayerItems.InventoryItems, item => item.Uid == SoldUid) : incoming;
        var expectedStack = merge ? (ushort)(selected.StackSize + 1) : groundStack;
        expectedItems = ExpectedPickupInventoryBytes(before.PlayerItems, incoming, merge, expectedStack,
            activate: accessory ? 1U : null, expectedPlus: expectedPlus, maximumDurability: 47);
        var first = player.Sent.Count;
        var lowerNow = DateTime.Now;
        armed = true;

        await PickupAdmissionFloor(owner, player, PersistencePickupFloorUid);

        var upperNow = DateTime.Now;
        Assert.Equal(1, observed);
        Assert.Null(callbackFailure);
        AssertPickupInventoryCommitted(owner, player, before, expectedItems, characterBytes);
        var selectedUid = merge ? SoldUid : incoming.Uid;
        var selectedAfter = Assert.Single(Current(owner, player).PlayerItems.InventoryItems, item => item.Uid == selectedUid);
        Assert.Equal(accessory ? 1U : selected.Activate, selectedAfter.Activate);
        Assert.Equal(selected.Expiration, selectedAfter.Expiration);
        var frames = player.Sent.Skip(first).ToArray();
        var itemFrame = Assert.Single(frames, bytes => LegacyGameItemPacket.TryParse(ReadFrame(bytes), out _));
        Assert.True(LegacyGameItemPacket.TryParse(ReadFrame(itemFrame), out var item));
        if (accessory)
        {
            // Send leaves EndDate unchanged. The active getter, not raw setter
            // UInt32.MaxValue, is used by DB and wire and therefore emits 0.
            Assert.Equal(0U, selectedAfter.TimeLeftInMinutes);
            Assert.Equal(0U, item.TimeLeftInMinutes);
            Assert.InRange(item.RemainingTime, unchecked((uint)(DateTime.FromBinary(selected.Expiration) - upperNow).TotalSeconds),
                unchecked((uint)(DateTime.FromBinary(selected.Expiration) - lowerNow).TotalSeconds));
        }
        else
        {
            Assert.Equal(selected.TimeLeftInMinutes, selectedAfter.TimeLeftInMinutes);
            Assert.Equal(0U, item.RemainingTime);
        }
        AssertPickupInventoryFrames(frames, expectedItems, selectedUid,
            merge ? LegacyGameItemPacket.UpdateItemMode : LegacyGameItemPacket.AddItemMode,
            remainingSeconds: accessory ? item.RemainingTime : null);
        await AssertPersistencePickupReplayIsSilent(owner, player);
    }

    [Theory]
    [InlineData((ushort)1, false)]
    [InlineData((ushort)3, false)]
    [InlineData((ushort)1, true)]
    public async Task AccessoryNativeSendActivatesAfterAddSelectionButOnlySingleAddResetsTheCatalogDeadline(ushort groundStack, bool merge)
    {
        const uint uid = 1_300_211;
        const uint accessoryId = 350_001;
        const uint minutes = 15;
        var oldDeadline = DateTime.Now.AddMinutes(5).ToBinary();
        SeedPickupSendInventory(uid, accessoryId, merge, maximumDurability: 10, destinationExpiration: oldDeadline);
        var owner = NewOwner(identity: new(42), stackLimit: 99, lifetimeMinutes: minutes, catalogItemId: accessoryId);
        var player = await Login(owner, uid);
        var before = Current(owner, player);
        var incoming = PickupSendRichItem(accessoryId, groundStack, maximumDurability: 10, oldDeadline);
        SeedPickupInventoryFloor(owner, player, incoming);
        var characterBytes = File.ReadAllBytes(UserPath(uid));
        var first = player.Sent.Count;
        var lower = DateTime.Now.AddMinutes(minutes);

        await PickupAdmissionFloor(owner, player, PersistencePickupFloorUid);

        var upper = DateTime.Now.AddMinutes(minutes);
        var selectedUid = merge ? SoldUid : incoming.Uid;
        var added = Assert.Single(Current(owner, player).PlayerItems.InventoryItems, item => item.Uid == selectedUid);
        Assert.Equal(1U, added.Activate);
        Assert.Equal(0U, added.TimeLeftInMinutes);
        if (groundStack == 1)
        {
            var deadline = DateTime.FromBinary(added.Expiration);
            Assert.Equal(DateTimeKind.Local, deadline.Kind);
            Assert.InRange(deadline, lower, upper);
            Assert.NotEqual(oldDeadline, added.Expiration);
        }
        else Assert.Equal(oldDeadline, added.Expiration);
        var expectedItems = ExpectedPickupInventoryBytes(before.PlayerItems, incoming, merge,
            stack: groundStack, activate: 1, expiration: added.Expiration,
            expectedPlus: 7, maximumDurability: 10);
        AssertPickupInventoryCommitted(owner, player, before, expectedItems, characterBytes);
        var frames = player.Sent.Skip(first).ToArray();
        var frame = Assert.Single(frames, bytes => LegacyGameItemPacket.TryParse(ReadFrame(bytes), out _));
        Assert.True(LegacyGameItemPacket.TryParse(ReadFrame(frame), out var item));
        Assert.InRange(item.RemainingTime, groundStack == 1 ? 899U : 298U, groundStack == 1 ? 900U : 300U);
        AssertPickupInventoryFrames(frames, expectedItems, selectedUid,
            merge ? LegacyGameItemPacket.UpdateItemMode : LegacyGameItemPacket.AddItemMode, item.RemainingTime);
        await AssertPersistencePickupReplayIsSilent(owner, player);
    }

    [Theory]
    [InlineData("not-ushort", true)]
    [InlineData("65536", true)]
    [InlineData("-1", true)]
    [InlineData("not-ushort", false)]
    public async Task NativeSendMissingMaximumRequiresValidCatalogUshortButExistingMaximumDoesNotReadMalformedField(
        string durability, bool missingMaximum)
    {
        const uint uid = 1_300_221;
        SeedPickupInventory(uid);
        var owner = NewOwner(identity: new(42), catalogDurability: durability);
        var player = await Login(owner, uid);
        var before = Current(owner, player);
        var incoming = PickupSendRichItem(ItemId, 1, missingMaximum ? (ushort)0 : (ushort)23, expiration: 0);
        var entry = SeedPickupInventoryFloor(owner, player, incoming);
        var characterBytes = File.ReadAllBytes(UserPath(uid));
        var itemBytes = File.ReadAllBytes(ItemPath(uid));
        var first = player.Sent.Count;

        await PickupAdmissionFloor(owner, player, PersistencePickupFloorUid);

        if (missingMaximum)
        {
            AssertPersistencePickupDenied(owner, player, before, entry, characterBytes, itemBytes, first);
            Assert.Contains("floorPickupItemSendCatalogDurabilityInvalid", owner.Log.ToString(), StringComparison.Ordinal);
        }
        else
        {
            var expectedItems = ExpectedPickupInventoryBytes(before.PlayerItems, incoming, false, 1, maximumDurability: 23);
            AssertPickupInventoryCommitted(owner, player, before, expectedItems, characterBytes);
            AssertPickupInventoryFrames(player.Sent.Skip(first).ToArray(), expectedItems, incoming.Uid, LegacyGameItemPacket.AddItemMode);
            await AssertPersistencePickupReplayIsSilent(owner, player);
        }
    }

    [WindowsNpcSaleTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NativeSendNormalizationDoesNotEscapeARefusedSaveAndRetryCommitsItOnce(bool itemTarget)
    {
        const uint uid = 1_300_231;
        const uint accessoryId = 370_001;
        SeedPickupSendInventory(uid, accessoryId, merge: false, maximumDurability: 0);
        var owner = NewOwner(identity: new(42), catalogItemId: accessoryId, catalogDurability: "47");
        var player = await Login(owner, uid);
        var before = Current(owner, player);
        var incoming = PickupSendRichItem(accessoryId, 3, 0, DateTime.Now.AddMinutes(5).ToBinary());
        var entry = SeedPickupInventoryFloor(owner, player, incoming);
        var characterBytes = File.ReadAllBytes(UserPath(uid));
        var itemBytes = File.ReadAllBytes(ItemPath(uid));
        var expectedItems = ExpectedPickupInventoryBytes(before.PlayerItems, incoming, false, 3,
            activate: 1, expectedPlus: 7, maximumDurability: 47);
        var first = player.Sent.Count;

        using (File.Open(itemTarget ? ItemPath(uid) : UserPath(uid), FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            await PickupAdmissionFloor(owner, player, PersistencePickupFloorUid);
            AssertPersistencePickupDenied(owner, player, before, entry, characterBytes, itemBytes, first);
            Assert.Equal(0U, entry.Item!.Activate);
            Assert.Equal((ushort)0, entry.Item.MaximumDurability);
            Assert.Equal(77U, entry.Item.TimeLeftInMinutes);
        }

        await PickupAdmissionFloor(owner, player, PersistencePickupFloorUid);

        AssertPickupInventoryCommitted(owner, player, before, expectedItems, characterBytes);
        var frames = player.Sent.Skip(first).ToArray();
        var frame = Assert.Single(frames, bytes => LegacyGameItemPacket.TryParse(ReadFrame(bytes), out _));
        Assert.True(LegacyGameItemPacket.TryParse(ReadFrame(frame), out var item));
        Assert.InRange(item.RemainingTime, 298U, 300U);
        AssertPickupInventoryFrames(frames, expectedItems, incoming.Uid, LegacyGameItemPacket.AddItemMode, item.RemainingTime);
        await AssertPersistencePickupReplayIsSilent(owner, player);
    }

    private void SeedPickupSendInventory(uint uid, uint itemId, bool merge, ushort maximumDurability,
        long? destinationExpiration = null)
    {
        SeedPickupInventory(uid, destinationStack: merge ? (ushort)0 : (ushort)10);
        var loader = new PlayerItemSnapshotLoader(ItemsRoot);
        var before = loader.LoadByCharacterUid(uid);
        var inventory = before.InventoryItems.Select(item => item.WithItemId(itemId)).ToArray();
        var selected = Assert.Single(inventory, item => item.Uid == SoldUid);
        var destination = selected.WithMaximumDurabilityAndPlus(maximumDurability, 7);
        if (destinationExpiration is { } expiration) destination = destination.WithActivation(0, expiration);
        loader.SaveByCharacterUid(uid, before with
        {
            InventoryItems = inventory.Select(item => item.Uid == SoldUid ? destination : item).ToArray(),
            WarehouseItems = before.WarehouseItems.Select(item => item.WithItemId(itemId)).ToArray(),
        });
    }

    private static PlayerItemSnapshotItem PickupSendRichItem(uint itemId, ushort stack, ushort maximumDurability, long expiration)
    {
        var item = new PlayerItemSnapshotItem
        {
            Uid = AdmissionPickedItemUid, ItemId = itemId, Durability = 7, MaximumDurability = maximumDurability,
            StackSize = stack, Bound = 1, Color = 7, Plus = 7, Bless = 3, Enchant = 21,
            SocketProgress = 1_234, SocketOne = 13, SocketTwo = 23, Effect = 2, Suspicious = 1,
            PlusProgress = 4_321, TimeLeftInMinutes = 77, ItemPoints = 900, Inscribed = 2,
            RuneExperience = 321, Attribute1 = 11, Attribute2 = 22, Attribute3 = 33, Attribute4 = 44, Attribute5 = 55,
            Expiration = expiration, MythSoul = 7, MythSoulPoints = 8, PerfectionLevel = 3, PerfectionProgress = 400,
            OwnerUid = 88, OwnerName = "FloorOwner", Signature = "PreserveEveryByte",
        };
        // Roundtrip preserves the same DB/perfection representation the actual
        // floor inventory owner will hand to the handler, including raw minutes.
        return Assert.Single(PlayerItemSnapshotLoader.Parse(PlayerItemSnapshotLoader.ToLegacyBytes(
            new PlayerItemSnapshot([item], [], [], [], []))).InventoryItems);
    }

    private static PlayerItemSnapshotItem WithRawPickupSendActivation(PlayerItemSnapshotItem item, uint activation, long expiration)
    {
        var bytes = PlayerItemSnapshotLoader.ToLegacyBytes(new PlayerItemSnapshot([item], [], [], [], []));
        // Raw DB preimage, deliberately not WithActivation's normalized setter.
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(sizeof(int) + 44, sizeof(uint)), activation);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(sizeof(int) + 48, sizeof(uint)), 77U);
        BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(sizeof(int) + 168, sizeof(long)), expiration);
        return Assert.Single(PlayerItemSnapshotLoader.Parse(bytes).InventoryItems);
    }
}
