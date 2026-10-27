namespace WolfConquer.Tests;

public sealed partial class LegacyInventorySashUpgradeTests
{
    [Theory]
    [InlineData(5052u, false, false)]
    [InlineData(5052u, true, true)]
    [InlineData(5057u, false, false)]
    [InlineData(5057u, true, true)]
    public Task TargetNinjaStagesUseRealColdRecoveryPinJournalRewardsAndIdempotentReplay(
        uint job, bool claimAll, bool allocated) => RunColdNinjaRecoveryScenarioAsync(claimAll, allocated, job);
}
