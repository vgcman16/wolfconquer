using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyInventorySashUpgradeTests
{
    [Theory]
    [InlineData(LegacyWarehouseAction.DepositItem, 8U, 60)]
    [InlineData(LegacyWarehouseAction.InventorySashDepositItem, 1_293_600U, 2)]
    [InlineData(LegacyWarehouseAction.DepositItem, 1_293_600U, 2)]
    public Task RegisteredWarehouseFamilyFullDestinationCountsRootsAndOwnUidAliasCannotBypassPurchasedCap(
        LegacyWarehouseAction action, uint bank, int capacity) => RunAsync(async fixture =>
    {
        fixture.SeedWarehouseFamily(action);
        fixture.FillWarehouseFamilyBank(bank, capacity);
        Assert.Equal(capacity, fixture.Current.PlayerItems.WarehouseItems.Count(item => item.WarehouseId == bank));
        var before = fixture.Capture();

        await fixture.InvokeWarehouseFamilyAsync(action, bank);

        fixture.AssertUnchanged(before);
    });

    [Theory]
    [InlineData("0")]
    [InlineData("301")]
    [InlineData("not-a-count")]
    public Task RegisteredInventoryWarehouseDepositZeroOrInvalidPurchasedCapacityCannotMove(string value) => RunAsync(async fixture =>
    {
        const LegacyWarehouseAction action = LegacyWarehouseAction.InventorySashDepositItem;
        fixture.SeedWarehouseFamily(action);
        fixture.SetWarehouseFamilyCount(value);
        var before = fixture.Capture();

        await fixture.InvokeWarehouseFamilyAsync(action);

        fixture.AssertUnchanged(before);
    });

    [Fact]
    public Task RegisteredWarehouseFamilyFortySlotInventoryRefusesWithdrawalWithoutPartialRemoval() => RunAsync(async fixture =>
    {
        const LegacyWarehouseAction action = LegacyWarehouseAction.WithdrawItem_WH_House;
        fixture.SeedWarehouseFamily(action);
        var fillers = Enumerable.Range(0, 39).Select(index => new WolfConquer.Core.Data.PlayerItemSnapshotItem
        { Uid = checked((uint)(50_000 + index)), ItemId = 1_088_001, StackSize = 1 });
        fixture.SaveTransferSetup(fixture.Current.PlayerItems with
        { InventoryItems = fixture.Current.PlayerItems.InventoryItems.Concat(fillers).OrderBy(item => item.Uid).ToArray() });
        Assert.Equal(40, fixture.Current.PlayerItems.InventoryItems.Count);
        var before = fixture.Capture();

        await fixture.InvokeWarehouseFamilyAsync(action);

        fixture.AssertUnchanged(before);
    });

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public Task RegisteredInventoryWarehouseMalformedOrOverfullCapacityStillAllowsRemovingAnExistingOwnedItem(bool malformed) => RunAsync(async fixture =>
    {
        const LegacyWarehouseAction action = LegacyWarehouseAction.InventorySashWithdrawItem;
        fixture.SeedWarehouseFamily(action);
        fixture.SetWarehouseFamilyCount(malformed ? "not-a-count" : "0");
        var before = fixture.Capture();

        await fixture.InvokeWarehouseFamilyAsync(action);

        AssertWarehouseFamilyCommitted(fixture, before, action);
        AssertWarehouseFamilyFrames(fixture, before, action);
    });
}
