using Xunit.Abstractions;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyNativeCombatGearEnumWireTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData("EnumNonzeroLiterals")]
    [InlineData("EnumForeignMapped")]
    [InlineData("EnumPassthruMutation")]
    [InlineData("EnumMappingMutation")]
    public void AssociatedNativeEnumContractsPreserveLiteralValuesAndCurrentAuthority(string scenario)
    {
        var result = LegacyNativeCombatGearLoadChildFixture.Run(scenario, output);
        Assert.True(result.GetProperty("Passed").GetBoolean());
        Assert.Equal(scenario != "EnumForeignMapped", result.GetProperty("Loaded").GetBoolean());
        if (scenario == "EnumNonzeroLiterals") Assert.True(result.GetProperty("Ready").GetBoolean());
        if (scenario == "EnumForeignMapped") Assert.False(result.GetProperty("Ready").GetBoolean());
        Assert.Equal(scenario, result.GetProperty("Scenario").GetString());
    }
}
