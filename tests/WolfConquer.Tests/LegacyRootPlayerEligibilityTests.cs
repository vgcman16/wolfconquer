using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Theory]
    [InlineData((ushort)1_000, "hp")]
    [InlineData((ushort)1_001, "hp")]
    [InlineData((ushort)1_000, "dead")]
    [InlineData((ushort)1_001, "dead")]
    [InlineData((ushort)1_000, "ghost")]
    [InlineData((ushort)1_001, "ghost")]
    [InlineData((ushort)1_000, "invisible")]
    [InlineData((ushort)1_001, "invisible")]
    [InlineData((ushort)1_000, "curse")]
    [InlineData((ushort)1_001, "curse")]
    [InlineData((ushort)1_000, "fly")]
    [InlineData((ushort)1_001, "fly")]
    [InlineData((ushort)1_000, "targetWatch")]
    [InlineData((ushort)1_001, "targetWatch")]
    [InlineData((ushort)1_000, "casterWatch")]
    [InlineData((ushort)1_001, "casterWatch")]
    [InlineData((ushort)1_000, "protect")]
    [InlineData((ushort)1_001, "protect")]
    [InlineData((ushort)1_000, "map700")]
    [InlineData((ushort)1_001, "map700")]
    [InlineData((ushort)1_000, "peace")]
    [InlineData((ushort)1_001, "peace")]
    [InlineData((ushort)1_000, "safeZone")]
    [InlineData((ushort)1_001, "safeZone")]
    public async Task RootPlayerEligibilityDenialPreservesAdmissionAndAllOutputs(ushort id, string denial)
    {
        await using var fixture = new RootMagicEntryFixture(selectedId: id, coldTime: 2_000,
            playerEligibilityControls: true);
        var reason = fixture.DeclareRootPlayerEligibilityInput(id, denial);
        var before = fixture.CaptureAdmission(id);
        await fixture.CastAsync(id);
        fixture.AssertAdmissionRejected(before);
        Assert.Contains(reason, fixture.EligibilityLog, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData((ushort)1_000)]
    [InlineData((ushort)1_001)]
    public async Task RootPlayerEligibilityDenialPrecedesRealManualItemPrefix(ushort id)
    {
        await using var fixture = new RootMagicEntryFixture(selectedId: id, coldTime: 2_000,
            mana: 100, itemEffects: new(RightEffect: 202, RingEffect: 1, NeckEffect: 200),
            playerEligibilityControls: true);
        await fixture.QueryRootItemEffectsAsync(); // Genuine cache publication, not seeded result fields.
        fixture.DeclareRootPlayerEligibilityInput(id, "invisible");
        var before = fixture.CaptureAdmission(id);
        await fixture.CastAsync(id);
        fixture.AssertAdmissionRejected(before);
    }

    [Theory]
    [InlineData((ushort)1_000, false)]
    [InlineData((ushort)1_001, false)]
    [InlineData((ushort)1_000, true)]
    [InlineData((ushort)1_001, true)]
    public async Task RootPlayerEligibilityHealthyAndAttackInFlyAllowedKeepNativeSuccess(ushort id, bool flying)
    {
        await using var fixture = new RootMagicEntryFixture(selectedId: id, coldTime: 2_000,
            playerEligibilityControls: true);
        if (flying) fixture.DeclareRootFlyingTarget();
        Assert.True(LegacyMagicRuntime.TryResolveNativeLearnedSpell(fixture.Owner.PlayerSpells,
            fixture.WorldData, id, out _, out var selected, out _));
        Assert.True(selected.AttackInFly); // Existing native field5=0 rule, not a blanket flying denial.
        var before = fixture.CaptureAdmission(id);
        await fixture.CastAsync(id);
        fixture.AssertProducedStamp(fixture.Now);
        fixture.AssertCostPairSince(before.FrameCount, 90, 960);
        Assert.Equal(fixture.Now.AddMilliseconds(2_000), fixture.Owner.MagicCooldowns![id]);
        Assert.True(fixture.Peer.Character.MinHitPoints < before.Peer.Character.MinHitPoints);
        var animation = Assert.Single(ParseSpellAnimations(fixture.FramesSince(PlayerUid, before.FrameCount)),
            packet => packet.SpellId == id);
        var target = Assert.Single(animation.Targets, row => row.Uid == TargetPlayerUid);
        Assert.True(target.Damage > 0);
        Assert.Equal(before.CoolDown, before.Row.CoolDown);
        Assert.Equal(before.SourceFields, before.Row.Source.Fields);
    }
}
