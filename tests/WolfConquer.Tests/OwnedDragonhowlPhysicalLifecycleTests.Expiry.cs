using System.Runtime.ExceptionServices;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class OwnedDragonhowlPhysicalLifecycleTests
{
    [Fact]
    public Task GenuineGlobalWorkerExpiresTheEarnedFlashingRowAtThirtySecondsThenDrains()
    {
        var clock = new RetirementClock();
        var inputs = LegacyMagicAttackRuntimeTests.OwnedOverlayReentryInputs(true, 17_490, false);
        var options = new LegacyRuntimeUpdateLoopOptions(true, TimeSpan.FromSeconds(1), clock,
            LegacySignedMillisecondClock.FromTimeProvider(clock, 10_000));
        return OwnedEquipmentBehaviorFixture.RunWithNativeGearAndSchedulingAsync(inputs.Actor, inputs.World,
            options, async f =>
        {
            // Actual startup registers actor player delay1, global delay2,
            // then peer player delay3. No delay has fired during admission.
            await clock.WaitForCountAsync(3).WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(3, clock.Count);
            var admittedWorkers = f.Handler.CaptureLegacyRuntimeWorkerTasks();
            Assert.Equal(3, admittedWorkers.Length);
            await f.SendAsync(Actor, Request());
            var earned = f.Current(Actor);
            Assert.True(LegacyCanAttackFlashingRegistration.TryRead(earned, out var registration));
            var vector = earned.NativeGameClient!.Player.BitVector;
            var native = Assert.Single(vector.GetFlags(), row => row.Key == Flashing);
            Assert.Equal(registration.Timer, native.Timer); Assert.Equal(30, native.Secounds);
            Assert.True(native.RemoveOnDead);
            Assert.Equal(99_709, f.Current(Target).Character.MinHitPoints);
            Assert.True(earned.NativeGameClient.OnAutoAttack);
            // The genuine repeat worker waits on its real300ms delay4. Cancel
            // by a real stance packet before advancing the source clock.
            await clock.WaitForCountAsync(4).WaitAsync(TimeSpan.FromSeconds(10));
            await f.SendAsync(Actor, new MsgActionPacket { Type = MsgActionType.ChangeStance, DwParam = 250 }.ToFrameBytes());
            Assert.False(f.Current(Actor).NativeGameClient!.OnAutoAttack);
            var files = f.Data.Files();
            var clears = 0; Exception? observationFailure = null;
            f.BeforeSend = (recipient, bytes) =>
            {
                if (recipient != Actor || !LegacyMsgUpdatePacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(bytes), out var packet) ||
                    packet.Uid != Actor || !packet.Updates.Any(row => row.Type == LegacyMsgUpdateDataType.StatusFlag) ||
                    WireFlag(bytes, Actor, Flashing)) return;
                try
                {
                    clears++;
                    Assert.Equal(2176, (int)OwnedEquipmentBehaviorFixture.ReadFrame(bytes).Header.Opcode);
                    AssertNoFlashing(f.Current(Actor));
                }
                catch (Exception error) { observationFailure ??= error; }
            };
            clock.FireRegisteredGlobalDelay(2, TimeSpan.FromSeconds(29));
            await AssertActualGlobalRearmAsync(f, clock, admittedWorkers, 5);
            await f.DrainWireObservationsAsync();
            Assert.True(Flag(f.Current(Actor), Flashing));
            Assert.True(vector.TryContainFlag(Flashing, out var beforeDeadline)); Assert.True(beforeDeadline);
            Assert.Same(native, Assert.Single(vector.GetFlags(), row => row.Key == Flashing));
            Assert.Equal(0, clears); f.Data.AssertFiles(files);
            clock.FireRegisteredGlobalDelay(5, TimeSpan.FromSeconds(1));
            await AssertActualGlobalRearmAsync(f, clock, admittedWorkers, 6);
            await f.DrainWireObservationsAsync();
            f.BeforeSend = null;
            if (observationFailure is not null) ExceptionDispatchInfo.Capture(observationFailure).Throw();
            Assert.Equal(1, clears);
            AssertNoFlashing(f.Current(Actor));
            Assert.Equal(99_709, f.Current(Target).Character.MinHitPoints);
            Assert.False(f.Recovery.IsBlocked); f.Data.AssertFiles(files);
            var workers = f.Handler.CaptureLegacyRuntimeWorkerTasks();
            await f.Runtime.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));
            Assert.All(workers, worker => Assert.True(worker.IsCompleted));
            Assert.True(clock.AllDisposed);
        }, inputs.Target);
    }

    private static async Task AssertActualGlobalRearmAsync(OwnedEquipmentBehaviorFixture f, RetirementClock clock,
        Task[] admittedWorkers, int expectedDelayCount)
    {
        var rearmed = clock.WaitForCountAsync(expectedDelayCount);
        await Task.WhenAny(rearmed, Task.WhenAny(admittedWorkers)).WaitAsync(TimeSpan.FromSeconds(10));
        Assert.All(admittedWorkers, worker => Assert.False(worker.IsCompleted, f.Log.ToString()));
        await rearmed.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(expectedDelayCount, clock.Count);
    }
}
