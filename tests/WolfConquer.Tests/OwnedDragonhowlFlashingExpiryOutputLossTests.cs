using System.Net.Sockets;
using System.Runtime.ExceptionServices;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

// Reuse the actual native19 admission and registered-delay clock in the existing
// lifecycle partial. This file does not create a native timer or expiry purpose.
public sealed partial class OwnedDragonhowlPhysicalLifecycleTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task GenuineExpiryOutputLossFencesTheEarnedNativeOwnerAndArmedRepeat(bool visibleRecipient)
    {
        var clock = new RetirementClock();
        var inputs = LegacyMagicAttackRuntimeTests.OwnedOverlayReentryInputs(true, 17_490, false);
        var options = new LegacyRuntimeUpdateLoopOptions(true, TimeSpan.FromSeconds(1), clock,
            LegacySignedMillisecondClock.FromTimeProvider(clock, 10_000));
        return OwnedEquipmentBehaviorFixture.RunWithNativeGearAndSchedulingAsync(inputs.Actor, inputs.World,
            options, async f =>
        {
            await clock.WaitForCountAsync(3).WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(3, clock.Count);
            var admittedWorkers = f.Handler.CaptureLegacyRuntimeWorkerTasks();
            Assert.Equal(3, admittedWorkers.Length);
            var earnedAt = clock.GetLocalNow().DateTime;
            await f.SendAsync(Actor, Request());
            var earned = f.Current(Actor); var target = f.Current(Target);
            AssertTimedFlashing(earned, earnedAt, earnedAt);
            var actorBinding = Assert.IsType<LegacyOrdinarySpellBinding>(earned.NativeGameClient!.OrdinarySpellBinding);
            var targetBinding = Assert.IsType<LegacyOrdinarySpellBinding>(target.NativeGameClient!.OrdinarySpellBinding);
            Assert.Equal(99_709, target.Character.MinHitPoints);
            Assert.Equal(99_709, targetBinding.Player.HitPoints);
            Assert.True(actorBinding.Client.OnAutoAttack);
            Assert.True(LegacyCanAttackFlashingRegistration.TryRead(earned, out var registration));
            var nativeRow = Assert.Single(actorBinding.Player.BitVector.GetFlags(), row => row.Key == Flashing);
            Assert.Equal(registration.Timer, nativeRow.Timer);
            // Keep the real repeat armed, sleeping on its actual300ms delay4.
            // Only the actual global delay2 is fired; no due attack is invented.
            await clock.WaitForCountAsync(4).WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(4, clock.Count);
            var workers = f.Handler.CaptureLegacyRuntimeWorkerTasks();
            Assert.Equal(4, workers.Length);
            var files = f.Data.Files();
            var attackStamp = actorBinding.Player.AttackStamp;
            f.ActorFrames.Clear(); f.Frames(Target).Clear();
            var attempts = new List<uint>();
            WorldSession? removed = null;
            Exception? observationFailure = null;
            f.BeforeSend = (recipient, bytes) =>
            {
                if (!IsOwnedExpiryClear(bytes)) return;
                try
                {
                    attempts.Add(recipient);
                    Assert.False(WorldSessionStore.IsCurrentThreadInsideAnyConnectionGate);
                    Assert.Equal(earnedAt.AddSeconds(30), clock.GetLocalNow().DateTime);
                    removed = f.Current(Actor);
                    AssertNoFlashing(removed);
                    Assert.DoesNotContain(actorBinding.Player.BitVector.GetFlags(), row => row.Key == Flashing);
                    Assert.True(f.Store.IsBoundSpellOwnerCurrent(actorBinding));
                    Assert.True(actorBinding.Client.OnAutoAttack);
                    Assert.Same(target, f.Current(Target));
                    Assert.Equal(99_709, targetBinding.Player.HitPoints);
                    f.Data.AssertFiles(files);
                }
                catch (Exception error) { observationFailure ??= error; throw; }
                if (recipient == (visibleRecipient ? Target : Actor))
                    throw new SocketException((int)SocketError.ConnectionReset);
            };
            try
            {
                clock.FireRegisteredGlobalDelay(2, TimeSpan.FromSeconds(30));
                await AssertExpiryLossEndsActualGlobalWorkerAsync(f, clock, admittedWorkers);
            }
            finally
            {
                f.BeforeSend = null;
                if (observationFailure is { } error) ExceptionDispatchInfo.Capture(error).Throw();
            }
            Assert.Equal(visibleRecipient ? new[] { Actor, Target } : new[] { Actor }, attempts);
            AssertNoFlashing(Assert.IsType<WorldSession>(removed));
            Assert.True(f.Recovery.IsBlocked);
            Assert.False(f.Store.TryGet(f.Connection(Actor), out _));
            Assert.True(actorBinding.IsRetired);
            Assert.False(actorBinding.Client.OnAutoAttack);
            Assert.Equal(attackStamp, actorBinding.Player.AttackStamp);
            Assert.Same(target, f.Current(Target));
            Assert.False(targetBinding.IsRetired);
            Assert.Equal(99_709, targetBinding.Player.HitPoints);
            AssertExpiryLossOutput(f, visibleRecipient);
            Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
            f.Data.AssertFiles(files);
            var outputCount = f.ActorFrames.Count + f.Frames(Target).Count;
            // This is the real no-input due-entry drain and ordinary ingress,
            // both denied by the actual fence, not an accepting test callback.
            await f.Handler.SendDueBasicPhysicalAttacksAsync(CancellationToken.None);
            await f.Handler.HandleFrameAsync(f.Connection(Actor), OwnedEquipmentBehaviorFixture.ReadFrame(Request()));
            Assert.Equal(outputCount, f.ActorFrames.Count + f.Frames(Target).Count);
            Assert.False(actorBinding.Client.OnAutoAttack);
            Assert.Equal(99_709, targetBinding.Player.HitPoints);
            f.Data.AssertFiles(files);
            await f.Runtime.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));
            Assert.All(workers, worker => Assert.True(worker.IsCompleted));
            Assert.True(clock.AllDisposed);
            Assert.Equal(outputCount, f.ActorFrames.Count + f.Frames(Target).Count);
            f.Data.AssertFiles(files);
        }, inputs.Target);
    }
}
