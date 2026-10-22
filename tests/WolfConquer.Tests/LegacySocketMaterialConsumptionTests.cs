using System.Buffers;
using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacySocketMaterialConsumptionTests
{
    private const uint MaterialItemId = 1_088_000;

    [Fact]
    public void FiveDistinctStackedMaterialsConsumeOneUnitFromEveryPhysicalUid()
    {
        var materials = Enumerable.Range(1, 5)
            .Select(index => Material(checked((uint)index), stackSize: 10))
            .ToArray();
        var items = Snapshot(materials);

        var (success, updated, frames) = Consume(items, materials.Select(item => item.Uid).ToArray(), 5);

        Assert.True(success);
        Assert.NotSame(items, updated);
        Assert.Equal(5, updated.InventoryItems.Count);
        Assert.Equal(5, frames.Count);
        for (var index = 0; index < materials.Length; index++)
        {
            var actual = Assert.Single(updated.InventoryItems, item => item.Uid == materials[index].Uid);
            Assert.Equal((ushort)9, actual.StackSize);
            Assert.NotSame(materials[index], actual);
            Assert.Equal((ushort)10, materials[index].StackSize);
            AssertUpdateFrame(frames[index], actual.Uid, stackSize: 9);
        }
    }

    [Fact]
    public void RepeatedSingleMaterialUidCannotSupplyFivePhysicalEntries()
    {
        var material = Material(11, stackSize: 10);
        var items = Snapshot(material);

        AssertRejected(items, [material.Uid, material.Uid, material.Uid, material.Uid, material.Uid], 5);

        Assert.Same(material, Assert.Single(items.InventoryItems));
        Assert.Equal((ushort)10, material.StackSize);
    }

    [Fact]
    public void MissingUidRejectsWithoutPublishingEarlierValidRemoval()
    {
        var material = Material(21, stackSize: 1);
        var items = Snapshot(material);

        AssertRejected(items, [material.Uid, 999], 2);

        Assert.Same(material, Assert.Single(items.InventoryItems));
    }

    [Fact]
    public void WrongItemIdRejectsWithoutPublishingEarlierValidRemoval()
    {
        var material = Material(31, stackSize: 1);
        var wrongItem = new PlayerItemSnapshotItem { Uid = 32, ItemId = MaterialItemId + 1, StackSize = 10 };
        var items = Snapshot(material, wrongItem);

        AssertRejected(items, [material.Uid, wrongItem.Uid], 2);

        Assert.Same(material, items.InventoryItems[0]);
        Assert.Same(wrongItem, items.InventoryItems[1]);
        Assert.Equal((ushort)10, wrongItem.StackSize);
    }

    [Fact]
    public void FiveDistinctUnitStacksAreRemovedWithInventoryRemovalFrames()
    {
        var materials = Enumerable.Range(41, 5)
            .Select(index => Material(checked((uint)index), stackSize: 1))
            .ToArray();
        var items = Snapshot(materials);

        var (success, updated, frames) = Consume(items, materials.Select(item => item.Uid).ToArray(), 5);

        Assert.True(success);
        Assert.Empty(updated.InventoryItems);
        Assert.Equal(5, frames.Count);
        for (var index = 0; index < materials.Length; index++)
        {
            Assert.Same(materials[index], items.InventoryItems[index]);
            Assert.Equal((ushort)1, materials[index].StackSize);
            AssertRemoveFrame(frames[index], materials[index].Uid);
        }
    }

    [Fact]
    public void SingleMaterialRequestConsumesOnlyOneUnit()
    {
        var material = Material(51, stackSize: 10);
        var items = Snapshot(material);

        var (success, updated, frames) = Consume(items, [material.Uid], 1);

        Assert.True(success);
        var actual = Assert.Single(updated.InventoryItems);
        Assert.Equal(material.Uid, actual.Uid);
        Assert.Equal((ushort)9, actual.StackSize);
        Assert.Equal((ushort)10, material.StackSize);
        AssertUpdateFrame(Assert.Single(frames), material.Uid, stackSize: 9);
    }

    [Fact]
    public void MixedZeroUnitAndStackedEntriesKeepNativeRemoveOrDecrementBehavior()
    {
        var zeroStack = Material(61, stackSize: 0);
        var unitStack = Material(62, stackSize: 1);
        var largerStack = Material(63, stackSize: 4);
        var items = Snapshot(zeroStack, unitStack, largerStack);

        var (success, updated, frames) = Consume(items, [61, 62, 63], 3);

        Assert.True(success);
        var actual = Assert.Single(updated.InventoryItems);
        Assert.Equal(largerStack.Uid, actual.Uid);
        Assert.Equal((ushort)3, actual.StackSize);
        Assert.Equal(3, frames.Count);
        AssertRemoveFrame(frames[0], zeroStack.Uid);
        AssertRemoveFrame(frames[1], unitStack.Uid);
        AssertUpdateFrame(frames[2], largerStack.Uid, stackSize: 3);
        Assert.Equal(new ushort[] { 0, 1, 4 }, items.InventoryItems.Select(item => item.StackSize));
    }

    [Fact]
    public void SuccessfulConsumptionPreservesUnselectedTargetContainersAndMaterialMetadata()
    {
        var target = new PlayerItemSnapshotItem { Uid = 71, ItemId = 410_009, SocketOne = 255, SocketTwo = 0 };
        var material = new PlayerItemSnapshotItem
        {
            Uid = 72,
            ItemId = MaterialItemId,
            StackSize = 10,
            Durability = 123,
            MaximumDurability = 456,
            Position = 0,
            Plus = 4,
            Bound = 1,
            Locked = 1,
            SocketProgress = 567,
            PerfectionLevel = 12,
            PerfectionProgress = 34,
            OwnerUid = 56,
            OwnerName = "material owner",
            Signature = "keep signature",
            Attribute1 = 78,
            DepositedItems = [new PlayerItemSnapshotItem { Uid = 73, ItemId = 100_000, StackSize = 2 }],
        };
        PlayerItemSnapshotItem[] relics = [Material(81, 2)];
        PlayerItemSnapshotItem[] mythsouls = [Material(82, 3)];
        PlayerItemSnapshotItem[] equipment = [Material(83, 4)];
        PlayerItemSnapshotItem[] runes = [Material(84, 5)];
        PlayerItemSnapshotItem[] wardrobe = [Material(85, 6)];
        PlayerItemSnapshotItem[] warehouse = [Material(86, 7)];
        var items = new PlayerItemSnapshot([target, material], relics, mythsouls, equipment, runes, wardrobe)
        {
            WarehouseItems = warehouse,
        };
        var originalBytes = PlayerItemSnapshotLoader.ToLegacyBytes(items);

        var (success, updated, frames) = Consume(items, [material.Uid], 1);

        Assert.True(success);
        Assert.Same(target, updated.InventoryItems[0]);
        Assert.Same(relics, updated.RelicFuseItems);
        Assert.Same(mythsouls, updated.MythsoulBagItems);
        Assert.Same(equipment, updated.EquipmentItems);
        Assert.Same(runes, updated.RuneItems);
        Assert.Same(wardrobe, updated.WardrobeItems);
        Assert.Same(warehouse, updated.WarehouseItems);
        Assert.Equal(originalBytes, PlayerItemSnapshotLoader.ToLegacyBytes(items));
        var expected = items with { InventoryItems = new[] { target, material.WithStackSize(9) } };
        Assert.Equal(
            PlayerItemSnapshotLoader.ToLegacyBytes(expected),
            PlayerItemSnapshotLoader.ToLegacyBytes(updated));
        Assert.Same(material.DepositedItems, updated.InventoryItems[1].DepositedItems);
        AssertUpdateFrame(Assert.Single(frames), material.Uid, stackSize: 9);
    }

    [Fact]
    public void EmptyOffersAndNonpositiveCountsRejectWithoutPublishingMutation()
    {
        var material = Material(91, stackSize: 10);
        var items = Snapshot(material);

        AssertRejected(items, [], 1);
        AssertRejected(items, [material.Uid], 0);
        AssertRejected(items, [material.Uid], -1);
    }

    [Fact]
    public void MaterialsInOtherContainersCannotSatisfyInventorySelection()
    {
        var material = Material(101, stackSize: 10);
        var items = new PlayerItemSnapshot([], [material], [material], [material], [material], [material])
        {
            WarehouseItems = new[] { material },
        };

        AssertRejected(items, [material.Uid], 1);

        Assert.Same(material, Assert.Single(items.WarehouseItems));
        Assert.Equal((ushort)10, material.StackSize);
    }

    private static PlayerItemSnapshotItem Material(uint uid, ushort stackSize) =>
        new() { Uid = uid, ItemId = MaterialItemId, Position = 0, StackSize = stackSize };

    private static PlayerItemSnapshot Snapshot(params PlayerItemSnapshotItem[] inventory) =>
        new(inventory, [], [], [], [], []);

    private static (bool Success, PlayerItemSnapshot Updated, IReadOnlyList<byte[]> Frames) Consume(
        PlayerItemSnapshot items,
        IReadOnlyList<uint> materialUids,
        int count)
    {
        var method = typeof(WorldPacketHandlers).GetMethod(
            "TryConsumeLegacySocketMaterials",
            BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly,
            binder: null,
            types:
            [
                typeof(PlayerItemSnapshot),
                typeof(IReadOnlyList<uint>),
                typeof(uint),
                typeof(int),
                typeof(PlayerItemSnapshot).MakeByRefType(),
                typeof(IReadOnlyList<byte[]>).MakeByRefType(),
            ],
            modifiers: null);
        Assert.NotNull(method);
        object?[] arguments = [items, materialUids, MaterialItemId, count, null, null];
        var success = Assert.IsType<bool>(method.Invoke(null, arguments));
        return (
            success,
            Assert.IsType<PlayerItemSnapshot>(arguments[4]),
            Assert.IsAssignableFrom<IReadOnlyList<byte[]>>(arguments[5]));
    }

    private static void AssertRejected(PlayerItemSnapshot items, IReadOnlyList<uint> materialUids, int count)
    {
        var originalBytes = PlayerItemSnapshotLoader.ToLegacyBytes(items);

        var (success, updated, frames) = Consume(items, materialUids, count);

        Assert.False(success);
        Assert.Same(items, updated);
        Assert.Empty(frames);
        Assert.Equal(originalBytes, PlayerItemSnapshotLoader.ToLegacyBytes(items));
    }

    private static void AssertUpdateFrame(byte[] bytes, uint uid, ushort stackSize)
    {
        Assert.True(LegacyGameItemPacket.TryParse(ReadFrame(bytes), out var packet));
        Assert.Equal(uid, packet.Uid);
        Assert.Equal(MaterialItemId, packet.ItemId);
        Assert.Equal(LegacyGameItemPacket.UpdateItemMode, packet.Mode);
        Assert.Equal(stackSize, packet.StackSize);
    }

    private static void AssertRemoveFrame(byte[] bytes, uint uid)
    {
        Assert.True(MsgItemUsagePacket.TryParse(ReadFrame(bytes), out var packet));
        Assert.Equal(
            new MsgItemUsagePacket(uid, DwParam: 0, MsgItemUsageAction.RemoveInventory, 0, 0, 0, 0),
            packet);
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }
}
