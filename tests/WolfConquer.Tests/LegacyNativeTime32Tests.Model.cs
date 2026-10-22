using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyNativeTime32Tests
{
    [Fact]
    public void NativeTime32KeepsSequentialFourByteStorageStaticFieldsAndDefaultNullValue()
    {
        Type type = typeof(LegacyNativeTime32);
        Assert.True(type.IsValueType); Assert.False(type.IsEnum);
        Assert.Equal(LayoutKind.Sequential, type.StructLayoutAttribute!.Value);
        Assert.Equal(4, Marshal.SizeOf<LegacyNativeTime32>());
        var instance = Assert.Single(type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic));
        Assert.Equal("int_0", instance.Name); Assert.Equal(typeof(int), instance.FieldType); Assert.True(instance.IsPrivate);
        Assert.Equal(IntPtr.Zero, Marshal.OffsetOf<LegacyNativeTime32>("int_0"));
        var unused = type.GetField("uint_0", BindingFlags.Static | BindingFlags.NonPublic)!;
        Assert.Equal(typeof(uint), unused.FieldType); Assert.False(unused.IsInitOnly); Assert.True(unused.IsPrivate);
        var nil = type.GetField("NULL", BindingFlags.Static | BindingFlags.Public)!;
        Assert.True(nil.IsInitOnly); Assert.Equal(type, nil.FieldType);
        Assert.NotEqual(0, (int)(type.Attributes & TypeAttributes.BeforeFieldInit));
        Assert.Equal(0, default(LegacyNativeTime32).Value);
        Assert.Equal(0, LegacyNativeTime32.NULL.Value);
        Assert.True(default(LegacyNativeTime32) == LegacyNativeTime32.NULL);
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(1L)]
    [InlineData(-1L)]
    [InlineData(2147483647L)]
    [InlineData(-2147483648L)]
    [InlineData(4294967295L)]
    [InlineData(4294967297L)]
    [InlineData(long.MaxValue)]
    [InlineData(long.MinValue)]
    public void EveryNativeConstructorKeepsOnlyTheLowSignedThirtyTwoBits(long input)
    {
        int expected = unchecked((int)input);
        var values = new[] { new LegacyNativeTime32(input), new LegacyNativeTime32(unchecked((uint)input)),
            new LegacyNativeTime32(unchecked((int)input)) };
        Assert.All(values, value =>
        {
            Assert.Equal(expected, value.Value); Assert.Equal(expected, value.TotalMilliseconds);
            Assert.Equal(expected, value.GetHashCode()); Assert.Equal(expected, value.AllMilliseconds());
        });
    }

    [Theory]
    [InlineData(-1, 0, -1)]
    [InlineData(0, 0, 0)]
    [InlineData(1, 0, 1)]
    [InlineData(int.MaxValue, int.MinValue, 1)]
    [InlineData(int.MinValue, int.MaxValue, -1)]
    public void AllOperatorsUseSignedComparisonAndUncheckedSubtraction(int left, int right, int relation)
    {
        var a = new LegacyNativeTime32(left); var b = new LegacyNativeTime32(right);
        Assert.Equal(relation == 0, a == b); Assert.Equal(relation != 0, a != b);
        Assert.Equal(relation > 0, a > b); Assert.Equal(relation < 0, a < b);
        Assert.Equal(relation >= 0, a >= b); Assert.Equal(relation <= 0, a <= b);
        Assert.Equal(unchecked((int)((long)left - right)), (a - b).Value);
        Assert.Equal(left, a.Value); Assert.Equal(right, b.Value);
    }

    [Fact]
    public void EqualityHashAndCopiedMutationRetainNativeValueTypeBehavior()
    {
        var value = new LegacyNativeTime32(-1);
        Assert.True(value.Equals(new LegacyNativeTime32(uint.MaxValue)));
        Assert.False(value.Equals(new LegacyNativeTime32(1)));
        Assert.False(value.Equals(-1)); Assert.False(value.Equals("-1")); Assert.False(value.Equals(null));
        var rows = new Dictionary<LegacyNativeTime32, string> { [value] = "same bits" };
        Assert.Equal("same bits", rows[new LegacyNativeTime32(uint.MaxValue)]);
        LegacyNativeTime32 copy = value;
        copy.Set(2, 3);
        Assert.Equal(5, copy.Value); Assert.Equal(-1, value.Value);
    }

    [Fact]
    public void NativeToStringReadsCurrentCultureRatherThanInvariantFormatting()
    {
        CultureInfo before = CultureInfo.CurrentCulture;
        CultureInfo beforeUi = CultureInfo.CurrentUICulture;
        try
        {
            var custom = (CultureInfo)CultureInfo.InvariantCulture.Clone();
            custom.NumberFormat.NegativeSign = "NEG";
            CultureInfo.CurrentCulture = custom;
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("tr-TR");
            Assert.Equal("NEG123", new LegacyNativeTime32(-123).ToString());
            Assert.Equal("0", LegacyNativeTime32.NULL.ToString());
        }
        finally { CultureInfo.CurrentCulture = before; CultureInfo.CurrentUICulture = beforeUi; }
    }
}
