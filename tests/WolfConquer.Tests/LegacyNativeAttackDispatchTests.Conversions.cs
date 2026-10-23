using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeAttackDispatchTests
{
    // Literal outcomes confirmed by root's owned Framework/x86 source-shaped probe,
    // default and optimized. These prove the narrow three-sort port, not other casts.
    [Theory]
    [InlineData(-1f, 255, 4294967295u)] [InlineData(-.5f, 0, 0u)]
    [InlineData(-255.75f, 1, 4294967041u)] [InlineData(-256f, 0, 4294967040u)] [InlineData(-257.75f, 255, 4294967039u)]
    [InlineData(0f, 0, 0u)] [InlineData(float.Epsilon, 0, 0u)] [InlineData(1.75f, 1, 1u)]
    [InlineData(255.75f, 255, 255u)] [InlineData(256f, 0, 256u)] [InlineData(257.75f, 1, 257u)]
    [InlineData(1020f, 252, 1020u)] [InlineData(16777215f, 255, 16777215u)] [InlineData(16777216f, 0, 16777216u)]
    [InlineData(2147483520f, 128, 2147483520u)] [InlineData(2147483648f, 0, 2147483648u)]
    [InlineData(-2147483520f, 128, 2147483776u)] [InlineData(-2147483648f, 0, 2147483648u)]
    [InlineData(4294967040f, 0, 4294967040u)] [InlineData(4294967296f, 0, 0u)]
    [InlineData(4294967808f, 0, 512u)] [InlineData(-4294967808f, 0, 4294966784u)]
    [InlineData(9223371487098961920f, 0, 0u)] [InlineData(9223372036854775808f, 0, 0u)]
    [InlineData(9223373136366403584f, 0, 0u)] [InlineData(-9223372036854775808f, 0, 0u)]
    [InlineData(-9223373136366403584f, 0, 0u)] [InlineData(float.MaxValue, 0, 0u)] [InlineData(-float.MaxValue, 0, 0u)]
    [InlineData(float.NaN, 0, 0u)] [InlineData(float.PositiveInfinity, 0, 0u)] [InlineData(float.NegativeInfinity, 0, 0u)]
    public void ScopedRestorationAndShieldConversionsRetainAllProbedScalarLiterals(float damage, byte expectedByte, uint expectedUInt)
    {
        Assert.Equal(expectedByte, LegacyNativeAttackConversions.Byte(damage));
        Assert.Equal(expectedUInt, LegacyNativeAttackConversions.UInt32(damage));
    }
}
