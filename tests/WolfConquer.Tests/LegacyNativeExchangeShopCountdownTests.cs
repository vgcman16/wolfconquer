using System.Globalization;
using Shop = WolfConquer.Core.World.LegacyNativeExchangeShopRuntime;

namespace WolfConquer.Tests;

public sealed class LegacyNativeExchangeShopCountdownTests
{
    [Theory]
    [InlineData("2026-10-23T00:00:00.0000000", 86400u)]
    [InlineData("2026-10-23T00:00:00.0000001", 86400u)]
    [InlineData("2026-10-23T00:00:00.9999999", 86400u)]
    [InlineData("2026-10-23T00:00:01.0000000", 86399u)]
    [InlineData("2026-10-23T12:00:00.0000000", 43200u)]
    [InlineData("2026-10-23T12:00:00.0000001", 43200u)]
    [InlineData("2026-10-23T23:58:59.0000000", 61u)]
    [InlineData("2026-10-23T23:59:00.0000000", 60u)]
    [InlineData("2026-10-23T23:59:58.0000000", 2u)]
    [InlineData("2026-10-23T23:59:59.0000000", 1u)]
    [InlineData("2026-10-23T23:59:59.9999999", 1u)]
    [InlineData("2026-10-24T00:00:00.0000000", 86400u)]
    [InlineData("2026-12-31T23:59:59.5000000", 1u)]
    [InlineData("2027-01-01T00:00:00.0000000", 86400u)]
    [InlineData("2024-02-28T23:59:59.9999999", 1u)]
    [InlineData("2024-02-29T00:00:00.0000000", 86400u)]
    [InlineData("2024-02-29T23:59:59.9999999", 1u)]
    [InlineData("2024-03-01T00:00:00.0000000", 86400u)]
    [InlineData("2025-02-28T23:59:59.9999999", 1u)]
    [InlineData("2025-03-01T00:00:00.0000000", 86400u)]
    [InlineData("2026-11-30T23:59:59.9999999", 1u)]
    [InlineData("2026-12-01T00:00:00.0000000", 86400u)]
    public void LiteralCalendarAndSubsecondCasesCountToMidnightNotOneMinuteLater(string text, uint expected)
    {
        var now = DateTime.ParseExact(text, "yyyy-MM-dd'T'HH:mm:ss.fffffff", CultureInfo.InvariantCulture, DateTimeStyles.None);
        Assert.Equal(expected, Shop.RemainingUntilNextDailyReset(now));
    }

    [Theory]
    [InlineData(DateTimeKind.Unspecified)]
    [InlineData(DateTimeKind.Local)]
    [InlineData(DateTimeKind.Utc)]
    public void PureCalculationUsesSuppliedCalendarFieldsWithoutTimezoneConversion(DateTimeKind kind)
    {
        var now = new DateTime(2026, 3, 8, 2, 30, 0, kind);
        Assert.Equal(77400u, Shop.RemainingUntilNextDailyReset(now));
    }

    [Fact]
    public void BoundedTimeOfDayArithmeticDoesNotNeedAnUnrepresentableNextDateAtDateTimeExtrema()
    {
        Assert.Equal(86400u, Shop.RemainingUntilNextDailyReset(DateTime.MinValue));
        Assert.Equal(1u, Shop.RemainingUntilNextDailyReset(DateTime.MaxValue));
    }
}
