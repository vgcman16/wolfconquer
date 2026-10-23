using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeItemRemainingTimeTests
{
    [Theory]
    [InlineData(0u)]
    [InlineData(2u)]
    [InlineData(uint.MaxValue)]
    public Task WholeInactiveGetterReturnsZeroWithoutMutatingActivationExpiryOrMinuteStorage(uint activate) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var item = new LegacyNativeGameItemRuntime { Activate=activate,EndDate=DateTime.MaxValue,TimeLeftInMinutes=123 };
        Assert.Equal(0u, item.RemainingTime); Assert.Equal(activate, item.Activate); Assert.Equal(DateTime.MaxValue, item.EndDate);
        Assert.Equal(activate == 0 ? 123u : 0u, item.TimeLeftInMinutes); Assert.Empty(scope.Native.Queue);
    });

    [Theory]
    [InlineData(-3600L)]
    [InlineData(-1L)]
    [InlineData(1L)]
    [InlineData(2147483648L)]
    [InlineData(4294967295L)]
    [InlineData(4294967296L)]
    [InlineData(4294967297L)]
    [InlineData(-4294967295L)]
    [InlineData(-4294967296L)]
    [InlineData(-4294967297L)]
    public Task WholeActiveGetterUsesTheSourceFiniteTimeExpressionAcrossSignedAndUIntWrapBoundaries(long seconds) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); DateTime end = EndAfter(seconds);
        var item = new LegacyNativeGameItemRuntime { Activate=1,EndDate=end,TimeLeftInMinutes=123 };
        DateTime before = DateTime.Now; uint actual = item.RemainingTime; DateTime after = DateTime.Now;
        AssertClockWindow(actual, end, before, after);
        Assert.Equal(1u, item.Activate); Assert.Equal(end, item.EndDate); Assert.Equal(0u, item.TimeLeftInMinutes);
        Assert.Empty(scope.Native.Queue);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task WholeGetterHandlesRealMinimumAndMaximumExpiryDatesWithoutClampingOrSigned64Overflow(bool maximum) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); DateTime end = maximum ? DateTime.MaxValue : DateTime.MinValue;
        var item = new LegacyNativeGameItemRuntime { Activate=1,EndDate=end };
        DateTime before = DateTime.Now; uint actual = item.RemainingTime; DateTime after = DateTime.Now;
        AssertClockWindow(actual, end, before, after);
        Assert.Equal(end, item.EndDate); Assert.Equal(1u, item.Activate); Assert.Empty(scope.Native.Queue);
    });
}
