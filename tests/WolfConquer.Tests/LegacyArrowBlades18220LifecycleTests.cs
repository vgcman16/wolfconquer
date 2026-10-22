using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Theory]
    [InlineData("physical-player", 450U)]
    [InlineData("archer-player", 450U)]
    [InlineData("monster", 1_200U)]
    [InlineData("archer-sob", 1_200U)]
    public async Task ArrowBladesRealThreeHitProducerSelectsSource18220AcrossOrdinaryCallers(string route, uint damage)
    {
        await using var fixture = new ArrowLifecycleFixture(route);
        await fixture.EarnWaterAsync();
        var originalWater = fixture.AssertRegistration(ArrowLifeWater, ArrowLifeOrigin.AddSeconds(2).DateTime, 2_592_000);
        var before = fixture.PrimaryHitpoints;
        var start = fixture.Order.Count;
        await fixture.AttackAsync();
        var owner = fixture.FramesSince(PlayerUid, start);
        var animation = Assert.Single(ParseSpellAnimations(owner), packet => packet.SpellId == 18_220);
        Assert.Equal(PlayerUid, animation.AttackerUid);
        Assert.Equal(0U, animation.AttackedUid);
        Assert.Equal((832, 584), ((int)animation.X, (int)animation.Y));
        var target = Assert.Single(animation.Targets);
        Assert.Equal(fixture.PrimaryUid, target.Uid);
        Assert.Equal(damage, target.Damage); // Literal source MIN0 player / MAX300% nonplayer, not a calculator oracle.
        Assert.True(target.Hit);
        Assert.Equal(MsgAttackEffect.None, target.Effect);
        Assert.Equal(before - damage, fixture.PrimaryHitpoints);
        Assert.Equal(0U, ArrowLifeCounter(fixture.Owner, "ArrowBlades"));
        Assert.DoesNotContain(ParseSpellAnimations(owner), packet => packet.SpellId == 17_400);
        Assert.DoesNotContain(owner, bytes => MsgAttackPacket.TryParse(ReadFrame(bytes), out var packet) &&
            packet.Query.AttackType is MsgAttackType.Physical or MsgAttackType.Archer);
        fixture.AssertRegistration(ArrowLifeFire, ArrowLifeOrigin.AddSeconds(3).DateTime, 10);
        var renewedWater = fixture.AssertRegistration(ArrowLifeWater, ArrowLifeOrigin.AddSeconds(3).DateTime, 2_592_000);
        Assert.NotEqual(originalWater.Identity, renewedWater.Identity);
        Assert.Equal("true", fixture.Owner.Character.Attributes[LegacyStatusFlagDeathRegistration.Key(ArrowLifeFire)]);
        Assert.Equal("true", fixture.Owner.Character.Attributes[LegacyStatusFlagDeathRegistration.Key(ArrowLifeWater)]);
        var fire = Assert.Single(ArrowLifeTyped(owner, 137, ArrowLifeFire));
        var water = Assert.Single(ArrowLifeTyped(owner, (uint)LegacyMsgUpdateDataType.AppendIcon, ArrowLifeWater));
        ArrowLifeAssertIcon(fire, 5);
        ArrowLifeAssertIcon(water, 2);
        var peer = fixture.FramesSince(TargetPlayerUid, start);
        Assert.Empty(ArrowLifeTyped(peer, 137, ArrowLifeFire));
        Assert.Empty(ArrowLifeTyped(peer, (uint)LegacyMsgUpdateDataType.AppendIcon, ArrowLifeWater));
        var lastAnimation = Array.FindLastIndex(owner, bytes =>
            LegacySpellAnimationPacket.TryParse(ReadFrame(bytes), out var packet) && packet.SpellId == 18_220);
        var fireIcon = Array.FindIndex(owner, bytes => ArrowLifeTyped([bytes], 137, ArrowLifeFire).Length == 1);
        var waterIcon = Array.FindIndex(owner, bytes =>
            ArrowLifeTyped([bytes], (uint)LegacyMsgUpdateDataType.AppendIcon, ArrowLifeWater).Length == 1);
        Assert.True(lastAnimation >= 0 && lastAnimation < fireIcon && fireIcon < waterIcon);
        foreach (var uid in new[] { PlayerUid, TargetPlayerUid })
        {
            var delivered = fixture.FramesSince(uid, start);
            var animationIndex = Array.FindLastIndex(delivered, bytes =>
                LegacySpellAnimationPacket.TryParse(ReadFrame(bytes), out var packet) && packet.SpellId == 18_220);
            Assert.True(animationIndex >= 0);
            Assert.Collection(ArrowLifeVectors(delivered.Skip(animationIndex + 1)),
                bytes => Assert.True(ArrowLifeVector(bytes, false, false)),
                bytes => Assert.True(ArrowLifeVector(bytes, true, false)),
                bytes => Assert.True(ArrowLifeVector(bytes, true, true)));
        }
    }

    [Fact]
    public async Task ArrowBladesPublicMagic18220UsesSameSourceExecutorAfterReal333Acquisition()
    {
        await using var fixture = new ArrowLifecycleFixture();
        await fixture.EarnWaterAsync();
        var hp = fixture.PrimaryHitpoints;
        var start = fixture.Order.Count;
        await fixture.Cast18220MagicAsync();
        var animation = Assert.Single(ParseSpellAnimations(fixture.FramesSince(PlayerUid, start)),
            packet => packet.SpellId == 18_220);
        var hit = Assert.Single(animation.Targets);
        Assert.Equal(TargetPlayerUid, hit.Uid);
        Assert.Equal(450U, hit.Damage);
        Assert.Equal(hp - 450, fixture.PrimaryHitpoints);
        Assert.Equal(0U, ArrowLifeCounter(fixture.Owner, "ArrowBlades"));
        fixture.AssertRegistration(ArrowLifeFire, ArrowLifeOrigin.AddSeconds(3).DateTime, 10);
        fixture.AssertRegistration(ArrowLifeWater, ArrowLifeOrigin.AddSeconds(3).DateTime, 2_592_000);
        Assert.False(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, 268));
        Assert.False(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, 269));
        Assert.False(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, 64));
    }

    [Theory]
    [InlineData("physical-player")]
    [InlineData("archer-player")]
    [InlineData("monster")]
    [InlineData("archer-sob")]
    public async Task ArrowBladesUnavailableSelectedBookConsumesActionUnder333Without17400OrOrdinaryFallback(string route)
    {
        await using var fixture = new ArrowLifecycleFixture(route, learnedArrow: false);
        await fixture.EarnWaterAsync();
        var registration = fixture.AssertRegistration(ArrowLifeWater, ArrowLifeOrigin.AddSeconds(2).DateTime, 2_592_000);
        var hp = fixture.PrimaryHitpoints;
        var start = fixture.Order.Count;
        await fixture.AttackAsync();
        Assert.Equal(hp, fixture.PrimaryHitpoints);
        Assert.Equal(3U, ArrowLifeCounter(fixture.Owner, "ArrowBlades"));
        Assert.Equal(registration, fixture.AssertRegistration(ArrowLifeWater, ArrowLifeOrigin.AddSeconds(2).DateTime, 2_592_000));
        Assert.False(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, ArrowLifeFire));
        Assert.Empty(ParseSpellAnimations(fixture.FramesSince(PlayerUid, start)));
        Assert.DoesNotContain(fixture.FramesSince(PlayerUid, start), bytes =>
            MsgAttackPacket.TryParse(ReadFrame(bytes), out var packet) &&
            packet.Query.AttackType is MsgAttackType.Physical or MsgAttackType.Archer);
        Assert.Empty(ArrowLifeTyped(fixture.FramesSince(PlayerUid, start), 137, ArrowLifeFire));
    }

    [Fact]
    public async Task ArrowBladesReal332ExpiresAtInclusiveTenSecondsWithoutRemovingIndependent333()
    {
        await using var fixture = new ArrowLifecycleFixture();
        await fixture.EarnWaterAsync();
        await fixture.AttackAsync();
        var fire = fixture.AssertRegistration(ArrowLifeFire, ArrowLifeOrigin.AddSeconds(3).DateTime, 10);
        var water = fixture.AssertRegistration(ArrowLifeWater, ArrowLifeOrigin.AddSeconds(3).DateTime, 2_592_000);
        await fixture.StartAsync();
        await fixture.AdvanceAsync(TimeSpan.FromMilliseconds(9_980));
        Assert.Equal(fire, fixture.AssertRegistration(ArrowLifeFire, ArrowLifeOrigin.AddSeconds(3).DateTime, 10));
        var start = fixture.Order.Count;
        await fixture.AdvanceAsync(TimeSpan.FromMilliseconds(20));
        ArrowLifeAssertRetired(fixture.Owner, ArrowLifeFire);
        Assert.Equal(water, fixture.AssertRegistration(ArrowLifeWater, ArrowLifeOrigin.AddSeconds(3).DateTime, 2_592_000));
        Assert.Equal(0U, ArrowLifeCounter(fixture.Owner, "ArrowBlades"));
        foreach (var uid in new[] { PlayerUid, TargetPlayerUid })
            Assert.Single(fixture.FramesSince(uid, start), bytes => ArrowLifeVector(bytes, false, true));
        Assert.Empty(ArrowLifeTyped(fixture.FramesSince(PlayerUid, start), 137, ArrowLifeFire));
        Assert.DoesNotContain(fixture.FramesSince(PlayerUid, start), bytes =>
            LegacyPlayerStatusPacket.TryParse(ReadFrame(bytes), out _)); // Source default expiry has no query/reset.
    }

    [Fact]
    public async Task ArrowBladesReal333ExpiresAtInclusiveThirtyDaysWithoutResettingEarnedCounter()
    {
        await using var fixture = new ArrowLifecycleFixture();
        await fixture.EarnWaterAsync();
        var water = fixture.AssertRegistration(ArrowLifeWater, ArrowLifeOrigin.AddSeconds(2).DateTime, 2_592_000);
        await fixture.StartAsync();
        await fixture.AdvanceAsync(TimeSpan.FromDays(30) - TimeSpan.FromMilliseconds(20));
        Assert.Equal(water, fixture.AssertRegistration(ArrowLifeWater, ArrowLifeOrigin.AddSeconds(2).DateTime, 2_592_000));
        var start = fixture.Order.Count;
        await fixture.AdvanceAsync(TimeSpan.FromMilliseconds(20));
        ArrowLifeAssertRetired(fixture.Owner, ArrowLifeWater);
        Assert.False(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, ArrowLifeFire));
        Assert.Equal(3U, ArrowLifeCounter(fixture.Owner, "ArrowBlades"));
        foreach (var uid in new[] { PlayerUid, TargetPlayerUid })
            Assert.Single(fixture.FramesSince(uid, start), bytes => ArrowLifeVector(bytes, false, false));
        Assert.Empty(ArrowLifeTyped(fixture.FramesSince(PlayerUid, start),
            (uint)LegacyMsgUpdateDataType.AppendIcon, ArrowLifeWater));
    }

    [Fact]
    public async Task ArrowBladesRealRecastBeforeCaptureReplaces332TimerAndOldDeadlineCannotExpireIt()
    {
        await using var fixture = new ArrowLifecycleFixture();
        await fixture.EarnWaterAsync();
        await fixture.AttackAsync();
        var oldFire = fixture.AssertRegistration(ArrowLifeFire, ArrowLifeOrigin.AddSeconds(3).DateTime, 10);
        await fixture.StartAsync();
        await fixture.AdvanceAsync(TimeSpan.FromSeconds(4));
        var recastStart = fixture.Order.Count;
        await fixture.AttackAsync(); // Real333 selected again; entered receive earns Arrow1, so333 is not restored.
        foreach (var uid in new[] { PlayerUid, TargetPlayerUid })
        {
            var frames = fixture.FramesSince(uid, recastStart);
            var animationIndex = Array.FindLastIndex(frames, bytes =>
                LegacySpellAnimationPacket.TryParse(ReadFrame(bytes), out var packet) && packet.SpellId == 18_220);
            Assert.True(animationIndex >= 0);
            // Explicit333 removal publishes once; AddSpellFlag332 quietly replaces the old Flag,
            // then AddFlag publishes ONCE. There is no fabricated332=false intermediate vector.
            Assert.Collection(ArrowLifeVectors(frames.Skip(animationIndex + 1)),
                bytes => Assert.True(ArrowLifeVector(bytes, true, false)),
                bytes => Assert.True(ArrowLifeVector(bytes, true, false)));
        }
        var newFire = fixture.AssertRegistration(ArrowLifeFire, ArrowLifeOrigin.AddSeconds(7).DateTime, 10);
        Assert.NotEqual(oldFire.Identity, newFire.Identity);
        ArrowLifeAssertRetired(fixture.Owner, ArrowLifeWater);
        Assert.Equal(1U, ArrowLifeCounter(fixture.Owner, "ArrowBlades"));
        await fixture.AdvanceAsync(TimeSpan.FromSeconds(6)); // Old W13 is not the NEW registration's W17 deadline.
        Assert.Equal(newFire, fixture.AssertRegistration(ArrowLifeFire, ArrowLifeOrigin.AddSeconds(7).DateTime, 10));
        await fixture.AdvanceAsync(TimeSpan.FromMilliseconds(3_980));
        Assert.True(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, ArrowLifeFire));
        var start = fixture.Order.Count;
        await fixture.AdvanceAsync(TimeSpan.FromMilliseconds(20));
        ArrowLifeAssertRetired(fixture.Owner, ArrowLifeFire);
        Assert.Single(fixture.FramesSince(PlayerUid, start), bytes => ArrowLifeVector(bytes, false, false));
        Assert.Equal(1U, ArrowLifeCounter(fixture.Owner, "ArrowBlades"));
    }

    [Fact]
    public async Task ArrowBladesNormalDeathRetiresBothRealRegistrationsButDoesNotResetArrowCounter()
    {
        await using var fixture = new ArrowLifecycleFixture(nativeDeathCasterQueryControls: true);
        await fixture.PrepareArrowNativeDeathCasterAsync();
        await fixture.EarnWaterAsync();
        await fixture.AttackAsync();
        await fixture.OrdinaryMagicAsync(); // Real receive, not a seeded Arrow counter;333 stays present.
        Assert.Equal(1U, ArrowLifeCounter(fixture.Owner, "ArrowBlades"));
        fixture.AssertRegistration(ArrowLifeFire, ArrowLifeOrigin.AddSeconds(3).DateTime, 10);
        fixture.AssertRegistration(ArrowLifeWater, ArrowLifeOrigin.AddSeconds(3).DateTime, 2_592_000);
        var start = fixture.Order.Count;
        await fixture.KillOwnerAsync();
        Assert.Equal(0, fixture.Owner.Character.MinHitPoints);
        Assert.True(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, 5));
        ArrowLifeAssertRetired(fixture.Owner, ArrowLifeFire);
        ArrowLifeAssertRetired(fixture.Owner, ArrowLifeWater);
        Assert.Equal(1U, ArrowLifeCounter(fixture.Owner, "ArrowBlades"));
        Assert.Contains(fixture.FramesSince(PlayerUid, start), bytes =>
            MsgAttackPacket.TryParse(ReadFrame(bytes), out var packet) && packet.Query.AttackType == MsgAttackType.Death);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ArrowBladesExpiryOriginalOwnerLossStopsCapturedRemovedVectorBeforePeerFanout(bool rebind)
    {
        var armed = false;
        var callbacks = 0;
        WorldSession? replacement = null;
        await using var fixture = new ArrowLifecycleFixture(onSend: (current, recipient, bytes) =>
        {
            if (armed && recipient == PlayerUid && ArrowLifeVector(bytes, false, true))
            {
                armed = false;
                callbacks++;
                if (rebind) replacement = current.RebindOwner();
                else current.RemoveOwner();
            }
            return ValueTask.CompletedTask;
        });
        await fixture.EarnWaterAsync();
        await fixture.AttackAsync();
        await fixture.StartAsync();
        await fixture.AdvanceAsync(TimeSpan.FromMilliseconds(9_980));
        var start = fixture.Order.Count;
        armed = true;
        await fixture.AdvanceAsync(TimeSpan.FromMilliseconds(20));
        Assert.Equal(1, callbacks);
        Assert.Single(fixture.FramesSince(PlayerUid, start), bytes => ArrowLifeVector(bytes, false, true));
        Assert.DoesNotContain(fixture.FramesSince(TargetPlayerUid, start), bytes => ArrowLifeVector(bytes, false, true));
        if (rebind)
        {
            Assert.Same(replacement, fixture.OwnerSlot);
            Assert.Equal(77_777, fixture.OwnerSlot!.Character.MinHitPoints);
            Assert.Equal(77, fixture.OwnerSlot.Character.MinMana);
            Assert.Equal("replacement", fixture.OwnerSlot.Character.Attributes["ArrowIdentity"]);
            Assert.False(LegacyArrowBladesFlagRegistration.TryRead(fixture.OwnerSlot.Character, ArrowLifeWater, out _));
        }
        else Assert.Null(fixture.OwnerSlot);
    }

    private static LegacyMsgUpdateEntry[] ArrowLifeTyped(byte[][] frames, uint type, int flag) =>
        ParseMsgUpdates(frames).Where(packet => packet.Uid == PlayerUid).SelectMany(packet => packet.Updates)
            .Where(entry => (uint)entry.Type == type && entry.Flag == checked((uint)flag)).ToArray();

    private static void ArrowLifeAssertIcon(LegacyMsgUpdateEntry entry, uint damage)
    {
        Assert.Equal(0UL, entry.Value);
        Assert.Equal(0U, entry.Time);
        Assert.Equal(damage, entry.Damage);
        Assert.Equal(0U, entry.SpellLevel);
    }

    private static byte[][] ArrowLifeVectors(IEnumerable<byte[]> frames) => frames.Where(bytes =>
        LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var packet) && packet.Uid == PlayerUid &&
        packet.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.StatusFlag)).ToArray();

    private static bool ArrowLifeVector(byte[] bytes, bool fire, bool water)
    {
        if (!LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var packet) || packet.Uid != PlayerUid) return false;
        var entry = packet.Updates.FirstOrDefault(update => update.Type == LegacyMsgUpdateDataType.StatusFlag);
        return entry.Values is { } values &&
            IsLegacyStatusFlagSet(values, ArrowLifeFire) == fire && IsLegacyStatusFlagSet(values, ArrowLifeWater) == water;
    }

    private static void ArrowLifeAssertRetired(WorldSession session, int flag)
    {
        Assert.False(HasLegacyStatusFlag(session.Character.Attributes, flag));
        Assert.False(LegacyArrowBladesFlagRegistration.TryRead(session.Character, flag, out _));
        Assert.False(session.Character.Attributes.ContainsKey(LegacyStatusFlagDeathRegistration.Key(flag)));
        foreach (var key in new[] { "OwnerUid", "Identity", "Seconds", "TimerTicks" })
            Assert.False(session.Character.Attributes.ContainsKey($"LegacyArrowBladesFlag.{flag}.{key}"));
    }
}
