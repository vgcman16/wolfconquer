namespace WolfConquer.Tests;

public sealed partial class MsgItemUsageNonEquipmentItemUseTests
{
    private const uint RuneBagFullUid = 5_349_975;
    private const uint RuneBagFullItemId = 4_020_517;

    [Fact]
    public async Task HandlerRuneBagFullInventoryPreservesUnboundRuneAndAllResources()
    {
        await AssertRuneBagFullInventoryAddFailureAsync(RuneBagFullCase.UnboundFull);
    }

    [Fact]
    public async Task HandlerRuneBagFullInventoryPreservesBoundRuneAndAllResources()
    {
        await AssertRuneBagFullInventoryAddFailureAsync(RuneBagFullCase.BoundFull);
    }

    [Fact]
    public async Task HandlerRuneBagThirtyNineRowsReturnsOneLosslessRegisteredFreshRoot()
    {
        await AssertRuneBagFullInventoryAddFailureAsync(RuneBagFullCase.ThirtyNine);
    }

    [Fact]
    public async Task HandlerRuneBagFullInventoryOtherRuneIsRejectedWithoutSideEffects()
    {
        await AssertRuneBagFullInventoryAddFailureAsync(RuneBagFullCase.OtherItem);
    }

    [Fact]
    public async Task HandlerRuneBagFullInventoryMissingMetadataIsRejectedWithoutSideEffects()
    {
        await AssertRuneBagFullInventoryAddFailureAsync(RuneBagFullCase.MissingMetadata);
    }

    [Fact]
    public async Task HandlerRuneBagFullInventoryWrongPositionIsRejectedWithoutSideEffects()
    {
        await AssertRuneBagFullInventoryAddFailureAsync(RuneBagFullCase.WrongPosition);
    }

    [Fact]
    public async Task HandlerRuneBagFullInventoryUnderAgeIsRejectedWithoutSideEffects()
    {
        await AssertRuneBagFullInventoryAddFailureAsync(RuneBagFullCase.UnderAge);
    }

    private enum RuneBagFullCase
    {
        UnboundFull, BoundFull, ThirtyNine, OtherItem, MissingMetadata, WrongPosition, UnderAge,
    }

    private Task AssertRuneBagFullInventoryAddFailureAsync(RuneBagFullCase scenario) => RunOwnedLegacyRuneBagFullCaseAsync(scenario);
}
