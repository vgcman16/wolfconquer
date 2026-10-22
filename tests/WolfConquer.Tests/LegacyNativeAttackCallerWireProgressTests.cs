using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Theory]
    [InlineData(1000, 0)]
    [InlineData(1000, 3)]
    [InlineData(1001, 0)]
    [InlineData(1001, 3)]
    public async Task NativeAttackCallerWireRetainsCalculatorScalarWhenRealReceiveKillsWithLessHitpoints(
        ushort spellId, byte soul)
    {
        using var fixture = new NativeAttackCallerPublicationFixture(spellId, soul, peerHitpoints: 1000);
        await fixture.PrepareRealQueriesAsync();
        await fixture.CastAsync(spellId);

        Assert.Equal(0L, fixture.Peer.Character.MinHitPoints);
        Assert.True(HasLegacyStatusFlag(fixture.Peer.Character.Attributes, 5));
        foreach (var frames in new[] { fixture.OwnerFrames, fixture.PeerFrames })
        {
            var animation = Assert.Single(ParseSpellAnimations(frames));
            Assert.Equal(spellId, animation.SpellId);
            Assert.Equal(soul, animation.LevelHu);
            var target = Assert.Single(animation.Targets);
            Assert.Equal(TargetPlayerUid, target.Uid);
            Assert.Equal(4500U, target.Damage); // Queried1 + raw8999, identitySoul /2; not min(HP1000,4500).
            Assert.True(target.Hit);
        }
    }

    [Theory]
    [InlineData(1000)]
    [InlineData(1001)]
    public async Task NativeAttackPlayerOnlyCallerLeavesLoadedWeaponAndSpellProgressUnchanged(
        ushort spellId)
    {
        using var fixture = new NativeAttackCallerPublicationFixture(spellId, soul: 0, peerHitpoints: 5000);
        await fixture.PrepareRealQueriesAsync();
        await fixture.CastAsync(spellId);

        Assert.Equal(500L, fixture.Peer.Character.MinHitPoints); // A real nonlethal4500 hit, not a no-op.
        Assert.Equal(4500U, Assert.Single(Assert.Single(ParseSpellAnimations(fixture.OwnerFrames)).Targets).Damage);
        Assert.Equal(fixture.InitialSpell, Assert.Single(fixture.Owner.PlayerSpells.Spells));
        Assert.Equal(fixture.InitialProficiency, Assert.Single(fixture.Owner.PlayerProficiencies.Proficiencies));
        Assert.Equal(fixture.InitialSpell, fixture.PersistedSpell);
        Assert.Equal(fixture.InitialProficiency, fixture.PersistedProficiency);
        Assert.DoesNotContain(fixture.OwnerFrames, bytes => LegacySpellPacket.TryParse(ReadFrame(bytes), out _));
        Assert.DoesNotContain(fixture.OwnerFrames, bytes => LegacyProficiencyPacket.TryParse(ReadFrame(bytes), out _));
    }
}
