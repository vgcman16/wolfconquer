using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Fact]
    public async Task RootMagicEntryNativeZeroCooldownMutationBelongsToHeldCatalogRowAcrossPublicCasts()
    {
        await using var fixture = new RootMagicEntryFixture();
        var held = fixture.NativeRow(1_000); // Observer only: no setter or result seed.
        Assert.Equal((ushort)0, held.CoolDown);
        Assert.True(LegacyMagicRuntime.TryResolveSpell(fixture.WorldData, 1_000, 0, out var priorDto));
        Assert.Equal((ushort)0, priorDto.CoolDown);
        await fixture.CastAsync(1_000);
        Assert.Equal((ushort)500, held.CoolDown);
        Assert.Same(held, fixture.NativeRow(1_000));
        Assert.Equal("0", held.Source.Fields[33]); // Loaded input bytes are not rewritten.
        Assert.Equal((ushort)0, priorDto.CoolDown); // Immutable transport snapshot is not live authority.
        fixture.AssertProducedStamp(RootMagicEntryOrigin);
        fixture.Advance(TimeSpan.FromMilliseconds(500));
        await fixture.CastAsync(1_000);
        fixture.AssertProducedStamp(RootMagicEntryOrigin.AddMilliseconds(500));
        Assert.Equal((ushort)500, held.CoolDown);
        Assert.Equal("80", fixture.Owner.Character.Attributes["Stamina"]);
        Assert.Equal(920L, fixture.Owner.Character.MinMana);
    }

    [Theory]
    [InlineData((ushort)0, (ushort)500)]
    [InlineData((ushort)1_500, (ushort)2_500)]
    public async Task RootMagicEntryNativeCooldownWritesPrecedeFailedResourceCheck(
        ushort input, ushort expected)
    {
        await using var fixture = new RootMagicEntryFixture(rawCadence: input, stamina: 9);
        var held = fixture.NativeRow(1_000);
        Assert.Equal(input, held.CoolDown);
        var start = fixture.Order.Count;
        await fixture.CastAsync(1_000);
        Assert.Equal(expected, held.CoolDown);
        fixture.AssertProducedStamp(RootMagicEntryOrigin);
        Assert.Empty(RootMagicEntryCosts(fixture.FramesSince(PlayerUid, start)));
        Assert.Empty(ParseSpellAnimations(fixture.FramesSince(PlayerUid, start)));
        Assert.Equal("9", fixture.Owner.Character.Attributes["Stamina"]);
        Assert.Equal(1_000L, fixture.Owner.Character.MinMana);
        Assert.Equal(100_000L, fixture.Peer.Character.MinHitPoints);
    }

    [Theory]
    [InlineData("Ride")]
    [InlineData("Book")]
    [InlineData("Passive")]
    public async Task RootMagicEntryNativeCooldownIsUntouchedByEarlierNativeDenials(string denial)
    {
        await using var fixture = new RootMagicEntryFixture(rawCadence: 1_500,
            passive: denial == "Passive", omitSelectedLearnedInput: denial == "Book");
        var held = fixture.NativeRow(1_000);
        if (denial == "Ride") await fixture.MountAndWaitAsync();
        var hadStamp = fixture.Owner.Character.Attributes.TryGetValue("LegacyAttackStampUtc", out var beforeStamp);
        var start = fixture.Order.Count;
        await fixture.CastAsync(1_000);
        Assert.Equal((ushort)1_500, held.CoolDown);
        Assert.Equal(hadStamp, fixture.Owner.Character.Attributes.TryGetValue("LegacyAttackStampUtc", out var afterStamp));
        Assert.Equal(beforeStamp, afterStamp);
        Assert.Empty(RootMagicEntryCosts(fixture.FramesSince(PlayerUid, start)));
        Assert.DoesNotContain(ParseSpellAnimations(fixture.FramesSince(PlayerUid, start)),
            packet => packet.SpellId == 1_000);
        Assert.Equal(100_000L, fixture.Peer.Character.MinHitPoints);
        if (denial == "Ride") Assert.False(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, 0x32));
    }

    [Theory]
    [InlineData((ushort)0, (ushort)500)]
    [InlineData((ushort)1_500, (ushort)1_500)]
    public async Task RootMagicEntrySpecial1115EarlyDenialOnlyOwnsPriorZeroNormalization(
        ushort input, ushort expected)
    {
        await using var fixture = new RootMagicEntryFixture(rawCadence: input, nativeCadenceControls: true);
        await fixture.CastAsync(1_000); // Real earlier public cast creates the only cadence stamp.
        fixture.AssertProducedStamp(RootMagicEntryOrigin);
        var held = fixture.NativeRow(1_115);
        Assert.Equal(input, held.CoolDown);
        fixture.Advance(TimeSpan.FromMilliseconds(500)); // Strictly before source1115's800ms early check.
        var start = fixture.Order.Count;
        await fixture.CastAsync(1_115);
        Assert.Equal(expected, held.CoolDown);
        fixture.AssertProducedStamp(RootMagicEntryOrigin);
        Assert.Empty(RootMagicEntryCosts(fixture.FramesSince(PlayerUid, start)));
        Assert.DoesNotContain(ParseSpellAnimations(fixture.FramesSince(PlayerUid, start)),
            packet => packet.SpellId == 1_115);
        Assert.Equal("90", fixture.Owner.Character.Attributes["Stamina"]);
        Assert.Equal(960L, fixture.Owner.Character.MinMana);
    }

    [Fact]
    public async Task RootMagicEntryNativeCooldownMutationDoesNotLeakIntoAnotherCatalog()
    {
        await using var left = new RootMagicEntryFixture(rawCadence: 1_500, stamina: 9);
        await using var right = new RootMagicEntryFixture(rawCadence: 1_500, stamina: 9);
        var heldLeft = left.NativeRow(1_000);
        var heldRight = right.NativeRow(1_000);
        Assert.NotSame(heldLeft, heldRight);
        Assert.Equal((ushort)1_500, heldLeft.CoolDown);
        Assert.Equal((ushort)1_500, heldRight.CoolDown);
        await left.CastAsync(1_000);
        Assert.Equal((ushort)2_500, heldLeft.CoolDown);
        Assert.Equal((ushort)1_500, heldRight.CoolDown);
        Assert.False(right.Owner.Character.Attributes.ContainsKey("LegacyAttackStampUtc"));
        await right.CastAsync(1_000);
        Assert.Equal((ushort)2_500, heldRight.CoolDown);
        Assert.Same(heldLeft, left.NativeRow(1_000));
        Assert.Same(heldRight, right.NativeRow(1_000));
    }
}
