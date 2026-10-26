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
    public Task ActualD13FrameWarehouseFamilyCommitsCompleteFilesBeforeEverySuccessFrameAndCannotRepeat(
        LegacyWarehouseAction action) => RunAsync(async fixture =>
    {
        await fixture.EnterWarehouseFamilyFrameAsync(action);
        var before = fixture.CaptureTransferFrame();
        var bank = WarehouseFamilyBank(action);
        var expectedFiles = ExpectedWarehouseFamilyFiles(fixture, before, action, bank);
        var observed = 0;
        fixture.SendObserver = _ =>
        {
            fixture.AssertFiles(expectedFiles);
            observed++; // An assertion swallowed by Frame must not count as a successful observation.
        };

        await fixture.FrameAsync(Fixture.WarehouseFamilyFrameRequest(action));

        Assert.Equal(IsWarehouseFamilyWithdraw(action) ? 2 : 4, observed);
        fixture.AssertWarehouseFamilyFrameResult(before, action);
        fixture.SendObserver = null;
        var frameCount = fixture.Sent.Count;

        await fixture.FrameAsync(Fixture.WarehouseFamilyFrameRequest(action));

        fixture.AssertFiles(expectedFiles);
        Assert.Equal(frameCount, fixture.Sent.Count);
    });

    [WindowsTradeJournalFailureFact]
    public Task ActualD13FrameOrdinaryWarehouseDepositHeldLeafPublishesNothingAndSameEnteredOwnerRetries() =>
        RunWarehouseFamilyFrameSharingFailureAsync(LegacyWarehouseAction.DepositItem);

    [WindowsTradeJournalFailureFact]
    public Task ActualD13FrameInventorySashWithdrawHeldLeafPublishesNothingAndSameEnteredOwnerRetries() =>
        RunWarehouseFamilyFrameSharingFailureAsync(LegacyWarehouseAction.InventorySashWithdrawItem);

    private static Task RunWarehouseFamilyFrameSharingFailureAsync(LegacyWarehouseAction action) =>
        RunAsync(async fixture =>
        {
            await fixture.EnterWarehouseFamilyFrameAsync(action);
            var before = fixture.CaptureTransferFrame();
            fixture.HoldTransferItemFile();

            await fixture.FrameAsync(Fixture.WarehouseFamilyFrameRequest(action));

            fixture.ReleaseFiles(); // Release only after the actual Frame task has terminated.
            fixture.AssertFiles(before.Files);
            Assert.Equal(before.FrameCount, fixture.Sent.Count);
            var expectedFiles = ExpectedWarehouseFamilyFiles(fixture, before, action, WarehouseFamilyBank(action));
            var observed = 0;
            fixture.SendObserver = _ =>
            {
                fixture.AssertFiles(expectedFiles);
                observed++;
            };

            // No re-login, replacement Store owner, setup save, or recovery reset between attempts.
            await fixture.FrameAsync(Fixture.WarehouseFamilyFrameRequest(action));

            Assert.Equal(IsWarehouseFamilyWithdraw(action) ? 2 : 4, observed);
            fixture.AssertWarehouseFamilyFrameResult(before, action);
            fixture.SendObserver = null;
        });
}
