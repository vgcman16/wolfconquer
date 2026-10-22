using System.Buffers.Binary;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNpcSaleTransactionTests
{
    [Fact]
    public async Task SingleItemPickupMergesIntoExistingUidAndPersistsDestinationMetadataBeforeUnlockedFirstSend()
    {
        const uint uid = 1_300_101;
        SeedPickupInventory(uid);
        var owner = NewOwner(identity: new(42), stackLimit: 10);
        var barrier = ActualOperationBarrier(owner);
        Player? player = null;
        WorldSession? before = null;
        byte[]? expectedItems = null;
        byte[]? characterBytes = null;
        var armed = false;
        var observed = 0;
        var leaseAvailable = false;
        Exception? callbackFailure = null;
        Task probeCleanup = Task.CompletedTask;
        player = await Login(owner, uid, _ =>
        {
            if (!armed || observed != 0) return;
            observed++;
            callbackFailure = Record.Exception(() =>
            {
                Assert.NotNull(before);
                Assert.NotNull(expectedItems);
                Assert.NotNull(characterBytes);
                AssertPickupInventoryCommitted(owner, player!, before, expectedItems, characterBytes);
            });
            using var cancellation = new CancellationTokenSource();
            var acquisition = barrier.EnterExclusiveAsync(cancellation.Token);
            leaseAvailable = acquisition.IsCompletedSuccessfully;
            if (leaseAvailable) acquisition.Result.Dispose();
            else
            {
                cancellation.Cancel();
                probeCleanup = DisposeCancelledProbe(acquisition);
            }
        });
        before = Current(owner, player);
        var incoming = AdmissionFloorItem().WithStackSize(1);
        SeedPickupInventoryFloor(owner, player, incoming);
        expectedItems = ExpectedPickupInventoryBytes(before.PlayerItems, incoming, merge: true, stack: 3);
        characterBytes = File.ReadAllBytes(UserPath(uid));
        var first = player.Sent.Count;
        armed = true;

        await PickupAdmissionFloor(owner, player, PersistencePickupFloorUid);
        await probeCleanup.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(1, observed);
        Assert.Null(callbackFailure);
        Assert.True(leaseAvailable, "Merged pickup must release the world lease before its first socket send.");
        AssertPickupInventoryCommitted(owner, player, before, expectedItems, characterBytes);
        AssertPickupInventoryFrames(player.Sent.Skip(first).ToArray(), expectedItems, SoldUid, LegacyGameItemPacket.UpdateItemMode);
        Assert.DoesNotContain(Current(owner, player).PlayerItems.InventoryItems, item => item.Uid == incoming.Uid);
        await AssertPersistencePickupReplayIsSilent(owner, player);
    }

    [Theory]
    [InlineData((ushort)10, 0U)]
    [InlineData((ushort)1, 15U)]
    public async Task WholeGroundStackBypassesSingleItemMergeAndTimerActivation(ushort stackLimit, uint lifetimeMinutes)
    {
        const uint uid = 1_300_111;
        SeedPickupInventory(uid);
        var owner = NewOwner(identity: new(42), stackLimit: stackLimit, lifetimeMinutes: lifetimeMinutes);
        var player = await Login(owner, uid);
        var before = Current(owner, player);
        var incoming = AdmissionFloorItem().WithStackSize(3);
        SeedPickupInventoryFloor(owner, player, incoming);
        var expectedItems = ExpectedPickupInventoryBytes(before.PlayerItems, incoming, merge: false, stack: 3);
        var characterBytes = File.ReadAllBytes(UserPath(uid));
        var first = player.Sent.Count;

        await PickupAdmissionFloor(owner, player, PersistencePickupFloorUid);

        AssertPickupInventoryCommitted(owner, player, before, expectedItems, characterBytes);
        var current = Current(owner, player);
        Assert.Equal((ushort)2, Assert.Single(current.PlayerItems.InventoryItems, item => item.Uid == SoldUid).StackSize);
        var appended = Assert.Single(current.PlayerItems.InventoryItems, item => item.Uid == incoming.Uid);
        Assert.Equal((ushort)3, appended.StackSize);
        Assert.Equal(incoming.Activate, appended.Activate);
        Assert.Equal(incoming.Expiration, appended.Expiration);
        AssertPickupInventoryFrames(player.Sent.Skip(first).ToArray(), expectedItems, incoming.Uid, LegacyGameItemPacket.AddItemMode);
        await AssertPersistencePickupReplayIsSilent(owner, player);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ZeroSizeGroundItemAddsOneOrMergesOneRatherThanPersistingAnEmptyStack(bool merge)
    {
        const uint uid = 1_300_121;
        SeedPickupInventory(uid, destinationStack: merge ? (ushort)2 : (ushort)10);
        var owner = NewOwner(identity: new(42), stackLimit: 10);
        var player = await Login(owner, uid);
        var before = Current(owner, player);
        var incoming = AdmissionFloorItem().WithStackSize(0);
        SeedPickupInventoryFloor(owner, player, incoming);
        var expectedItems = ExpectedPickupInventoryBytes(before.PlayerItems, incoming, merge, stack: merge ? (ushort)3 : (ushort)1);
        var characterBytes = File.ReadAllBytes(UserPath(uid));
        var first = player.Sent.Count;

        await PickupAdmissionFloor(owner, player, PersistencePickupFloorUid);

        AssertPickupInventoryCommitted(owner, player, before, expectedItems, characterBytes);
        AssertPickupInventoryFrames(player.Sent.Skip(first).ToArray(), expectedItems, merge ? SoldUid : incoming.Uid,
            merge ? LegacyGameItemPacket.UpdateItemMode : LegacyGameItemPacket.AddItemMode);
        Assert.Equal(merge ? 2 : 3, Current(owner, player).PlayerItems.InventoryItems.Count);
        Assert.DoesNotContain(Current(owner, player).PlayerItems.InventoryItems, item => item.StackSize == 0);
        await AssertPersistencePickupReplayIsSilent(owner, player);
    }

    [Fact]
    public async Task FullInventoryRefusesEvenASingleItemThatCouldOtherwiseMerge()
    {
        const uint uid = 1_300_131;
        SeedPickupInventory(uid);
        var owner = NewOwner(identity: new(42), stackLimit: 10);
        var player = await Login(owner, uid);
        var initial = Current(owner, player);
        var fullItems = initial.PlayerItems with
        {
            InventoryItems = initial.PlayerItems.InventoryItems.Concat(Enumerable.Range(0, 38).Select(index =>
                new PlayerItemSnapshotItem { Uid = KeptUid + 100U + (uint)index, ItemId = ItemId, StackSize = 10 })).ToArray(),
        };
        owner.Sessions.Set(player.Connection, initial with { PlayerItems = fullItems });
        var before = Current(owner, player);
        Assert.Equal(40, before.PlayerItems.InventoryItems.Count);
        Assert.Equal((ushort)2, Assert.Single(before.PlayerItems.InventoryItems, item => item.Uid == SoldUid).StackSize);
        var entry = SeedPickupInventoryFloor(owner, player, AdmissionFloorItem().WithStackSize(1));
        var characterBytes = File.ReadAllBytes(UserPath(uid));
        var itemBytes = File.ReadAllBytes(ItemPath(uid));
        var first = player.Sent.Count;

        await PickupAdmissionFloor(owner, player, PersistencePickupFloorUid);

        AssertPersistencePickupDenied(owner, player, before, entry, characterBytes, itemBytes, first);
    }

    [Fact]
    public async Task SingleItemMergeIgnoresIncomingBoundAndPlusDifferencesThenNativeSendClearsOrdinaryPlusOnly()
    {
        const uint uid = 1_300_141;
        SeedPickupInventory(uid, bound: 1);
        var owner = NewOwner(identity: new(42), stackLimit: 10);
        var player = await Login(owner, uid);
        var before = Current(owner, player);
        var destination = Assert.Single(before.PlayerItems.InventoryItems, item => item.Uid == SoldUid);
        var incoming = AdmissionFloorItem().WithStackSize(1).WithBound(0);
        Assert.NotEqual(destination.Bound, incoming.Bound);
        Assert.NotEqual(destination.Plus, incoming.Plus);
        SeedPickupInventoryFloor(owner, player, incoming);
        var expectedItems = ExpectedPickupInventoryBytes(before.PlayerItems, incoming, merge: true, stack: 3);
        var characterBytes = File.ReadAllBytes(UserPath(uid));
        var first = player.Sent.Count;

        await PickupAdmissionFloor(owner, player, PersistencePickupFloorUid);

        AssertPickupInventoryCommitted(owner, player, before, expectedItems, characterBytes);
        var after = Assert.Single(Current(owner, player).PlayerItems.InventoryItems, item => item.Uid == SoldUid);
        Assert.Equal(destination.Bound, after.Bound);
        Assert.Equal((byte)0, after.Plus); // MsgGameItem.Send clears Plus for non-equipment.
        Assert.DoesNotContain(Current(owner, player).PlayerItems.InventoryItems, item => item.Uid == incoming.Uid);
        AssertPickupInventoryFrames(player.Sent.Skip(first).ToArray(), expectedItems, SoldUid, LegacyGameItemPacket.UpdateItemMode);
        await AssertPersistencePickupReplayIsSilent(owner, player);
    }

    [Fact]
    public async Task TimedSingleLimitCatalogActivatesAppendedItemWithLocalBinaryDeadlineAndPersistsOnlyAllowedChanges()
    {
        const uint uid = 1_300_151;
        const uint minutes = 15;
        SeedPickupInventory(uid);
        var owner = NewOwner(identity: new(42), stackLimit: 1, lifetimeMinutes: minutes);
        var player = await Login(owner, uid);
        var before = Current(owner, player);
        var incomingImage = PlayerItemSnapshotLoader.ToLegacyBytes(new PlayerItemSnapshot(
            [AdmissionFloorItem().WithStackSize(1)], [], [], [], []));
        BinaryPrimitives.WriteUInt32LittleEndian(incomingImage.AsSpan(sizeof(int) + 48, sizeof(uint)), 77U);
        var incoming = Assert.Single(PlayerItemSnapshotLoader.Parse(incomingImage).InventoryItems);
        Assert.Equal(77U, incoming.TimeLeftInMinutes);
        SeedPickupInventoryFloor(owner, player, incoming);
        var characterBytes = File.ReadAllBytes(UserPath(uid));
        var first = player.Sent.Count;
        var lower = DateTime.Now.AddMinutes(minutes);

        await PickupAdmissionFloor(owner, player, PersistencePickupFloorUid);

        var upper = DateTime.Now.AddMinutes(minutes);
        var added = Assert.Single(Current(owner, player).PlayerItems.InventoryItems, item => item.Uid == incoming.Uid);
        Assert.Equal(1U, added.Activate);
        Assert.Equal(0U, added.TimeLeftInMinutes);
        var deadline = DateTime.FromBinary(added.Expiration);
        Assert.Equal(DateTimeKind.Local, deadline.Kind);
        Assert.InRange(deadline, lower, upper);
        Assert.Equal(deadline.ToBinary(), added.Expiration);
        var expectedItems = ExpectedPickupInventoryBytes(before.PlayerItems, incoming, merge: false, stack: 1,
            activate: 1, expiration: added.Expiration);
        AssertPickupInventoryCommitted(owner, player, before, expectedItems, characterBytes);
        AssertPickupInventoryFrames(player.Sent.Skip(first).ToArray(), expectedItems, incoming.Uid,
            LegacyGameItemPacket.AddItemMode, remainingSeconds: 900);
        await AssertPersistencePickupReplayIsSilent(owner, player);
    }

    [Fact]
    public async Task NonzeroCatalogTimeDoesNotActivateMergedItemUnlessCatalogStackLimitIsOne()
    {
        const uint uid = 1_300_161;
        SeedPickupInventory(uid);
        var owner = NewOwner(identity: new(42), stackLimit: 10, lifetimeMinutes: 15);
        var player = await Login(owner, uid);
        var before = Current(owner, player);
        var incoming = AdmissionFloorItem().WithStackSize(1);
        SeedPickupInventoryFloor(owner, player, incoming);
        var expectedItems = ExpectedPickupInventoryBytes(before.PlayerItems, incoming, merge: true, stack: 3);
        var characterBytes = File.ReadAllBytes(UserPath(uid));
        var first = player.Sent.Count;

        await PickupAdmissionFloor(owner, player, PersistencePickupFloorUid);

        AssertPickupInventoryCommitted(owner, player, before, expectedItems, characterBytes);
        var destination = Assert.Single(Current(owner, player).PlayerItems.InventoryItems, item => item.Uid == SoldUid);
        Assert.Equal(0U, destination.Activate);
        Assert.Equal(0L, destination.Expiration);
        AssertPickupInventoryFrames(player.Sent.Skip(first).ToArray(), expectedItems, SoldUid, LegacyGameItemPacket.UpdateItemMode);
        await AssertPersistencePickupReplayIsSilent(owner, player);
    }

    [WindowsNpcSaleTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MergeSharingRefusalRetainsExactDestinationAndFloorThenUnlockRetryAddsOnlyOne(bool itemTarget)
    {
        const uint uid = 1_300_171;
        SeedPickupInventory(uid);
        var owner = NewOwner(identity: new(42), stackLimit: 10);
        var player = await Login(owner, uid);
        var before = Current(owner, player);
        var incoming = AdmissionFloorItem().WithStackSize(1);
        var entry = SeedPickupInventoryFloor(owner, player, incoming);
        var expectedItems = ExpectedPickupInventoryBytes(before.PlayerItems, incoming, merge: true, stack: 3);
        var characterBytes = File.ReadAllBytes(UserPath(uid));
        var itemBytes = File.ReadAllBytes(ItemPath(uid));
        var first = player.Sent.Count;

        using (File.Open(itemTarget ? ItemPath(uid) : UserPath(uid), FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            await PickupAdmissionFloor(owner, player, PersistencePickupFloorUid);
            AssertPersistencePickupDenied(owner, player, before, entry, characterBytes, itemBytes, first);
        }

        await PickupAdmissionFloor(owner, player, PersistencePickupFloorUid);

        AssertPickupInventoryCommitted(owner, player, before, expectedItems, characterBytes);
        AssertPickupInventoryFrames(player.Sent.Skip(first).ToArray(), expectedItems, SoldUid, LegacyGameItemPacket.UpdateItemMode);
        await AssertPersistencePickupReplayIsSilent(owner, player);
    }

    private void SeedPickupInventory(uint uid, ushort destinationStack = 2, byte bound = 0)
    {
        Seed(uid);
        var loader = new PlayerItemSnapshotLoader(ItemsRoot);
        var seeded = loader.LoadByCharacterUid(uid);
        var destination = new PlayerItemSnapshotItem
        {
            Uid = SoldUid, ItemId = ItemId, Durability = 8, MaximumDurability = 10,
            StackSize = destinationStack, Bound = bound, Color = 7, Plus = 9, Bless = 3, Enchant = 21,
            SocketProgress = 1_234, SocketOne = 13, SocketTwo = 23, Effect = 2, Suspicious = 1,
            PlusProgress = 4_321, TimeLeftInMinutes = 31, ItemPoints = 900,
            RuneExperience = 321, Attribute1 = 11, Attribute2 = 22, Attribute3 = 33, Attribute4 = 44, Attribute5 = 55,
            MythSoul = 7, MythSoulPoints = 8, PerfectionLevel = 3, PerfectionProgress = 400,
            OwnerUid = uid, OwnerName = "MergeOwner", Signature = "KeepMe",
        };
        loader.SaveByCharacterUid(uid, seeded with
        {
            InventoryItems = [destination, .. seeded.InventoryItems.Where(item => item.Uid != SoldUid).Select(item => item.WithStackSize(10))],
        });
    }

    private FloorItemRuntimeEntry SeedPickupInventoryFloor(Owner owner, Player player, PlayerItemSnapshotItem incoming)
    {
        var character = Current(owner, player).Character;
        var packet = LegacyFloorItemPacket.FromDroppedItem(incoming, character, PersistencePickupFloorUid);
        var entry = FloorItemRuntimeEntry.ForItem(character.MapId, packet, incoming);
        DropFloor(owner).AddVisible(entry);
        return entry;
    }

    private static byte[] ExpectedPickupInventoryBytes(PlayerItemSnapshot before, PlayerItemSnapshotItem incoming,
        bool merge, ushort stack, uint? activate = null, long? expiration = null,
        byte expectedPlus = 0, ushort? maximumDurability = null)
    {
        var expected = merge ? before : before with { InventoryItems = before.InventoryItems.Append(incoming).ToArray() };
        Assert.All(expected.InventoryItems.Concat(expected.WarehouseItems), item => Assert.Empty(item.DepositedItems));
        var bytes = PlayerItemSnapshotLoader.ToLegacyBytes(expected);
        var count = BinaryPrimitives.ReadInt32LittleEndian(bytes);
        var uid = merge ? SoldUid : incoming.Uid;
        var offsets = Enumerable.Range(0, count).Select(index => sizeof(int) + index * PlayerItemSnapshotLoader.LegacyDbItemSize)
            .Where(offset => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, sizeof(uint))) == uid).ToArray();
        var itemOffset = Assert.Single(offsets);
        // Independent DB-record oracle: permit only native Add's stack/timer
        // fields and Send's Plus/max repair, retaining all other full-image bytes.
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(itemOffset + 52, sizeof(ushort)), stack);
        bytes[itemOffset + 28] = expectedPlus;
        if (maximumDurability is { } maximum)
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(itemOffset + 10, sizeof(ushort)), maximum);
        if (activate is { } active)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(itemOffset + 44, sizeof(uint)), active);
            // Native GetDBItem stores the active TimeLeftInMinutes getter (0).
            if (active != 0) BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(itemOffset + 48, sizeof(uint)), 0U);
        }
        if (expiration is { } end) BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(itemOffset + 168, sizeof(long)), end);
        return bytes;
    }

    private void AssertPickupInventoryCommitted(Owner owner, Player player, WorldSession before, byte[] expectedItems, byte[] characterBytes)
    {
        var after = Current(owner, player);
        Assert.Equal(expectedItems, File.ReadAllBytes(ItemPath(player.Uid)));
        Assert.Equal(expectedItems, PlayerItemSnapshotLoader.ToLegacyBytes(after.PlayerItems));
        Assert.Equal(characterBytes, File.ReadAllBytes(UserPath(player.Uid)));
        Assert.Equal(before.Character, after.Character);
        Assert.Equal(before with { PlayerItems = after.PlayerItems }, after);
        Assert.Same(before.NativeGameClient, after.NativeGameClient);
        Assert.Same(before.NativePlayer, after.NativePlayer);
        Assert.Empty(DropFloor(owner).SnapshotVisible(DateTimeOffset.UtcNow));
        Assert.False(LegacyTradeJournal.HasPending(UsersRoot));
    }

    private static void AssertPickupInventoryFrames(IReadOnlyList<byte[]> frames, byte[] expectedItems, uint uid, ushort mode,
        uint? remainingSeconds = null)
    {
        var itemFrame = Assert.Single(frames, bytes => LegacyGameItemPacket.TryParse(ReadFrame(bytes), out _));
        Assert.True(LegacyGameItemPacket.TryParse(ReadFrame(itemFrame), out var item));
        var expected = Assert.Single(PlayerItemSnapshotLoader.Parse(expectedItems).InventoryItems, entry => entry.Uid == uid);
        Assert.Equal(uid, item.Uid);
        Assert.Equal(mode, item.Mode);
        Assert.Equal(expected.StackSize, item.StackSize);
        var expectedPacket = LegacyGameItemPacket.FromSnapshotItem(expected, mode: mode);
        if (remainingSeconds is { } seconds)
        {
            Assert.InRange(item.RemainingTime, seconds - 1U, seconds);
            Assert.Equal(0U, item.TimeLeftInMinutes);
            expectedPacket = expectedPacket with { RemainingTime = item.RemainingTime };
        }
        Assert.Equal(expectedPacket, item);
        var removal = Assert.Single(frames, bytes => IsAdmissionFloorRemoval(bytes, PersistencePickupFloorUid));
        Assert.True(Array.IndexOf(frames.ToArray(), itemFrame) < Array.IndexOf(frames.ToArray(), removal));
        var text = Assert.Single(frames, bytes => LegacyMsgMessagePacket.TryParse(ReadFrame(bytes), out var message) &&
            message.Content.Count > 3 && message.Content[3] == "You have picked up a OrdinaryNpcSaleFixture.");
        Assert.True(Array.IndexOf(frames.ToArray(), removal) < Array.IndexOf(frames.ToArray(), text));
        Assert.DoesNotContain(frames, bytes => MsgItemUsagePacket.TryParse(ReadFrame(bytes), out var usage) && usage.Action == MsgItemUsageAction.RemoveInventory);
        Assert.DoesNotContain(frames, bytes => LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var update) &&
            update.Updates.Any(value => value.Type is LegacyMsgUpdateDataType.Money or LegacyMsgUpdateDataType.ConquerPoints or LegacyMsgUpdateDataType.WarehouseMoney));
    }
}
