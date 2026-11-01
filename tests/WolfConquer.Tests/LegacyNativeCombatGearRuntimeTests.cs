using Xunit.Abstractions;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyNativeCombatGearRuntimeTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData("RuntimeConfiguration")]
    [InlineData("RuntimePublication")]
    [InlineData("RuntimeOwnedFrame")]
    [InlineData("RuntimeExtendedSchemaHeld")]
    public void ExplicitRuntimeStartupUsesTheActualColdPublisherAndCurrentOwnedFrame(string scenario)
    {
        var result = LegacyNativeCombatGearLoadChildFixture.Run(scenario, output);
        Assert.True(result.GetProperty("Passed").GetBoolean());
        Assert.Equal(scenario, result.GetProperty("Scenario").GetString());
    }
}
