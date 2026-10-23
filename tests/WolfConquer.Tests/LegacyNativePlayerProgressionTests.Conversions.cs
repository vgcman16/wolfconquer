using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativePlayerProgressionTests
{
    public static IEnumerable<object[]> UInt64Controls()
    {
        yield return [0x0000000000000000UL, 0UL];
        yield return [0x8000000000000000UL, 0UL];
        yield return [0x3FF0000000000000UL, 1UL];
        yield return [0xBFF0000000000000UL, ulong.MaxValue];
        yield return [0x3FFC000000000000UL, 1UL];
        yield return [0xBFFC000000000000UL, ulong.MaxValue];
        yield return [0x3FE8000000000000UL, 0UL];
        yield return [0xBFE8000000000000UL, 0UL];
        yield return [0x41F0000000000000UL, 4294967296UL];
        yield return [0xC1F0000000000000UL, 18446744069414584320UL];
        yield return [0x43DFFFFFFFFFFFFFUL, 9223372036854774784UL];
        yield return [0x43E0000000000000UL, 9223372036854775808UL];
        yield return [0x43E0000000000001UL, 9223372036854777856UL];
        yield return [0x43EFFFFFFFFFFFFFUL, 18446744073709549568UL];
        yield return [0x43F0000000000000UL, 0UL];
        yield return [0x43F0000000000001UL, 0UL];
        yield return [0xC3E0000000000000UL, 9223372036854775808UL];
        yield return [0xC3E0000000000001UL, 9223372036854775808UL];
        yield return [0x7FF0000000000000UL, 0UL];
        yield return [0xFFF0000000000000UL, 9223372036854775808UL];
        yield return [0x7FF8000000000000UL, 0UL];
        yield return [0xFFF8000000000000UL, 0UL];
    }

    [Theory]
    [MemberData(nameof(UInt64Controls))]
    public void ExperienceUnsignedConversionMatchesOriginalFrameworkBitPatternControls(ulong bits, ulong expected)
    {
        double input = BitConverter.Int64BitsToDouble(unchecked((long)bits));
        Assert.Equal(expected, LegacyNativeExperienceConversions.UInt64(input));
    }
}
