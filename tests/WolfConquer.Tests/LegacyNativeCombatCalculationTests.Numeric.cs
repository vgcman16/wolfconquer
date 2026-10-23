using System.Reflection;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeCombatCalculationTests
{
    private static T Numeric<T>(Type owner, string method, params object[] args)
        => (T)owner.GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, args)!;

    [Theory]
    [InlineData(0, 0, 0, 0u)]
    [InlineData(-0.0, 0, 0, 0u)]
    [InlineData(1.25, 1, 0, 1u)]
    [InlineData(-1.25, -1, 0, 4294967295u)]
    [InlineData(99.99, 99, 0, 99u)]
    [InlineData(-99.99, -99, 0, 4294967197u)]
    [InlineData(12345.75, 45, 12, 12345u)]
    [InlineData(-12345.75, -45, -12, 4294954951u)]
    [InlineData(double.Epsilon, 0, 0, 0u)]
    [InlineData(-double.Epsilon, 0, 0, 0u)]
    [InlineData(double.NaN, -2147483648, -2147483648, 0u)]
    [InlineData(double.PositiveInfinity, -2147483648, -2147483648, 0u)]
    [InlineData(double.NegativeInfinity, -2147483648, -2147483648, 0u)]
    [InlineData(2147483647.0, 47, 2147483, 2147483647u)]
    [InlineData(2147483648.0, 48, 2147483, 2147483648u)]
    [InlineData(-2147483648.0, -48, -2147483, 2147483648u)]
    [InlineData(-2147483649.0, -49, -2147483, 2147483647u)]
    [InlineData(4294967295.0, 95, 4294967, 4294967295u)]
    [InlineData(4294967296.0, 96, 4294967, 0u)]
    [InlineData(-4294967295.0, -95, -4294967, 1u)]
    [InlineData(-4294967296.0, -96, -4294967, 0u)]
    [InlineData(2147483647999.9998, 99, 2147483647, 4294967295u)]
    [InlineData(2147483648000.0, 0, -2147483648, 0u)]
    [InlineData(2147483648000.0002, 0, -2147483648, 0u)]
    [InlineData(-2147483648000.0002, 0, -2147483648, 0u)]
    [InlineData(-2147483648000.0, 0, -2147483648, 0u)]
    [InlineData(-2147483647999.9998, -99, -2147483647, 1u)]
    [InlineData(9223372036854774784.0, 84, -2147483648, 4294966272u)]
    [InlineData(9223372036854775808.0, 8, -2147483648, 0u)]
    [InlineData(-9223372036854775808.0, -8, -2147483648, 0u)]
    [InlineData(-9223372036854777856.0, -56, -2147483648, 0u)]
    [InlineData(double.MaxValue, 68, -2147483648, 0u)]
    [InlineData(-double.MaxValue, -68, -2147483648, 0u)]
    [InlineData(4294967295999.9995, 99, -2147483648, 4294967295u)]
    [InlineData(4294967296000.0, 0, -2147483648, 0u)]
    [InlineData(-4294967296000.0005, 0, -2147483648, 0u)]
    public Task GearScopedProductionConversionsMatchLiteralFrameworkRows(double value, int modulo, int divided, uint shield)
        => NativePacketTestThread.Run(() =>
    {
        var type = typeof(LegacyNativeMyCombatGear);
        Assert.Equal(modulo, Numeric<int>(type, "NativeGearAttackInt32", value % 100.0));
        Assert.Equal(divided, Numeric<int>(type, "NativeGearAttackInt32", value / 1000.0));
        Assert.Equal(shield, Numeric<uint>(type, "NativeGearShieldUInt32", value));
    });

    [Fact]
    public Task ActualPhysicalProductionConvertersMatchFullFrameworkVectorDigestNotAnIntegerProductOracle()
        => NativePacketTestThread.Run(() =>
    {
        uint digest = 2166136261; int controls = 0;
        void Add(long value) { digest = unchecked((digest ^ (uint)value) * 16777619u); controls++; }
        int Factor(float value) => Numeric<int>(typeof(LegacyNativeCalculatePhysical), "PhysicalMonsterFactor", value);
        uint Scale(uint damage, uint power) => Numeric<uint>(typeof(LegacyNativeCalculatePhysical), "PhysicalMonsterScale", damage, power);
        float[] factors = [0, -0.0f, 9.9f, 10, 99.9f, 100, -1.25f, 1.25f, float.Epsilon,
            -float.Epsilon, float.NaN, float.PositiveInfinity, float.NegativeInfinity, 2147483520f, 2147483648f,
            -2147483648f, -2147483904f, 4294967296f, -4294967296f, float.MaxValue, -float.MaxValue];
        foreach (float factor in factors) Add(Factor(factor));
        uint[] edges = [0, 1, 2, 99, 100, 101, 125, 2147483647, 2147483648, 4294967040, 4294967294, 4294967295];
        foreach (uint damage in edges) foreach (uint power in edges) Add(Scale(damage, power));
        uint seed = 123456789;
        for (int index = 0; index < 1024; index++)
        {
            seed = unchecked(seed * 1664525u + 1013904223u); uint damage = seed;
            seed = unchecked(seed * 1664525u + 1013904223u); uint power = seed;
            Add(Scale(damage, power));
        }
        Add(Scale(1001, 125)); Add(Scale(uint.MaxValue, uint.MaxValue)); Add(Factor(float.NaN)); Add(Factor(-1.25f));
        Assert.Equal(1193, controls); Assert.Equal(4121549926u, digest);
        Assert.Equal(4037269248u, Scale(uint.MaxValue, uint.MaxValue)); // Exact ulong formula incorrectly gives4037269258.
        Assert.Equal(987842480u, Scale(2147483647, 2147483647)); // Exact integer formula incorrectly gives987842478.
        Assert.Equal(1251u, Scale(1001, 125)); Assert.Equal(int.MinValue, Factor(float.NaN));
    });
}
