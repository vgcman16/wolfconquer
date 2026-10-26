using Xunit.Abstractions;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyMailboxPackagePirateChildTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData("Mode5")]
    [InlineData("ClaimAll")]
    public void RegisteredD13PiratePackageUsesGenuineLoadedNativeContractsAndCommitsExactlyOnce(string scenario)
    {
        var result = LegacyNativeCombatGearLoadChildFixture.Run(scenario, output, knownEntry: "ExecuteMailboxPackage");
        Assert.True(result.GetProperty("Passed").GetBoolean());
        Assert.True(result.GetProperty("Loaded").GetBoolean());
        Assert.Equal(scenario, result.GetProperty("Scenario").GetString());
    }

    [Fact]
    public void RegisteredD13PiratePackageMissingNativeWireRefusesTheEntireActionBeforeAnyIntent()
    {
        var result = LegacyNativeCombatGearLoadChildFixture.Run("MissingWire", output, knownEntry: "ExecuteMailboxPackage");
        Assert.True(result.GetProperty("Passed").GetBoolean());
        Assert.False(result.GetProperty("Loaded").GetBoolean());
        Assert.Equal("MissingWire", result.GetProperty("Scenario").GetString());
    }
}
