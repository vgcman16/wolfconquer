using System.Net.Sockets;
using System.Runtime.ExceptionServices;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class OwnedDragonhowlPhysicalLifecycleTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task FailedFirstFlashingOutputFencesBothActualParticipantsBeforeTargetHp(bool visibleRecipient) => Run(async f =>
    {
        var actor = f.Current(Actor); var target = f.Current(Target);
        var actorBinding = Assert.IsType<LegacyOrdinarySpellBinding>(actor.NativeGameClient!.OrdinarySpellBinding);
        var targetBinding = Assert.IsType<LegacyOrdinarySpellBinding>(target.NativeGameClient!.OrdinarySpellBinding);
        var targetPlayer = target.NativeGameClient.Player;
        AssertNoFlashing(actor);
        var files = f.Data.Files();
        var earliest = DateTime.Now;
        var observed = false;
        Exception? observationFailure = null;
        f.ActorFrames.Clear(); f.Frames(Target).Clear();
        f.BeforeSend = (recipient, bytes) =>
        {
            if (recipient != (visibleRecipient ? Target : Actor) || !WireFlag(bytes, Actor, Flashing) ||
                WireFlag(bytes, Actor, FrontBreak)) return;
            try
            {
                Assert.False(observed); observed = true;
                AssertTimedFlashing(f.Current(Actor), earliest, DateTime.Now);
                Assert.False(Flag(f.Current(Actor), FrontBreak));
                Assert.Equal(100_000, f.Current(Target).Character.MinHitPoints);
                Assert.Equal(100_000, targetPlayer.HitPoints);
                f.Data.AssertFiles(files);
            }
            catch (Exception error) { observationFailure = error; throw; }
            throw new SocketException((int)SocketError.ConnectionReset);
        };
        // The Frame owns the real transport failure. Avoid the shared fixture's
        // deliberate sender-diagnostic latch when injecting this expected loss.
        await f.Handler.HandleFrameAsync(f.Connection(Actor), OwnedEquipmentBehaviorFixture.ReadFrame(Request()));
        f.BeforeSend = null;
        if (observationFailure is not null) ExceptionDispatchInfo.Capture(observationFailure).Throw();
        Assert.True(observed, f.Log.ToString());
        Assert.True(f.Recovery.IsBlocked);
        Assert.False(f.Store.TryGet(f.Connection(Actor), out _));
        Assert.False(f.Store.TryGet(f.Connection(Target), out _));
        Assert.True(actorBinding.IsRetired); Assert.True(targetBinding.IsRetired);
        Assert.False(actorBinding.Client.OnAutoAttack);
        Assert.Equal(100_000, targetPlayer.HitPoints);
        Assert.Equal(100_000, target.Character.MinHitPoints);
        Assert.DoesNotContain(f.ActorFrames, Animation);
        Assert.DoesNotContain(f.Frames(Target), bytes => Hp(bytes, Target));
        Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
        f.Data.AssertFiles(files);
        var actualOutputCount = f.ActorFrames.Count + f.Frames(Target).Count;
        await f.Handler.HandleFrameAsync(f.Connection(Actor), OwnedEquipmentBehaviorFixture.ReadFrame(Request()));
        Assert.Equal(actualOutputCount, f.ActorFrames.Count + f.Frames(Target).Count);
        Assert.False(actorBinding.Client.OnAutoAttack);
        Assert.Equal(100_000, targetPlayer.HitPoints);
        f.Data.AssertFiles(files);
    });
}
