using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{

    [Fact]
    public async Task PublicFrameShieldWithoutXpListDoesNotDebitOrPublish()
    {
        await using var fixture = new RootMagicEntryFixture(itemEffects:
            new(RightEffect: 0, RingEffect: 0, NeckEffect: 200), itemEffectLifetimeControls: true,
            itemEffectXpControls: true, shieldMana: 40, shieldStamina: 10, shieldCooldown: 1_000, shieldColdTime: 300);
        await fixture.QueryRootItemEffectsThroughFrameAsync();
        var before = fixture.Owner;
        Assert.False(HasLegacyStatusFlag(before.Character.Attributes, 4));
        int start = fixture.Order.Count;
        await fixture.CastShieldThroughFrameAsync();
        Assert.Same(before, fixture.Owner);
        Assert.Equal(before.Character.MinMana, fixture.Owner.Character.MinMana);
        Assert.Equal(before.Character.Attributes["Stamina"], fixture.Owner.Character.Attributes["Stamina"]);
        Assert.Equal(before.MagicCooldowns, fixture.Owner.MagicCooldowns);
        Assert.False(fixture.RootShieldClockExists());
        Assert.Empty(fixture.FramesSince(PlayerUid, start));
        Assert.Empty(fixture.FramesSince(TargetPlayerUid, start));
    }

    [Theory]
    [InlineData("marker")]
    [InlineData("status")]
    public async Task PublicFrameShieldRejectsMalformedUsedFieldsBeforeAnyCost(string kind)
    {
        await using var fixture = new RootMagicEntryFixture(itemEffects:
            new(RightEffect: 0, RingEffect: 0, NeckEffect: 200), itemEffectLifetimeControls: true,
            itemEffectXpControls: true, shieldMana: 40, shieldStamina: 10, shieldCooldown: 1_000, shieldColdTime: 300);
        await fixture.QueryRootItemEffectsThroughFrameAsync();
        await fixture.EarnRootItemEffectXpListAsync();
        fixture.SetMalformedShieldInput(kind);
        var before = fixture.Owner;
        Assert.True(HasLegacyStatusFlag(before.Character.Attributes, 4));
        int start = fixture.Order.Count;
        await fixture.CastShieldThroughFrameAsync();
        Assert.Same(before, fixture.Owner);
        Assert.Equal(before.Character.MinMana, fixture.Owner.Character.MinMana);
        Assert.Equal(before.Character.Attributes["Stamina"], fixture.Owner.Character.Attributes["Stamina"]);
        Assert.Equal(before.MagicCooldowns, fixture.Owner.MagicCooldowns);
        Assert.True(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, 4));
        Assert.False(fixture.RootShieldClockExists());
        Assert.Empty(fixture.FramesSince(PlayerUid, start));
        Assert.Empty(fixture.FramesSince(TargetPlayerUid, start));
    }

    [Fact]
    public async Task PublicFrameExistingShieldKeepsTimerWhenUnusedDurationIsInvalid()
    {
        await using var fixture = new RootMagicEntryFixture(itemEffects:
            new(RightEffect: 0, RingEffect: 0, NeckEffect: 200), itemEffectLifetimeControls: true,
            itemEffectXpControls: true, shieldDuration: uint.MaxValue);
        await fixture.QueryRootItemEffectsThroughFrameAsync();
        await fixture.EarnRootItemEffectXpListAsync();
        await fixture.CastAsync(1_000); // Genuine item-effect producer, not a seeded Shield.
        Assert.True(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, 8));
        var deadline = RootManualLifetimeDeadline(fixture, 8);
        int start = fixture.Order.Count;
        await fixture.CastShieldThroughFrameAsync();
        Assert.False(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, 4));
        Assert.True(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, 8));
        Assert.Equal(deadline, RootManualLifetimeDeadline(fixture, 8));
        Assert.Contains(ParseSpellAnimations(fixture.FramesSince(PlayerUid, start)), packet => packet.SpellId == 1_020);
    }

    [Fact]
    public void RootXpShieldCanonicalProjectionComparesExactMembershipWithoutOrdering()
    {
        var character = CreateSession().Character;
        var first = new Dictionary<string, string>(character.Attributes, StringComparer.Ordinal)
            { ["Opaque"] = "one", ["opaque"] = "two" };
        var reordered = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in first.Reverse()) reordered.Add(pair.Key, pair.Value);
        Assert.True(WorldPacketHandlers.RootXpShieldProjectionMatches(
            character with { Attributes = first }, character with { Attributes = reordered }));
        reordered["opaque"] = "wrong";
        Assert.False(WorldPacketHandlers.RootXpShieldProjectionMatches(
            character with { Attributes = first }, character with { Attributes = reordered }));
    }

    [Fact]
    public void RootXpShieldDurationAllowsZeroAndRejectsOverflowingActualDeadline()
    {
        var clock = new ManualRuntimeTimeProvider(DateTimeOffset.MaxValue.AddSeconds(-1));
        Assert.True(LegacyRootItemEffectFlagClock.CanRepresentDuration(0, clock));
        Assert.True(LegacyRootItemEffectFlagClock.CanRepresentDuration(1, clock));
        Assert.False(LegacyRootItemEffectFlagClock.CanRepresentDuration(2, clock));
        Assert.False(LegacyRootItemEffectFlagClock.CanRepresentDuration(uint.MaxValue, clock));
    }

    [Fact]
    public async Task RootXpShieldGenuineEarnedXpWithoutReentryPublishesShieldAndAnimation()
    {
        await using var fixture = new RootMagicEntryFixture(itemEffects:
            new(RightEffect: 0, RingEffect: 0, NeckEffect: 200), itemEffectLifetimeControls: true,
            itemEffectXpControls: true, opaqueCaseControls: true);
        await fixture.QueryRootItemEffectsThroughFrameAsync();
        Assert.Equal("one", fixture.Owner.Character.Attributes["Opaque"]);
        Assert.Equal("two", fixture.Owner.Character.Attributes["opaque"]);
        await fixture.EarnRootItemEffectXpListAsync();
        Assert.Equal("one", fixture.Owner.Character.Attributes["Opaque"]);
        Assert.Equal("two", fixture.Owner.Character.Attributes["opaque"]);
        Assert.True(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, 4));
        Assert.False(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, 8));
        int start = fixture.Order.Count;
        await fixture.CastAsync(1_020, target: PlayerUid, x: 829, y: 583);
        Assert.False(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, 4));
        Assert.True(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, 8));
        Assert.Equal("one", fixture.Owner.Character.Attributes["Opaque"]);
        Assert.Equal("two", fixture.Owner.Character.Attributes["opaque"]);
        Assert.Equal(fixture.Now.AddSeconds(30), RootManualLifetimeDeadline(fixture, 8));
        foreach (var uid in new[] { PlayerUid, TargetPlayerUid })
        {
            var animation = Assert.Single(ParseSpellAnimations(fixture.FramesSince(uid, start)),
                packet => packet.SpellId == 1_020 && packet.SpellLevel == 0);
            AssertRootManualPrefixAnimation(animation, 1_020, 0, 829, 583, 30);
        }
    }

    [Fact]
    public async Task RootXpShieldZeroDurationRetainsImmediateSourceExpiry()
    {
        await using var fixture = new RootMagicEntryFixture(itemEffects:
            new(RightEffect: 0, RingEffect: 0, NeckEffect: 200), itemEffectLifetimeControls: true,
            itemEffectXpControls: true, shieldDuration: 0);
        await fixture.QueryRootItemEffectsThroughFrameAsync();
        await fixture.EarnRootItemEffectXpListAsync();
        int start = fixture.Order.Count;
        await fixture.CastAsync(1_020, target: PlayerUid, x: 829, y: 583);
        Assert.False(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, 4));
        Assert.True(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, 8));
        Assert.True(fixture.RootShieldClockExists());
        Assert.Equal(fixture.Now, RootManualLifetimeDeadline(fixture, 8));
        foreach (var uid in new[] { PlayerUid, TargetPlayerUid })
        {
            var animation = Assert.Single(ParseSpellAnimations(fixture.FramesSince(uid, start)),
                packet => packet.SpellId == 1_020 && packet.SpellLevel == 0);
            AssertRootManualPrefixAnimation(animation, 1_020, 0, 829, 583, 0);
        }
    }

    [Fact]
    public async Task RootXpShieldUnrepresentableDurationPreservesGenuineXpAndResources()
    {
        await using var fixture = new RootMagicEntryFixture(itemEffects:
            new(RightEffect: 0, RingEffect: 0, NeckEffect: 200), itemEffectLifetimeControls: true,
            itemEffectXpControls: true, shieldDuration: uint.MaxValue, shieldMana: 40, shieldStamina: 10);
        await fixture.QueryRootItemEffectsThroughFrameAsync();
        await fixture.EarnRootItemEffectXpListAsync();
        var before = fixture.Owner;
        Assert.True(HasLegacyStatusFlag(before.Character.Attributes, 4));
        Assert.False(HasLegacyStatusFlag(before.Character.Attributes, 8));
        Assert.False(fixture.RootShieldClockExists());
        int start = fixture.Order.Count;
        await fixture.CastAsync(1_020, target: PlayerUid, x: 829, y: 583);
        Assert.Same(before, fixture.Owner);
        Assert.Equal(before.Character.MinMana, fixture.Owner.Character.MinMana);
        Assert.Equal(before.Character.Attributes["Stamina"], fixture.Owner.Character.Attributes["Stamina"]);
        Assert.True(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, 4));
        Assert.False(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, 8));
        Assert.False(fixture.RootShieldClockExists());
        Assert.Empty(fixture.FramesSince(PlayerUid, start));
        Assert.Empty(fixture.FramesSince(TargetPlayerUid, start));
    }

    [Fact]
    public async Task RootXpShieldEarnedXpRemovalCallbackRealDeathStopsStaleShieldContinuation()
    {
        var armed = false;
        var entered = false;
        var afterDeath = -1;
        WorldSession? deadSession = null;
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
                deadSession = current.Owner;
                afterDeath = current.Order.Count;
            });
        await fixture.PrepareRootNativeDeathCasterAsync();
        await fixture.QueryRootItemEffectsThroughFrameAsync();
        await fixture.EarnRootItemEffectXpListAsync();
        await fixture.CastAsync(1_000); // Earn the old15-second shield AFTER the800-second XP acquisition.
        Assert.True(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, 4));
        Assert.True(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, 8));
        Assert.Equal(fixture.Now.AddSeconds(15), RootManualLifetimeDeadline(fixture, 8));
        await fixture.AdvanceRootItemEffectXpRuntimeAsync(TimeSpan.FromMilliseconds(500));
        var start = fixture.Order.Count;
        armed = true;
        await fixture.CastAsync(1_020, target: PlayerUid, x: 829, y: 583);
        armed = false;
        Assert.True(entered && afterDeath >= start);
        Assert.Equal(0L, fixture.Owner.Character.MinHitPoints);
        Assert.True(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, 5));
        Assert.False(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, 4));
        Assert.False(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, 8));
        Assert.Same(deadSession, fixture.Owner);
        Assert.DoesNotContain(WorldPacketHandlers.RuntimeLegacyMagicShieldExpiresAtUtcAttribute, fixture.Owner.Character.Attributes.Keys);
        Assert.DoesNotContain(WorldPacketHandlers.RuntimeLegacyMagicShieldRemoveOnDeadAttribute, fixture.Owner.Character.Attributes.Keys);
        Assert.Single(ParseMsgUpdates(fixture.FramesSince(PlayerUid, start)).SelectMany(packet => packet.Updates),
            entry => entry.Type == LegacyMsgUpdateDataType.XPList && entry.Value == 0);
        foreach (var uid in new[] { PlayerUid, TargetPlayerUid })
        {
            Assert.Empty(RootManualLifetimeVectors(fixture.FramesSince(uid, afterDeath)));
            Assert.DoesNotContain(ParseSpellAnimations(fixture.FramesSince(uid, start)), packet => packet.SpellId == 1_020);
        }
        // XPList consumption preceded the callback and is not rolled back.
        // The real death remains authoritative; no stale Shield success follows it.
    }
}
