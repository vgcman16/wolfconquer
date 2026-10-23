using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeMonsterDeathSupportTests
{
    [Theory]
    [InlineData(0u, false, 55)] [InlineData(0u, true, 58)]
    [InlineData(9u, false, 55)] [InlineData(9u, true, 58)]
    [InlineData(10u, false, 35)] [InlineData(10u, true, 35)]
    [InlineData(11u, false, 35)] [InlineData(11u, true, 35)]
    [InlineData(12u, false, 25)] [InlineData(12u, true, 25)]
    [InlineData(13u, false, 100)] [InlineData(13u, true, 100)]
    [InlineData(18u, false, 100)] [InlineData(18u, true, 100)]
    [InlineData(19u, false, 0)] [InlineData(19u, true, 0)]
    [InlineData(20u, false, 55)] [InlineData(20u, true, 58)]
    [InlineData(3007110u, false, 35)] [InlineData(3007112u, true, 25)]
    [InlineData(3007118u, false, 100)] [InlineData(3007119u, true, 0)]
    [InlineData(uint.MaxValue, false, 55)] [InlineData(uint.MaxValue, true, 58)]
    public void UpgradeRateUsesOnlyNativeLastTwoDigitsAndForgingDefault(uint id, bool forging, int rate)
    {
        Assert.Equal((byte)rate, LegacyNativeBaseFunc.AnimaUpgradeRate(id, forging));
    }
    [Fact]
    public void FullRemainderDomainMatchesIndependentLiteralTable()
    {
        byte[] expected = {
            55,55,55,55,55,55,55,55,55,55,35,35,25,100,100,100,100,100,100,0,
            55,55,55,55,55,55,55,55,55,55,55,55,55,55,55,55,55,55,55,55,
            55,55,55,55,55,55,55,55,55,55,55,55,55,55,55,55,55,55,55,55,
            55,55,55,55,55,55,55,55,55,55,55,55,55,55,55,55,55,55,55,55,
            55,55,55,55,55,55,55,55,55,55,55,55,55,55,55,55,55,55,55,55
        };
        foreach (uint prefix in new uint[] { 0, 100, 10000, 1000000000 })
            for (uint remainder = 0; remainder < 100; remainder++)
            {
                Assert.Equal(expected[remainder], LegacyNativeBaseFunc.AnimaUpgradeRate(prefix + remainder));
                byte forged = expected[remainder] == 55 ? (byte)58 : expected[remainder];
                Assert.Equal(forged, LegacyNativeBaseFunc.AnimaUpgradeRate(prefix + remainder, true));
            }
    }
}
