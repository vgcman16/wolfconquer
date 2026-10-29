using System.Reflection;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyStaminaValidationTests
{
    private static (bool Valid, ulong Value) Read(IEnumerable<KeyValuePair<string, string>> rows)
    {
        var character = OwnedEquipmentBehaviorFixture.Character() with
            { Attributes = new Dictionary<string, string>(rows, StringComparer.OrdinalIgnoreCase) };
        var method = typeof(WorldPacketHandlers).GetMethod("TryReadLegacyRuntimeStamina", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        object?[] arguments = [character, 0UL];
        return (Assert.IsType<bool>(method.Invoke(null, arguments)), Assert.IsType<ulong>(arguments[1]));
    }

    [Theory]
    [InlineData("0", 0UL)] [InlineData("100", 100UL)] [InlineData("150", 150UL)]
    [InlineData("+120", 120UL)] [InlineData(" \t+120\r\n", 120UL)]
    [InlineData("18446744073709551615", ulong.MaxValue)]
    public void PresentUnsignedIntegerUsesExistingInvariantContractWithoutInventingACap(string raw, ulong expected)
    {
        var result = Read([new("Stamina", raw)]); Assert.True(result.Valid); Assert.Equal(expected, result.Value);
    }

    [Theory]
    [InlineData("")] [InlineData(" ")] [InlineData("-1")] [InlineData("not-a-number")]
    [InlineData("18446744073709551616")] [InlineData("1,000")] [InlineData("1.0")] [InlineData("0x64")]
    public void PresentMalformedValueNeverBecomesBlessingDefaults(string raw)
    {
        var result = Read([new("HeavenBlessing", "1"), new("Stamina", raw)]);
        Assert.False(result.Valid); Assert.Equal(0UL, result.Value);
    }

    [Fact]
    public void GenuineAbsenceRetainsDefaultAndOrderedBlessingAliasRules()
    {
        Assert.Equal((true, 100UL), Read([]));
        Assert.Equal((true, 150UL), Read([new("HeavenBlessing", "1")]));
        Assert.Equal((true, 150UL), Read([new("HeavensBlessing", "1")]));
        Assert.Equal((true, 100UL), Read([new("HeavenBlessing", "0"), new("HeavensBlessing", "1")]));
        Assert.Equal((true, 150UL), Read([new("HeavenBlessing", "bad"), new("HeavensBlessing", "1")]));
        Assert.Equal((true, 100UL), Read([new("HeavenBlessing", "bad")]));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void RowOrderCannotHideRecognizedInvalidStaminaOrOverridePresentValidStamina(bool reverse)
    {
        KeyValuePair<string, string>[] rows = [new("Stamina", "bad"), new("HeavenBlessing", "1"), new("Unrelated", "20")];
        Assert.Equal((false, 0UL), Read(reverse ? rows.Reverse() : rows));
        rows[0] = new("Stamina", "123");
        Assert.Equal((true, 123UL), Read(reverse ? rows.Reverse() : rows));
    }
}
