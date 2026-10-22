using WolfConquer.Core.World;
using Air = WolfConquer.Core.World.LegacyNativeMsgAirPowerUp;
using Gear = WolfConquer.Core.World.LegacyNativeCombatGear;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeAirPowerTests
{
    [Theory]
    [InlineData(701U)]
    [InlineData(702U)]
    [InlineData(703U)]
    public void InsufficientPointsStillSendUpgradeUsingTheSeparateUseridArgument(uint kind)
    {
        using var load = new LoadScope(true);
        var gear = new Gear();
        var powers = gear.AirPowers;
        powers.Id = 17;
        powers.SenseLevel = 2;
        powers.ArmedLevel = 3;
        powers.OverLoadLevel = 4;
        powers.SenseAipPower = 7;
        powers.ArmedAipPower = 11;
        powers.OverLoadAipPower = 13;
        powers.Points = 1999;
        var request = Practice(kind, 2);

        var packet = Read(Assert.Single(Air.Process(request, gear, 999)));

        Assert.Same(powers, gear.AirPowers);
        Assert.Equal(1999U, powers.Points);
        Assert.Equal(LegacyAirPowerUpType.Upgrade, packet.Type);
        Assert.Equal(999U, packet.UserId);
        Assert.Equal(new LegacyAirPowerUpPower(17, 2, 7, 3, 11, 4, 13, 1999), Assert.Single(packet.Powers));
        Assert.Equal(2U, request.UserId);
        Assert.Equal(kind, request.PowerType);
        Assert.Empty(request.Powers);
    }

    [Fact]
    public void UnknownPracticePowerStillNormalizesSenseAndSendsOneUpgrade()
    {
        using var load = new LoadScope(true);
        var gear = new Gear();
        gear.AirPowers.Points = 123;
        var packet = Read(Assert.Single(Air.Process(Practice(777, uint.MaxValue), gear, 91)));
        Assert.Equal(1U, gear.AirPowers.SenseLevel);
        Assert.Equal(123U, gear.AirPowers.Points);
        Assert.Equal(new LegacyAirPowerUpPower(0, 1, 0, 0, 0, 0, 0, 123), Assert.Single(packet.Powers));
        Assert.Equal(91U, packet.UserId);
    }

    [Theory]
    [InlineData(701U, 1U, 750U, 1250U)]
    [InlineData(702U, 1U, 750U, 1250U)]
    [InlineData(703U, 1U, 750U, 1250U)]
    [InlineData(701U, 2U, 1500U, 2501U)]
    [InlineData(702U, 2U, 1500U, 2501U)]
    [InlineData(703U, 2U, 1500U, 2501U)]
    [InlineData(701U, 3U, 2250U, 3752U)]
    [InlineData(702U, 3U, 2250U, 3752U)]
    [InlineData(703U, 3U, 2250U, 3752U)]
    public void PurchaseDebitsNativeMultiplierAndAddsOnlyTheSelectedRandomPower(
        uint kind, uint multiplier, uint minimum, uint maximum)
    {
        using var load = new LoadScope(true);
        var gear = new Gear();
        var powers = gear.AirPowers;
        powers.SenseLevel = powers.ArmedLevel = powers.OverLoadLevel = 1;
        powers.Points = 5000;

        var packet = Read(Assert.Single(Air.Process(Practice(kind, multiplier), gear, 91)));

        Assert.Equal(5000U - 1000U * multiplier, powers.Points);
        // Native Next(750*m,1251*m) has an EXCLUSIVE upper bound:
        // the inclusive maximum is1251*m-1, not1250*m when m>1.
        Assert.InRange(SelectedPower(powers, kind), minimum, maximum);
        if (kind != 701) Assert.Equal(0U, powers.SenseAipPower);
        if (kind != 702) Assert.Equal(0U, powers.ArmedAipPower);
        if (kind != 703) Assert.Equal(0U, powers.OverLoadAipPower);
        Assert.Equal(1U, powers.SenseLevel);
        Assert.Equal(1U, powers.ArmedLevel);
        Assert.Equal(1U, powers.OverLoadLevel);
        var sent = Assert.Single(packet.Powers);
        Assert.Equal(powers.SenseAipPower, sent.SenseAipPower);
        Assert.Equal(powers.ArmedAipPower, sent.ArmedAipPower);
        Assert.Equal(powers.OverLoadAipPower, sent.OverLoadAipPower);
    }

    [Theory]
    [InlineData(1U, 39999U, 2U, 1U, 0U)]
    [InlineData(2U, 119999U, 3U, 1U, 1U)]
    [InlineData(8U, 827999U, 9U, 1U, 1U)]
    public void SenseThresholdIncrementsOnceThenUnlocksDependentPowers(
        uint level, uint aip, uint expectedLevel, uint armed, uint overload)
    {
        using var load = new LoadScope(true);
        var gear = new Gear();
        gear.AirPowers.SenseLevel = level;
        gear.AirPowers.SenseAipPower = aip;
        gear.AirPowers.Points = 1000;
        Read(Assert.Single(Air.Process(Practice(701, 1), gear, 91)));
        Assert.Equal(expectedLevel, gear.AirPowers.SenseLevel);
        Assert.Equal(armed, gear.AirPowers.ArmedLevel);
        Assert.Equal(overload, gear.AirPowers.OverLoadLevel);
        Assert.Equal(0U, gear.AirPowers.Points);
        Assert.InRange(gear.AirPowers.SenseAipPower - aip, 750U, 1250U);
    }

    [Theory]
    [InlineData(701U, 0U, 1U)]
    [InlineData(702U, 0U, 0U)]
    [InlineData(703U, 9U, 9U)]
    public void ZeroOrHighLevelFallbackAddsPowerWithoutThresholdPromotion(uint kind, uint level, uint expected)
    {
        using var load = new LoadScope(true);
        var gear = new Gear();
        var powers = gear.AirPowers;
        powers.SenseLevel = 1;
        powers.Points = 1000;
        if (kind == 701) { powers.SenseLevel = level; powers.SenseAipPower = 1000000; }
        if (kind == 702) { powers.ArmedLevel = level; powers.ArmedAipPower = 1000000; }
        if (kind == 703) { powers.OverLoadLevel = level; powers.OverLoadAipPower = 1000000; }
        Read(Assert.Single(Air.Process(Practice(kind, 1), gear, 91)));
        Assert.InRange(SelectedPower(powers, kind), 1000750U, 1001250U);
        Assert.Equal(expected, kind == 701 ? powers.SenseLevel : kind == 702 ? powers.ArmedLevel : powers.OverLoadLevel);
        Assert.Equal(0U, powers.Points);
    }

    [Fact]
    public void ZeroMultiplierAllowsThresholdPromotionWithoutSpendingOrRandomGain()
    {
        using var load = new LoadScope(true);
        var gear = new Gear();
        gear.AirPowers.SenseLevel = 2;
        gear.AirPowers.SenseAipPower = 120000;
        gear.AirPowers.Points = 7;
        Read(Assert.Single(Air.Process(Practice(701, 0), gear, 91)));
        Assert.Equal(7U, gear.AirPowers.Points);
        Assert.Equal(120000U, gear.AirPowers.SenseAipPower);
        Assert.Equal(3U, gear.AirPowers.SenseLevel);
        Assert.Equal(1U, gear.AirPowers.ArmedLevel);
        Assert.Equal(1U, gear.AirPowers.OverLoadLevel);
    }

    [Fact]
    public void RandomGainUsesUncheckedUInt32AccumulationBeforeThresholdComparison()
    {
        using var load = new LoadScope(true);
        var gear = new Gear();
        gear.AirPowers.SenseLevel = 1;
        gear.AirPowers.SenseAipPower = uint.MaxValue - 100;
        gear.AirPowers.Points = 1000;
        Read(Assert.Single(Air.Process(Practice(701, 1), gear, 91)));
        Assert.InRange(gear.AirPowers.SenseAipPower, 649U, 1149U);
        Assert.Equal(1U, gear.AirPowers.SenseLevel);
        Assert.Equal(0U, gear.AirPowers.ArmedLevel);
        Assert.Equal(0U, gear.AirPowers.Points);
    }

    [Theory]
    [InlineData(701U)]
    [InlineData(702U)]
    [InlineData(703U)]
    public void SignedRandomBoundFaultHappensAfterPointDebitButBeforePowerMutation(uint kind)
    {
        using var load = new LoadScope(true);
        var gear = new Gear();
        gear.AirPowers.SenseLevel = gear.AirPowers.ArmedLevel = gear.AirPowers.OverLoadLevel = 1;
        gear.AirPowers.Points = uint.MaxValue;
        // 750*2M is positive Int32; 1251*2M narrows negative, making min > max.
        Assert.Throws<ArgumentOutOfRangeException>(() => Air.Process(Practice(kind, 2000000), gear, 91));
        Assert.Equal(2294967295U, gear.AirPowers.Points);
        Assert.Equal(0U, SelectedPower(gear.AirPowers, kind));
        Assert.Equal(1U, gear.AirPowers.SenseLevel);
        Assert.Equal(1U, gear.AirPowers.ArmedLevel);
        Assert.Equal(1U, gear.AirPowers.OverLoadLevel);
    }

    private static Air Practice(uint kind, uint multiplier) => new()
        { Type = Air.Types.PRACTICE, PowerType = kind, UserId = multiplier };

    private static uint SelectedPower(Gear.Powers powers, uint kind) => kind switch
        { 701 => powers.SenseAipPower, 702 => powers.ArmedAipPower, _ => powers.OverLoadAipPower };
}
