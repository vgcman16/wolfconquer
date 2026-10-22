using System.Buffers.Binary;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNpcSaleTransactionTests
{
    [Theory]
    [InlineData(0, (byte)2, 0, true)]
    [InlineData(1, (byte)2, 20_000_101, true)]
    [InlineData(2, (byte)2, 20_000_101, true)]
    [InlineData(0, (byte)2, 29_991_231, false)]
    [InlineData(1, (byte)2, 29_991_231, false)]
    [InlineData(2, (byte)1, 20_261_301, false)]
    public async Task PickupItemLockLifecycleSavesFinalFullImageBeforeInitialLockedFrameAndKeepsPhysicalHolder(
        int selection, byte locked, int timer, bool unlock)
    {
        const uint uid = 1_300_241;
        var merge = selection == 1;
        var wholeStack = selection == 2;
        SeedPickupItemLock(uid, selection, locked, timer);
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
                Assert.NotNull(characterBytes);
                Assert.NotNull(expectedItems);
                AssertPickupInventoryCommitted(owner, player!, before, expectedItems, characterBytes);
                Assert.True(LegacyGameItemPacket.TryParse(ReadFrame(bytes), out var firstItem));
                Assert.Equal(locked, firstItem.Locked);
                var saved = Assert.Single(Current(owner, player!).PlayerItems.InventoryItems,
                    item => item.Uid == (merge ? SoldUid : AdmissionPickedItemUid));
                Assert.Equal(unlock ? (byte)0 : locked, saved.Locked);
                Assert.Equal(timer, saved.UnlockTimer);
            });
        });
        before = Current(owner, player);
        var destination = Assert.Single(before.PlayerItems.InventoryItems, item => item.Uid == SoldUid);
        var incoming = WithRawPickupItemLock(PickupSendRichItem(ItemId, wholeStack ? (ushort)3 : (ushort)1,
            maximumDurability: 10, expiration: 0), locked, timer);
        var rawIncomingBytes = PlayerItemSnapshotLoader.ToLegacyBytes(new PlayerItemSnapshot([incoming], [], [], [], []));
        var entry = SeedPickupInventoryFloor(owner, player, incoming);
        characterBytes = File.ReadAllBytes(UserPath(uid));
        expectedItems = ExpectedPickupItemLockBytes(before.PlayerItems, incoming, merge,
            wholeStack ? (ushort)3 : (ushort)1, unlock ? (byte)0 : locked);
        var first = player.Sent.Count;
        armed = true;

        await PickupAdmissionFloor(owner, player, PersistencePickupFloorUid);

        Assert.Equal(1, observed);
        Assert.Null(callbackFailure);
        AssertPickupInventoryCommitted(owner, player, before, expectedItems, characterBytes);
        AssertPickupItemLockFrames(player.Sent.Skip(first).ToArray(), expectedItems,
            merge ? SoldUid : incoming.Uid, merge, locked, timer, unlock);
        Assert.Same(incoming, entry.Item);
        Assert.Equal(rawIncomingBytes, PlayerItemSnapshotLoader.ToLegacyBytes(new PlayerItemSnapshot([incoming], [], [], [], [])));
        if (!merge)
        {
            var unchanged = Assert.Single(Current(owner, player).PlayerItems.InventoryItems, item => item.Uid == SoldUid);
            Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(new PlayerItemSnapshot([destination], [], [], [], [])),
                PlayerItemSnapshotLoader.ToLegacyBytes(new PlayerItemSnapshot([unchanged], [], [], [], [])));
        }
        await AssertPersistencePickupReplayIsSilent(owner, player);
    }

    [Theory]
    [InlineData(false, 20_261_301)]
    [InlineData(true, 20_260_230)]
    public async Task MalformedSelectedLockedTwoDateRefusesBeforeAnyFileFloorOrSessionWrite(bool merge, int timer)
    {
        const uint uid = 1_300_251;
        SeedPickupItemLock(uid, merge ? 1 : 0, locked: 2, timer);
        var owner = NewOwner(identity: new(42), stackLimit: merge ? (ushort)10 : (ushort)0);
        var player = await Login(owner, uid);
        var before = Current(owner, player);
        // Merge must validate the destination timer, not a well-formed incoming
        // timer. Append instead validates the malformed ground item itself.
        var incoming = WithRawPickupItemLock(PickupSendRichItem(ItemId, 1, 10, expiration: 0), 2, merge ? 0 : timer);
        var entry = SeedPickupInventoryFloor(owner, player, incoming);
        var characterBytes = File.ReadAllBytes(UserPath(uid));
        var itemBytes = File.ReadAllBytes(ItemPath(uid));
        var first = player.Sent.Count;

        await PickupAdmissionFloor(owner, player, PersistencePickupFloorUid);

        AssertPersistencePickupDenied(owner, player, before, entry, characterBytes, itemBytes, first);
        Assert.Contains("floorPickupItemLockTimerInvalid", owner.Log.ToString(), StringComparison.Ordinal);
        Assert.Equal((byte)2, entry.Item!.Locked);
    }

    [WindowsNpcSaleTheory]
    [InlineData(true)]
    public async Task RefusedLockedPickupSaveRetainsBothLockImagesAndRetryPublishesTheNativeTwoFrameSequenceOnce(bool itemTarget)
    {
        const uint uid = 1_300_261;
        const int timer = 20_000_101;
        SeedPickupItemLock(uid, selection: 0, locked: 2, timer);
        var owner = NewOwner(identity: new(42));
        var player = await Login(owner, uid);
        var before = Current(owner, player);
        var incoming = WithRawPickupItemLock(PickupSendRichItem(ItemId, 1, 10, expiration: 0), 2, timer);
        var entry = SeedPickupInventoryFloor(owner, player, incoming);
        var characterBytes = File.ReadAllBytes(UserPath(uid));
        var itemBytes = File.ReadAllBytes(ItemPath(uid));
        var expectedItems = ExpectedPickupItemLockBytes(before.PlayerItems, incoming, merge: false, stack: 1, locked: 0);
        var first = player.Sent.Count;

        using (File.Open(itemTarget ? ItemPath(uid) : UserPath(uid), FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            await PickupAdmissionFloor(owner, player, PersistencePickupFloorUid);
            AssertPersistencePickupDenied(owner, player, before, entry, characterBytes, itemBytes, first);
            Assert.Equal((byte)2, incoming.Locked);
            Assert.Equal(timer, incoming.UnlockTimer);
        }

        await PickupAdmissionFloor(owner, player, PersistencePickupFloorUid);

        AssertPickupInventoryCommitted(owner, player, before, expectedItems, characterBytes);
        AssertPickupItemLockFrames(player.Sent.Skip(first).ToArray(), expectedItems, incoming.Uid,
            merge: false, originalLocked: 2, timer, unlock: true);
        await AssertPersistencePickupReplayIsSilent(owner, player);
    }

    private void SeedPickupItemLock(uint uid, int selection, byte locked, int timer)
    {
        SeedPickupSendInventory(uid, ItemId, merge: selection != 0, maximumDurability: 10);
        var loader = new PlayerItemSnapshotLoader(ItemsRoot);
        var before = loader.LoadByCharacterUid(uid);
        loader.SaveByCharacterUid(uid, before with
        {
            InventoryItems = before.InventoryItems.Select(item => item.Uid == SoldUid
                ? WithRawPickupItemLock(item, locked, timer) : item).ToArray(),
        });
    }

    private static PlayerItemSnapshotItem WithRawPickupItemLock(PlayerItemSnapshotItem item, byte locked, int timer)
    {
        var bytes = PlayerItemSnapshotLoader.ToLegacyBytes(new PlayerItemSnapshot([item], [], [], [], []));
        bytes[sizeof(int) + 33] = locked;
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(sizeof(int) + 64, sizeof(int)), timer);
        return Assert.Single(PlayerItemSnapshotLoader.Parse(bytes).InventoryItems);
    }

    private static byte[] ExpectedPickupItemLockBytes(PlayerItemSnapshot before, PlayerItemSnapshotItem incoming,
        bool merge, ushort stack, byte locked)
    {
        var bytes = ExpectedPickupInventoryBytes(before, incoming, merge, stack);
        var count = BinaryPrimitives.ReadInt32LittleEndian(bytes);
        var uid = merge ? SoldUid : incoming.Uid;
        var offsets = Enumerable.Range(0, count).Select(index => sizeof(int) + index * PlayerItemSnapshotLoader.LegacyDbItemSize)
            .Where(offset => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, sizeof(uint))) == uid).ToArray();
        // The only additional persisted mutation is Locked. In particular, the
        // expired UnLockTimer is not reset, and every perfection byte is kept.
        bytes[Assert.Single(offsets) + 33] = locked;
        return bytes;
    }

    private static void AssertPickupItemLockFrames(IReadOnlyList<byte[]> frames, byte[] expectedItems,
        uint uid, bool merge, byte originalLocked, int timer, bool unlock)
    {
        var dateReply = originalLocked == 2 && !unlock;
        Assert.Equal(unlock || dateReply ? 4 : 3, frames.Count);
        var saved = Assert.Single(PlayerItemSnapshotLoader.Parse(expectedItems).InventoryItems, item => item.Uid == uid);
        Assert.True(LegacyGameItemPacket.TryParse(ReadFrame(frames[0]), out var initial));
        Assert.Equal(LegacyGameItemPacket.FromSnapshotItem(saved.WithLocked(originalLocked),
            mode: merge ? LegacyGameItemPacket.UpdateItemMode : LegacyGameItemPacket.AddItemMode), initial);
        var next = 1;
        if (unlock)
        {
            Assert.True(LegacyGameItemPacket.TryParse(ReadFrame(frames[next++]), out var updated));
            Assert.Equal(LegacyGameItemPacket.FromSnapshotItem(saved, mode: LegacyGameItemPacket.UpdateItemMode), updated);
            Assert.Equal((byte)0, updated.Locked);
        }
        else if (dateReply)
        {
            Assert.True(LegacyItemLockPacket.TryParse(ReadFrame(frames[next++]), out var date));
            Assert.Equal(new LegacyItemLockPacket(uid, Type: 2, LockedMode: 0, unchecked((uint)timer)), date);
        }
        Assert.True(IsAdmissionFloorRemoval(frames[next++], PersistencePickupFloorUid));
        Assert.True(LegacyMsgMessagePacket.TryParse(ReadFrame(frames[next++]), out var message));
        Assert.Equal("You have picked up a OrdinaryNpcSaleFixture.", message.Content[3]);
        Assert.Equal(frames.Count, next);
    }
}
