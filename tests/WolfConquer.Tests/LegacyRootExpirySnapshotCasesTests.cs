using System.Globalization;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Fact]
    public async Task RootExpiryRecognizesProducerPreservedUniqueKeySpellingAndClocklessClassicDeadline()
    {
        await using var fixture = new RootMagicEntryFixture(itemEffects:
            new(RightEffect: 0, RingEffect: 0, NeckEffect: 200), itemEffectLifetimeControls: true,
            opaqueCaseControls: true, mixedCaseRootKeys: true);
        Assert.False(fixture.RootShieldClockExists());
        await fixture.QueryRootItemEffectsThroughFrameAsync();
        await fixture.CastRootItemAttackThroughFrameAsync();
        Assert.True(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, 8));
        Assert.NotNull(fixture.ReadActualRootClock(8));
        Assert.True(fixture.Owner.Character.Attributes.ContainsKey("legacyrootitemeffectflag.8.nativeowned"));
        Assert.False(fixture.Owner.Character.Attributes.ContainsKey("LegacyRootItemEffectFlag.8.NativeOwned"));
        Assert.Equal(fixture.Now.AddSeconds(15), DateTimeOffset.Parse(
            fixture.Owner.Character.Attributes["legacymagicshieldexpiresatutc"], CultureInfo.InvariantCulture));
        await fixture.StartRootItemEffectRuntimeAsync();
        await fixture.AdvanceRootItemEffectRuntimeAsync(TimeSpan.FromSeconds(15));
        Assert.False(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, 8));
        Assert.False(fixture.RootShieldClockExists());
        Assert.False(fixture.Owner.Character.Attributes.ContainsKey("legacyrootitemeffectflag.8.nativeowned"));
        Assert.False(fixture.Owner.Character.Attributes.ContainsKey("legacymagicshieldexpiresatutc"));
        Assert.Equal("one", fixture.Owner.Character.Attributes["Opaque"]);
        Assert.Equal("two", fixture.Owner.Character.Attributes["opaque"]);

        // This is the real Classic Stigma producer, which intentionally has no
        // Root clock; the actual producer writes the unique preserved spelling.
        await fixture.CastClassicStigmaThroughFrameAsync();
        Assert.True(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, 9));
        Assert.Null(fixture.ReadActualRootClock(9));
        Assert.False(fixture.Owner.Character.Attributes.ContainsKey("LegacyStigmaExpiresAtUtc"));
        var deadline = DateTimeOffset.Parse(fixture.Owner.Character.Attributes["legacystigmaexpiresatutc"], CultureInfo.InvariantCulture);
        var untilDeadline = deadline - fixture.Now;
        Assert.True(untilDeadline > TimeSpan.Zero);
        await fixture.AdvanceRootItemEffectRuntimeAsync(untilDeadline);
        Assert.False(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, 9));
        Assert.False(fixture.Owner.Character.Attributes.ContainsKey("legacystigmaexpiresatutc"));
        Assert.Equal("one", fixture.Owner.Character.Attributes["Opaque"]);
        Assert.Equal("two", fixture.Owner.Character.Attributes["opaque"]);
    }

    [Theory]
    [InlineData("LegacyRootItemEffectFlag.8.NativeOwned")]
    [InlineData("LegacyMagicShieldExpiresAtUtc")]
    [InlineData("LegacyMagicShieldRemoveOnDead")]
    [InlineData("StatusFlags")]
    public async Task RootStoreRejectsAmbiguousProjectionWithoutRetiringActualClock(string ownedKey)
    {
        await using var fixture = new RootMagicEntryFixture(itemEffects:
            new(RightEffect: 0, RingEffect: 0, NeckEffect: 200), itemEffectLifetimeControls: true,
            opaqueCaseControls: true, mixedCaseRootKeys: true);
        await fixture.QueryRootItemEffectsThroughFrameAsync();
        await fixture.CastRootItemAttackThroughFrameAsync();
        var original = fixture.Owner;
        var clock = Assert.IsType<LegacyRootItemEffectFlagClock>(fixture.ReadActualRootClock(8));
        var start = fixture.Order.Count;
        var failure = Assert.Throws<InvalidDataException>(() => fixture.AttemptAmbiguousRootProjectionThroughStore(ownedKey));
        Assert.Equal("rootFlagSuccessionProjectionInvalid", failure.Message);
        Assert.IsType<InvalidDataException>(failure.InnerException);
        Assert.Same(original, fixture.Owner);
        Assert.Same(clock, fixture.ReadActualRootClock(8));
        Assert.True(fixture.RootShieldClockExists());
        Assert.True(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, 8));
        Assert.Equal(start, fixture.Order.Count);
        Assert.Equal("one", fixture.Owner.Character.Attributes["Opaque"]);
        Assert.Equal("two", fixture.Owner.Character.Attributes["opaque"]);

        await fixture.StartRootItemEffectRuntimeAsync();
        await fixture.AdvanceRootItemEffectRuntimeAsync(TimeSpan.FromSeconds(15));
        Assert.False(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, 8));
        Assert.False(fixture.RootShieldClockExists());
        Assert.Null(fixture.ReadActualRootClock(8));
    }

    [Fact]
    public async Task RootExpirySkipsExternallyMalformedSubjectAndContinuesRealPeer()
    {
        await using var fixture = new RootMagicEntryFixture(itemEffects:
            new(RightEffect: 0, RingEffect: 0, NeckEffect: 200), itemEffectLifetimeControls: true,
            opaqueCaseControls: true, mixedCaseRootKeys: true);
        await fixture.QueryRootItemEffectsThroughFrameAsync();
        await fixture.CastRootItemAttackThroughFrameAsync();
        Assert.True(fixture.RootShieldClockExists());
        fixture.CorruptRootMarkerForExpiryRead();
        var current = fixture.Owner;
        Assert.True(HasLegacyStatusFlag(current.Character.Attributes, 8));
        Assert.True(fixture.RootShieldClockExists()); // The real owner is retained; malformed external DATA cannot authorize expiry.
        // A second real subject earns Classic Stigma through its public Frame;
        // it must still expire while the first subject's marker is ambiguous.
        await fixture.CastPeerClassicStigmaThroughFrameAsync();
        Assert.True(HasLegacyStatusFlag(fixture.Peer.Character.Attributes, 9));
        var peerDeadline = DateTimeOffset.Parse(fixture.Peer.Character.Attributes["LegacyStigmaExpiresAtUtc"], CultureInfo.InvariantCulture);
        var untilPeer = peerDeadline - fixture.Now;
        Assert.True(untilPeer >= TimeSpan.FromSeconds(15));
        await fixture.StartRootItemEffectRuntimeAsync();
        int before = fixture.Order.Count;
        await fixture.AdvanceRootItemEffectRuntimeAsync(untilPeer);
        Assert.Same(current, fixture.Owner);
        Assert.True(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, 8));
        Assert.True(fixture.RootShieldClockExists());
        Assert.False(HasLegacyStatusFlag(fixture.Peer.Character.Attributes, 9));
        Assert.False(fixture.Peer.Character.Attributes.ContainsKey("LegacyStigmaExpiresAtUtc"));
        Assert.Contains(ParseMsgUpdates(fixture.FramesSince(TargetPlayerUid, before)), packet =>
            packet.Uid == TargetPlayerUid && packet.Updates.Any(entry =>
                entry.Type == LegacyMsgUpdateDataType.StatusFlag && entry.Values is not null &&
                !IsLegacyStatusFlagSet(entry.Values, 9)));
        Assert.Empty(RootManualLifetimeVectors(fixture.FramesSince(PlayerUid, before)));
        Assert.Empty(RootManualLifetimeVectors(fixture.FramesSince(TargetPlayerUid, before)));
        Assert.Equal("one", fixture.Owner.Character.Attributes["Opaque"]);
        Assert.Equal("two", fixture.Owner.Character.Attributes["opaque"]);
    }
}
