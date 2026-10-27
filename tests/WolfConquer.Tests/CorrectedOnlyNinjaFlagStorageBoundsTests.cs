namespace WolfConquer.Tests;

// Never run against old native encoders: the actual eight-byte allocation
// cannot hold the required user frame. Ordinary baseline files exclude this class.
[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class CorrectedOnlyNinjaFlagStorageBoundsTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task FlagPreparationRefusesRealTinyStorageBeforeBytesOrState(bool remove) =>
        LegacyNativeNinjaMutationTests.RunCorrectedOnlyFlagCapacityFailure(remove);
}
