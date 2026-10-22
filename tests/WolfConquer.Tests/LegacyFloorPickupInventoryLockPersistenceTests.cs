using System.Buffers.Binary;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyFloorPickupInventoryPersistenceTests
{
    private static readonly DateTime LockNow = new(2026, 10, 21, 0, 0, 0, DateTimeKind.Local);

    [Theory]
    [InlineData("append", 0, 0)]
    [InlineData("merge", 20261020, 0)]
    [InlineData("whole-stack", 20261020, 0)]
    [InlineData("append", 20261021, 2)]
    [InlineData("merge", 20261022, 2)]
    public void PickupLockClearOrDatePersistsOnlyTheExactFinalItemAndPreservesInitialSendImage(
        string path, int timer, byte expectedLocked)
    {
        var input = CreateLockPersistenceInput(path, timer);
        var character = File.ReadAllBytes(CharacterPath);
        var before = File.ReadAllBytes(ItemPath);
        var sourceBefore = SendDbRecordOracle(input.Source);
        Assert.True(LegacyFloorPickupInventory.TryPlan(input.Original.PlayerItems,
            input.Ground.WithPosition(0), input.Catalog, LockNow, out var inventory, out var reason), reason);
        var expectedInitial = SendDbRecordOracle(input.Source, position: 0,
            stack: path == "merge" ? (ushort)3 : input.Source.StackSize);
        var expectedFinal = expectedInitial.ToArray();
        expectedFinal[33] = expectedLocked;
        var expected = path == "merge" ? before.ToArray() :
            AppendSendRecordOracle(before, expectedFinal, SendPerfectionRecordOracle(input.Source));
        if (path == "merge") expectedFinal.CopyTo(expected.AsSpan(sizeof(int), 184));
        LegacyItemLockPacket? expectedDate = expectedLocked == 2
            ? new LegacyItemLockPacket(input.Source.Uid, 2, 0, unchecked((uint)timer)) : null;

        Assert.Equal(path == "merge", inventory.IsMerge);
        Assert.Equal(expectedInitial, SendDbRecordOracle(inventory.InitialSendItem));
        Assert.Equal(SendPerfectionRecordOracle(input.Source), SendPerfectionRecordOracle(inventory.InitialSendItem));
        Assert.Equal(expectedFinal, SendDbRecordOracle(inventory.Item));
        Assert.Equal(expectedDate, inventory.UnlockDate);
        Assert.Equal(timer, inventory.Item.UnlockTimer);
        Assert.Equal(character, File.ReadAllBytes(CharacterPath));
        Assert.Equal(before, File.ReadAllBytes(ItemPath));
        Assert.Equal(sourceBefore, SendDbRecordOracle(input.Source));
        Assert.True(Persist(input.Original, input.Entry, inventory, out reason), reason);
        Assert.Equal(character, File.ReadAllBytes(CharacterPath));
        Assert.Equal(expected, File.ReadAllBytes(ItemPath));
        Assert.Equal(sourceBefore, SendDbRecordOracle(input.Source));
        Assert.False(LegacyTradeJournal.HasPending(Users));
    }

    [Theory]
    [InlineData("initial-image")]
    [InlineData("initial-null")]
    [InlineData("notification-missing")]
    [InlineData("notification-forged")]
    [InlineData("final-locked")]
    public void ClaimedInitialLockImageNotificationOrFinalLockCannotOverrideTheRecomputedPlan(string mutation)
    {
        var timer = mutation.StartsWith("notification-", StringComparison.Ordinal) ? 20261022 : 0;
        var input = CreateLockPersistenceInput("append", timer);
        Assert.True(LegacyFloorPickupInventory.TryPlan(input.Original.PlayerItems,
            input.Ground.WithPosition(0), input.Catalog, LockNow, out var inventory, out var reason), reason);
        if (mutation == "initial-image")
        {
            var record = SendDbRecordOracle(inventory.InitialSendItem);
            record[164] ^= 1;
            inventory = inventory with
            { InitialSendItem = ParseSendFixtureRecord(record, SendPerfectionRecordOracle(inventory.InitialSendItem)) };
        }
        else if (mutation == "initial-null") inventory = inventory with { InitialSendItem = null! };
        else if (mutation == "notification-missing") inventory = inventory with { UnlockDate = null };
        else if (mutation == "notification-forged") inventory = inventory with
        { UnlockDate = new LegacyItemLockPacket(input.Source.Uid, 2, 0, 20261023) };
        else
        {
            var record = SendDbRecordOracle(inventory.Item);
            record[33] = 2;
            var forgedItem = ParseSendFixtureRecord(record, SendPerfectionRecordOracle(inventory.Item));
            inventory = inventory with
            {
                Item = forgedItem,
                UpdatedItems = inventory.UpdatedItems with
                {
                    InventoryItems = inventory.UpdatedItems.InventoryItems
                        .Select(item => item.Uid == forgedItem.Uid ? forgedItem : item).ToArray(),
                },
            };
        }

        var character = File.ReadAllBytes(CharacterPath);
        var items = File.ReadAllBytes(ItemPath);
        var journal = Path.Combine(Users, ".managed-trade-journal");
        Directory.CreateDirectory(journal);
        Assert.False(Persist(input.Original, input.Entry, inventory, out reason));
        Assert.Equal(mutation == "initial-null" ? "floorPickupInventoryPlanBindingInvalid" :
            "floorPickupInventoryPlanChanged", reason);
        Assert.Equal(character, File.ReadAllBytes(CharacterPath));
        Assert.Equal(items, File.ReadAllBytes(ItemPath));
        Assert.Empty(Directory.EnumerateFileSystemEntries(journal));
        Assert.False(LegacyTradeJournal.HasPending(Users));
    }

    [Fact]
    public void InterruptedPickupRecoversTheClearedLockAndRetainedTimerExactlyOnce()
    {
        var input = CreateLockPersistenceInput("whole-stack", 20261020);
        var character = File.ReadAllBytes(CharacterPath);
        var before = File.ReadAllBytes(ItemPath);
        Assert.True(LegacyFloorPickupInventory.TryPlan(input.Original.PlayerItems,
            input.Ground.WithPosition(0), input.Catalog, LockNow, out var inventory, out var reason), reason);
        var record = SendDbRecordOracle(input.Source, position: 0);
        record[33] = 0;
        var expected = AppendSendRecordOracle(before, record, SendPerfectionRecordOracle(input.Source));

        Assert.False(LegacyNpcSalePersistence.TryPersistFloorPickupInventory(input.Original,
            input.Original with { PlayerItems = inventory.UpdatedItems }, Items, input.Entry, inventory,
            count => count == 1, out reason));
        Assert.StartsWith(LegacyTradeJournal.RecoveryRequired, reason);
        Assert.True(LegacyTradeJournal.TryRecover(Users, Items, out var recovered, out reason), reason);
        Assert.Equal(1, recovered);
        Assert.Equal(character, File.ReadAllBytes(CharacterPath));
        Assert.Equal(expected, File.ReadAllBytes(ItemPath));
        Assert.True(LegacyTradeJournal.TryRecover(Users, Items, out recovered, out reason), reason);
        Assert.Equal(0, recovered);
        Assert.Equal(character, File.ReadAllBytes(CharacterPath));
        Assert.Equal(expected, File.ReadAllBytes(ItemPath));
        Assert.False(LegacyTradeJournal.HasPending(Users));
    }

    [Fact]
    public void MalformedLockedTwoDateCannotCreateAPlanOrTouchEitherPreimageOrTheJournal()
    {
        var input = CreateLockPersistenceInput("append", 20261301);
        var character = File.ReadAllBytes(CharacterPath);
        var items = File.ReadAllBytes(ItemPath);
        var source = SendDbRecordOracle(input.Source);

        Assert.False(LegacyFloorPickupInventory.TryPlan(input.Original.PlayerItems,
            input.Ground.WithPosition(0), input.Catalog, LockNow, out _, out var reason));
        Assert.Equal("floorPickupItemLockTimerInvalid", reason);
        Assert.Equal(character, File.ReadAllBytes(CharacterPath));
        Assert.Equal(items, File.ReadAllBytes(ItemPath));
        Assert.Equal(source, SendDbRecordOracle(input.Source));
        Assert.False(Directory.Exists(Path.Combine(Users, ".managed-trade-journal")));
        Assert.False(LegacyTradeJournal.HasPending(Users));
    }

    private LockPersistenceInput CreateLockPersistenceInput(string path, int timer)
    {
        var original = WriteSession(mergeCandidate: path == "merge");
        PlayerItemSnapshotItem WithLock(PlayerItemSnapshotItem item)
        {
            var record = SendDbRecordOracle(item, position: 0);
            record[33] = 2;
            BinaryPrimitives.WriteInt32LittleEndian(record.AsSpan(64), timer);
            return ParseSendFixtureRecord(record, SendPerfectionRecordOracle(item));
        }
        var ground = Item(900, itemId: ItemId, stack: path == "whole-stack" ? (ushort)3 : (ushort)1);
        PlayerItemSnapshotItem source;
        if (path == "merge")
        {
            source = WithLock(original.PlayerItems.InventoryItems[0]);
            var items = original.PlayerItems with
            { InventoryItems = [source, original.PlayerItems.InventoryItems[1]] };
            File.WriteAllBytes(ItemPath, PlayerItemSnapshotLoader.ToLegacyBytes(items));
            original = original with { PlayerItems = items };
        }
        else ground = source = WithLock(ground);
        ground = ground.WithPosition(9);
        var packet = LegacyFloorItemPacket.FromDroppedItem(ground, original.Character, 5000);
        var entry = FloorItemRuntimeEntry.ForItem(original.Character.MapId, packet, ground);
        var fields = Enumerable.Repeat("0", 47).ToArray();
        fields[21] = "321";
        fields[46] = "10";
        return new(original, entry, ground, source, new ItemType(ItemId, "LockPersistence", fields));
    }

    private sealed record LockPersistenceInput(WorldSession Original, FloorItemRuntimeEntry Entry,
        PlayerItemSnapshotItem Ground, PlayerItemSnapshotItem Source, ItemType Catalog);
}
