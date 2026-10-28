using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class WorldServerFrameHandlerTests
{
    // CancelAsync joins cancellation callbacks, not the actual runtime Tasks.
    // This specific fixture's persisted monster HP/position writes belong to the GLOBAL task.
    private static Task WaitForTargetLockRuntimeGlobalStopAsync(WorldServerFrameHandler handler) =>
        WaitUntilAsync(() => TargetLockRuntimeGlobalStopped(handler), TimeSpan.FromSeconds(5));

    private static bool TargetLockRuntimeGlobalStopped(WorldServerFrameHandler handler)
    {
        var field = typeof(WorldServerFrameHandler).GetField("_legacyRuntimeGlobalLoopStarted",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Equals(0, field.GetValue(handler)); // Runtime global finally, after every entered tail/log await.
    }

    [Fact]
    public async Task HandlerRuntimeCancellationJoinWaitsForEnteredGlobalTickBeforeFixtureCleanup()
    {
        using var clock = new TargetLockEnteredGlobalTickClock();
        using var cancel = new CancellationTokenSource();
        var handler = new WorldServerFrameHandler(snapshotLoader: null, handoffService: null,
            worldData: null, runtimeUpdateLoop: new LegacyRuntimeUpdateLoopOptions(
                Enabled: true, TimeSpan.FromMilliseconds(20), clock), log: TextWriter.Null);
        var start = typeof(WorldServerFrameHandler).GetMethod("TryStartLegacyGlobalRuntimeUpdateLoop",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(start);
        Task? joined = null;
        Task[] enteredWorkers = [];
        Exception? primary = null;
        clock.Arm();
        try
        {
            start.Invoke(handler, [cancel.Token]); // Actual global owner; no seeded runtime counter.
            await clock.Entered.WaitAsync(TimeSpan.FromSeconds(5));
            enteredWorkers = handler.CaptureLegacyRuntimeWorkerTasks();
            Assert.NotEmpty(enteredWorkers);
            Assert.Contains(enteredWorkers, task => !task.IsCompleted);
            await cancel.CancelAsync();
            Assert.True(cancel.IsCancellationRequested);
            Assert.False(TargetLockRuntimeGlobalStopped(handler));
            joined = Task.WhenAll(enteredWorkers); // Join the real returned tasks, not only a counter.
            Assert.False(joined.IsCompleted); // The entered tick is deterministically held, not a sleep race.
        }
        catch (Exception error) { primary = error; throw; }
        finally
        {
            clock.Release();
            try
            {
                await cancel.CancelAsync();
                await Task.WhenAll(enteredWorkers.Concat(handler.CaptureLegacyRuntimeWorkerTasks()).Distinct())
                    .WaitAsync(TimeSpan.FromSeconds(5));
                await WaitForTargetLockRuntimeGlobalStopAsync(handler);
            }
            catch (Exception cleanup) when (primary is not null) { throw new AggregateException(primary, cleanup); }
        }
        Assert.NotNull(joined);
        await joined!;
        Assert.All(enteredWorkers, task => Assert.True(task.IsCompleted));
        Assert.Equal(0L, handler.LegacyRuntimeFaultedWorkerCount);
        Assert.True(TargetLockRuntimeGlobalStopped(handler));
    }

    [Fact]
    public async Task HandlerSessionRetirementCompletesWhilePostPublicationObserverWaits()
    {
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim(false);
        WorldSession? observed = null;
        var sessions = new WorldSessionStore((_, previous, current) =>
        {
            if (previous is null) return;
            Assert.False(WorldSessionStore.IsCurrentThreadInsideAnyConnectionGate);
            observed = current;
            entered.TrySetResult(true); // External observer: actual memory publication and gates already completed.
            if (!release.Wait(TimeSpan.FromSeconds(10)))
                throw new TimeoutException("Controlled postpublication observer release was not supplied.");
        });
        var connection = CreateSendingConnection(new List<byte[]>());
        var original = new WorldSession(1_293_601, "target-lock-peer",
            CharacterSnapshotParser.ParseText(
                """
                [Character]
                UID=1293601
                Name=target-lock-peer
                Body=1003
                Face=0
                Haire=0
                Class=10
                FirstClass=10
                SecoundeClass=10
                Reborn=0
                Level=1
                Experience=0
                Map=1002
                X=430
                Y=378
                PMap=1002
                PMapX=430
                PMapY=378
                Strength=0
                Agility=0
                Vitaliti=0
                Spirit=0
                Atributes=0
                MinHitPoints=1000
                MinMana=0
                Money=0
                ConquerPoints=0
                BoundConquerPoints=0
                BankCps=0
                PkPoints=0
                Online=1
                """),
            PlayerItemSnapshot.Empty, PlayerSpellSnapshot.Empty, PlayerProficiencySnapshot.Empty);
        sessions.Set(connection, original);
        var updated = original with { Character = original.Character with { MinHitPoints = 999 } };
        WorldSession? committed = null;
        var pendingWrite = Task.Run(() => sessions.TryCompareAndSet(connection, original, updated, out committed));
        Task<bool>? retiring = null;
        WorldSession? removed = null;
        Exception? primary = null;
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Same(updated, observed);
            Assert.Same(updated, Volatile.Read(ref committed));
            Assert.True(sessions.TryGet(connection, out var current));
            Assert.Same(updated, current);
            Assert.False(pendingWrite.IsCompleted);
            var attempted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            retiring = Task.Run(() =>
            {
                attempted.TrySetResult(true);
                return sessions.TryRemove(connection, out removed);
            });
            await attempted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(await retiring.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.Same(updated, removed);
            Assert.False(release.IsSet); // The observer is still held while removal has actually completed.
            Assert.False(pendingWrite.IsCompleted);
            Assert.False(sessions.TryGet(connection, out _));
        }
        catch (Exception error) { primary = error; throw; }
        finally
        {
            release.Set();
            try
            {
                var cleanupTasks = retiring is null ? new Task[] { pendingWrite } : [pendingWrite, retiring];
                await Task.WhenAll(cleanupTasks).WaitAsync(TimeSpan.FromSeconds(5));
            }
            catch (Exception cleanup) when (primary is not null) { throw new AggregateException(primary, cleanup); }
        }
        Assert.True(await pendingWrite);
        Assert.Same(updated, committed); // Historical committed result survives retirement before observer return.
        Assert.NotNull(retiring);
        Assert.True(await retiring!);
        Assert.False(sessions.TryGet(connection, out _));
        Assert.False(sessions.TryCompareAndSet(connection, updated, original));
        Assert.False(sessions.TryGet(connection, out _)); // A late expected-session CAS cannot restore the owner.
    }

    private sealed class TargetLockEnteredGlobalTickClock : TimeProvider, IDisposable
    {
        private readonly TaskCompletionSource<bool> entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly ManualResetEventSlim release = new(false);
        private int armed;
        public Task Entered => entered.Task;
        public void Arm() => Volatile.Write(ref armed, 1);
        public void Release() => release.Set();
        public override DateTimeOffset GetUtcNow()
        {
            if (Interlocked.Exchange(ref armed, 0) == 1)
            {
                entered.TrySetResult(true);
                if (!release.Wait(TimeSpan.FromSeconds(10)))
                    throw new TimeoutException("Controlled entered-global-tick release was not supplied.");
            }
            return DateTimeOffset.UtcNow;
        }
        public void Dispose()
        {
            release.Set();
            release.Dispose();
        }
    }
}
