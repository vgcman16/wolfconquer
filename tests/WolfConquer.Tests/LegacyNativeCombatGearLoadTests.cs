using Xunit.Abstractions;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyNativeCombatGearLoadTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData("ColumnsAndDuplicates")]
    [InlineData("Culture")]
    [InlineData("RawPrefix")]
    [InlineData("MissingGear")]
    [InlineData("EmptyGear")]
    [InlineData("SharedGear")]
    [InlineData("LateBlank")]
    [InlineData("LateFormat")]
    [InlineData("LateOverflow")]
    [InlineData("DependenciesContinue")]
    [InlineData("RepeatedLoad")]
    [InlineData("ConflictFirst")]
    [InlineData("ConflictLate")]
    [InlineData("FrozenFirst")]
    [InlineData("RankColumnsAndPrefix")]
    [InlineData("RegisteredWire")]
    [InlineData("SeparatorFault")]
    public void GenuineDefaultRegistryAndNativeLoaderRunInAFreshMeasuredChild(string scenario)
    {
        var result = LegacyNativeCombatGearLoadChildFixture.Run(scenario, output);
        Assert.True(result.GetProperty("Passed").GetBoolean());
        Assert.True(result.GetProperty("Loaded").GetBoolean());
        Assert.Equal(scenario, result.GetProperty("Scenario").GetString());
    }
}
