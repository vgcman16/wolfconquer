using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.ExceptionServices;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyDeitylandChestCapacitySafetyTests
{
    private static readonly TimeSpan ChestFrameBound = TimeSpan.FromSeconds(5);
    private static readonly ConcurrentBag<Fixture> UnfinishedChestFrameFixtures = [];

    private static async Task RunChestFrameAsync(Func<Fixture, Task> body)
    {
        var fixture = new Fixture();
        var errors = new List<Exception>();
        try { await body(fixture); }
        catch (Exception error) { errors.Add(error); }
        try { await fixture.CleanupChestFrameAsync(errors); }
        catch (Exception error)
        {
            errors.Add(error);
            UnfinishedChestFrameFixtures.Add(fixture);
        }
        if (errors.Count == 1) ExceptionDispatchInfo.Capture(errors[0]).Throw();
        if (errors.Count > 1) throw new AggregateException("Deity chest Frame test/cleanup failed; unfinished owners retain their domain.", errors);
    }

    private sealed partial class Fixture
    {
        private WorldServerFrameHandler? chestFrameOwner;
        private WorldOperationBarrier chestFrameBarrier = null!;
        private LegacyTradeRecoveryState chestFrameRecovery = null!;
        private LegacyTradeStateStore chestFrameTrades = null!;
        private readonly List<Task> chestFrameTasks = [];
        private readonly HashSet<Task> chestFrameObserved = [];
        private readonly TaskCompletionSource chestFrameReaderRelease = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private Task? chestFrameReader;
        private int chestFrameCommittedSends;

        internal void AttachChestFrameOwner()
        {
            Assert.Null(chestFrameOwner);
            Assert.Same(Session, Current);
            var loader = new CharacterSnapshotLoader(Users, Items);
            chestFrameOwner = new WorldServerFrameHandler(loader, handoffService: null, worldData: null,
                runtimeUpdateLoop: LegacyRuntimeUpdateLoopOptions.Disabled, log: Log,
                protocolProfile: ConquerProtocolProfile.VisibleDx8D13);
            sessions = ReadChestFrameField<WorldSessionStore>("_sessions");
            chestFrameBarrier = ReadChestFrameField<WorldOperationBarrier>("_worldOperations");
            chestFrameRecovery = ReadChestFrameField<LegacyTradeRecoveryState>("_tradeRecovery");
            chestFrameTrades = ReadChestFrameField<LegacyTradeStateStore>("_tradeStates");
            Assert.NotSame(requiredChestOperations, chestFrameBarrier);
            Assert.NotSame(requiredChestRecovery, chestFrameRecovery);
            Assert.NotSame(requiredChestTrades, chestFrameTrades);
            Assert.True(chestFrameRecovery.TryEnsureInitialized(out var reason), reason);
            Assert.True(chestFrameRecovery.IsReadyFor(Users, Items));
            Assert.False(chestFrameRecovery.IsBlocked);
            sessions.Set(Connection, Session);
            Assert.Same(Session, Current);
            Assert.Equal(itemsBefore, File.ReadAllBytes(ItemPath));
            Assert.Equal(characterBefore, File.ReadAllBytes(CharacterPath));
        }

        private T ReadChestFrameField<T>(string name) where T : class => Assert.IsType<T>(
            typeof(WorldServerFrameHandler).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(chestFrameOwner));

        internal async Task HoldIndependentChestFrameReaderAsync()
        {
            Assert.Null(chestFrameReader);
            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            // A distinct actual task owns this competing reader. It is not a
            // nested acquisition inside the executing Frame/handler lifetime.
            chestFrameReader = Task.Run(async () =>
            {
                try
                {
                    using var budget = new CancellationTokenSource(ChestFrameBound);
                    using var lease = await chestFrameBarrier.EnterSharedAsync(budget.Token);
                    entered.TrySetResult();
                    await chestFrameReaderRelease.Task;
                }
                catch (Exception error) { entered.TrySetException(error); throw; }
            });
            chestFrameTasks.Add(chestFrameReader);
            await entered.Task.WaitAsync(ChestFrameBound + ChestFrameBound);
            Assert.False(chestFrameReader.IsCompleted);
        }

        internal async Task HandleChestFrameAsync(PacketFrame frame)
        {
            // None is intentional: a per-call deadline is not the connection's
            // lifetime. This current handler otherwise queues detached cleanup
            // on token cancellation. Bound observation, never orphan ownership.
            var actual = chestFrameOwner!.HandleFrameAsync(Connection, frame, CancellationToken.None).AsTask();
            chestFrameTasks.Add(actual);
            await ObserveChestFrameTaskAsync(actual);
        }

        private async Task ObserveChestFrameTaskAsync(Task actual)
        {
            try { await actual.WaitAsync(ChestFrameBound + ChestFrameBound); }
            catch (Exception observation)
            {
                if (actual.IsCompleted)
                {
                    Exception? terminal = null;
                    try { await actual; } catch (Exception error) { terminal = error; }
                    chestFrameObserved.Add(actual);
                    if (terminal is not null && !ReferenceEquals(observation, terminal))
                        throw new AggregateException(observation, terminal);
                }
                throw;
            }
            chestFrameObserved.Add(actual);
        }

        internal async Task ProveChestFrameSharedLeaseReleasedAsync()
        {
            Assert.NotNull(chestFrameReader);
            Assert.False(chestFrameReader.IsCompleted);
            chestFrameReaderRelease.TrySetResult();
            await ObserveChestFrameTaskAsync(chestFrameReader);
            // Unlike timeout-only observation, the real acquisition token
            // removes a failed queued writer; no orphan exclusive lease.
            using var budget = new CancellationTokenSource(ChestFrameBound);
            using var exclusive = await chestFrameBarrier.EnterExclusiveAsync(budget.Token);
            Assert.False(chestFrameRecovery.IsBlocked);
        }

        internal void ObserveChestFrameCommittedSends()
        {
            ChestSendObserver = () =>
            {
                Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(Current.PlayerItems), File.ReadAllBytes(ItemPath));
                var saved = CharacterSnapshotParser.ParseText(File.ReadAllText(CharacterPath), CharacterPath);
                Assert.Equal(Current.Character.Uid, saved.Uid);
                Assert.Equal(Current.Character.ConquerPoints, saved.ConquerPoints);
                Assert.Equal(Current.Character.Money, saved.Money);
                Assert.False(LegacyTradeJournal.HasPending(Users));
                // Publication contains observer errors, so the outer test also
                // requires two completed observations; a swallowed assertion
                // cannot make the test pass.
                chestFrameCommittedSends++;
            };
        }

        internal void AssertChestFrameCommittedSends() => Assert.Equal(2, chestFrameCommittedSends);

        internal async Task CleanupChestFrameAsync(List<Exception> errors)
        {
            chestFrameReaderRelease.TrySetResult(); // Only our distinct test-owned reader.
            var quiescent = true;
            foreach (var actual in chestFrameTasks)
            {
                if (chestFrameObserved.Contains(actual)) continue;
                try { await ObserveChestFrameTaskAsync(actual); }
                catch (Exception error) { errors.Add(error); if (!actual.IsCompleted) quiescent = false; }
            }
            if (!quiescent)
            {
                UnfinishedChestFrameFixtures.Add(this);
                errors.Add(new InvalidOperationException($"Actual Deity Frame work did not terminate; retained ownedRoot={root}."));
                return;
            }
            var close = Connection.CompleteTransportCloseAsync();
            chestFrameTasks.Add(close);
            try
            {
                await ObserveChestFrameTaskAsync(close);
                if (chestFrameOwner is not null) Assert.False(sessions.TryGet(Connection, out _));
            }
            catch (Exception error) { errors.Add(error); quiescent = false; }
            if (quiescent && (object?)chestFrameOwner is IAsyncDisposable disposable)
            {
                var drain = disposable.DisposeAsync().AsTask();
                chestFrameTasks.Add(drain);
                try { await ObserveChestFrameTaskAsync(drain); }
                catch (Exception error) { errors.Add(error); quiescent = false; }
            }
            if (!quiescent) { UnfinishedChestFrameFixtures.Add(this); return; }
            ChestSendObserver = null;
            try { DeleteOwnedRoot(); } catch (Exception error) { errors.Add(error); }
            try { Log.Dispose(); } catch (Exception error) { errors.Add(error); }
        }
    }
}
