using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Fact]
    public async Task RootMagicEntryNativeZeroCooldownStaysImmutableAcrossPublicCasts()
    {
        await using var fixture = new RootMagicEntryFixture();
        var held = fixture.NativeRow(1_000);
        var fields = held.Source.Fields.ToArray();
        Assert.Equal((ushort)0, held.CoolDown);
        Assert.True(LegacyMagicRuntime.TryResolveSpell(fixture.WorldData, 1_000, 0, out var priorDto));
        Assert.Equal((ushort)0, priorDto.CoolDown);
        await fixture.CastAsync(1_000);
        Assert.Equal((ushort)0, held.CoolDown);
        Assert.Same(held, fixture.NativeRow(1_000));
        Assert.Equal(fields, held.Source.Fields);
        Assert.Equal("0", held.Source.Fields[33]);
        Assert.Equal((ushort)0, priorDto.CoolDown);
        fixture.AssertProducedStamp(RootMagicEntryOrigin);
        fixture.Advance(TimeSpan.FromMilliseconds(500));
        await fixture.CastAsync(1_000);
        fixture.AssertProducedStamp(RootMagicEntryOrigin.AddMilliseconds(500));
        Assert.Equal((ushort)0, held.CoolDown);
        Assert.Equal("80", fixture.Owner.Character.Attributes["Stamina"]);
        Assert.Equal(920L, fixture.Owner.Character.MinMana);
    }

    [Theory]
    [InlineData((ushort)0)]
    [InlineData((ushort)1_500)]
    public async Task RootMagicEntryNativeCooldownAndOwnerAreUntouchedByFailedResourceCheck(ushort input)
    {
        await using var fixture = new RootMagicEntryFixture(rawCadence: input, stamina: 9);
        var before = fixture.CaptureAdmission();
        await fixture.CastAsync(1_000);
        fixture.AssertAdmissionRejected(before);
        Assert.Equal(input, fixture.NativeRow(1_000).CoolDown);
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
    [InlineData((ushort)0)]
    [InlineData((ushort)1_500)]
    public async Task RootMagicEntrySpecial1115EarlyDenialNeverNormalizesCatalog(ushort input)
    {
        await using var fixture = new RootMagicEntryFixture(rawCadence: input, nativeCadenceControls: true);
        await fixture.CastAsync(1_000);
        fixture.AssertProducedStamp(RootMagicEntryOrigin);
        var held = fixture.NativeRow(1_115);
        Assert.Equal(input, held.CoolDown);
        fixture.Advance(TimeSpan.FromMilliseconds(500)); // Strictly before source1115's800ms early check.
        var before = fixture.CaptureAdmission(1_115);
        await fixture.CastAsync(1_115);
        fixture.AssertAdmissionRejected(before);
        Assert.Equal(input, held.CoolDown);
        fixture.AssertProducedStamp(RootMagicEntryOrigin);
        Assert.Equal("90", fixture.Owner.Character.Attributes["Stamina"]);
        Assert.Equal(960L, fixture.Owner.Character.MinMana);
    }

    [Fact]
    public async Task RootMagicEntryNativeCooldownStaysImmutableAcrossSeparateCatalogs()
    {
        await using var left = new RootMagicEntryFixture(rawCadence: 1_500, stamina: 9);
        await using var right = new RootMagicEntryFixture(rawCadence: 1_500, stamina: 9);
        var heldLeft = left.NativeRow(1_000);
        var heldRight = right.NativeRow(1_000);
        Assert.NotSame(heldLeft, heldRight);
        var beforeLeft = left.CaptureAdmission();
        var beforeRight = right.CaptureAdmission();
        await left.CastAsync(1_000);
        left.AssertAdmissionRejected(beforeLeft);
        right.AssertAdmissionRejected(beforeRight);
        Assert.Equal((ushort)1_500, heldLeft.CoolDown);
        Assert.Equal((ushort)1_500, heldRight.CoolDown);
        await right.CastAsync(1_000);
        left.AssertAdmissionRejected(beforeLeft);
        right.AssertAdmissionRejected(beforeRight);
        Assert.Same(heldLeft, left.NativeRow(1_000));
        Assert.Same(heldRight, right.NativeRow(1_000));
    }
}
