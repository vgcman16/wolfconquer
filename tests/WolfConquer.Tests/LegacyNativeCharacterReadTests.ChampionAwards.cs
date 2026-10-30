using System.Runtime.ExceptionServices;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeCharacterReadTests
{
    private readonly record struct ChampionBalances(uint Current, uint Today, uint History)
    {
        internal static ChampionBalances Capture(LegacyNativePlayerRuntime player) =>
            new(player.ChampionPoints, player.TodayChampionPoints, player.HistoryChampionPoints);
    }

    [Theory]
    [InlineData(4294967294u, 10u, 2u, 4294967295u)]
    [InlineData(20u, 4294967285u, 20u, 30u)]
    public Task ChampionEitherAwardSumOverflowPreservesBothBalancesNonzeroHistoryAndPriorQueue(
        uint current, uint today, uint amount, uint history) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var player = scope.Player;
        player.ChampionPoints = current; player.TodayChampionPoints = today; player.HistoryChampionPoints = history;
        var before = ChampionBalances.Capture(player); byte[] prior = [0xD3, 0xE2]; scope.Queue.Enqueue(prior);

        Assert.Throws<OverflowException>(() => player.AddChampionPoints(amount, false));

        Assert.Equal(before, ChampionBalances.Capture(player)); Assert.Same(prior, Assert.Single(scope.Queue));
        Assert.Equal(new byte[] { 0xD3, 0xE2 }, prior);
    });

    [Fact]
    public Task ChampionNearUintLimitSuccessRefusalAndSmallerRetryKeepExactValidAwards() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var player = scope.Player;
        player.ChampionPoints = uint.MaxValue - 2; player.TodayChampionPoints = uint.MaxValue - 2;
        player.HistoryChampionPoints = uint.MaxValue - 1;
        byte[] prior = [0xD3, 0xE3]; scope.Queue.Enqueue(prior);
        player.AddChampionPoints(1, false);
        Assert.Equal(new ChampionBalances(uint.MaxValue - 1, uint.MaxValue - 1, uint.MaxValue - 1),
            ChampionBalances.Capture(player));
        var before = ChampionBalances.Capture(player);
        Assert.Throws<OverflowException>(() => player.AddChampionPoints(2, false));
        Assert.Equal(before, ChampionBalances.Capture(player));
        player.AddChampionPoints(1, false);
        Assert.Equal(new ChampionBalances(uint.MaxValue, uint.MaxValue, uint.MaxValue), ChampionBalances.Capture(player));
        player.AddChampionPoints(0, false);
        Assert.Throws<OverflowException>(() => player.AddChampionPoints(1, false));
        Assert.Equal(new ChampionBalances(uint.MaxValue, uint.MaxValue, uint.MaxValue), ChampionBalances.Capture(player));
        Assert.Same(prior, Assert.Single(scope.Queue)); Assert.Equal(new byte[] { 0xD3, 0xE3 }, prior);
    });

    [Fact]
    public Task ChampionEligibilityRejectionPrecedesPotentialArithmeticOverflowAndKeepsRealTalkCount() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var player = scope.Player;
        player.ChampionPoints = uint.MaxValue; player.TodayChampionPoints = 651;
        var before = ChampionBalances.Capture(player);
        player.AddChampionPoints(1, true);
        Assert.Equal(before, ChampionBalances.Capture(player));
        Assert.Equal((ushort)2179, Opcode(Assert.Single(scope.Queue)));
    });

    [Theory]
    [InlineData(false, 700u)]
    [InlineData(true, 600u)]
    public Task ChampionNoOutputAwardNeedsNoUnusedCurrentOwnerSlotUidSocketOrCatalog(bool checkEligibility, uint today) =>
        NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var player = scope.Player; var socket = scope.Client.Socket;
        uint uid = player.UID;
        player.ChampionPoints = 20; player.TodayChampionPoints = today; player.HistoryChampionPoints = 100;
        byte[] prior = [0xD3, 0xE4]; scope.Queue.Enqueue(prior);
        int available = LegacyNativePacketRecycle.Count;
        try
        {
            // This is the genuine constructed player/owner. The no-wire path
            // must not invent dependencies on unrelated nullable owner slots.
            scope.Client.Player = null!; scope.Client.Socket = null; player.UID = 0;
            LegacyNativePoolItems.ItemsBase = null;
            player.AddChampionPoints(1, checkEligibility);
            Assert.Equal(new ChampionBalances(21, today + 1, 100), ChampionBalances.Capture(player));
            Assert.Null(scope.Client.Player); Assert.Null(scope.Client.Socket); Assert.Null(LegacyNativePoolItems.ItemsBase);
            Assert.Equal(available, LegacyNativePacketRecycle.Count);
            Assert.Same(prior, Assert.Single(scope.Queue)); Assert.Equal(new byte[] { 0xD3, 0xE4 }, prior);
        }
        finally { scope.Client.Player = player; scope.Client.Socket = socket; player.UID = uid; }
    });

    private sealed class ChampionTestFailures
    {
        private ExceptionDispatchInfo? primary;
        private readonly List<Exception> cleanup = new();
        private readonly HashSet<Exception> observed = new(ReferenceEqualityComparer.Instance);

        internal void RecordPrimary(Exception error)
        { primary = ExceptionDispatchInfo.Capture(error); observed.Add(error); }

        private void RecordCleanup(Exception error)
        { if (observed.Add(error)) cleanup.Add(error); }

        internal void Attempt(Action cleanupStep)
        {
            try { cleanupStep(); }
            catch (Exception error) { RecordCleanup(error); }
        }

        internal async Task AttemptAsync(Func<Task> cleanupStep)
        {
            try { await cleanupStep(); }
            catch (Exception error) { RecordCleanup(error); }
        }

        internal async Task JoinAsync(IEnumerable<Task> workers)
        {
            Task? all = null;
            try
            {
                all = Task.WhenAll(workers);
                await all.WaitAsync(TimeSpan.FromSeconds(20));
            }
            catch (Exception error)
            {
                if (all?.Exception is { } failures)
                    foreach (var worker in failures.Flatten().InnerExceptions) RecordCleanup(worker);
                else RecordCleanup(error);
            }
        }

        internal void ThrowIfAny()
        {
            if (primary is not null && cleanup.Count == 0) primary.Throw();
            if (primary is not null)
                throw new AggregateException("Champion test and cleanup failed.",
                    new[] { primary.SourceException }.Concat(cleanup));
            if (cleanup.Count == 1) ExceptionDispatchInfo.Capture(cleanup[0]).Throw();
            if (cleanup.Count > 1) throw new AggregateException("Champion test cleanup failed.", cleanup);
        }
    }

    [Fact]
    public async Task ChampionConcurrentCooperatingAwardsLoseNoCurrentTodayOrHistoricalProgress()
    {
        Scope? scope = null;
        ManualResetEventSlim? start = null;
        var workers = new List<Task>(6);
        var failures = new ChampionTestFailures();
        try
        {
            await NativePacketTestThread.Run(() => scope = new Scope());
            var player = scope!.Player;
            player.ChampionPoints = 100; player.TodayChampionPoints = 200; player.HistoryChampionPoints = 5000;
            const int count = 6, repetitions = 1000;
            start = new ManualResetEventSlim();
            var startGate = start;
            var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            int waiting = 0;
            for (int index = 0; index < count; index++)
                workers.Add(Task.Factory.StartNew(() =>
                {
                    if (Interlocked.Increment(ref waiting) == count) ready.TrySetResult();
                    if (!startGate.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException("Champion award start barrier timed out.");
                    for (int award = 0; award < repetitions; award++) player.AddChampionPoints(1, false);
                }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default));
            await ready.Task.WaitAsync(TimeSpan.FromSeconds(10)); start.Set();
            await Task.WhenAll(workers).WaitAsync(TimeSpan.FromSeconds(20));
            Assert.Equal(new ChampionBalances(6100, 6200, 6100), ChampionBalances.Capture(player));
            Assert.Empty(scope.Queue);
        }
        catch (Exception error) { failures.RecordPrimary(error); }
        finally
        {
            failures.Attempt(() => start?.Set());
            await failures.JoinAsync(workers);
            failures.Attempt(() => start?.Dispose());
            if (scope is { } owned) await failures.AttemptAsync(() => NativePacketTestThread.Run(owned.Dispose));
        }
        failures.ThrowIfAny();
    }

    [Fact]
    public async Task ChampionSetterAndAwardWaitForTheSameRealPlayerGate()
    {
        Scope? scope = null;
        ManualResetEventSlim? release = null;
        Task holder = Task.CompletedTask, setter = Task.CompletedTask, award = Task.CompletedTask;
        var failures = new ChampionTestFailures();
        try
        {
            await NativePacketTestThread.Run(() => scope = new Scope());
            var player = scope!.Player; player.ChampionPoints = 10;
            player.TodayChampionPoints = 20; player.HistoryChampionPoints = 30;
            release = new ManualResetEventSlim();
            var releaseGate = release;
            var held = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var setterStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var awardStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            holder = Task.Factory.StartNew(() =>
            {
                lock (player.SpellAdmissionSyncRoot)
                {
                    held.TrySetResult();
                    if (!releaseGate.Wait(TimeSpan.FromSeconds(15))) throw new TimeoutException("Champion gate holder timed out.");
                }
            }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            await held.Task.WaitAsync(TimeSpan.FromSeconds(10));
            setter = Task.Factory.StartNew(() =>
            { setterStarted.TrySetResult(); player.ChampionPoints = 200; },
                CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            award = Task.Factory.StartNew(() =>
            { awardStarted.TrySetResult(); player.AddChampionPoints(7, false); },
                CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            await Task.WhenAll(setterStarted.Task, awardStarted.Task).WaitAsync(TimeSpan.FromSeconds(10));
            // Both real calls have started while another real worker holds
            // the player gate. This bounded observation is not every schedule.
            await Task.Delay(100);
            Assert.False(setter.IsCompleted); Assert.False(award.IsCompleted);
            release.Set();
            await Task.WhenAll(holder, setter, award).WaitAsync(TimeSpan.FromSeconds(20));
            uint current = player.ChampionPoints;
            Assert.Contains(current, new uint[] { 200, 207 });
            Assert.Equal(27u, player.TodayChampionPoints); Assert.Equal(current, player.HistoryChampionPoints);
            Assert.Empty(scope.Queue);
        }
        catch (Exception error) { failures.RecordPrimary(error); }
        finally
        {
            failures.Attempt(() => release?.Set());
            await failures.JoinAsync(new[] { holder, setter, award });
            failures.Attempt(() => release?.Dispose());
            if (scope is { } owned) await failures.AttemptAsync(() => NativePacketTestThread.Run(owned.Dispose));
        }
        failures.ThrowIfAny();
    }
}
