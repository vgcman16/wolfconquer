using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{

    [Fact]
    public async Task RootXpShieldEarnedXpRemovalCallbackRealDeathClearsOldShieldThenEnteredTailAddsCurrentMissingFlag()
    {
        var armed = false;
        var entered = false;
        var afterDeath = -1;
        await using var fixture = new RootMagicEntryFixture(itemEffects:
            new(RightEffect: 0, RingEffect: 0, NeckEffect: 200), itemEffectLifetimeControls: true,
            itemEffectXpControls: true, nativeDeathCasterQueryControls: true,
            onSend: async (current, uid, bytes) =>
            {
                if (!armed || entered || uid != PlayerUid ||
                    !RootManualItemScalar(bytes, LegacyMsgUpdateDataType.XPList, 0)) return;
                entered = true;
                Assert.False(HasLegacyStatusFlag(current.Owner.Character.Attributes, 4));
                Assert.True(HasLegacyStatusFlag(current.Owner.Character.Attributes, 8));
                await current.KillRootItemEffectOwnerAsync(); // Queried peer MA1/raw9999; native /2, not a flag/HP seed.
                Assert.Equal(0L, current.Owner.Character.MinHitPoints);
                Assert.True(HasLegacyStatusFlag(current.Owner.Character.Attributes, 5));
                Assert.False(HasLegacyStatusFlag(current.Owner.Character.Attributes, 8));
                afterDeath = current.Order.Count;
            });
        await fixture.PrepareRootNativeDeathCasterAsync();
        await fixture.QueryRootItemEffectsAsync();
        await fixture.EarnRootItemEffectXpListAsync();
        await fixture.CastAsync(1_000); // Earn the old15-second shield AFTER the800-second XP acquisition.
        Assert.True(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, 4));
        Assert.True(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, 8));
        var oldDeadline = RootManualLifetimeDeadline(fixture, 8);
        await fixture.AdvanceRootItemEffectXpRuntimeAsync(TimeSpan.FromMilliseconds(500));
        var start = fixture.Order.Count;
        armed = true;
        await fixture.CastAsync(1_020, target: PlayerUid, x: 829, y: 583);
        armed = false;
        Assert.True(entered && afterDeath >= start);
        Assert.Equal(0L, fixture.Owner.Character.MinHitPoints);
        Assert.True(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, 5));
        Assert.False(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, 4));
        Assert.True(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, 8));
        Assert.Equal(fixture.Now.AddSeconds(30), RootManualLifetimeDeadline(fixture, 8));
        Assert.NotEqual(oldDeadline, RootManualLifetimeDeadline(fixture, 8));
        foreach (var uid in new[] { PlayerUid, TargetPlayerUid })
        {
            Assert.Equal(new ulong[] { 0, 256 }, RootManualLifetimeVectors(fixture.FramesSince(uid, afterDeath)));
            var animation = Assert.Single(ParseSpellAnimations(fixture.FramesSince(uid, start)),
                packet => packet.SpellId == 1_020 && packet.SpellLevel == 0);
            AssertRootManualPrefixAnimation(animation, 1_020, 0, 829, 583, 30);
        }
        // Source AttachStatus1020 has no new Alive gate after RemoveFlag(XPList)'s callback.
        // This does NOT claim that arbitrary fresh casts after death are admitted.
    }
}
