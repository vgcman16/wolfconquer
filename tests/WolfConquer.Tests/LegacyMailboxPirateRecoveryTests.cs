using Xunit.Abstractions;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyMailboxPirateRecoveryTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData("Mode5")]
    [InlineData("ClaimAll")]
    [InlineData("MissingTables")]
    [InlineData("BadTables")]
    [InlineData("MetadataMiss")]
    [InlineData("DuplicateItem")]
    [InlineData("DuplicateRune")]
    [InlineData("Noncanonical")]
    [InlineData("MissingAnima")]
    [InlineData("OwnerSwap")]
    [InlineData("MetadataSwap")]
    [InlineData("StaleOwner")]
    [InlineData("Publish-valid")]
    [InlineData("Publish-unadmitted")]
    [InlineData("Publish-rebound-player")]
    [InlineData("Publish-rebound-owner")]
    [InlineData("Publish-loaded-phase")]
    [InlineData("Publish-stale-session")]
    [InlineData("Publish-callback-store")]
    [InlineData("Publish-callback-exp")]
    public void RealFreshTableLoadAndNativePendingLoginKeepPirateRecoveryAndRewardsFailClosed(string scenario)
    {
        var result = LegacyNativeCombatGearLoadChildFixture.Run(scenario, output, knownEntry: "ExecutePirateRecovery");
        Assert.True(result.GetProperty("Passed").GetBoolean());
        Assert.Equal(scenario, result.GetProperty("Scenario").GetString());
    }
}
