using System.Globalization;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Fact]
    public async Task ArrowBladesFirstMonsterCounterPublishesCurrentSightAfterTwoRealPlayerReceives()
    {
        var armed = false;
        var callbacks = 0;
        string? sightAtWater = null;
        uint arrowAtWater = 0;
        await using var fixture = new ArrowLifecycleFixture("monster", onSend: (current, recipient, bytes) =>
        {
            if (armed && recipient == PlayerUid && ArrowLifeVector(bytes, false, true))
            {
                armed = false;
                callbacks++;
                current.Owner.Character.Attributes.TryGetValue("ThundercloudSight", out sightAtWater);
                arrowAtWater = ArrowLifeCounter(current.Owner, "ArrowBlades");
            }
            return ValueTask.CompletedTask;
        });
        for (uint hit = 1; hit <= 2; hit++)
        {
            var previousPlayerHp = fixture.Peer.Character.MinHitPoints;
            await fixture.OrdinaryMagicAsync(); // Two actual player receives, not a seeded Arrow counter.
            Assert.True(fixture.Peer.Character.MinHitPoints < previousPlayerHp);
            Assert.Equal(hit, ArrowLifeCounter(fixture.Owner, "ArrowBlades"));
            Assert.False(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, ArrowLifeWater));
            Assert.False(LegacyArrowBladesFlagRegistration.TryRead(fixture.Owner.Character, ArrowLifeWater, out _));
        }
        Assert.Equal(2U, ArrowLifeCounter(fixture.Owner, "StarFlow"));
        Assert.Equal(TargetPlayerUid.ToString(CultureInfo.InvariantCulture),
            fixture.Owner.Character.Attributes["ThundercloudSight"]); // No previous monster receive/Sight.
        var previousMonsterHp = fixture.PrimaryHitpoints;
        Assert.Equal(100_000L, previousMonsterHp);
        var start = fixture.Order.Count;
        armed = true;
        await fixture.AttackAsync(); // FIRST real monster17400 receive is the third Arrow increment.
        Assert.Equal(1, callbacks);
        Assert.Equal(fixture.PrimaryUid.ToString(CultureInfo.InvariantCulture), sightAtWater);
        Assert.Equal(3U, arrowAtWater);
        Assert.True(fixture.PrimaryHitpoints < previousMonsterHp);
        Assert.Equal(3U, ArrowLifeCounter(fixture.Owner, "ArrowBlades"));
        Assert.Equal(0U, ArrowLifeCounter(fixture.Owner, "StarFlow"));
        Assert.False(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, ArrowLifeFire));
        fixture.AssertRegistration(ArrowLifeWater, ArrowLifeOrigin.AddSeconds(2).DateTime, 2_592_000);
        var ownerFrames = fixture.FramesSince(PlayerUid, start);
        Assert.Single(ParseSpellAnimations(ownerFrames), packet => packet.SpellId == 17_400);
        Assert.DoesNotContain(ParseSpellAnimations(ownerFrames), packet => packet.SpellId == 18_220);
        var waterVector = Array.FindIndex(ownerFrames, bytes => ArrowLifeVector(bytes, false, true));
        var waterIcon = Array.FindIndex(ownerFrames, bytes =>
            ArrowLifeTyped([bytes], (uint)LegacyMsgUpdateDataType.AppendIcon, ArrowLifeWater).Length == 1);
        var warmAnimation = Array.FindIndex(ownerFrames, bytes =>
            LegacySpellAnimationPacket.TryParse(ReadFrame(bytes), out var packet) && packet.SpellId == 17_400);
        Assert.True(waterVector >= 0 && waterVector < waterIcon && waterIcon < warmAnimation);
        ArrowLifeAssertIcon(Assert.Single(ArrowLifeTyped(
            ownerFrames, (uint)LegacyMsgUpdateDataType.AppendIcon, ArrowLifeWater)), 2);
    }
}
