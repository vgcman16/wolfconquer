using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeTime32Tests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(123, 1)]
    [InlineData(-123, -1)]
    [InlineData(int.MaxValue, 1)]
    [InlineData(int.MinValue, -1)]
    [InlineData(0, int.MaxValue)]
    [InlineData(0, int.MinValue)]
    public void CompleteAddFamilyWrapsEveryUnitWithoutMutatingItsReceiver(int start, int amount)
    {
        var value = new LegacyNativeTime32(start);
        // Independent widened arithmetic reduced to the original 32-bit result.
        Assert.Equal(unchecked((int)((long)start + amount)), value.AddMilliseconds(amount).Value);
        Assert.Equal(unchecked((int)((long)start + (long)amount * 1000)), value.AddSeconds(amount).Value);
        Assert.Equal(unchecked((int)((long)start + (long)amount * 60000)), value.AddMinutes(amount).Value);
        Assert.Equal(unchecked((int)((long)start + (long)amount * 3600000)), value.AddHours(amount).Value);
        Assert.Equal(unchecked((int)((long)start + (long)amount * 86400000)), value.AddDays(amount).Value);
        Assert.Equal(start, value.Value);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(999)]
    [InlineData(1000)]
    [InlineData(60001)]
    [InlineData(3600001)]
    [InlineData(86400001)]
    [InlineData(-999)]
    [InlineData(-1000)]
    [InlineData(-60001)]
    [InlineData(int.MaxValue)]
    [InlineData(int.MinValue)]
    public void CompleteAllFamilyDividesSignedMillisecondsTowardZero(int raw)
    {
        var value = new LegacyNativeTime32(raw);
        Assert.Equal(raw, value.AllMilliseconds());
        Assert.Equal(raw / 1000, value.AllSeconds());
        Assert.Equal(raw / 60000, value.AllMinutes());
        Assert.Equal(raw / 3600000, value.AllHours());
        Assert.Equal(raw / 86400000, value.AllDays());
        Assert.Equal(raw, value.Value);
    }
}
