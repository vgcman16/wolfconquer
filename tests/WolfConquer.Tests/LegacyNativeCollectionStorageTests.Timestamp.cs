using Message = WolfConquer.Core.World.LegacyNativeMsgCollectionStorage;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeCollectionStorageTests
{
    [Fact]
    public void TimestampUsesCurrentLocalWallClockSecondsSinceUnspecifiedEpoch()
    {
        int before = LocalSeconds();
        int actual = Message.Timestamp();
        int after = LocalSeconds();
        Assert.InRange(actual, Math.Min(before, after), Math.Max(before, after));
    }

    [Fact]
    public void ZeroAndAlreadyExpiredEpochValuesProduceZeroRemainingTime()
    {
        int now = LocalSeconds();
        Assert.True(now > 10, "These expired-epoch controls require the current post-1970 local test clock.");
        Assert.Equal(0u, Message.Timestamp(0));
        Assert.Equal(0u, Message.Timestamp(1));
        Assert.Equal(0u, Message.Timestamp((uint)(now - 10)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FutureAndUnsignedMaximumExpirationUseUncappedMixedSignedUnsignedSubtraction(bool maximum)
    {
        int before = LocalSeconds();
        uint expiration = maximum ? uint.MaxValue : checked((uint)((long)before + 600));
        uint actual = Message.Timestamp(expiration);
        int after = LocalSeconds();
        RemainingIsBetween(actual, expiration, before, after);
        if (maximum) Assert.True(actual > int.MaxValue);
    }
}
