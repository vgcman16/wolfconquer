namespace WolfConquer.Tests;

public sealed partial class LegacyInventorySashUpgradeTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public Task ActualD13ColdNinjaPendingLoginLoadsRealOwnerThenVerifiesPinAndReplaysOriginalPackage(bool claimAll, bool allocated) =>
        RunColdNinjaRecoveryScenarioAsync(claimAll, allocated);
}
