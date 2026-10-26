using Xunit.Abstractions;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyNativeCombatGearWireOptionLoadTests(ITestOutputHelper output)
{
    [Fact]
    public void CorrectTagsAndMembersWithPackedRepeatedUInt32CannotAuthorizeNativeReadiness()
    {
        var result = LegacyNativeCombatGearLoadChildFixture.Run("WirePackedOpt", output);
        Assert.True(result.GetProperty("Passed").GetBoolean());
        Assert.False(result.GetProperty("Loaded").GetBoolean());
        Assert.False(result.GetProperty("Ready").GetBoolean());
        Assert.Equal("WirePackedOpt", result.GetProperty("Scenario").GetString());
    }
}
