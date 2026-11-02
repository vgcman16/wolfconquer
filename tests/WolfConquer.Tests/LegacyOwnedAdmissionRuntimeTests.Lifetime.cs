using System.Collections.Concurrent;
using System.Net;
using System.Runtime.ExceptionServices;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyOwnedAdmissionRuntimeTests
{
    [Fact]
    public async Task HeldActualWorldLeasePreventsSynchronousCloseAndKeepsRegistryOwnedUntilAsyncDrainCompletes()
    {
        var fixture = new LegacyOwnedAdmissionRuntimeFixture();
        LegacyOwnedAdmissionRuntime? runtime = null;
        WorldServerFrameHandler? handler = null;
        IDisposable? gate = null;
        Task? drain = null;
        Exception? primary = null;
        try
        {
            runtime = Open(fixture);
            handler = CreateHandler(fixture, runtime, TextWriter.Null);
            gate = await Field<WorldOperationBarrier>(handler, "_worldOperations").EnterExclusiveAsync();
            Assert.Throws<InvalidOperationException>(() => runtime.Dispose());
            AssertRegistryLeaseHeld(fixture);

            drain = runtime.DisposeAsync().AsTask();
            Assert.False(drain.IsCompleted);
            AssertRegistryLeaseHeld(fixture);
            gate.Dispose();

            await drain.WaitAsync(TimeSpan.FromSeconds(20));
            using var reopened = Open(fixture);
        }
        catch (Exception error) { primary = error; }

        await FinishOwnedLifetimeAsync(fixture, runtime, handler, primary,
            () => gate?.Dispose(), drain);
    }

    [Fact]
    public Task ABlockedActualPlayerRuntimeSendRetainsOwnershipUntilTheReturnedWorkersReallyFinish()
    {
        var actor = OwnedEquipmentBehaviorFixture.Character(classCode: 7051, strength: 10, agility: 10,
            mapId: 1002, x: 430, y: 380, minHitPoints: 330) with
        {
            Vitality = 10, Spirit = 10, AttributePoints = 0, MinMana = 50, MaxMana = 50,
            // Declared full stamina leaves the intended real XP-circle send
            // as the first blocked owned output, rather than an earlier refill.
            Attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["Stamina"] = "100" },
        };
        var loop = LegacyRuntimeUpdateLoopOptions.EnabledDefault with { TickInterval = TimeSpan.FromMilliseconds(10) };
        return OwnedEquipmentBehaviorFixture.RunWithEnabledSchedulingAsync(
            new(actor, PlayerItemSnapshot.Empty), OwnedEquipmentBehaviorFixture.Catalog(), loop, async f =>
        {
            var connection = f.Connection(OwnedEquipmentBehaviorFixture.Actor);
            Assert.True(f.SnapshotFrames(OwnedEquipmentBehaviorFixture.Actor).Any(IsAnswerOk), "Real native admission did not send ANSWER_OK.\n" + f.Log);
            var admitted = Assert.IsType<LegacyOrdinarySpellBinding>(connection.NativeGameClient!.OrdinarySpellBinding);
            Assert.False(admitted.IsRetired); Assert.True(f.Store.IsBoundSpellOwnerCurrent(admitted));
            var peer = f.WirePeer();
            var filler = peer.StartBackpressuredWrite();
            try
            {
                await peer.WaitForBackpressure();
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
                while (peer.ExpiryWriter.TickerWaitingForGate == 0)
                    await Task.Delay(10, timeout.Token);
                Assert.True(peer.ActualBackpressure);
                var workers = f.Handler.CaptureLegacyRuntimeWorkerTasks();
                Assert.NotEmpty(workers); Assert.Contains(workers, task => !task.IsCompleted);

                // Shutdown cancels this real gate wait. A refusal must keep the
                // registry until workers drain; a synchronous success is legal
                // only if cancellation has already completed every worker.
                try
                {
                    f.Runtime.Dispose();
                    Assert.All(workers, task => Assert.True(task.IsCompleted));
                }
                catch (InvalidOperationException)
                {
                    AssertRegistryLeaseHeld(f.Data);
                }
                var drain = f.Runtime.DisposeAsync().AsTask();
                if (!drain.IsCompleted) AssertRegistryLeaseHeld(f.Data);
                await drain.WaitAsync(TimeSpan.FromSeconds(20));
                Assert.All(workers, task => Assert.True(task.IsCompleted));
                Assert.Equal(0, f.Handler.LegacyRuntimeFaultedWorkerCount);
                using var reopened = Open(f.Data);
                await peer.DrainBackpressuredWrite();
                await filler.WaitAsync(TimeSpan.FromSeconds(5));
            }
            finally { peer.ReleaseBackpressuredWrite(); }

        });
    }

    private static async Task FinishOwnedLifetimeAsync(LegacyOwnedAdmissionRuntimeFixture fixture,
        LegacyOwnedAdmissionRuntime? runtime, WorldServerFrameHandler? handler, Exception? primary,
        Action release, Task? drain, ConquerTcpConnection? connection = null, TextWriter? log = null,
        IDisposable? metadata = null)
    {
        var failures = new List<Exception>();
        if (primary is not null) AddOwnedLifetimeFailure(failures, primary);
        try { release(); } catch (Exception error) { AddOwnedLifetimeFailure(failures, error); }

        // Request actual cancellation/drain even when an assertion prevented
        // its initial start. Retain the actual task, including a faulted one.
        try { drain ??= runtime is null ? Task.CompletedTask : runtime.DisposeAsync().AsTask(); }
        catch (Exception error) { AddOwnedLifetimeFailure(failures, error); }

        Task? close = connection is null ? Task.CompletedTask : null;
        try
        {
            close ??= connection!.CompleteTransportCloseAsync();
            await close.WaitAsync(TimeSpan.FromSeconds(20));
        }
        catch (Exception error) { AddOwnedLifetimeFailure(failures, error); }

        try { if (drain is not null) await drain.WaitAsync(TimeSpan.FromSeconds(20)); }
        catch (Exception error) { AddOwnedLifetimeFailure(failures, error); }

        Task[]? workers = null;
        try
        {
            workers = handler?.CaptureLegacyRuntimeWorkerTasks() ?? [];
            await Task.WhenAll(workers).WaitAsync(TimeSpan.FromSeconds(20));
        }
        catch (Exception error) { AddOwnedLifetimeFailure(failures, error); }

        // A timeout is not permission to dispose a live worker's log or delete
        // its fixture. Preserve those resources unless actual cleanup returned.
        if (drain?.IsCompletedSuccessfully == true && close?.IsCompletedSuccessfully == true &&
            workers is not null && workers.All(task => task.IsCompleted))
        {
            try { log?.Dispose(); } catch (Exception error) { AddOwnedLifetimeFailure(failures, error); }
            try { fixture.Dispose(); } catch (Exception error) { AddOwnedLifetimeFailure(failures, error); }
            try { metadata?.Dispose(); } catch (Exception error) { AddOwnedLifetimeFailure(failures, error); }
        }
        if (failures.Count == 1) ExceptionDispatchInfo.Capture(failures[0]).Throw();
        if (failures.Count > 1) throw new AggregateException("Owned lifetime assertion and/or cleanup failed.", failures);
    }

    private static void AddOwnedLifetimeFailure(List<Exception> failures, Exception error)
    {
        if (error is AggregateException aggregate)
        {
            foreach (var inner in aggregate.Flatten().InnerExceptions) AddOwnedLifetimeFailure(failures, inner);
        }
        else if (!failures.Any(previous => ReferenceEquals(previous, error))) failures.Add(error);
    }

    private static void AssertRegistryLeaseHeld(LegacyOwnedAdmissionRuntimeFixture fixture)
    {
        Assert.False(LegacyItemUidRegistry.TryOpen(fixture.RegistryOptions(), out var competing, out var reason));
        Assert.Null(competing);
        Assert.NotEmpty(reason);
    }
}
