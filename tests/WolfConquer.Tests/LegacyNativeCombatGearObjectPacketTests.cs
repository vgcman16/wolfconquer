using Xunit.Abstractions;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyNativeCombatGearObjectPacketTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData("ColdGate")]
    [InlineData("UnregisteredGear")]
    [InlineData("UnregisteredSkin")]
    [InlineData("UnregisteredOpt")]
    [InlineData("UnregisteredDao")]
    [InlineData("UnregisteredDict")]
    [InlineData("AttributedAirOnly")]
    [InlineData("RegisteredSixFrames")]
    [InlineData("RepeatedRegistration")]
    [InlineData("NullObjects")]
    public void GenuineObjectAndImplicitPacketPathsUseTheActualFreshDefaultModel(string scenario)
    {
        var result = LegacyNativeCombatGearLoadChildFixture.Run(scenario, output, objectPacket: true);
        Assert.True(result.GetProperty("Passed").GetBoolean());
        Assert.Equal(scenario, result.GetProperty("Scenario").GetString());
    }
}
