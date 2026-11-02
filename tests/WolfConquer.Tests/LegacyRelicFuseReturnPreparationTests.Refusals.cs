using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyRelicFuseReturnPreparationTests
{
    [Theory]
    [InlineData(40)]
    [InlineData(41)]
    public void InventoryCapacityRefusesBeforeAnyDataProjection(int count) =>
        Refuses(Owner(count), expected: "relicFuseReturnInventoryFull");

    [Fact]
    public void ActualHeldIdentityAndExactThreeSlotShapeAreRequired()
    {
        Refuses(null!, expected: "relicFuseReturnOwnerAndUidRequired");
        Refuses(Owner(), 0, "relicFuseReturnOwnerAndUidRequired");
        Refuses(Owner(), 1, "relicFuseReturnSelectedHeldItemRequired");
        Refuses(Owner(), 900, "relicFuseReturnSelectedHeldItemRequired");
        Refuses(Owner(held: new[] { Item(100, 213), Item(101, 214) }), expected: "relicFuseReturnHeldTripleRequired");
        Refuses(Owner(held: new[] { Item(100, 213), Item(101, 214), Item(102, 215), Item(103, 215) }),
            expected: "relicFuseReturnHeldTripleRequired");
        Refuses(Owner(held: new[] { Item(100, 213), Item(101, 213), Item(102, 215) }), expected: "relicFuseReturnHeldTripleRequired");
        Refuses(Owner(held: new[] { Item(100, 216), Item(101, 214), Item(102, 215) }), expected: "relicFuseReturnHeldTripleRequired");
        Refuses(Owner(held: new[] { Item(100, 213, warehouse: 44), Item(101, 214), Item(102, 215) }),
            expected: "relicFuseReturnHeldTripleRequired");
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)] [InlineData(6)]
    public void EveryContainerNullOrDuplicateTailRejectsAfterTheSelectedHeldRoot(int group)
    {
        var owner = Owner();
        var stores = new[] { owner.InventoryItems, owner.RelicFuseItems, owner.MythsoulBagItems,
            owner.EquipmentItems, owner.RuneItems, owner.WardrobeItems, owner.WarehouseItems };
        Refuses(Store(owner, group, null!), expected: "relicFuseReturnItemGraphInvalid");
        Refuses(Store(owner, group, stores[group].Concat(new PlayerItemSnapshotItem[] { null! }).ToArray()),
            expected: "relicFuseReturnItemGraphInvalid");
        Refuses(Store(owner, group, stores[group].Append(Item(100)).ToArray()), expected: "relicFuseReturnItemGraphInvalid");
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)] [InlineData(6)]
    public void EveryDepositedCorruptTailIsValidatedBeforeSavedImageConstruction(int group)
    {
        var owner = Owner();
        var stores = new[] { owner.InventoryItems, owner.RelicFuseItems, owner.MythsoulBagItems,
            owner.EquipmentItems, owner.RuneItems, owner.WardrobeItems, owner.WarehouseItems };
        var parent = Item(500, deposits: new[] { Item(501), Item(0) });
        Refuses(Store(owner, group, stores[group].Append(parent).ToArray()), expected: "relicFuseReturnItemGraphInvalid");
    }

    [Fact]
    public void CyclesAndUnsupportedNestedSavedGraphsDoNotGetSerializedAsValidation()
    {
        var cycle = new List<PlayerItemSnapshotItem>(); var parent = Item(500, deposits: cycle); cycle.Add(parent);
        Refuses(Store(Owner(), 0, new[] { Item(1), parent }), expected: "relicFuseReturnItemGraphInvalid");
        var nested = Item(500, deposits: new[] { Item(501, deposits: new[] { Item(502) }) });
        Refuses(Store(Owner(), 0, new[] { Item(1), nested }), expected: "relicFuseReturnNestedSaveUnsupported");
    }

    [Theory]
    [InlineData("name")]
    [InlineData("signature")]
    [InlineData("nul")]
    [InlineData("surrogate")]
    public void LossyTextIsRefusedByTheSharedCompleteSaveImageValidator(string change)
    {
        var owner = Owner(); var selected = owner.RelicFuseItems[0];
        switch (change)
        {
            case "name": selected.OwnerName = new string('x', 33); break;
            case "signature": selected.Signature = new string('x', 33); break;
            case "nul": selected.OwnerName = "a\0b"; break;
            default: selected.OwnerName = "\uD800"; break;
        }
        Refuses(owner, expected: "relicFuseItemGraphNotPersistable");
    }

    [Fact]
    public void UnrelatedWrongContainerPlacementIsNotSilentlyNormalizedByAClone()
    {
        var owner = Store(Owner(), 3, new[] { Item(500, position: 0) });
        Refuses(owner, expected: "relicFuseItemGraphNotPersistable");
    }

    private static void Refuses(PlayerItemSnapshot owner, uint uid = 100, string? expected = null)
    {
        Assert.False(LegacyRelicFuseReturnPreparation.TryPrepare(owner, uid, out var preparation, out var reason));
        Assert.Null(preparation); Assert.NotEmpty(reason);
        if (expected is not null) Assert.Equal(expected, reason);
    }
}
