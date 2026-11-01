using System.Net.Sockets;
using System.Runtime.ExceptionServices;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class OwnedDragonhowlAwardSequenceTests
{
    [Theory]
    [InlineData((ushort)17_490)]
    [InlineData((ushort)17_650)]
    public Task MissingSecondAwardAutomaticStatsRefusesWholeCastBeforeFirstCostOrKill(ushort spellId) =>
        RunSequence(spellId, multiple: true, missingSpells: true, async s =>
        {
            var f = s.Fixture; var actor = f.Current(); var peer = f.Current(Peer);
            var a = Assert.IsType<LegacyOrdinarySpellBinding>(actor.NativeGameClient!.OrdinarySpellBinding);
            var p = Assert.IsType<LegacyOrdinarySpellBinding>(peer.NativeGameClient!.OrdinarySpellBinding);
            var files = f.Data.Files();
            var actorNative = (a.Player.HitPoints, a.Player.Mana, a.Player.Stamina, a.Player.ConquerPoints,
                a.Player.Level, a.Player.Experience, a.Client.OnAutoAttack, a.Client.AutoAttack);
            var peerNative = (p.Player.HitPoints, p.Player.Mana, p.Player.Stamina, p.Player.Level, p.Player.Experience);
            var hp = peer.EquipmentHitpointsStatus; var mana = peer.PhysicalStatusOverlay;
            await f.SendAsync(Actor, Request(s));
            Assert.Same(actor, f.Current()); Assert.Same(peer, f.Current(Peer));
            Assert.Equal(actorNative, (a.Player.HitPoints, a.Player.Mana, a.Player.Stamina, a.Player.ConquerPoints,
                a.Player.Level, a.Player.Experience, a.Client.OnAutoAttack, a.Client.AutoAttack));
            Assert.Equal(peerNative, (p.Player.HitPoints, p.Player.Mana, p.Player.Stamina, p.Player.Level, p.Player.Experience));
            Assert.Same(hp, f.Current(Peer).EquipmentHitpointsStatus); Assert.Same(mana, f.Current(Peer).PhysicalStatusOverlay);
            Assert.True(f.Store.IsBoundSpellOwnerCurrent(a)); Assert.True(f.Store.IsBoundSpellOwnerCurrent(p));
            foreach (var monster in s.Monsters)
            {
                Assert.True(s.Store.TryGetVisibleInstance(monster, s.Clock.GetUtcNow(), out var current));
                Assert.Equal(500U, current.Hitpoints); Assert.Equal(monster.Uid, current.Uid);
            }
            Assert.Empty(f.ActorFrames); Assert.Empty(f.Frames(Peer));
            Assert.False(f.Recovery.IsBlocked); Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
            Assert.False(File.Exists(s.SpellPath)); f.Data.AssertFiles(files);
            Assert.Contains("ownedDragonhowlTeamExperiencePreparationUnavailable", Log(s), StringComparison.Ordinal);
        }, omitSecondStats: true);

    [Theory]
    [InlineData((ushort)17_490, false)]
    [InlineData((ushort)17_650, false)]
    [InlineData((ushort)17_490, true)]
    [InlineData((ushort)17_650, true)]
    public Task FirstRealAwardOutputLossFencesCommittedPrefixBeforeLaterKillOrPeerReceive(ushort spellId, bool multiple) =>
        RunSequence(spellId, multiple, missingSpells: true, async s =>
        {
            var f = s.Fixture;
            var a = Assert.IsType<LegacyOrdinarySpellBinding>(f.Current().NativeGameClient!.OrdinarySpellBinding);
            var p = Assert.IsType<LegacyOrdinarySpellBinding>(f.Current(Peer).NativeGameClient!.OrdinarySpellBinding);
            var expected = ExpectedFiles(s, awards: 1, dead: false);
            byte[] xp = multiple ? ExperienceNine : ExperienceTen;
            int interrupted = 0;
            Exception? assertionFailure = null;
            f.BeforeSend = (recipient, bytes) =>
            {
                if (recipient != Peer || !bytes.AsSpan().SequenceEqual(xp)) return;
                try
                {
                    Assert.False(WorldSessionStore.IsCurrentThreadInsideAnyConnectionGate);
                    Assert.Equal(0, interrupted++);
                    AssertPeer(s, p, awards: 1, dead: false);
                    Assert.Equal(639_778_909L, f.Current().Character.ConquerPoints);
                    Assert.Equal(639_778_909U, a.Player.ConquerPoints);
                    Assert.False(s.Store.TryGetVisibleInstance(s.Monsters[0], s.Clock.GetUtcNow(), out _));
                    if (multiple)
                    {
                        Assert.True(s.Store.TryGetVisibleInstance(s.Monsters[1], s.Clock.GetUtcNow(), out var next));
                        Assert.Equal(500U, next.Hitpoints);
                    }
                    Assert.False(File.Exists(s.SpellPath)); f.Data.AssertFiles(expected);
                }
                catch (Exception error) { assertionFailure = error; throw; }
                throw new SocketException((int)SocketError.ConnectionReset);
            };
            try { await f.Handler.HandleFrameAsync(f.Connection(Actor), OwnedEquipmentBehaviorFixture.ReadFrame(Request(s))); }
            finally { f.BeforeSend = null; }
            if (assertionFailure is not null) ExceptionDispatchInfo.Capture(assertionFailure).Throw();
            Assert.Equal(1, interrupted);
            Assert.True(f.Recovery.IsBlocked);
            Assert.True(a.IsRetired); Assert.True(p.IsRetired);
            Assert.False(f.Store.TryGet(f.Connection(Actor), out _)); Assert.False(f.Store.TryGet(f.Connection(Peer), out _));
            Assert.False(a.Client.OnAutoAttack); Assert.False(p.Client.OnAutoAttack);
            Assert.Equal(493U, a.Player.Mana); Assert.Equal(95, (int)a.Player.Stamina);
            Assert.Equal(639_778_909U, a.Player.ConquerPoints);
            Assert.Equal(multiple ? 540 : 30, p.Player.HitPoints);
            Assert.Equal((ushort)(multiple ? 11 : 10), p.Player.Level);
            Assert.Equal(multiple ? 9UL : 10UL, p.Player.Experience);
            Assert.False(s.Store.TryGetVisibleInstance(s.Monsters[0], s.Clock.GetUtcNow(), out _));
            if (multiple)
            {
                Assert.True(s.Store.TryGetVisibleInstance(s.Monsters[1], s.Clock.GetUtcNow(), out var next));
                Assert.Equal(500U, next.Hitpoints);
            }
            Assert.DoesNotContain(f.ActorFrames, bytes => IsAnimation(bytes, spellId));
            Assert.DoesNotContain(f.Frames(Peer), bytes => bytes.AsSpan().SequenceEqual(xp));
            Assert.DoesNotContain(f.Frames(Peer), bytes => bytes.AsSpan().SequenceEqual(Dead30) || bytes.AsSpan().SequenceEqual(Dead810));
            Assert.False(File.Exists(s.SpellPath)); Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
            f.Data.AssertFiles(expected);
            int count = f.ActorFrames.Count + f.Frames(Peer).Count;
            await f.Handler.HandleFrameAsync(f.Connection(Actor), OwnedEquipmentBehaviorFixture.ReadFrame(Request(s)));
            Assert.Equal(count, f.ActorFrames.Count + f.Frames(Peer).Count);
            Assert.Equal(639_778_909U, a.Player.ConquerPoints);
            f.Data.AssertFiles(expected);
        });
}
