using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class OwnedDragonhowlOutsideRosterTests
{
    public static IEnumerable<object[]> InvalidSelectedPolicyInputs()
    {
        foreach (ushort spell in new ushort[] { 17_490, 17_650 })
            foreach (var input in new[] { PolicyInput.UserNaN, PolicyInput.UserInfinity, PolicyInput.UserNegative,
                PolicyInput.UserMalformed, PolicyInput.SelectedServerNaN, PolicyInput.DoubleNaN,
                PolicyInput.DoubleInfinity, PolicyInput.DoubleNegative, PolicyInput.MaximumMalformed, PolicyInput.Overflow,
                PolicyInput.MissingSecondStats, PolicyInput.NegativeSecondStats,
                PolicyInput.MissingSecondThreshold, PolicyInput.ZeroSecondThreshold })
                yield return new object[] { spell, input };
    }

    [Theory]
    [MemberData(nameof(InvalidSelectedPolicyInputs))]
    public Task SelectedInvalidPolicyOrLaterRequiredProgressionRefusesBeforeAnyCostOrReward(ushort spell, PolicyInput input) =>
        RunPolicy(spell, input, async c =>
        {
            var f = c.Fixture; var actor = f.Current(); var peer = f.Current(Peer);
            var a = Binding(c, Actor); var p = Binding(c, Peer);
            var files = f.Data.Files(); var an = Native(a); var pn = Native(p);
            var actorFields = actor.Character.Attributes.ToArray(); var peerFields = peer.Character.Attributes.ToArray();
            var cooldowns = actor.MagicCooldowns?.ToArray();
            var items = PlayerItemSnapshotLoader.ToLegacyBytes(actor.PlayerItems);
            var spells = PlayerSpellSnapshotLoader.ToLegacyBytes(actor.PlayerSpells);
            var actorQuery = QueryIdentity(a); var peerQuery = QueryIdentity(p);
            await f.SendAsync(Actor, Physical(c));
            Assert.Same(actor, f.Current()); Assert.Same(peer, f.Current(Peer));
            Assert.Equal(an, Native(a)); Assert.Equal(pn, Native(p));
            Assert.Equal(actorFields, f.Current().Character.Attributes.ToArray());
            Assert.Equal(peerFields, f.Current(Peer).Character.Attributes.ToArray());
            Assert.Equal(cooldowns, f.Current().MagicCooldowns?.ToArray());
            Assert.Equal(items, PlayerItemSnapshotLoader.ToLegacyBytes(f.Current().PlayerItems));
            Assert.Equal(spells, PlayerSpellSnapshotLoader.ToLegacyBytes(f.Current().PlayerSpells));
            Assert.Same(actorQuery, QueryIdentity(a)); Assert.Same(peerQuery, QueryIdentity(p));
            Assert.True(f.Store.IsBoundSpellOwnerCurrent(a)); Assert.True(f.Store.IsBoundSpellOwnerCurrent(p));
            foreach (var target in c.Targets)
            {
                Assert.True(c.Monsters.TryGetVisibleInstance(target, c.Clock.GetUtcNow(), out var current));
                Assert.Equal(500U, current.Hitpoints); Assert.Equal(target.Uid, current.Uid);
            }
            Assert.Empty(f.ActorFrames); Assert.Empty(f.Frames(Peer));
            Assert.False(f.Recovery.IsBlocked); Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
            Assert.False(File.Exists(c.SpellPath)); f.Data.AssertFiles(files);
            Assert.Contains("ownedDragonhowlTeamExperiencePreparationUnavailable", Log(c), StringComparison.Ordinal);
        });

    [Theory]
    [InlineData((ushort)17_490, PolicyInput.EligibleZero)]
    [InlineData((ushort)17_650, PolicyInput.EligibleZero)]
    [InlineData((ushort)17_490, PolicyInput.IneligibleNaN)]
    [InlineData((ushort)17_650, PolicyInput.IneligibleNaN)]
    public Task ZeroGainDoesNotExcuseAnActuallyRetiredRequiredRosterMember(ushort spell, PolicyInput input) =>
        RunPolicy(spell, input, async c =>
        {
            var f = c.Fixture; var actor = f.Current(); var a = Binding(c, Actor); var p = Binding(c, Peer);
            var files = f.Data.Files(); var native = Native(a); var retiredBefore = Native(p);
            await f.Connection(Peer).CompleteTransportCloseAsync();
            Assert.True(p.IsRetired); Assert.False(f.Store.TryGet(f.Connection(Peer), out _));
            Assert.Contains(f.Connection(Peer), c.Teams.GetConnections(f.Connection(Actor)));
            Assert.Equal(retiredBefore, Native(p)); f.Data.AssertFiles(files);
            f.ActorFrames.Clear(); f.Frames(Peer).Clear();
            await f.SendAsync(Actor, Physical(c));
            Assert.Same(actor, f.Current()); Assert.Equal(native, Native(a)); Assert.Equal(retiredBefore, Native(p));
            Assert.True(f.Store.IsBoundSpellOwnerCurrent(a));
            Assert.True(c.Monsters.TryGetVisibleInstance(c.Targets[0], c.Clock.GetUtcNow(), out var target));
            Assert.Equal(500U, target.Hitpoints);
            Assert.Empty(f.ActorFrames); Assert.Empty(f.Frames(Peer)); f.Data.AssertFiles(files);
            Assert.False(f.Recovery.IsBlocked); Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
        });
}
