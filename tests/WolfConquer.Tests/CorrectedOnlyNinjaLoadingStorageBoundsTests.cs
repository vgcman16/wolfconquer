namespace WolfConquer.Tests;

// NEVER execute on the old native writers: genuine 8-byte allocations would
// be overrun. The oversized-envelope case also avoids old NativeConsole and
// its possible unowned exception-file path. Separate from the ordinary filter.
[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class CorrectedOnlyNinjaLoadingStorageBoundsTests
{
    [Fact]
    public Task ExactFullItemEnvelopeOverflowCannotLeaveEarlierLoadingOutputs() =>
        LegacyNativeNinjaLoadingTests.RunCorrectedOnlyLoadingEnvelopeFailure();

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public Task EveryRequiredLoadingRentalIsValidatedBeforeAnyCopyOrStore(int tinyGroup) =>
        LegacyNativeNinjaLoadingTests.RunCorrectedOnlyLoadingRentalFailure(tinyGroup);

    [Theory]
    [InlineData("add")]
    [InlineData("remove")]
    [InlineData("reborn")]
    [InlineData("clear")]
    [InlineData("update")]
    public Task ActualTinySpellCallerStorageCannotCausePartialMutation(string operation) =>
        LegacyNativeNinjaLoadingTests.RunCorrectedOnlySpellCallerFailure(operation);
}
