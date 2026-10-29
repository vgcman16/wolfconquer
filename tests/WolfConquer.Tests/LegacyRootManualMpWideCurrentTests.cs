using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    // Component route control, distinct from the genuine admitted Frame cast
    // in LegacyNativeOwnedVitalsTests. Existing in-range byte/order goldens stay.
    [Theory]
    [InlineData(65535L)] [InlineData(65536L)] [InlineData(4294967295L)]
    public async Task RootManualMpPreservesWideCurrentManaBeforeItsOriginalValidatedCosts(long mana)
    {
        await using var fixture = new RootMagicEntryFixture(mana: 1000, itemEffects: new(MaximumMana: 1000));
        await fixture.QueryRootItemEffectsAsync();
        fixture.SetWideCurrentManaInput(mana);
        var start = fixture.Order.Count;
        await fixture.CastAsync(1000);
        Assert.Equal(mana - 40, fixture.Owner.Character.MinMana);
        Assert.Equal(new[] { (LegacyMsgUpdateDataType.Mana, (ulong)mana),
            (LegacyMsgUpdateDataType.Stamina, 90UL), (LegacyMsgUpdateDataType.Mana, checked((ulong)(mana - 40))) },
            RootMagicEntryCosts(fixture.FramesSince(PlayerUid, start)));
        foreach (var uid in new[] { PlayerUid, TargetPlayerUid })
            AssertRootManualPrefixAnimation(Assert.Single(ParseSpellAnimations(fixture.FramesSince(uid, start)),
                packet => packet.SpellId == 1175), 1175, 0, 830, 584, 0);
    }

    private sealed partial class RootMagicEntryFixture
    {
        public void SetWideCurrentManaInput(long mana)
        {
            // Independent current-Mana input on the actual component Store,
            // after earning its equipment maximum. Do not construct an unrelated
            // overflowing uint.MaxValue base maximum plus equipment bonuses.
            var owner = Owner;
            sessions.Set(connections[PlayerUid], owner with
                { Character = owner.Character with { MinMana = mana } });
        }
    }
}
