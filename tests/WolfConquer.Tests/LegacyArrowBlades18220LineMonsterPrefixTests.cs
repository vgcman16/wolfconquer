using System.Globalization;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Fact]
    public async Task ArrowBladesFirstLineMonsterCounterPublishesCurrentSightAfterThreeRealPlayerReceives()
    {
        const int sourceStarFlowCounterFlag = 0x111 | 0x40; // Source integer key337, not two independent bits.
        var armed = false;
        var callbacks = 0;
        string? sightAtCounter = null;
        uint arrowAtCounter = 0;
        uint starAtCounter = 0;
        long monsterHpAtCounter = -1;
        var fireAtCounter = true;
        LegacyArrowBladesFlagRegistration? waterAtCounter = null;
        await using var fixture = new ArrowLifecycleFixture("monster", onSend: (current, recipient, bytes) =>
        {
            var entries = ArrowLifeTyped([bytes],
                (uint)LegacyMsgUpdateDataType.SageSpeedModeCounter, sourceStarFlowCounterFlag);
            if (armed && recipient == PlayerUid && entries is [{ Damage: 1 }])
            {
                armed = false;
                callbacks++;
                current.Owner.Character.Attributes.TryGetValue("ThundercloudSight", out sightAtCounter);
                arrowAtCounter = ArrowLifeCounter(current.Owner, "ArrowBlades");
                starAtCounter = ArrowLifeCounter(current.Owner, "StarFlow");
                monsterHpAtCounter = current.PrimaryHitpoints;
                fireAtCounter = HasLegacyStatusFlag(current.Owner.Character.Attributes, ArrowLifeFire);
                if (LegacyArrowBladesFlagRegistration.TryRead(current.Owner.Character, ArrowLifeWater, out var water))
                    waterAtCounter = water;
            }
            return ValueTask.CompletedTask;
        });
        for (uint hit = 1; hit <= 3; hit++)
        {
            var previousPlayerHp = fixture.Peer.Character.MinHitPoints;
            await fixture.OrdinaryMagicAsync(); // Real player receives earn333; no preceding monster receive.
            Assert.True(fixture.Peer.Character.MinHitPoints < previousPlayerHp);
            Assert.Equal(hit, ArrowLifeCounter(fixture.Owner, "ArrowBlades"));
            Assert.Equal(hit == 3, HasLegacyStatusFlag(fixture.Owner.Character.Attributes, ArrowLifeWater));
        }
        var originalWater = fixture.AssertRegistration(ArrowLifeWater, ArrowLifeOrigin.AddSeconds(2).DateTime, 2_592_000);
        Assert.Equal(0U, ArrowLifeCounter(fixture.Owner, "StarFlow"));
        Assert.Equal(TargetPlayerUid.ToString(CultureInfo.InvariantCulture),
            fixture.Owner.Character.Attributes["ThundercloudSight"]);
        Assert.Equal(100_000L, fixture.PrimaryHitpoints);
        Assert.False(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, ArrowLifeFire));
        var start = fixture.Order.Count;
        armed = true;
        await fixture.AttackAsync(); // FIRST monster receive is the333-selected18220 LINE branch at W3.
        Assert.Equal(1, callbacks);
        Assert.Equal(fixture.PrimaryUid.ToString(CultureInfo.InvariantCulture), sightAtCounter);
        Assert.Equal(3U, arrowAtCounter);
        Assert.Equal(1U, starAtCounter);
        Assert.Equal(100_000L, monsterHpAtCounter); // Update publishes BEFORE this monster's HP mutation.
        Assert.False(fireAtCounter);
        Assert.Equal(originalWater, waterAtCounter);
        var owner = fixture.FramesSince(PlayerUid, start);
        var animation = Assert.Single(ParseSpellAnimations(owner), packet => packet.SpellId == 18_220);
        var target = Assert.Single(animation.Targets);
        Assert.Equal(fixture.PrimaryUid, target.Uid);
        Assert.Equal(1_200U, target.Damage); // Source literal neutral MAX300% nonplayer scalar.
        Assert.True(target.Hit);
        Assert.Equal(MsgAttackEffect.None, target.Effect);
        Assert.Equal(98_800L, fixture.PrimaryHitpoints);
        Assert.Equal(0U, ArrowLifeCounter(fixture.Owner, "ArrowBlades"));
        Assert.Equal(1U, ArrowLifeCounter(fixture.Owner, "StarFlow"));
        fixture.AssertRegistration(ArrowLifeFire, ArrowLifeOrigin.AddSeconds(3).DateTime, 10);
        var renewedWater = fixture.AssertRegistration(ArrowLifeWater, ArrowLifeOrigin.AddSeconds(3).DateTime, 2_592_000);
        Assert.NotEqual(originalWater.Identity, renewedWater.Identity);
        var counter = Assert.Single(ArrowLifeTyped(owner,
            (uint)LegacyMsgUpdateDataType.SageSpeedModeCounter, sourceStarFlowCounterFlag));
        ArrowLifeAssertIcon(counter, 1);
        var counterIndex = Array.FindIndex(owner, bytes => ArrowLifeTyped([bytes],
            (uint)LegacyMsgUpdateDataType.SageSpeedModeCounter, sourceStarFlowCounterFlag).Length == 1);
        var animationIndex = Array.FindIndex(owner, bytes =>
            LegacySpellAnimationPacket.TryParse(ReadFrame(bytes), out var packet) && packet.SpellId == 18_220);
        Assert.True(counterIndex >= 0 && counterIndex < animationIndex);
        Assert.DoesNotContain(ParseSpellAnimations(owner), packet => packet.SpellId == 17_400);
        Assert.DoesNotContain(owner, bytes => MsgAttackPacket.TryParse(ReadFrame(bytes), out var packet) &&
            packet.Query.AttackType is MsgAttackType.Physical or MsgAttackType.Archer);
    }
}
