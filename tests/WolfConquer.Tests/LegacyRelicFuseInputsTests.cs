using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyRelicFuseInputsTests
{
    private static PlayerItemSnapshotItem Item(uint uid, uint itemId = 4_100_001, ushort position = 0,
        uint warehouse = 0, ushort stack = 1, byte bound = 0, byte locked = 0, byte suspicious = 0,
        IReadOnlyList<PlayerItemSnapshotItem>? deposits = null, uint attribute = 100_101) => new()
    {
        Uid = uid, ItemId = itemId, Position = position, WarehouseId = warehouse, StackSize = stack,
        Bound = bound, Locked = locked, Suspicious = suspicious, DepositedItems = deposits ?? [],
        Attribute1 = attribute, Attribute2 = 200_103, Attribute3 = 300_104, Attribute4 = 400_105, Attribute5 = 500_109,
        Durability = 100, MaximumDurability = 100,
    };

    private static PlayerItemSnapshot Owner(IReadOnlyList<PlayerItemSnapshotItem>? inventory = null) =>
        new(inventory ?? new[] { Item(1), Item(2, itemId: 4_100_002) }, [], [], [], [], []);

    private static PlayerItemSnapshot Store(PlayerItemSnapshot owner, int group, IReadOnlyList<PlayerItemSnapshotItem> rows)
    {
        var stores = new[] { owner.InventoryItems, owner.RelicFuseItems, owner.MythsoulBagItems,
            owner.EquipmentItems, owner.RuneItems, owner.WardrobeItems, owner.WarehouseItems };
        stores[group] = rows;
        return new(stores[0], stores[1], stores[2], stores[3], stores[4], stores[5]) { WarehouseItems = stores[6] };
    }

    private static LegacyRelicFuseInputs Capture(PlayerItemSnapshot owner)
    {
        Assert.True(LegacyRelicFuseInputs.TryCapture(owner, 1, 2, out var inputs, out var reason), reason);
        Assert.Empty(reason);
        return Assert.IsType<LegacyRelicFuseInputs>(inputs);
    }

    [Fact]
    public void CapturesTheActualDistinctInventoryRootsAndWholeGraphMaximum()
    {
        var first = Item(1); var second = Item(2, itemId: 4_100_002);
        var descendant = Item(900);
        var owner = Store(Owner([first, second]), 6, [Item(800, deposits: [descendant])]);
        var inputs = Capture(owner);
        Assert.Same(first, inputs.First); Assert.Same(second, inputs.Second);
        Assert.Equal(900U, inputs.MaximumUid);
        Assert.Equal(1, inputs.MaximumDepth);
        Assert.True(inputs.IsCurrent(owner));
        Assert.False(inputs.IsCurrent(owner with { }));
    }

    [Theory]
    [InlineData((byte)0, (byte)0, (byte)0)]
    [InlineData((byte)0, (byte)1, (byte)1)]
    [InlineData((byte)1, (byte)0, (byte)1)]
    [InlineData((byte)1, (byte)1, (byte)1)]
    public void ResultBoundRetainsTheMoreRestrictiveValidatedInput(byte first, byte second, byte expected)
    {
        var inputs = Capture(Owner([Item(1, bound: first), Item(2, bound: second)]));
        Assert.Equal(expected, inputs.ResultBound);
    }

    [Theory]
    [InlineData((ushort)0)]
    [InlineData((ushort)1)]
    public void LegacyZeroAndSingleStacksAreBothOneEligibleUnit(ushort stack)
    {
        var owner = Owner([Item(1, stack: stack), Item(2, stack: stack)]);
        Assert.True(Capture(owner).IsCurrent(owner));
    }

    [Fact]
    public void SharedEmptyCollectionsAreNotMistakenForPhysicalItemAliases()
    {
        var empty = Array.Empty<PlayerItemSnapshotItem>();
        var inventory = new[] { Item(1, deposits: empty), Item(2, deposits: empty), Item(3, deposits: empty) };
        var owner = new PlayerItemSnapshot(inventory, empty, empty, empty, empty, empty) { WarehouseItems = empty };
        Assert.True(Capture(owner).IsCurrent(owner));
    }

    [Theory]
    [InlineData("position")]
    [InlineData("warehouse")]
    [InlineData("stack")]
    [InlineData("locked")]
    [InlineData("suspicious")]
    [InlineData("bound")]
    [InlineData("deposits")]
    public void SelectedInputRestrictionsRejectWithoutReturningAnOwner(string change)
    {
        var first = change switch
        {
            "position" => Item(1, position: 1), "warehouse" => Item(1, warehouse: 44), "stack" => Item(1, stack: 2),
            "locked" => Item(1, locked: 1), "suspicious" => Item(1, suspicious: 1), "bound" => Item(1, bound: 2),
            _ => Item(1, deposits: [Item(3)]),
        };
        Refuses(Owner([first, Item(2)]), expected: "relicFuseInputNotEligible");
    }

    [Fact]
    public void RequestedIdentitiesMustBeNonzeroDistinctAndBothPresentAtInventoryRoots()
    {
        var owner = Owner();
        Refuses(owner, 0, 2, "relicFuseDistinctInputsRequired");
        Refuses(owner, 1, 0, "relicFuseDistinctInputsRequired");
        Refuses(owner, 1, 1, "relicFuseDistinctInputsRequired");
        Refuses(owner, 1, 99, "relicFuseInputNotEligible");
        Refuses(Store(Owner([Item(1)]), 3, [Item(2)]), expected: "relicFuseInputNotEligible");
        Refuses(Owner([Item(1), Item(3, deposits: [Item(2)])]), expected: "relicFuseInputNotEligible");
        Refuses(null!, expected: "relicFuseOwnerRequired");
    }

    [Fact]
    public void AlreadyHeldFuseStateCannotBeOverwrittenByAnotherTriple() =>
        Refuses(Store(Owner(), 1, [Item(3, position: 213)]), expected: "relicFuseHeldStateNotEmpty");

    [Fact]
    public void FullUidMaximumIsReportedForTheCallerToRefuseBeforeRandomOrUidEffects()
    {
        var owner = Store(Owner(), 6, [Item(3, deposits: [Item(uint.MaxValue)])]);
        Assert.Equal(uint.MaxValue, Capture(owner).MaximumUid);
    }

    private static void Refuses(PlayerItemSnapshot owner, uint first = 1, uint second = 2, string? expected = null)
    {
        Assert.False(LegacyRelicFuseInputs.TryCapture(owner, first, second, out var inputs, out var reason));
        Assert.Null(inputs); Assert.NotEmpty(reason);
        if (expected is not null) Assert.Equal(expected, reason);
    }
}
