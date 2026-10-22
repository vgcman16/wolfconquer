using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Theory]
    [InlineData(1_000, false, 0, 50)]
    [InlineData(1_000, false, 3, 50)]
    [InlineData(1_001, false, 0, 50)]
    [InlineData(1_001, false, 3, 50)]
    [InlineData(1_000, true, 0, 0)]
    [InlineData(1_000, true, 3, 0)]
    [InlineData(1_001, true, 0, 0)]
    [InlineData(1_001, true, 3, 0)]
    public async Task NativeAttack1000And1001CompletePlayerMagicWithIdentitySoulThenHalfIncludingZeroHit(
        ushort spellId, bool magicDefender, byte soul, uint expectedDamage)
    {
        using var fixture = new NativeMagicCallerFixture(spellId, rawDamage: 100, soul);
        await fixture.QueryMagicAttackAsync();
        Assert.Equal(5_000L, fixture.Peer.Character.MinHitPoints);
        if (magicDefender) await fixture.EarnPeerMagicDefenderAsync();
        var ownStart = fixture.OwnerFrames.Count;
        var peerStart = fixture.PeerFrames.Count;
        await fixture.CastAsync(spellId, TargetPlayerUid);
        Assert.Equal(5_000L - expectedDamage, fixture.Peer.Character.MinHitPoints);
        Assert.Equal(1_000L, fixture.Owner.Character.MinMana);
        Assert.False(HasLegacyStatusFlag(fixture.Peer.Character.Attributes, 5));
        foreach (var frames in new[]
                 { fixture.OwnerFrames.Skip(ownStart), fixture.PeerFrames.Skip(peerStart) })
        {
            var animation = Assert.Single(ParseSpellAnimations(frames));
            Assert.Equal(PlayerUid, animation.AttackerUid);
            Assert.Equal(spellId, animation.SpellId);
            Assert.Equal(soul, animation.LevelHu); // Nonzero metadata is preserved; never a damage multiplier.
            Assert.Equal((ushort)832, animation.X);
            Assert.Equal((ushort)584, animation.Y);
            var target = Assert.Single(animation.Targets);
            Assert.Equal(TargetPlayerUid, target.Uid);
            Assert.Equal(expectedDamage, target.Damage);
            Assert.True(target.Hit); // The genuinely earned early-return1 /2 result0 still enqueues.
            Assert.Equal(magicDefender ? MsgAttackEffect.Imunity : MsgAttackEffect.None, target.Effect);
        }
        Assert.Contains(ParseMsgUpdates(fixture.PeerFrames.Skip(peerStart)), packet =>
            packet.Uid == TargetPlayerUid && packet.Updates.Any(entry =>
                entry.Type == LegacyMsgUpdateDataType.Hitpoints &&
                entry.Value == 5_000UL - expectedDamage)); // Includes the real unchanged-HP setter for0.
    }

    [Theory]
    [InlineData(1_000)]
    [InlineData(1_001)]
    public async Task NativeAttack1000And1001DoNotApplyPlayerHalfToMonsterOneClamp(ushort spellId)
    {
        using var fixture = new NativeMagicCallerFixture(spellId, rawDamage: 100, soul: 3);
        await fixture.QueryMagicAttackAsync();
        var start = fixture.OwnerFrames.Count;
        await fixture.CastAsync(spellId, fixture.Monster.Uid);
        var animation = Assert.Single(ParseSpellAnimations(fixture.OwnerFrames.Skip(start)));
        Assert.Equal(spellId, animation.SpellId);
        Assert.Equal((byte)3, animation.LevelHu);
        var target = Assert.Single(animation.Targets);
        Assert.Equal(fixture.Monster.Uid, target.Uid);
        Assert.Equal(1U, target.Damage); // Native Magic65 Defense2==0 clamp, no player /2.
        Assert.True(target.Hit);
        Assert.True(fixture.MonsterStates.TryGetVisibleInstance(
            fixture.Monster, fixture.Now, out var current));
        Assert.Equal(fixture.Monster.MaxLife - 1U, current.Hitpoints);
        Assert.Equal(5_000L, fixture.Peer.Character.MinHitPoints);
    }
}
