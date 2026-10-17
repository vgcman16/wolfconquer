using System.Globalization;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Fact]
    public async Task ArrowBladesPublicMagic18220HonorsNormal500msAdmissionWithoutRenewingRejectedStamp()
    {
        await using var fixture = new ArrowLifecycleFixture(sourcePassiveArrow: true);
        var effect = Assert.IsType<WolfConquer.Core.Data.MagicEffect>(fixture.SourceArrowEffect);
        Assert.Equal(1_822_000L, effect.Id);
        Assert.True(effect.DescriptionEx!.Contains("Passive", StringComparison.Ordinal)); // Source MagicType340.
        await fixture.EarnWaterAsync();
        Assert.Equal((ushort)0, fixture.Owner.LegacyRandomSpell);
        Assert.True(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, ArrowLifeWater));
        await fixture.Cast18220MagicAsync(); // W3: real first Magic; Arrow3 restores333.
        var firstAt = ArrowLifeOrigin.AddSeconds(3);
        var stamp = firstAt.ToString("O", CultureInfo.InvariantCulture);
        Assert.Equal(stamp, fixture.Owner.Character.Attributes["LegacyAttackStampUtc"]);
        Assert.Equal((ushort)0, fixture.Owner.LegacyRandomSpell);
        var fire = fixture.AssertRegistration(ArrowLifeFire, firstAt.DateTime, 10);
        var water = fixture.AssertRegistration(ArrowLifeWater, firstAt.DateTime, 2_592_000);
        var hp = fixture.PrimaryHitpoints;
        foreach (var elapsed in new[] { TimeSpan.Zero, TimeSpan.FromMilliseconds(499) })
        {
            fixture.AdvanceUnstartedArrowInputClock(elapsed);
            var start = fixture.Order.Count;
            await fixture.Cast18220UnspacedAsync(); // Bypass only the TEST input-spacing convenience, not a handler gate.
            Assert.Equal(hp, fixture.PrimaryHitpoints);
            Assert.Equal(0U, ArrowLifeCounter(fixture.Owner, "ArrowBlades"));
            Assert.Equal(stamp, fixture.Owner.Character.Attributes["LegacyAttackStampUtc"]);
            Assert.Equal(fire, fixture.AssertRegistration(ArrowLifeFire, firstAt.DateTime, 10));
            Assert.Equal(water, fixture.AssertRegistration(ArrowLifeWater, firstAt.DateTime, 2_592_000));
            Assert.Empty(ParseSpellAnimations(fixture.FramesSince(PlayerUid, start)));
            Assert.Empty(ArrowLifeTyped(fixture.FramesSince(PlayerUid, start), 137, ArrowLifeFire));
        }
        fixture.AdvanceUnstartedArrowInputClock(TimeSpan.FromMilliseconds(1));
        var admittedStart = fixture.Order.Count;
        await fixture.Cast18220UnspacedAsync();
        var animation = Assert.Single(ParseSpellAnimations(fixture.FramesSince(PlayerUid, admittedStart)),
            packet => packet.SpellId == 18_220);
        Assert.Equal(450U, Assert.Single(animation.Targets).Damage);
        Assert.Equal(hp - 450, fixture.PrimaryHitpoints);
        Assert.Equal(1U, ArrowLifeCounter(fixture.Owner, "ArrowBlades"));
        Assert.Equal(firstAt.AddMilliseconds(500).ToString("O", CultureInfo.InvariantCulture),
            fixture.Owner.Character.Attributes["LegacyAttackStampUtc"]);
        var renewed = fixture.AssertRegistration(ArrowLifeFire, firstAt.AddMilliseconds(500).DateTime, 10);
        Assert.NotEqual(fire.Identity, renewed.Identity);
        ArrowLifeAssertRetired(fixture.Owner, ArrowLifeWater);
    }

    [Fact]
    public async Task ArrowBladesSourcePassiveMagicWithout333OrPendingTokenStopsBeforeNormalStamp()
    {
        await using var fixture = new ArrowLifecycleFixture(sourcePassiveArrow: true);
        var effect = Assert.IsType<WolfConquer.Core.Data.MagicEffect>(fixture.SourceArrowEffect);
        Assert.True(effect.DescriptionEx!.Contains("Passive", StringComparison.Ordinal));
        Assert.Equal((ushort)0, fixture.Owner.LegacyRandomSpell);
        Assert.False(fixture.Owner.Character.Attributes.ContainsKey("LegacyAttackStampUtc"));
        var start = fixture.Order.Count;
        await fixture.Cast18220UnspacedAsync(); // Real initial state: no counter,333,token or desired result seed.
        Assert.False(fixture.Owner.Character.Attributes.ContainsKey("LegacyAttackStampUtc"));
        Assert.Equal((ushort)0, fixture.Owner.LegacyRandomSpell);
        Assert.Equal(5_000, fixture.Owner.Character.MinHitPoints);
        Assert.Equal(100_000, fixture.PrimaryHitpoints);
        Assert.Equal(0U, ArrowLifeCounter(fixture.Owner, "ArrowBlades"));
        Assert.Equal(0U, ArrowLifeCounter(fixture.Owner, "StarFlow"));
        ArrowLifeAssertRetired(fixture.Owner, ArrowLifeFire);
        ArrowLifeAssertRetired(fixture.Owner, ArrowLifeWater);
        Assert.Empty(ParseSpellAnimations(fixture.FramesSince(PlayerUid, start)));
        Assert.Empty(ArrowLifeVectors(fixture.FramesSince(PlayerUid, start)));
        Assert.Null(fixture.Owner.EquipmentHitpointsStatus);
    }

}
