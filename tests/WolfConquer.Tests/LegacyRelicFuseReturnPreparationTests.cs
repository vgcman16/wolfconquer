using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyRelicFuseReturnPreparationTests
{
    private static PlayerItemSnapshotItem Item(uint uid, ushort position = 0, uint warehouse = 0,
        IReadOnlyList<PlayerItemSnapshotItem>? deposits = null) => new()
    {
        Uid = uid, ItemId = 4_100_001, Position = position, WarehouseId = warehouse,
        Durability = 6000, MaximumDurability = 7000, StackSize = 1, DepositedItems = deposits ?? [],
    };

    private static PlayerItemSnapshot Owner(int inventoryCount = 1,
        IReadOnlyList<PlayerItemSnapshotItem>? held = null) => new(
        Enumerable.Range(1, inventoryCount).Select(uid => Item((uint)uid)).ToArray(),
        held ?? new[] { Item(100, 213), Item(101, 214), Item(102, 215) }, [], [], [], []);

    private static PlayerItemSnapshot Store(PlayerItemSnapshot owner, int group, IReadOnlyList<PlayerItemSnapshotItem> rows)
    {
        var stores = new[] { owner.InventoryItems, owner.RelicFuseItems, owner.MythsoulBagItems,
            owner.EquipmentItems, owner.RuneItems, owner.WardrobeItems, owner.WarehouseItems };
        stores[group] = rows;
        return new(stores[0], stores[1], stores[2], stores[3], stores[4], stores[5]) { WarehouseItems = stores[6] };
    }

    private static LegacyRelicFuseReturnPreparation Prepare(PlayerItemSnapshot owner, uint selectedUid = 100)
    {
        Assert.True(LegacyRelicFuseReturnPreparation.TryPrepare(owner, selectedUid, out var preparation, out var reason), reason);
        Assert.Empty(reason);
        return Assert.IsType<LegacyRelicFuseReturnPreparation>(preparation);
    }

    private static LegacyRelicFuseReturnProjection Project(LegacyRelicFuseReturnPreparation preparation, PlayerItemSnapshot owner)
    {
        Assert.True(preparation.TryProject(owner, out var projection, out var reason), reason);
        Assert.Empty(reason);
        return Assert.IsType<LegacyRelicFuseReturnProjection>(projection);
    }

    [Theory]
    [InlineData(100U)]
    [InlineData(101U)]
    [InlineData(102U)]
    public void AnyActualHeldChoiceReturnsThatSavedItemAndClearsOnlyThePrivateHeldTriple(uint uid)
    {
        var owner = Owner(); var before = PlayerItemSnapshotLoader.ToLegacyBytes(owner);
        var preparation = Prepare(owner, uid);
        Assert.Same(owner.RelicFuseItems.Single(item => item.Uid == uid), preparation.Selected);
        var projection = Project(preparation, owner);
        Assert.Equal(new uint[] { 1, uid }, projection.Items.InventoryItems.Select(item => item.Uid));
        Assert.Empty(projection.Items.RelicFuseItems);
        Assert.Equal(uid, projection.Returned.Uid);
        Assert.Equal((ushort)0, projection.Returned.Position);
        Assert.Equal(0U, projection.Returned.WarehouseId);
        Assert.Same(projection.Returned, projection.Items.InventoryItems[^1]);
        Assert.NotSame(preparation.Selected, projection.Returned);
        Assert.Equal(before, PlayerItemSnapshotLoader.ToLegacyBytes(owner));
        Assert.Equal(3, owner.RelicFuseItems.Count);
        Assert.True(preparation.IsCurrent(owner));
    }

    [Fact]
    public void ReturnPreservesSelectedPayloadFlagsStackChildrenAndUnrelatedSavedData()
    {
        var child = Item(1000); child.OwnerName = "ChildOwner"; child.Signature = "ChildSignature";
        var selected = new PlayerItemSnapshotItem
        {
            Uid = 100, ItemId = 4_100_001, Position = 213, StackSize = 7, Bound = 1, Locked = 1, Suspicious = 1,
            Durability = 6000, MaximumDurability = 7000, SocketProgress = 9, Plus = 3, Bless = 2, Enchant = 4,
            Activate = 1, Expiration = 999, RuneExperience = 123, Attribute1 = 100_101, Attribute2 = 200_103,
            Attribute3 = 300_104, Attribute4 = 400_105, Attribute5 = 500_109,
            PerfectionLevel = 2, PerfectionProgress = 3, OwnerUid = 44, OwnerName = "Owner", Signature = "Signature",
            DepositedItems = new[] { child },
        };
        var owner = new PlayerItemSnapshot(new[] { Item(1) }, new[] { selected, Item(101, 214), Item(102, 215) },
            new[] { Item(2, 217) }, new[] { Item(3, 1) }, new[] { Item(4, 211) }, new[] { Item(5, warehouse: 100) })
            { WarehouseItems = new[] { Item(6, warehouse: 44) } };
        var before = PlayerItemSnapshotLoader.ToLegacyBytes(owner);
        var projection = Project(Prepare(owner), owner);
        var returned = projection.Returned;
        Assert.Equal((ushort)7, returned.StackSize);
        Assert.Equal((byte)1, returned.Bound); Assert.Equal((byte)1, returned.Locked); Assert.Equal((byte)1, returned.Suspicious);
        Assert.Equal(9U, returned.SocketProgress); Assert.Equal((byte)3, returned.Plus);
        Assert.Equal((byte)2, returned.Bless); Assert.Equal((byte)4, returned.Enchant);
        Assert.Equal(1U, returned.Activate); Assert.Equal(999L, returned.Expiration); Assert.Equal(123U, returned.RuneExperience);
        Assert.Equal(new uint[] { 100_101, 200_103, 300_104, 400_105, 500_109 },
            new[] { returned.Attribute1, returned.Attribute2, returned.Attribute3, returned.Attribute4, returned.Attribute5 });
        Assert.Equal(2U, returned.PerfectionLevel); Assert.Equal(3U, returned.PerfectionProgress); Assert.Equal(44U, returned.OwnerUid);
        Assert.Equal("Owner", returned.OwnerName); Assert.Equal("Signature", returned.Signature);
        Assert.Single(returned.DepositedItems); Assert.Equal(1000U, returned.DepositedItems[0].Uid);
        Assert.Equal("ChildOwner", returned.DepositedItems[0].OwnerName);
        Assert.NotSame(child, returned.DepositedItems[0]);
        Assert.NotSame(owner.EquipmentItems[0], projection.Items.EquipmentItems[0]);
        Assert.Equal(3U, projection.Items.EquipmentItems[0].Uid);
        Assert.Equal(6U, projection.Items.WarehouseItems[0].Uid);
        Assert.Equal(before, PlayerItemSnapshotLoader.ToLegacyBytes(owner));
        Assert.True(LegacyRelicFuseImages.TryCapture(projection.Items, false, out _, out _));
    }

    [Fact]
    public void EachPrivateProjectionIsFreshAndCannotPoisonTheRecipeOrActualOwner()
    {
        var owner = Owner(); var preparation = Prepare(owner);
        var first = Project(preparation, owner); var expected = PlayerItemSnapshotLoader.ToLegacyBytes(first.Items);
        first.Returned.OwnerName = "mutated";
        ((PlayerItemSnapshotItem[])first.Items.InventoryItems)[0] = Item(9000);
        var second = Project(preparation, owner);
        Assert.NotSame(first.Items, second.Items); Assert.NotSame(first.Returned, second.Returned);
        Assert.Equal(expected, PlayerItemSnapshotLoader.ToLegacyBytes(second.Items));
        Assert.Equal(1U, owner.InventoryItems[0].Uid);
        Assert.Equal("", owner.RelicFuseItems[0].OwnerName);
    }

    [Fact]
    public void ExactlyOneFreeInventorySlotCanHoldTheReturnedExistingIdentity()
    {
        var owner = Owner(39); var preparation = Prepare(owner); var projection = Project(preparation, owner);
        Assert.Equal(40, projection.Items.InventoryItems.Count);
        Assert.Equal(100U, projection.Items.InventoryItems[^1].Uid);
        Assert.Equal(39, owner.InventoryItems.Count);
    }
}
