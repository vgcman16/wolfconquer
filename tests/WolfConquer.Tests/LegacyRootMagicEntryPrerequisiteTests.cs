using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Theory]
    [InlineData((ushort)1_000)]
    [InlineData((ushort)1_011)]
    [InlineData((ushort)1_010)]
    [InlineData((ushort)1_005)]
    public async Task RootMagicEntryAdmitsAttackDirectBombAndRecruitWithOnePreparation(ushort id)
    {
        await using var fixture = new RootMagicEntryFixture(selectedId: id);
        var start = fixture.Order.Count;
        await fixture.CastAsync(id);
        fixture.AssertProducedStamp(fixture.Now);
        fixture.AssertCostPairSince(start, 90, 960);
        Assert.Single(ParseSpellAnimations(fixture.FramesSince(PlayerUid, start)), packet => packet.SpellId == id);
    }

    [Theory]
    [InlineData((ushort)1_000)]
    [InlineData((ushort)1_011)]
    [InlineData((ushort)1_010)]
    [InlineData((ushort)1_005)]
    [InlineData((ushort)18_220)]
    public async Task RootMagicEntryRideWithoutCropDismountsBeforeFirstFamilyAdmission(ushort id)
    {
        await using var fixture = new RootMagicEntryFixture(selectedId: id);
        await fixture.MountAndWaitAsync();
        var hadStamp = fixture.Owner.Character.Attributes.TryGetValue("LegacyAttackStampUtc", out var stampBefore);
        var start = fixture.Order.Count;
        var peerHp = fixture.Peer.Character.MinHitPoints;
        await fixture.CastAsync(id);
        Assert.False(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, 0x32));
        Assert.Equal(hadStamp, fixture.Owner.Character.Attributes.TryGetValue("LegacyAttackStampUtc", out var stampAfter));
        Assert.Equal(stampBefore, stampAfter); // Ride denial never replaces a prior public cast's stamp.
        Assert.Equal("100", fixture.Owner.Character.Attributes["Stamina"]);
        Assert.Equal(1_000, fixture.Owner.Character.MinMana);
        Assert.Equal(peerHp, fixture.Peer.Character.MinHitPoints);
        Assert.Empty(RootMagicEntryCosts(fixture.FramesSince(PlayerUid, start)));
        Assert.DoesNotContain(ParseSpellAnimations(fixture.FramesSince(PlayerUid, start)), packet => packet.SpellId == id);
        foreach (var recipient in new[] { PlayerUid, TargetPlayerUid })
            Assert.Contains(ParseMsgUpdates(fixture.FramesSince(recipient, start)), packet =>
                packet.Uid == PlayerUid && packet.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.StatusFlag && entry.Values is { } bits &&
                    !IsLegacyStatusFlagSet(bits, 0x32)));
    }

    [Theory]
    [InlineData((ushort)1_000)]
    [InlineData((ushort)1_011)]
    [InlineData((ushort)1_010)]
    [InlineData((ushort)1_005)]
    public async Task RootMagicEntryEquippedRidingCropPreservesRideAndAllowsFirstFamily(ushort id)
    {
        await using var fixture = new RootMagicEntryFixture(selectedId: id, ridingCrop: true);
        await fixture.MountAndWaitAsync();
        var start = fixture.Order.Count;
        await fixture.CastAsync(id);
        Assert.True(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, 0x32));
        fixture.AssertProducedStamp(fixture.Now);
        fixture.AssertCostPairSince(start, 90, 960);
        Assert.Single(ParseSpellAnimations(fixture.FramesSince(PlayerUid, start)), packet => packet.SpellId == id);
    }

    [Theory]
    [InlineData((ushort)1_000)]
    [InlineData((ushort)1_011)]
    [InlineData((ushort)1_010)]
    [InlineData((ushort)1_005)]
    public async Task RootMagicEntryPassiveMetadataRejectsBeforeStampAndResources(ushort id)
    {
        await using var fixture = new RootMagicEntryFixture(selectedId: id, passive: true);
        Assert.True(fixture.SelectedEffect!.DescriptionEx!.Contains("Passive", StringComparison.Ordinal));
        Assert.Equal((ushort)0, fixture.Owner.LegacyRandomSpell);
        var start = fixture.Order.Count;
        await fixture.CastAsync(id);
        Assert.False(fixture.Owner.Character.Attributes.ContainsKey("LegacyAttackStampUtc"));
        Assert.Equal("100", fixture.Owner.Character.Attributes["Stamina"]);
        Assert.Equal(1_000, fixture.Owner.Character.MinMana);
        Assert.Equal(100_000, fixture.Peer.Character.MinHitPoints);
        Assert.Empty(RootMagicEntryCosts(fixture.FramesSince(PlayerUid, start)));
        Assert.Empty(ParseSpellAnimations(fixture.FramesSince(PlayerUid, start)));
        Assert.Empty(ParseMagicColdTimePackets(fixture.FramesSince(PlayerUid, start)));
    }

    [Theory]
    [InlineData((ushort)0, 500)]
    [InlineData((ushort)1_000, 1_000)]
    [InlineData((ushort)1_500, 2_500)]
    [InlineData((ushort)3_000, 3_000)]
    public async Task RootMagicEntryUsesEffectiveSharedCadenceAtExactInclusiveBoundary(
        ushort rawCadence, int effectiveMilliseconds)
    {
        await using var fixture = new RootMagicEntryFixture(rawCadence: rawCadence);
        await fixture.CastAsync(1_000);
        fixture.AssertProducedStamp(RootMagicEntryOrigin);
        foreach (var elapsed in new[] { TimeSpan.Zero, TimeSpan.FromMilliseconds(effectiveMilliseconds - 1) })
        {
            fixture.Advance(elapsed);
            var rejectedStart = fixture.Order.Count;
            var hp = fixture.Peer.Character.MinHitPoints;
            await fixture.CastAsync(1_000);
            Assert.Equal(hp, fixture.Peer.Character.MinHitPoints);
            fixture.AssertProducedStamp(RootMagicEntryOrigin);
            Assert.Equal("90", fixture.Owner.Character.Attributes["Stamina"]);
            Assert.Equal(960, fixture.Owner.Character.MinMana);
            Assert.Empty(RootMagicEntryCosts(fixture.FramesSince(PlayerUid, rejectedStart)));
            Assert.Empty(ParseSpellAnimations(fixture.FramesSince(PlayerUid, rejectedStart)));
        }
        fixture.Advance(TimeSpan.FromMilliseconds(1));
        var acceptedStart = fixture.Order.Count;
        await fixture.CastAsync(1_000);
        fixture.AssertProducedStamp(RootMagicEntryOrigin.AddMilliseconds(effectiveMilliseconds));
        fixture.AssertCostPairSince(acceptedStart, 80, 920);
        Assert.Single(ParseSpellAnimations(fixture.FramesSince(PlayerUid, acceptedStart)), packet => packet.SpellId == 1_000);
    }

    [Fact]
    public async Task RootMagicEntrySharesProducedStampAcrossDifferentCaseFamilies()
    {
        await using var fixture = new RootMagicEntryFixture();
        await fixture.CastAsync(1_000);
        var start = fixture.Order.Count;
        await fixture.CastAsync(1_010);
        Assert.Empty(RootMagicEntryCosts(fixture.FramesSince(PlayerUid, start)));
        Assert.DoesNotContain(ParseSpellAnimations(fixture.FramesSince(PlayerUid, start)), packet => packet.SpellId == 1_010);
        fixture.AssertProducedStamp(RootMagicEntryOrigin);
        fixture.Advance(TimeSpan.FromMilliseconds(500));
        start = fixture.Order.Count;
        await fixture.CastAsync(1_010);
        fixture.AssertProducedStamp(RootMagicEntryOrigin.AddMilliseconds(500));
        fixture.AssertCostPairSince(start, 80, 920);
        Assert.Single(ParseSpellAnimations(fixture.FramesSince(PlayerUid, start)), packet => packet.SpellId == 1_010);
    }

    [Theory]
    [InlineData((ushort)1_000)]
    [InlineData((ushort)1_011)]
    public async Task RootMagicEntryAttackAndDirectUnknownTargetRejectBeforeCanUseCosts(ushort id)
    {
        await using var fixture = new RootMagicEntryFixture(selectedId: id, coldTime: 1_500);
        var start = fixture.Order.Count;
        await fixture.CastAsync(id, target: 9_999_999, x: 880);
        fixture.AssertProducedStamp(RootMagicEntryOrigin); // Native root stamp precedes CanUse target/range.
        Assert.Equal("100", fixture.Owner.Character.Attributes["Stamina"]);
        Assert.Equal(1_000, fixture.Owner.Character.MinMana);
        Assert.Equal(100_000, fixture.Peer.Character.MinHitPoints);
        Assert.Empty(RootMagicEntryCosts(fixture.FramesSince(PlayerUid, start)));
        Assert.Empty(ParseMagicColdTimePackets(fixture.FramesSince(PlayerUid, start)));
        Assert.True(fixture.Owner.MagicCooldowns is null || !fixture.Owner.MagicCooldowns.ContainsKey(id));
        fixture.Advance(TimeSpan.FromMilliseconds(500));
        start = fixture.Order.Count;
        await fixture.CastAsync(id);
        fixture.AssertCostPairSince(start, 90, 960);
        Assert.Single(ParseSpellAnimations(fixture.FramesSince(PlayerUid, start)), packet => packet.SpellId == id);
    }

    [Fact]
    public async Task RootMagicEntryUnknownTargetBombStillRunsCanUseBeforeItsEmptyCase()
    {
        await using var fixture = new RootMagicEntryFixture(selectedId: 1_010, coldTime: 1_500);
        var start = fixture.Order.Count;
        await fixture.CastAsync(1_010, target: 9_999_999, x: 880);
        fixture.AssertProducedStamp(RootMagicEntryOrigin);
        fixture.AssertCostPairSince(start, 90, 960);
        Assert.Equal(RootMagicEntryOrigin.AddMilliseconds(1_500), fixture.Owner.MagicCooldowns![1_010]);
        Assert.Empty(ParseMagicColdTimePackets(fixture.FramesSince(PlayerUid, start))); // MsgSpell.ColdTime is a silent field.
        Assert.Equal(100_000, fixture.Peer.Character.MinHitPoints);
        Assert.All(ParseSpellAnimations(fixture.FramesSince(PlayerUid, start)), packet => Assert.Empty(packet.Targets));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RootMagicEntryNativeCostFailureRetainsRootStampAndOnlyPriorCost(
        bool insufficientStamina)
    {
        await using var fixture = new RootMagicEntryFixture(coldTime: 1_500,
            stamina: insufficientStamina ? 5UL : 100UL, mana: insufficientStamina ? 1_000 : 20);
        var start = fixture.Order.Count;
        await fixture.CastAsync(1_000);
        fixture.AssertProducedStamp(RootMagicEntryOrigin);
        Assert.Equal(insufficientStamina ? "5" : "90", fixture.Owner.Character.Attributes["Stamina"]);
        Assert.Equal(insufficientStamina ? 1_000 : 20, fixture.Owner.Character.MinMana);
        (LegacyMsgUpdateDataType Type, ulong Value)[] expectedCosts = insufficientStamina
            ? [] : [(LegacyMsgUpdateDataType.Stamina, 90UL)];
        Assert.Equal(expectedCosts, RootMagicEntryCosts(fixture.FramesSince(PlayerUid, start)));
        Assert.Empty(ParseMagicColdTimePackets(fixture.FramesSince(PlayerUid, start)));
        Assert.True(fixture.Owner.MagicCooldowns is null || !fixture.Owner.MagicCooldowns.ContainsKey(1_000));
        Assert.Empty(ParseSpellAnimations(fixture.FramesSince(PlayerUid, start)));
        Assert.Equal(100_000, fixture.Peer.Character.MinHitPoints);
    }

    [Fact]
    public async Task RootMagicEntryColdRejectionOccursAfterFreshCostsWithoutRenewingColdDeadline()
    {
        await using var fixture = new RootMagicEntryFixture(coldTime: 2_000);
        await fixture.CastAsync(1_000);
        var ready = RootMagicEntryOrigin.AddMilliseconds(2_000);
        Assert.Equal(ready, fixture.Owner.MagicCooldowns![1_000]);
        fixture.Advance(TimeSpan.FromMilliseconds(500)); // Real next root input; NOT a seeded cooldown/stamp.
        var start = fixture.Order.Count;
        var hp = fixture.Peer.Character.MinHitPoints;
        await fixture.CastAsync(1_000);
        fixture.AssertProducedStamp(RootMagicEntryOrigin.AddMilliseconds(500));
        fixture.AssertCostPairSince(start, 80, 920);
        Assert.Equal(ready, fixture.Owner.MagicCooldowns![1_000]);
        Assert.Equal(hp, fixture.Peer.Character.MinHitPoints);
        Assert.Empty(ParseMagicColdTimePackets(fixture.FramesSince(PlayerUid, start)));
        Assert.Empty(ParseSpellAnimations(fixture.FramesSince(PlayerUid, start)));
    }

    [Fact]
    public async Task RootMagicEntryNestedPublicInputCannotReuseOuterInvocationCadencePermission()
    {
        var entered = false;
        var callbacks = 0;
        await using var fixture = new RootMagicEntryFixture(onSend: async (current, recipient, bytes) =>
        {
            if (!entered && recipient == PlayerUid &&
                RootMagicEntryCosts([bytes]).SequenceEqual([(LegacyMsgUpdateDataType.Stamina, 90UL)]))
            {
                entered = true;
                callbacks++;
                await current.CastAsync(1_000); // Native same-tick root must reject, not inherit outer permission.
            }
        });
        var start = fixture.Order.Count;
        await fixture.CastAsync(1_000);
        Assert.Equal(1, callbacks);
        fixture.AssertProducedStamp(RootMagicEntryOrigin);
        fixture.AssertCostPairSince(start, 90, 960);
        Assert.Single(ParseSpellAnimations(fixture.FramesSince(PlayerUid, start)), packet => packet.SpellId == 1_000);
    }

    [Fact]
    public async Task RootMagicEntryManual18220UsesEarned333AndDoesNotDoubleApplyRootCadence()
    {
        await using var fixture = new ArrowLifecycleFixture(sourcePassiveArrow: true);
        await fixture.EarnWaterAsync();
        Assert.True(fixture.SourceArrowEffect!.DescriptionEx!.Contains("Passive", StringComparison.Ordinal));
        Assert.Equal((ushort)0, fixture.Owner.LegacyRandomSpell);
        Assert.True(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, ArrowLifeWater));
        var start = fixture.Order.Count;
        await fixture.Cast18220MagicAsync(); // Real W3, after natural W0/W1/W2 ordinary inputs.
        Assert.Equal(ArrowLifeOrigin.AddSeconds(3).ToString("O", System.Globalization.CultureInfo.InvariantCulture),
            fixture.Owner.Character.Attributes["LegacyAttackStampUtc"]);
        var animation = Assert.Single(ParseSpellAnimations(fixture.FramesSince(PlayerUid, start)),
            packet => packet.SpellId == 18_220);
        Assert.Equal(450U, Assert.Single(animation.Targets).Damage);
        Assert.True(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, ArrowLifeFire));
        Assert.Empty(RootMagicEntryCosts(fixture.FramesSince(PlayerUid, start))); // The actual18220 fixture row costs0.
        Assert.Equal((ushort)0, fixture.Owner.LegacyRandomSpell);
    }
}
