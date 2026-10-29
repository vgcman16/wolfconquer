using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class OwnedEquipmentReentryTests
{
    [Fact]
    public Task DirectQueryStagesAppendKeepsEarnedPairUntilSourceResetAndRebuildsCapturedGearAfterReentry()
    {
        var inputs = LegacyMagicAttackRuntimeTests.OwnedQueryAppendInputs();
        return OwnedEquipmentBehaviorFixture.RunAsync(inputs.Actor, inputs.World, async f =>
        {
            var firstStart = f.ActorFrames.Count;
            await f.SendAsync(Actor, Attack(Actor, Target));
            Assert.Equal(225u, Assert.Single(Animation(f.ActorFrames.Skip(firstStart), 17_490).Targets).Damage);
            Assert.Equal(66U, (await Status(f, Actor)).Breakthrough);
            var peerStart = f.Frames(Target).Count;
            await f.SendAsync(Target, Attack(Target, Actor));
            Assert.Equal(225u, Assert.Single(Animation(f.Frames(Target).Skip(peerStart), 17_490).Targets).Damage);
            var earned = await Status(f, Actor);
            Assert.Equal(66U, earned.Breakthrough); Assert.Equal(75U, earned.Counteraction);
            Assert.Equal(99_775, f.Current().Character.MinHitPoints);
            Assert.True(Flag(f.Current(), 313));

            using var producerCancellation = new CancellationTokenSource();
            var enqueue = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var issued = new TaskCompletionSource<Task>(TaskCreationOptions.RunContinuationsAsynchronously);
            var producer = Task.Run(async () =>
            {
                if (!await enqueue.Task) return;
                var actual = f.Handler.HandleFrameAsync(f.Connection(Actor), OwnedEquipmentBehaviorFixture.ReadFrame(
                    new MsgItemUsagePacket(98_803, 1, MsgItemUsageAction.Equip, 0, 0, 0, 0).ToFrameBytes()), producerCancellation.Token).AsTask();
                issued.TrySetResult(actual);
                await actual;
            });
            var callbacks = 0; uint beforeBt = 0, beforeCa = 0;
            f.BeforeSend = (uid, bytes) =>
            {
                if (uid != Actor || !LegacyShowEquipmentPacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(bytes), out _) || callbacks != 0) return;
                callbacks++;
                var overlay = LegacyPhysicalStatusOverlay.ForOwner(f.Current());
                beforeBt = overlay.Breakthrough ?? 0; beforeCa = overlay.Counteraction ?? 0;
                Assert.True(enqueue.TrySetResult(true));
                Assert.True(issued.Task.Wait(TimeSpan.FromSeconds(5)), "Independent packet producer did not enter dispatch.");
                Assert.False(issued.Task.Result.IsCompleted, "Exclusive equip must wait for the query's Shared lease.");
            };
            Exception? outerFailure = null;
            try { await f.SendAsync(Actor, LegacyRuneStoragePacket.Startup().ToFrameBytes()); Assert.Equal(1, callbacks); }
            catch (Exception error) { outerFailure = error; }
            f.BeforeSend = null;
            enqueue.TrySetResult(false);
            Exception? producerFailure = null;
            if (outerFailure is not null) producerCancellation.Cancel();
            try { await producer.WaitAsync(TimeSpan.FromSeconds(10)); }
            catch (TimeoutException timeout)
            {
                // The real queued lease receives cancellation; do not dispose
                // the owner while that packet producer remains unjoined.
                producerCancellation.Cancel();
                try { await producer; }
                catch (OperationCanceledException) when (producerCancellation.IsCancellationRequested) { }
                catch (Exception cleanup) { producerFailure = new AggregateException(timeout, cleanup); }
                producerFailure ??= timeout;
            }
            catch (OperationCanceledException) when (outerFailure is not null && producerCancellation.IsCancellationRequested) { }
            catch (Exception error) { producerFailure = error; }
            if (outerFailure is not null && producerFailure is not null) throw new AggregateException(outerFailure, producerFailure);
            if (outerFailure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(outerFailure).Throw();
            if (producerFailure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(producerFailure).Throw();
            Assert.Equal(66U, beforeBt); Assert.Equal(75U, beforeCa);
            // The queued equip runs after the old query releases Shared; its
            // own real query therefore publishes the new 80/70 pair last.
            var rebuilt = await Status(f, Actor);
            Assert.Equal(80U, rebuilt.Breakthrough); Assert.Equal(70U, rebuilt.Counteraction);
            Assert.Equal(80U, LegacyPhysicalStatusOverlay.ForOwner(f.Current()).Breakthrough);
            Assert.Equal(70U, LegacyPhysicalStatusOverlay.ForOwner(f.Current()).Counteraction);
            Assert.True(Flag(f.Current(), 313));
            Assert.False(Flag(f.Current(), 212));
            Assert.Equal(98_803U, Assert.Single(f.Current().PlayerItems.EquipmentItems, row => row.Position == 1).Uid);
            Assert.Contains(f.Current().PlayerItems.InventoryItems, row => row.Uid == 98_801);
            Assert.Equal(99_775, f.Current().Character.MinHitPoints);
            Assert.True(LegacyEquipmentHitpointsStatus.TryGetForOwner(f.Current(), out var maximum));
            Assert.Equal(100_000U, maximum);
        }, inputs.Peer);
    }
}
