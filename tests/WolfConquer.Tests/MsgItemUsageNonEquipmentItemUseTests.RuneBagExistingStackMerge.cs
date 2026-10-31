namespace WolfConquer.Tests;

public sealed partial class MsgItemUsageNonEquipmentItemUseTests
{
    private const uint RuneBagMergeExistingUid = 5_349_950;
    private const uint RuneBagMergeSecondUid = 5_349_951;
    private const uint RuneBagMergeBagUid = 5_349_952;
    private const uint RuneBagMergeFragmentId = 3_306_366;
    private const uint RuneBagMergeOtherFragmentId = 3_306_368;

    [Fact]
    public async Task HandlerRuneBagReturnMergesExistingFragmentStackWithSpace()
    {
        await AssertRuneBagExistingStackMergeAsync(RuneBagMergeCase.Free);
    }

    [Fact]
    public async Task HandlerRuneBagReturnMergesExistingFragmentStackAtFortyInventoryRows()
    {
        await AssertRuneBagExistingStackMergeAsync(RuneBagMergeCase.Full);
    }

    [Fact]
    public async Task HandlerRuneBagReturnDifferentBoundRefusesWithoutChangingEitherRoot()
    {
        await AssertRuneBagExistingStackMergeAsync(RuneBagMergeCase.DifferentBoundFull);
    }

    [Fact]
    public async Task HandlerRuneBagReturnOverCapacityRefusesWithoutChangingEitherRoot()
    {
        await AssertRuneBagExistingStackMergeAsync(RuneBagMergeCase.OverCapacityFull);
    }

    [Fact]
    public async Task HandlerRuneBagReturnMultipleFittingStacksMergesTheLowestUidWithoutAllocating()
    {
        await AssertRuneBagExistingStackMergeAsync(RuneBagMergeCase.MultipleFree);
    }

    [Fact]
    public async Task HandlerRuneBagReturnMissingMetadataRefusesBeforeUidAllocationOrRank()
    {
        await AssertRuneBagExistingStackMergeAsync(RuneBagMergeCase.MissingMetadataFree);
    }

    [Fact]
    public async Task HandlerRuneBagReturnTimedMetadataPreservesTheCompatibleExistingStackWithoutRenewal()
    {
        await AssertRuneBagExistingStackMergeAsync(RuneBagMergeCase.TimedFull);
    }

    [Fact]
    public async Task HandlerRuneBagReturnOtherFragmentFamilyMergesWithoutAnItemAllowlist()
    {
        await AssertRuneBagExistingStackMergeAsync(RuneBagMergeCase.OtherFamilyFull);
    }

    [Fact]
    public async Task HandlerRuneBagReturnPlusMismatchRefusesWithoutDiscardingPayload()
    {
        await AssertRuneBagExistingStackMergeAsync(RuneBagMergeCase.PlusFull);
    }

    [Fact]
    public async Task HandlerRuneBagReturnMaximumDurabilityMismatchRefusesWithoutDiscardingPayload()
    {
        await AssertRuneBagExistingStackMergeAsync(RuneBagMergeCase.MaximumDurabilityFull);
    }

    [Fact]
    public async Task HandlerRuneBagReturnPurificationMismatchRefusesWithoutDiscardingPayload()
    {
        await AssertRuneBagExistingStackMergeAsync(RuneBagMergeCase.PurificationFull);
    }

    [Fact]
    public async Task HandlerRuneBagReturnRefineryMismatchRefusesWithoutDiscardingPayload()
    {
        await AssertRuneBagExistingStackMergeAsync(RuneBagMergeCase.RefineryFull);
    }

    [Fact]
    public async Task HandlerRuneBagReturnUnlockTimerMismatchRefusesWithoutDiscardingPayload()
    {
        await AssertRuneBagExistingStackMergeAsync(RuneBagMergeCase.UnlockTimerFull);
    }

    [Fact]
    public async Task HandlerRuneBagReturnMissingUidPreservesFilesNativeStateAndRank()
    {
        await AssertRuneBagExistingStackMergeAsync(RuneBagMergeCase.MissingUidFree);
    }

    [Fact]
    public async Task HandlerRuneBagReturnWrongPositionPreservesFilesNativeStateAndRank()
    {
        await AssertRuneBagExistingStackMergeAsync(RuneBagMergeCase.WrongPositionFree);
    }

    [Fact]
    public async Task HandlerRuneBagReturnIneligibleCharacterPreservesFilesNativeStateAndRank()
    {
        await AssertRuneBagExistingStackMergeAsync(RuneBagMergeCase.IneligibleFull);
    }

    [Fact]
    public async Task HandlerRuneBagReturnActivationMismatchRefusesWithoutDiscardingPayload()
    {
        await AssertRuneBagExistingStackMergeAsync(RuneBagMergeCase.ActivateFull);
    }

    [Fact]
    public async Task HandlerRuneBagReturnExpirationMismatchRefusesWithoutDiscardingPayload()
    {
        await AssertRuneBagExistingStackMergeAsync(RuneBagMergeCase.ExpirationFull);
    }

    private enum RuneBagMergeCase
    {
        Free, Full, DifferentBoundFull, OverCapacityFull, MultipleFree, MissingMetadataFree, TimedFull, OtherFamilyFull, PlusFull, MaximumDurabilityFull, PurificationFull, RefineryFull, UnlockTimerFull, MissingUidFree, WrongPositionFree, IneligibleFull, ActivateFull, ExpirationFull,
    }

    private Task AssertRuneBagExistingStackMergeAsync(RuneBagMergeCase scenario) => RunOwnedLegacyRuneBagStackCaseAsync(scenario);
}
