using System.Net.Sockets;
using System.Runtime.ExceptionServices;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class OwnedDragonhowlFineRainAwardTests
{
    [Theory]
    [InlineData((ushort)17_490, false, "hitpoints")]
    [InlineData((ushort)17_650, true, "hitpoints")]
    [InlineData((ushort)17_490, true, "mana")]
    [InlineData((ushort)17_650, false, "mana")]
    [InlineData((ushort)17_490, false, "sender")]
    [InlineData((ushort)17_650, true, "sender")]
    public Task RealCommittedDeathNativeMaximumDriftOrSenderLossFencesLaterOutputAndRepeat(
        ushort spell, bool threshold, string fault) => Run(spell, threshold, async c =>
    {
        var f = c.Owner; var actor = Binding(f, Actor); var peer = Binding(f, Peer);
        var status = peer.Client.Status;
        var unjoined = NativeUnjoinedStatus(status);
        var files = ExpectedFiles(c, dead: true);
        var senderFailure = new SocketException((int)SocketError.ConnectionReset);
        Exception? observationFailure = null; int attempts = 0;
        f.BeforeSend = (recipient, bytes) =>
        {
            if (!IsFirstFineRainDeathClear(recipient, bytes)) return;
            try
            {
                Assert.False(WorldSessionStore.IsCurrentThreadInsideAnyConnectionGate);
                Assert.Equal(0, attempts++);
                AssertDead(c, peer); Assert.Same(status, peer.Client.Status);
                Assert.Equal(unjoined, NativeUnjoinedStatus(status));
                Assert.False(f.Recovery.IsBlocked); f.Data.AssertFiles(files);
                // Mutate only a genuinely exposed scalar alias on the actual
                // captured native Status. No owners, epochs or private flags
                // are replaced; its real post-send receipt must reject drift.
                if (fault == "hitpoints") status.MaxHitpoints = checked(status.MaxHitpoints + 1);
                else if (fault == "mana") status.MaxMana = checked(status.MaxMana + 1);
                else Assert.Equal("sender", fault);
            }
            catch (Exception error) { observationFailure = error; throw; }
            if (fault == "sender") throw senderFailure;
        };
        // Expected transport loss travels through the real Frame. Rethrow any
        // observer assertion independently, never use a swallowed fixture latch
        // as evidence that the actual publication failed safely.
        try { await f.Handler.HandleFrameAsync(f.Connection(Actor), OwnedEquipmentBehaviorFixture.ReadFrame(c.Request())); }
        finally { f.BeforeSend = null; }
        if (observationFailure is not null) ExceptionDispatchInfo.Capture(observationFailure).Throw();
        Assert.Equal(1, attempts);
        Assert.True(f.Recovery.IsBlocked); Assert.True(actor.IsRetired); Assert.True(peer.IsRetired);
        Assert.False(f.Store.TryGet(f.Connection(Actor), out _)); Assert.False(f.Store.TryGet(f.Connection(Peer), out _));
        Assert.False(actor.Client.OnAutoAttack); Assert.False(peer.Client.OnAutoAttack);
        Assert.Same(status, peer.Client.Status); Assert.Equal(unjoined, NativeUnjoinedStatus(status));
        uint expectedHp = c.AfterDeathMaximum + (fault == "hitpoints" ? 1U : 0U);
        uint expectedMana = (threshold ? 600U : 300U) + (fault == "mana" ? 1U : 0U);
        // A committed publication is not rolled back or recomputed after its
        // first output. Drift remains fenced and sender loss keeps the result.
        Assert.Equal(expectedHp, status.MaxHitpoints); Assert.Equal(expectedMana, status.MaxMana);
        Assert.Equal(0, peer.Player.HitPoints); Assert.Equal(20U, peer.Player.Mana);
        Assert.Equal(493U, actor.Player.Mana); Assert.Equal(95, (int)actor.Player.Stamina);
        Assert.Equal(639_778_909U, actor.Player.ConquerPoints);
        Assert.False(c.Monsters.TryGetVisibleInstance(c.Monster, c.Clock.GetUtcNow(), out _));
        Assert.DoesNotContain(f.ActorFrames, bytes => Animation(bytes, spell));
        Assert.DoesNotContain(f.Frames(Peer), bytes => bytes.AsSpan().SequenceEqual(Dead30) || bytes.AsSpan().SequenceEqual(Dead540));
        Assert.DoesNotContain(f.Frames(Peer), bytes => StatusVector(bytes, out var words) &&
            Flag(words, LegacyPlayerDeathState.DeadStatusFlag));
        Assert.Equal(fault == "sender" ? 0 : 1, f.Frames(Peer).Count(bytes => IsFirstFineRainDeathClear(Peer, bytes)));
        f.Data.AssertFiles(files); Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
        int outputs = f.ActorFrames.Count + f.Frames(Peer).Count; var stamp = actor.Player.AttackStamp;
        c.Clock.Advance(700); await f.Handler.SendDueBasicPhysicalAttacksAsync(CancellationToken.None);
        await f.Handler.HandleFrameAsync(f.Connection(Actor), OwnedEquipmentBehaviorFixture.ReadFrame(c.Request()));
        Assert.Equal(outputs, f.ActorFrames.Count + f.Frames(Peer).Count); Assert.Equal(stamp, actor.Player.AttackStamp);
        Assert.Equal(expectedHp, status.MaxHitpoints); Assert.Equal(expectedMana, status.MaxMana);
        Assert.Equal(0, peer.Player.HitPoints); Assert.Equal(639_778_909U, actor.Player.ConquerPoints);
        f.Data.AssertFiles(files);
    });
}
