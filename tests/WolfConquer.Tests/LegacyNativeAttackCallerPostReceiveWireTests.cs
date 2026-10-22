using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Theory]
    [InlineData(1000)]
    [InlineData(1001)]
    public async Task NativeAttackCallerWireUsesPostReceiveZeroAfterRealLightningShieldAbsorbsWholeHit(
        ushort spellId)
    {
        using var fixture = new NativeAttackCallerPublicationFixture(spellId, soul: 0,
            peerHitpoints: 5000, lightningShield: true);
        await fixture.PrepareRealQueriesAsync();
        await fixture.PrepareRealLightningShieldAsync();
        await fixture.CastAsync(spellId);

        Assert.Equal(5000L, fixture.Peer.Character.MinHitPoints);
        Assert.False(HasLegacyStatusFlag(fixture.Peer.Character.Attributes, 5));
        Assert.True(HasLegacyStatusFlag(fixture.Peer.Character.Attributes, LegacyLightningShieldFlag));
        Assert.True(HasLegacyStatusFlag(fixture.Peer.Character.Attributes, LegacyLightningShieldActivatedFlag));
        var azure = ParseMsgUpdates(fixture.PeerFrames)
            .Where(packet => packet.Uid == TargetPlayerUid).SelectMany(packet => packet.Updates)
            .Where(entry => entry.Type == LegacyMsgUpdateDataType.AzureShield &&
                entry.Flag == LegacyLightningShieldActivatedFlag).ToArray();
        Assert.Equal(new uint[] { 20_000, 15_500 }, azure.Select(entry => entry.Damage));
        Assert.Equal("15500", fixture.Peer.Character.Attributes["LightningShieldLeft"]);
        foreach (var frames in new[] { fixture.OwnerFrames, fixture.PeerFrames })
        {
            var animation = Assert.Single(ParseSpellAnimations(frames));
            Assert.Equal(spellId, animation.SpellId);
            var target = Assert.Single(animation.Targets);
            Assert.Equal(TargetPlayerUid, target.Uid);
            Assert.Equal(0U, target.Damage); // Actual Receive absorbs4500; pre-Receive scalar is not wire.
            Assert.True(target.Hit);
        }
    }
}
