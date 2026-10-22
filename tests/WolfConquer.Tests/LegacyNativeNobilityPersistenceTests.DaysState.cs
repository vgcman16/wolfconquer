using WolfConquer.Core.World;
using Rank = WolfConquer.Core.World.LegacyNativeNobilityRank;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeNobilityPersistenceTests
{
    [Fact]
    public Task DaysConstructorCapturesUidAndDoesNotInitializePlayerSlotOrOtherState() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        Assert.Null(scope.Player.PayNobilitySystem);
        var days = new LegacyNativeDaysNobility(scope.Player);
        Assert.Equal(17u, days.Identifier); Assert.False(days.IsActive);
        Assert.Equal(0ul, days.LastNobilityDonation); Assert.Equal(Rank.Serf, days.PaidRank);
        Assert.Equal(DateTime.MinValue, days.PeriodTime); Assert.Equal(DateTimeKind.Unspecified, days.PeriodTime.Kind);
        scope.Player.UID = 999;
        Assert.Equal(17u, days.Identifier); Assert.Null(scope.Player.PayNobilitySystem);
        Assert.Throws<NullReferenceException>(() => new LegacyNativeDaysNobility(null!));
        Assert.Equal(12, scope.Available);
    });

    [Fact]
    public Task SetDataAnyWayRetainsFullWidthsUnknownEnumAndDateKindThenResetsEveryDefault() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var days = new LegacyNativeDaysNobility(scope.Player);
        var time = new DateTime(2026, 10, 22, 12, 34, 56, DateTimeKind.Utc);
        days.SetDataAnyWay(uint.MaxValue, ulong.MaxValue, byte.MaxValue, time.ToBinary(), true);
        Assert.Equal(uint.MaxValue, days.Identifier); Assert.Equal(ulong.MaxValue, days.LastNobilityDonation);
        Assert.Equal((Rank)byte.MaxValue, days.PaidRank); Assert.True(days.IsActive);
        Assert.Equal(time.ToBinary(), days.PeriodTime.ToBinary()); Assert.Equal(DateTimeKind.Utc, days.PeriodTime.Kind);
        days.SetDataAnyWay();
        Assert.Equal(0u, days.Identifier); Assert.Equal(0ul, days.LastNobilityDonation);
        Assert.Equal(Rank.Serf, days.PaidRank); Assert.Equal(DateTime.MinValue, days.PeriodTime); Assert.False(days.IsActive);
    });

    [Fact]
    public Task SetDataSkipsEveryZeroAndCannotClearActiveWithFalse() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var days = new LegacyNativeDaysNobility(scope.Player);
        var first = new DateTime(2026, 1, 1);
        var second = new DateTime(2026, 2, 2);
        days.SetDataAnyWay(42, 99, (byte)Rank.King, first.ToBinary(), true);
        days.SetData();
        Assert.Equal(42u, days.Identifier); Assert.Equal(99ul, days.LastNobilityDonation);
        Assert.Equal(Rank.King, days.PaidRank); Assert.Equal(first, days.PeriodTime); Assert.True(days.IsActive);
        days.SetData(43, 101, (byte)Rank.Prince, second.ToBinary(), false);
        Assert.Equal(43u, days.Identifier); Assert.Equal(101ul, days.LastNobilityDonation);
        Assert.Equal(Rank.Prince, days.PaidRank); Assert.Equal(second, days.PeriodTime); Assert.True(days.IsActive);
        days.SetDataAnyWay(act: false);
        days.SetData(act: true);
        Assert.True(days.IsActive); Assert.Equal(0u, days.Identifier); Assert.Equal(0ul, days.LastNobilityDonation);
    });

    [Fact]
    public Task InvalidDateBinaryIsStoredUntilPeriodGetterAndOtherFieldsStayPublished() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var days = new LegacyNativeDaysNobility(scope.Player);
        days.SetDataAnyWay(77, 88, (byte)Rank.Earl, long.MaxValue, true);
        Assert.Equal(77u, days.Identifier); Assert.Equal(88ul, days.LastNobilityDonation);
        Assert.Equal(Rank.Earl, days.PaidRank); Assert.True(days.IsActive);
        Assert.Throws<ArgumentException>(() => _ = days.PeriodTime);
        days.SetData(tick: 0); // Native conditional setter cannot clear the invalid raw value.
        Assert.Throws<ArgumentException>(() => _ = days.PeriodTime);
        days.SetDataAnyWay();
        Assert.Equal(DateTime.MinValue, days.PeriodTime);
    });
}
