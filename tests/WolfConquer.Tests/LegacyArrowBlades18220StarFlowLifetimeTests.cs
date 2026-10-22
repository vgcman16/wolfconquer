using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const int ArrowStarLifetimeFlag = 337;
    private const int ArrowStarLifetimeSeconds = 2_592_000;

    [Fact]
    public async Task ArrowBladesRealStarFlow337ExpiresAtInclusiveThirtyDaysWithoutResettingCounters()
    {
        await using var fixture = new ArrowLifecycleFixture();
        await fixture.AttackAsync(); // FIRST real17400 receive earns Arrow1/Star1/337.
        var registration = fixture.AssertRegistration(
            ArrowStarLifetimeFlag, ArrowLifeOrigin.DateTime, ArrowStarLifetimeSeconds);
        Assert.Equal("true", fixture.Owner.Character.Attributes[LegacyStatusFlagDeathRegistration.Key(ArrowStarLifetimeFlag)]);
        Assert.Equal(1U, ArrowLifeCounter(fixture.Owner, "ArrowBlades"));
        Assert.Equal(1U, ArrowLifeCounter(fixture.Owner, "StarFlow"));
        Assert.True(fixture.Peer.Character.MinHitPoints < 100_000);
        var peerHitpoints = fixture.Peer.Character.MinHitPoints;
        await fixture.StartAsync();
        await fixture.AdvanceAsync(TimeSpan.FromDays(30) - TimeSpan.FromMilliseconds(20));
        Assert.Equal(registration, fixture.AssertRegistration(
            ArrowStarLifetimeFlag, ArrowLifeOrigin.DateTime, ArrowStarLifetimeSeconds));
        var start = fixture.Order.Count;
        await fixture.AdvanceAsync(TimeSpan.FromMilliseconds(20));
        ArrowLifeAssertRetired(fixture.Owner, ArrowStarLifetimeFlag);
        Assert.Equal(1U, ArrowLifeCounter(fixture.Owner, "ArrowBlades"));
        Assert.Equal(1U, ArrowLifeCounter(fixture.Owner, "StarFlow"));
        Assert.Equal(5_000, fixture.Owner.Character.MinHitPoints);
        Assert.Equal(peerHitpoints, fixture.Peer.Character.MinHitPoints);
        var owner = fixture.FramesSince(PlayerUid, start);
        var peer = fixture.FramesSince(TargetPlayerUid, start);
        ArrowStarLifetimeAssertOnlyRemovedVector(owner);
        ArrowStarLifetimeAssertOnlyRemovedVector(peer);
        Assert.Equal(Assert.Single(owner), Assert.Single(peer));
    }

    [Fact]
    public async Task ArrowBladesRealSecondStarFlowChargeQuietlyReplaces337AndRenewsItsSourceTimer()
    {
        await using var fixture = new ArrowLifecycleFixture();
        await fixture.AttackAsync();
        var original = fixture.AssertRegistration(
            ArrowStarLifetimeFlag, ArrowLifeOrigin.DateTime, ArrowStarLifetimeSeconds);
        var secondStart = fixture.Order.Count;
        await fixture.OrdinaryMagicAsync(); // Real second receive at W1 replaces337 through AddSpellFlag.
        var renewed = fixture.AssertRegistration(
            ArrowStarLifetimeFlag, ArrowLifeOrigin.AddSeconds(1).DateTime, ArrowStarLifetimeSeconds);
        Assert.NotEqual(original.Identity, renewed.Identity);
        Assert.Equal(2U, ArrowLifeCounter(fixture.Owner, "ArrowBlades"));
        Assert.Equal(2U, ArrowLifeCounter(fixture.Owner, "StarFlow"));
        Assert.False(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, ArrowLifeWater));
        foreach (var uid in new[] { PlayerUid, TargetPlayerUid })
        {
            var vectors = ArrowLifeVectors(fixture.FramesSince(uid, secondStart));
            Assert.NotEmpty(vectors);
            Assert.All(vectors, bytes => Assert.True(ArrowStarLifetimeVectorHas(bytes, active: true)));
        }
        ArrowLifeAssertIcon(Assert.Single(ArrowLifeTyped(
            fixture.FramesSince(PlayerUid, secondStart), 136, ArrowStarLifetimeFlag)), 2);
        Assert.Empty(ArrowLifeTyped(fixture.FramesSince(TargetPlayerUid, secondStart), 136, ArrowStarLifetimeFlag));
        await fixture.StartAsync();
        await fixture.AdvanceAsync(TimeSpan.FromDays(30) - TimeSpan.FromSeconds(1)); // OLD W0 deadline, not renewed W1.
        Assert.Equal(renewed, fixture.AssertRegistration(
            ArrowStarLifetimeFlag, ArrowLifeOrigin.AddSeconds(1).DateTime, ArrowStarLifetimeSeconds));
        await fixture.AdvanceAsync(TimeSpan.FromMilliseconds(980));
        Assert.Equal(renewed, fixture.AssertRegistration(
            ArrowStarLifetimeFlag, ArrowLifeOrigin.AddSeconds(1).DateTime, ArrowStarLifetimeSeconds));
        var start = fixture.Order.Count;
        await fixture.AdvanceAsync(TimeSpan.FromMilliseconds(20));
        ArrowLifeAssertRetired(fixture.Owner, ArrowStarLifetimeFlag);
        Assert.Equal(2U, ArrowLifeCounter(fixture.Owner, "ArrowBlades"));
        Assert.Equal(2U, ArrowLifeCounter(fixture.Owner, "StarFlow"));
        foreach (var uid in new[] { PlayerUid, TargetPlayerUid })
            ArrowStarLifetimeAssertOnlyRemovedVector(fixture.FramesSince(uid, start));
    }

    [Fact]
    public async Task ArrowBladesNormalDeathRetiresReal337RegistrationWithoutResettingEitherCounter()
    {
        await using var fixture = new ArrowLifecycleFixture(nativeDeathCasterQueryControls: true);
        await fixture.PrepareArrowNativeDeathCasterAsync();
        await fixture.AttackAsync();
        fixture.AssertRegistration(ArrowStarLifetimeFlag, ArrowLifeOrigin.DateTime, ArrowStarLifetimeSeconds);
        Assert.Equal(1U, ArrowLifeCounter(fixture.Owner, "ArrowBlades"));
        Assert.Equal(1U, ArrowLifeCounter(fixture.Owner, "StarFlow"));
        await fixture.KillOwnerAsync(); // Queried peer MA1 + raw9999, then native player /2 = lethal5000.
        Assert.Equal(0, fixture.Owner.Character.MinHitPoints);
        Assert.True(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, LegacyPlayerDeathState.DeadStatusFlag));
        ArrowLifeAssertRetired(fixture.Owner, ArrowStarLifetimeFlag);
        Assert.Equal(1U, ArrowLifeCounter(fixture.Owner, "ArrowBlades"));
        Assert.Equal(1U, ArrowLifeCounter(fixture.Owner, "StarFlow"));
    }

    [Fact]
    public async Task ArrowBladesRealThirdStarFlowStepRetires337TimerBeforeUnavailable10950AndResetsStarFlowOnly()
    {
        await using var fixture = new ArrowLifecycleFixture();
        await fixture.AttackAsync();
        fixture.AssertRegistration(ArrowStarLifetimeFlag, ArrowLifeOrigin.DateTime, ArrowStarLifetimeSeconds);
        await fixture.OrdinaryMagicAsync();
        fixture.AssertRegistration(
            ArrowStarLifetimeFlag, ArrowLifeOrigin.AddSeconds(1).DateTime, ArrowStarLifetimeSeconds);
        Assert.Equal(2U, ArrowLifeCounter(fixture.Owner, "ArrowBlades"));
        Assert.Equal(2U, ArrowLifeCounter(fixture.Owner, "StarFlow"));
        Assert.False(fixture.Owner.PlayerSpells.ContainsSpell(10_950));
        await fixture.OrdinaryMagicAsync();
        ArrowLifeAssertRetired(fixture.Owner, ArrowStarLifetimeFlag);
        Assert.Equal(3U, ArrowLifeCounter(fixture.Owner, "ArrowBlades"));
        Assert.Equal(0U, ArrowLifeCounter(fixture.Owner, "StarFlow"));
        Assert.True(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, ArrowLifeWater));
        Assert.True(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, 336));
        fixture.AssertRegistration(ArrowLifeWater, ArrowLifeOrigin.AddSeconds(2).DateTime, ArrowStarLifetimeSeconds);
    }

    private static bool ArrowStarLifetimeVectorHas(byte[] bytes, bool active) =>
        LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var packet) && packet.Uid == PlayerUid &&
        packet.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
            entry.Values is { } values && IsLegacyStatusFlagSet(values, ArrowStarLifetimeFlag) == active);

    private static void ArrowStarLifetimeAssertOnlyRemovedVector(byte[][] frames)
    {
        Assert.True(ArrowStarLifetimeVectorHas(Assert.Single(frames), active: false));
        Assert.Empty(ArrowLifeTyped(frames, 136, ArrowStarLifetimeFlag));
    }
}
