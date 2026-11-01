using System.Runtime.ExceptionServices;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class OwnedDragonhowlOutsideRosterTests
{
    [Theory]
    [InlineData((ushort)17_490)]
    [InlineData((ushort)17_650)]
    public Task ActuallyClosedRequiredTeamMemberRefusesBeforeCostStampKillOrOutput(ushort spell) =>
        Run(spell, threshold: true, secondMonster: false, async c =>
        {
            var f = c.Fixture; var actor = f.Current(); var peer = f.Current(Peer);
            var a = Binding(c, Actor); var p = Binding(c, Peer);
            var files = f.Data.Files(); var actorNative = Native(a);
            var maximum = actor.EquipmentHitpointsStatus; var overlay = actor.PhysicalStatusOverlay;
            var fields = actor.Character.Attributes.ToArray();
            var cooldowns = actor.MagicCooldowns?.ToArray();
            var items = PlayerItemSnapshotLoader.ToLegacyBytes(actor.PlayerItems);
            var spells = PlayerSpellSnapshotLoader.ToLegacyBytes(actor.PlayerSpells);
            await f.Connection(Peer).CompleteTransportCloseAsync();
            Assert.True(p.IsRetired); Assert.False(f.Store.TryGet(f.Connection(Peer), out _));
            // Observe the actual retained Team row; never inject/re-add a dead
            // connection to force the desired stale-member boundary.
            var roster = c.Teams.GetConnections(f.Connection(Actor));
            Assert.Equal(2, roster.Count); Assert.Contains(f.Connection(Peer), roster);
            Assert.Same(peer.NativeGameClient, f.Connection(Peer).NativeGameClient);
            f.Data.AssertFiles(files); var retiredNative = Native(p);
            f.ActorFrames.Clear(); f.Frames(Peer).Clear();
            await f.SendAsync(Actor, Physical(c));
            Assert.Same(actor, f.Current()); Assert.Equal(actorNative, Native(a)); Assert.Equal(retiredNative, Native(p));
            Assert.Same(maximum, f.Current().EquipmentHitpointsStatus); Assert.Same(overlay, f.Current().PhysicalStatusOverlay);
            Assert.Equal(fields, f.Current().Character.Attributes.ToArray());
            Assert.Equal(cooldowns, f.Current().MagicCooldowns?.ToArray());
            Assert.Equal(items, PlayerItemSnapshotLoader.ToLegacyBytes(f.Current().PlayerItems));
            Assert.Equal(spells, PlayerSpellSnapshotLoader.ToLegacyBytes(f.Current().PlayerSpells));
            Assert.True(f.Store.IsBoundSpellOwnerCurrent(a)); Assert.True(p.IsRetired);
            Assert.False(f.Store.TryGet(f.Connection(Peer), out _));
            Assert.True(c.Monsters.TryGetVisibleInstance(c.Targets[0], c.Clock.GetUtcNow(), out var target));
            Assert.Equal(500U, target.Hitpoints); Assert.Equal(c.Targets[0].Uid, target.Uid);
            Assert.Empty(f.ActorFrames); Assert.Empty(f.Frames(Peer));
            Assert.False(f.Recovery.IsBlocked); Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
            Assert.False(File.Exists(c.SpellPath)); f.Data.AssertFiles(files);
            Assert.Contains("refused owned Dragonhowl physical", Log(c), StringComparison.Ordinal);
        });

    [Theory]
    [InlineData((ushort)17_490, false)]
    [InlineData((ushort)17_650, false)]
    [InlineData((ushort)17_490, true)]
    [InlineData((ushort)17_650, true)]
    public Task ActualRecipientEofAtFirstAwardStopsLaterTargetWhilePreservingCommittedPrefix(ushort spell, bool threshold) =>
        Run(spell, threshold, secondMonster: true, async c =>
        {
            var f = c.Fixture; var actor = Binding(c, Actor); var peer = Binding(c, Peer);
            var expected = ExpectedAwardFiles(c);
            Task? close = null; Exception? assertionFailure = null; int observed = 0;
            f.BeforeSend = (recipient, bytes) =>
            {
                if (recipient != Peer || !bytes.AsSpan().SequenceEqual(threshold ? Exp9 : Exp10)) return;
                try
                {
                    Assert.False(WorldSessionStore.IsCurrentThreadInsideAnyConnectionGate);
                    Assert.Equal(0, observed++); AssertAward(c, peer);
                    Assert.False(c.Monsters.TryGetVisibleInstance(c.Targets[0], c.Clock.GetUtcNow(), out _));
                    Assert.True(c.Monsters.TryGetVisibleInstance(c.Targets[1], c.Clock.GetUtcNow(), out var second));
                    Assert.Equal(500U, second.Hitpoints); Assert.Equal(639_778_909U, actor.Player.ConquerPoints);
                    f.Data.AssertFiles(expected);
                    close = f.Connection(Peer).CompleteTransportCloseAsync();
                    // The real close marks EOF synchronously; actual removal
                    // awaits this operation. Never synchronously await here.
                    Assert.False(close.IsCompleted);
                }
                catch (Exception error) { assertionFailure = error; throw; }
            };
            try { await f.Handler.HandleFrameAsync(f.Connection(Actor), OwnedEquipmentBehaviorFixture.ReadFrame(Physical(c))); }
            finally
            {
                f.BeforeSend = null;
                if (close is not null) await close;
            }
            if (assertionFailure is not null) ExceptionDispatchInfo.Capture(assertionFailure).Throw();
            Assert.NotNull(close); Assert.Equal(1, observed);
            Assert.True(f.Recovery.IsBlocked); Assert.True(actor.IsRetired); Assert.True(peer.IsRetired);
            Assert.False(f.Store.TryGet(f.Connection(Actor), out _)); Assert.False(f.Store.TryGet(f.Connection(Peer), out _));
            Assert.False(actor.Client.OnAutoAttack); Assert.False(peer.Client.OnAutoAttack);
            Assert.Equal(493U, actor.Player.Mana); Assert.Equal((ushort)95, actor.Player.Stamina);
            Assert.Equal(639_778_909U, actor.Player.ConquerPoints);
            Assert.Equal(threshold ? 540 : 30, peer.Player.HitPoints);
            Assert.Equal((ushort)(threshold ? 11 : 10), peer.Player.Level);
            Assert.Equal(threshold ? 9UL : 10UL, peer.Player.Experience);
            Assert.False(c.Monsters.TryGetVisibleInstance(c.Targets[0], c.Clock.GetUtcNow(), out _));
            Assert.True(c.Monsters.TryGetVisibleInstance(c.Targets[1], c.Clock.GetUtcNow(), out var remaining));
            Assert.Equal(500U, remaining.Hitpoints);
            Assert.Equal(1, f.Frames(Peer).Count(bytes => bytes.AsSpan().SequenceEqual(threshold ? Exp9 : Exp10)));
            Assert.DoesNotContain(f.ActorFrames, bytes => IsAnimation(bytes, spell));
            Assert.DoesNotContain(f.Frames(Peer), bytes => IsAnimation(bytes, spell));
            Assert.False(File.Exists(c.SpellPath)); Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
            f.Data.AssertFiles(expected);
            int count = f.ActorFrames.Count + f.Frames(Peer).Count;
            await f.Handler.HandleFrameAsync(f.Connection(Actor), OwnedEquipmentBehaviorFixture.ReadFrame(Physical(c)));
            Assert.Equal(count, f.ActorFrames.Count + f.Frames(Peer).Count);
            Assert.Equal(639_778_909U, actor.Player.ConquerPoints); f.Data.AssertFiles(expected);
        });
}
