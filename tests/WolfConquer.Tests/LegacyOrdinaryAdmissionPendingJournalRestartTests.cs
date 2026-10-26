using Xunit.Abstractions;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyOrdinaryAdmissionPendingJournalRestartTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ActualFreshProcessRegistersUidFreePendingFenceFromRealFrameOrRecoveryConstructor(bool frameConstructor) =>
        LegacyNativeHairfaceStorageLoginTests.RunUidFreePendingJournalRestartParentAsync(output, frameConstructor);
}
