using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Theory]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public async Task RootAdmissionNegativeRequiredColdTimeCannotBecomeNoCooldown(int coldTime)
    {
        await using var fixture = new RootMagicEntryFixture(coldTime: coldTime);
        var before = fixture.CaptureAdmission();
        await fixture.CastAsync(1_000).WaitAsync(TimeSpan.FromSeconds(5));
        fixture.AssertAdmissionRejected(before);
    }

    [Theory]
    [InlineData((ushort)1_000, "mana")]
    [InlineData((ushort)1_011, "mana")]
    [InlineData((ushort)1_010, "mana")]
    [InlineData((ushort)1_005, "mana")]
    [InlineData((ushort)1_000, "stamina")]
    [InlineData((ushort)1_011, "stamina")]
    [InlineData((ushort)1_010, "stamina")]
    [InlineData((ushort)1_005, "stamina")]
    [InlineData((ushort)1_000, "book")]
    [InlineData((ushort)1_011, "book")]
    [InlineData((ushort)1_010, "book")]
    [InlineData((ushort)1_005, "book")]
    [InlineData((ushort)1_000, "passive")]
    [InlineData((ushort)1_011, "passive")]
    [InlineData((ushort)1_010, "passive")]
    [InlineData((ushort)1_005, "passive")]
    public async Task RootAdmissionRegisteredRejectionPreservesCompleteOwnerAndOutput(ushort id, string denial)
    {
        await using var fixture = new RootMagicEntryFixture(selectedId: id, rawCadence: 1_500,
            coldTime: 2_000, mana: denial == "mana" ? 20 : 1_000,
            stamina: denial == "stamina" ? 9UL : 100UL,
            omitSelectedLearnedInput: denial == "book", passive: denial == "passive");
        var before = fixture.CaptureAdmission(id);
        await fixture.CastAsync(id).WaitAsync(TimeSpan.FromSeconds(5));
        fixture.AssertAdmissionRejected(before);
    }

    [Theory]
    [InlineData((ushort)1_000, false)]
    [InlineData((ushort)1_000, true)]
    [InlineData((ushort)1_011, false)]
    [InlineData((ushort)1_011, true)]
    public async Task RootAdmissionRegisteredMissingOrOutOfRangeTargetHasNoPrefix(ushort id, bool missing)
    {
        await using var fixture = new RootMagicEntryFixture(selectedId: id, coldTime: 2_000);
        if (!missing) fixture.MoveAdmissionPeer(840); // Same real map block; distance10 > real range4.
        var before = fixture.CaptureAdmission(id);
        await fixture.CastAsync(id, target: missing ? 9_999_999U : TargetPlayerUid)
            .WaitAsync(TimeSpan.FromSeconds(5));
        fixture.AssertAdmissionRejected(before);
    }

    [Theory]
    [InlineData((ushort)1_000)]
    [InlineData((ushort)1_011)]
    [InlineData((ushort)1_010)]
    [InlineData((ushort)1_005)]
    public async Task RootAdmissionRegisteredCooldownRejectionPreservesEarnedDeadlineAndResources(ushort id)
    {
        await using var fixture = new RootMagicEntryFixture(selectedId: id, coldTime: 2_000);
        await fixture.CastAsync(id).WaitAsync(TimeSpan.FromSeconds(5));
        fixture.AssertCostPairSince(0, 90, 960);
        Assert.Equal(RootMagicEntryOrigin.AddMilliseconds(2_000), fixture.Owner.MagicCooldowns![id]);
        fixture.Advance(TimeSpan.FromMilliseconds(500)); // Cadence is valid; the independently earned cold deadline is not.
        var before = fixture.CaptureAdmission(id);
        await fixture.CastAsync(id).WaitAsync(TimeSpan.FromSeconds(5));
        fixture.AssertAdmissionRejected(before);
    }

    [Theory]
    [InlineData("range")]
    [InlineData("stamina")]
    [InlineData("mana")]
    [InlineData("cooldown")]
    public async Task RootAdmissionManualEffectsCannotAuthorizeOrPublishBeforeRejection(string denial)
    {
        await using var fixture = new RootMagicEntryFixture(coldTime: 2_000,
            mana: denial == "mana" ? 20 : 100, stamina: denial == "stamina" ? 9UL : 100UL,
            itemEffects: new(RightEffect: denial == "mana" ? 0U : 202U, RingEffect: 1, NeckEffect: 200));
        await fixture.QueryRootItemEffectsAsync().WaitAsync(TimeSpan.FromSeconds(5));
        if (denial == "cooldown")
        {
            await fixture.CastAsync(1_000).WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal((ushort)1_175, fixture.Owner.LegacyRandomSpell); // Real accepted MP proc, not injected authorization.
            fixture.Advance(TimeSpan.FromMilliseconds(500));
        }
        var before = fixture.CaptureAdmission();
        await fixture.CastAsync(1_000, target: denial == "range" ? 9_999_999U : TargetPlayerUid)
            .WaitAsync(TimeSpan.FromSeconds(5));
        fixture.AssertAdmissionRejected(before);
    }

    [Theory]
    [InlineData((ushort)0, 500)]
    [InlineData((ushort)1_500, 2_500)]
    public async Task RootAdmissionTwoCastersShareImmutableCatalogButOwnIndependentCadence(
        ushort rawCadence, int effectiveMilliseconds)
    {
        await using var fixture = new RootMagicEntryFixture(rawCadence: rawCadence);
        fixture.EnableAdmissionPeerCaster();
        var held = fixture.NativeRow(1_000);
        var fields = held.Source.Fields.ToArray();
        var start = fixture.Order.Count;
        await fixture.CastAsync(1_000).WaitAsync(TimeSpan.FromSeconds(5));
        await fixture.CastAdmissionPeerAsync(1_000).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Same(held, fixture.NativeRow(1_000));
        Assert.Equal(rawCadence, held.CoolDown);
        Assert.Equal(fields, held.Source.Fields);
        Assert.Equal("90", fixture.Owner.Character.Attributes["Stamina"]);
        Assert.Equal("90", fixture.Peer.Character.Attributes["Stamina"]);
        Assert.Equal(960L, fixture.Owner.Character.MinMana);
        Assert.Equal(960L, fixture.Peer.Character.MinMana);
        foreach (var uid in new[] { PlayerUid, TargetPlayerUid })
        {
            var costs = ParseMsgUpdates(fixture.FramesSince(uid, start)).Where(packet => packet.Uid == uid)
                .SelectMany(packet => packet.Updates)
                .Where(entry => entry.Type is LegacyMsgUpdateDataType.Stamina or LegacyMsgUpdateDataType.Mana)
                .Select(entry => (entry.Type, entry.Value)).ToArray();
            Assert.Equal(new[] { (LegacyMsgUpdateDataType.Stamina, 90UL), (LegacyMsgUpdateDataType.Mana, 960UL) }, costs);
        }
        fixture.Advance(TimeSpan.FromMilliseconds(effectiveMilliseconds - 1));
        var before = fixture.CaptureAdmission();
        await fixture.CastAsync(1_000).WaitAsync(TimeSpan.FromSeconds(5));
        await fixture.CastAdmissionPeerAsync(1_000).WaitAsync(TimeSpan.FromSeconds(5));
        fixture.AssertAdmissionRejected(before);
        fixture.Advance(TimeSpan.FromMilliseconds(1));
        await fixture.CastAsync(1_000).WaitAsync(TimeSpan.FromSeconds(5));
        await fixture.CastAdmissionPeerAsync(1_000).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(rawCadence, held.CoolDown);
        Assert.Equal(fields, held.Source.Fields);
        Assert.Equal("80", fixture.Owner.Character.Attributes["Stamina"]);
        Assert.Equal("80", fixture.Peer.Character.Attributes["Stamina"]);
        Assert.Equal(920L, fixture.Owner.Character.MinMana);
        Assert.Equal(920L, fixture.Peer.Character.MinMana);
    }
}
