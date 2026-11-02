using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

public sealed partial class LegacyRelicDecomposePreparationTests
{
    [Theory]
    [InlineData("locked")] [InlineData("stacked")] [InlineData("suspicious")]
    [InlineData("bad-bound")] [InlineData("not-relic")] [InlineData("child")]
    public void OneIneligibleSelectedRowRefusesTheEntireOtherwiseValidBatch(string defect)
    {
        var bad = defect switch
        {
            "locked" => Item(Second).WithLocked(1),
            "stacked" => Item(Second).WithStackSize(2),
            "suspicious" => new PlayerItemSnapshotItem { Uid = Second, ItemId = 4_100_001, StackSize = 1, Suspicious = 1 },
            "bad-bound" => Item(Second, bound: 2),
            "not-relic" => Item(Second, 1_000_000),
            _ => Item(Second).WithDepositedItems([Item(200)]),
        };
        Refuses(Owner() with { InventoryItems = new[] { Item(First), bad, Item(Kept, 1_000_000) } }, Request(First, Second));
    }

    [Theory]
    [InlineData("equipment")] [InlineData("warehouse")] [InlineData("fuse")]
    [InlineData("rune")] [InlineData("mythsoul")] [InlineData("wardrobe")] [InlineData("deposited")]
    public void ARealUidOutsideInventoryCannotBeReplacedByAnItemTypeFallback(string location)
    {
        var owner = PlayerItemSnapshot.Empty with { InventoryItems = new[] { Item(Kept, 1_000_000) } };
        owner = location switch
        {
            "equipment" => owner with { EquipmentItems = new[] { Item(First).WithPosition(1) } },
            "warehouse" => owner with { WarehouseItems = new[] { Item(First).WithWarehouseId(44) } },
            "fuse" => owner with { RelicFuseItems = new[] { Item(First).WithPosition(213) } },
            "rune" => owner with { RuneItems = new[] { Item(First).WithPosition(211) } },
            "mythsoul" => owner with { MythsoulBagItems = new[] { Item(First).WithPosition(217) } },
            "wardrobe" => owner with { WardrobeItems = new[] { Item(First).WithWarehouseId(100) } },
            _ => owner with { WarehouseItems = new[] { Item(200).WithWarehouseId(44).WithDepositedItems([Item(First)]) } },
        };
        Refuses(owner, Request(First));
    }

    [Theory]
    [InlineData("duplicate-tail")] [InlineData("grandchild")]
    [InlineData("text-overflow")] [InlineData("wrong-placement")]
    public void InvalidUnselectedTailCannotBeHiddenByAValidSelectedPrefix(string defect)
    {
        var owner = Owner();
        if (defect == "duplicate-tail") owner = owner with { WarehouseItems = new[] { Item(First).WithWarehouseId(44) } };
        else if (defect == "grandchild") owner = owner with
        { WarehouseItems = new[] { Item(200).WithWarehouseId(44).WithDepositedItems([Item(201).WithDepositedItems([Item(202)])]) } };
        else if (defect == "wrong-placement") owner = owner with
        { InventoryItems = owner.InventoryItems.Append(Item(200).WithPosition(1)).ToArray() };
        else owner.InventoryItems[^1].OwnerName = new string('x', 33);
        Refuses(owner, Request(First));
    }
}
