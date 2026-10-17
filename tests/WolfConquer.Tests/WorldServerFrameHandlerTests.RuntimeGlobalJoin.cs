using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class WorldServerFrameHandlerTests
{
    // CancelAsync joins cancellation callbacks, not the discarded runtime Tasks.
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
        clock.Arm();
        try
        {
            start.Invoke(handler, [cancel.Token]); // Actual global owner; no seeded runtime counter.
            await clock.Entered.WaitAsync(TimeSpan.FromSeconds(5));
            await cancel.CancelAsync();
            Assert.True(cancel.IsCancellationRequested);
            Assert.False(TargetLockRuntimeGlobalStopped(handler));
            joined = WaitForTargetLockRuntimeGlobalStopAsync(handler);
            Assert.False(joined.IsCompleted); // The entered tick is deterministically held, not a sleep race.
        }
        finally
        {
            clock.Release();
            await cancel.CancelAsync();
            await WaitForTargetLockRuntimeGlobalStopAsync(handler);
        }
        Assert.NotNull(joined);
        await joined!;
        Assert.True(TargetLockRuntimeGlobalStopped(handler));
        await VerifyTargetLockSessionRetirementGateAsync();
    }

    private static async Task VerifyTargetLockSessionRetirementGateAsync()
    {
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim(false);
        var sessions = new WorldSessionStore((_, previous, _) =>
        {
            if (previous is null) return;
            entered.TrySetResult(true); // Same callback boundary as the handler's synchronous INI persistence.
            if (!release.Wait(TimeSpan.FromSeconds(10)))
                throw new TimeoutException("Controlled persistence callback release was not supplied.");
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
        var pendingWrite = Task.Run(() => sessions.TryCompareAndSet(connection, original, updated));
        Task<bool>? retiring = null;
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var attempted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            retiring = Task.Run(() =>
            {
                attempted.TrySetResult(true);
                return sessions.TryRemove(connection, out _);
            });
            await attempted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(retiring.IsCompleted); // Callback still owns the SAME per-connection lock.
        }
        finally
        {
            release.Set();
            await pendingWrite.WaitAsync(TimeSpan.FromSeconds(5));
            if (retiring is not null) await retiring.WaitAsync(TimeSpan.FromSeconds(5));
        }
        Assert.True(await pendingWrite);
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
