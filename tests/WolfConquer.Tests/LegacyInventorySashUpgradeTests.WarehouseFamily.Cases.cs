using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyInventorySashUpgradeTests
{
    [Theory]
    [InlineData(LegacyWarehouseAction.DepositItem)]
    [InlineData(LegacyWarehouseAction.WithdrawItem)]
    [InlineData(LegacyWarehouseAction.DepositItem_WH_House)]
    [InlineData(LegacyWarehouseAction.WithdrawItem_WH_House)]
    [InlineData(LegacyWarehouseAction.InventorySashDepositItem)]
    [InlineData(LegacyWarehouseAction.InventorySashWithdrawItem)]
    public Task RegisteredWarehouseFamilyAllSixRoutesPersistCompleteContainerBeforeExactLegacySuccess(
        LegacyWarehouseAction action) => RunAsync(async fixture =>
    {
        fixture.SeedWarehouseFamily(action);
        var before = fixture.Capture();
        var observed = 0;
        fixture.SendObserver = _ =>
        {
            AssertWarehouseFamilyCommitted(fixture, before, action);
            observed++;
        };

        await fixture.InvokeWarehouseFamilyAsync(action);

        Assert.Equal(IsWarehouseFamilyWithdraw(action) ? 2 : 4, observed);
        AssertWarehouseFamilyCommitted(fixture, before, action);
        AssertWarehouseFamilyFrames(fixture, before, action);
        fixture.SendObserver = null;
        var committed = fixture.Capture();

        await fixture.InvokeWarehouseFamilyAsync(action);

        fixture.AssertUnchanged(committed);
    });

    [WindowsTradeJournalFailureFact]
    public Task RegisteredOrdinaryWarehouseDepositHeldItemLeafRefusesBeforeSuccessAndSameOwnerRetries() =>
        RunWarehouseFamilySharingFailureAsync(LegacyWarehouseAction.DepositItem);

    [WindowsTradeJournalFailureFact]
    public Task RegisteredInventoryWarehouseWithdrawHeldItemLeafRefusesBeforeSuccessAndSameOwnerRetries() =>
        RunWarehouseFamilySharingFailureAsync(LegacyWarehouseAction.InventorySashWithdrawItem);

    private static Task RunWarehouseFamilySharingFailureAsync(LegacyWarehouseAction action) => RunAsync(async fixture =>
    {
        fixture.SeedWarehouseFamily(action);
        var before = fixture.Capture();
        fixture.HoldTransferItemFile();

        await fixture.InvokeWarehouseFamilyAsync(action);

        fixture.ReleaseFiles(); // Actual operation terminal first; no timeout is treated as a completed producer.
        fixture.AssertUnchanged(before);
        await fixture.InvokeWarehouseFamilyAsync(action);
        AssertWarehouseFamilyCommitted(fixture, before, action);
        AssertWarehouseFamilyFrames(fixture, before, action);
    });

    [Fact]
    public Task RegisteredWarehouseFamilyUnrelatedStoreDiskChangeRefusesUntilExactOwnedLeafRepair() => RunAsync(async fixture =>
    {
        const LegacyWarehouseAction action = LegacyWarehouseAction.WithdrawItem_WH_House;
        fixture.SeedWarehouseFamily(action);
        var original = File.ReadAllBytes(fixture.ItemPath);
        var changedDisk = fixture.Current.PlayerItems with
        { RuneItems = fixture.Current.PlayerItems.RuneItems.Select(item => item.WithStackSize(8)).ToArray() };
        var changedBytes = PlayerItemSnapshotLoader.ToLegacyBytes(changedDisk);
        Assert.NotEqual(Convert.ToHexString(original), Convert.ToHexString(changedBytes));
        File.WriteAllBytes(fixture.ItemPath, changedBytes);
        Assert.Equal(changedBytes, PlayerItemSnapshotLoader.ToLegacyBytes(
            new PlayerItemSnapshotLoader(fixture.Items).LoadByCharacterUid(Fixture.Uid)));
        var before = fixture.Capture();

        await fixture.InvokeWarehouseFamilyAsync(action);

        fixture.AssertUnchanged(before);
        File.WriteAllBytes(fixture.ItemPath, original); // Only our unrelated-store fault is repaired after terminal invocation.
        var repaired = fixture.Capture();
        await fixture.InvokeWarehouseFamilyAsync(action);
        AssertWarehouseFamilyCommitted(fixture, repaired, action);
        AssertWarehouseFamilyFrames(fixture, repaired, action);
    });

    [Theory]
    [InlineData(LegacyWarehouseAction.DepositItem, 7U, 8U)]
    [InlineData(LegacyWarehouseAction.WithdrawItem_WH_House, 7U, 8U)]
    [InlineData(LegacyWarehouseAction.InventorySashDepositItem, 8U, 1_293_600U)]
    [InlineData(LegacyWarehouseAction.InventorySashWithdrawItem, 8U, 1_293_600U)]
    [InlineData(LegacyWarehouseAction.WithdrawItem, 8U, 44U)]
    public Task RegisteredWarehouseFamilyInvalidTargetOrDifferentOwnedBankCannotMoveItems(
        LegacyWarehouseAction action, uint requestBank, uint storedBank) => RunAsync(async fixture =>
    {
        fixture.SeedWarehouseFamily(action, storedBank);
        var before = fixture.Capture();

        await fixture.InvokeWarehouseFamilyAsync(action, requestBank);

        fixture.AssertUnchanged(before);
    });

    [Theory]
    [InlineData(LegacyWarehouseAction.DepositItem)]
    [InlineData(LegacyWarehouseAction.DepositItem_WH_House)]
    [InlineData(LegacyWarehouseAction.InventorySashDepositItem)]
    public Task RegisteredWarehouseFamilyArrestTokenCannotEnterAnyDestinationFamily(LegacyWarehouseAction action) => RunAsync(async fixture =>
    {
        fixture.SeedWarehouseFamily(action, moving: TransferRow().WithItemId(711_624));
        var before = fixture.Capture();

        await fixture.InvokeWarehouseFamilyAsync(action);

        fixture.AssertUnchanged(before);
    });

    [Fact]
    public Task RegisteredWarehouseFamilyParsedContainerCannotHideAnArrestTokenFromDepositAdmission() => RunAsync(async fixture =>
    {
        const LegacyWarehouseAction action = LegacyWarehouseAction.DepositItem;
        var container = WarehouseFamilyMovingItem().WithDepositedItems([TransferRow(12).WithItemId(711_624)]);
        fixture.SeedWarehouseFamily(action, moving: container);
        var parsedContainer = Assert.Single(fixture.Current.PlayerItems.InventoryItems, item => item.Uid == WarehouseFamilyItemUid);
        Assert.Equal(1_100_009U, parsedContainer.ItemId);
        Assert.Equal(711_624U, Assert.Single(parsedContainer.DepositedItems).ItemId);
        var before = fixture.Capture();

        await fixture.InvokeWarehouseFamilyAsync(action);

        fixture.AssertUnchanged(before);
    });

    [Fact]
    public Task RegisteredWarehouseFamilyAlreadyStoredContainerWithArrestTokenCanExitToInventory() => RunAsync(async fixture =>
    {
        const LegacyWarehouseAction action = LegacyWarehouseAction.WithdrawItem;
        var container = WarehouseFamilyMovingItem().WithDepositedItems([TransferRow(12).WithItemId(711_624)]);
        fixture.SeedWarehouseFamily(action, moving: container);
        var before = fixture.Capture();

        await fixture.InvokeWarehouseFamilyAsync(action);

        AssertWarehouseFamilyCommitted(fixture, before, action);
        AssertWarehouseFamilyFrames(fixture, before, action);
        Assert.Equal(711_624U, Assert.Single(Assert.Single(fixture.Current.PlayerItems.InventoryItems,
            item => item.Uid == WarehouseFamilyItemUid).DepositedItems).ItemId);
    });

    [Fact]
    public Task RegisteredInventoryWarehouseDepositRequiresCurrentSavedPurchasedCapacityNotStaleLiveCount() => RunAsync(async fixture =>
    {
        const LegacyWarehouseAction action = LegacyWarehouseAction.InventorySashDepositItem;
        fixture.SeedWarehouseFamily(action);
        var original = File.ReadAllBytes(fixture.CharacterPath);
        WriteSashField(fixture, "InventorySashCount", "3"); // The real file changes, while live purchased capacity remains two.
        Assert.Equal("2", fixture.Current.Character.Attributes["InventorySashCount"]);
        Assert.Equal("3", CharacterSnapshotParser.ParseText(File.ReadAllText(fixture.CharacterPath)).Attributes["InventorySashCount"]);
        var before = fixture.Capture();

        await fixture.InvokeWarehouseFamilyAsync(action);

        fixture.AssertUnchanged(before);
        File.WriteAllBytes(fixture.CharacterPath, original);
        var repaired = fixture.Capture();
        await fixture.InvokeWarehouseFamilyAsync(action);
        AssertWarehouseFamilyCommitted(fixture, repaired, action);
        AssertWarehouseFamilyFrames(fixture, repaired, action);
    });
}
