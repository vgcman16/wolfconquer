using Xunit.Abstractions;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyOrdinaryCombatGearLoadedAdmissionTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData("OrdinaryGearValid")]
    [InlineData("OrdinaryGearMissingTables")]
    [InlineData("OrdinaryGearUnknown")]
    [InlineData("OrdinaryGearSchema22Refused")]
    public void RealColdMetadataLoadAndSavedOrdinaryGearRequireActualOwners(string scenario)
    {
        var result = LegacyNativeCombatGearLoadChildFixture.Run(scenario, output, knownEntry: "ExecutePirateRecovery");
        Assert.True(result.GetProperty("Passed").GetBoolean());
        Assert.Equal(scenario, result.GetProperty("Scenario").GetString());
    }
}
