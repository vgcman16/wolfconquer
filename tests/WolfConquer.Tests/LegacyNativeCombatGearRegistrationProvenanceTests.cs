using Xunit.Abstractions;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyNativeCombatGearRegistrationProvenanceTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData("RegistrationOwned", true)]
    [InlineData("RegistrationForeignExact", false)]
    [InlineData("RegistrationForeignExactFrozen", false)]
    [InlineData("RegistrationFrozenFactory", true)]
    [InlineData("RegistrationFrozenSurrogate", true)]
    [InlineData("RegistrationFrozenPacked", true)]
    public void OnlyTheActualOwnedSerializedRegistrarCanSupportACompletedLoad(string scenario, bool expectedReady)
    {
        var result = LegacyNativeCombatGearLoadChildFixture.Run(scenario, output);
        Assert.True(result.GetProperty("Passed").GetBoolean());
        Assert.Equal(expectedReady, result.GetProperty("Loaded").GetBoolean());
        Assert.Equal(expectedReady, result.GetProperty("Ready").GetBoolean());
        Assert.Equal(scenario, result.GetProperty("Scenario").GetString());
    }
}
