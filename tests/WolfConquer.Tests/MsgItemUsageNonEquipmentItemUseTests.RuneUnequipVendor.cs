using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class MsgItemUsageNonEquipmentItemUseTests
{
    private const string RuneUnequipVendorDialogText = "Sorry, you cannot do this while vending.";

    [Fact]
    public async Task HandlerRuneUnequipActiveVendorMissingUidRejectsBeforeLookup()
    {
        await AssertGuardedRuneUnequipRankingAsync(missingUid: true, activeVendor: true);
    }

    [Fact]
    public async Task HandlerRuneUnequipAfterStopVendingTransfersAndPublishesRank()
    {
        await AssertGuardedRuneUnequipRankingAsync(missingUid: false, activeVendor: true, stopVendor: true);
    }

    private static void AssertRuneUnequipVendorBoxDialog(byte[] frame)
    {
        AssertRuneUnequipMessageBox(frame, fullInventory: false);
    }
}
