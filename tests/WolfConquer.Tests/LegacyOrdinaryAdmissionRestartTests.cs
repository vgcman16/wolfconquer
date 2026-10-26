using Xunit.Abstractions;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyOrdinaryAdmissionRestartTests(ITestOutputHelper output)
{
    [Fact]
    public Task ActualFreshProcessDefaultFrameCannotBypassCommittedOrdinaryUidReconciliation() =>
        LegacyNativeHairfaceStorageLoginTests.RunOrdinaryRestartParentAsync(output);
}
