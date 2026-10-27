using Xunit.Abstractions;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyMailboxPirateRecoveryTargetClassTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData("Target7052Mode5", 7052u)]
    [InlineData("Target7052ClaimAll", 7052u)]
    [InlineData("Target7057Mode5", 7057u)]
    [InlineData("Target7057ClaimAll", 7057u)]
    public void TargetPirateStagesUseRealFreshTableLoadColdRecoveryPinJournalRewardsAndReplay(string scenario, uint expectedClass)
    {
        var result = LegacyNativeCombatGearLoadChildFixture.Run(scenario, output, knownEntry: "ExecutePirateRecovery");
        Assert.True(result.GetProperty("Passed").GetBoolean());
        Assert.Equal(scenario, result.GetProperty("Scenario").GetString());
        Assert.Equal(expectedClass, result.GetProperty("FixtureClass").GetUInt32());
    }
}
