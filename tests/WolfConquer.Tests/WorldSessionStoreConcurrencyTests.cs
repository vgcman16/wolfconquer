using System.Collections.Concurrent;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class WorldSessionStoreConcurrencyTests
{
    private const int RaceWriterCount = 4;
    private const int RaceUpdatesPerWriter = 20_000;

    [Fact]
    public async Task ConcurrentSetCallbacksCanOverlapAfterOrderedPublication()
    {
        using var firstUpdateEntered = new ManualResetEventSlim();
        using var releaseFirstUpdate = new ManualResetEventSlim();
        using var secondSetStarted = new ManualResetEventSlim();
        using var secondCallbackEntered = new ManualResetEventSlim();
        var callbacks = new ConcurrentQueue<(WorldSession? Previous, WorldSession Current)>();
        var store = new WorldSessionStore((_, previous, current) =>
        {
            Assert.False(WorldSessionStore.IsCurrentThreadInsideAnyConnectionGate);
            callbacks.Enqueue((previous, current));
            if (current.AccountId == 1)
            {
                firstUpdateEntered.Set();
                if (!releaseFirstUpdate.Wait(TimeSpan.FromSeconds(5)))
                {
                    throw new TimeoutException("Timed out waiting to release the first update callback.");
                }
            }
            else if (current.AccountId == 2)
            {
                secondCallbackEntered.Set();
            }
        });
        var connection = new ConquerTcpConnection(null, null);
        var initial = CreateSession(accountId: 0);
        var first = CreateSession(accountId: 1);
        var second = CreateSession(accountId: 2);
        store.Set(connection, initial);

        var firstSet = StartLongRunning(() => store.Set(connection, first));
        Assert.True(firstUpdateEntered.Wait(TimeSpan.FromSeconds(5)));

        var secondSet = StartLongRunning(() =>
        {
            secondSetStarted.Set();
            store.Set(connection, second);
        });
        Assert.True(secondSetStarted.Wait(TimeSpan.FromSeconds(5)));

        bool secondCallbackOverlappedFirst;
        try
        {
            secondCallbackOverlappedFirst = secondCallbackEntered.Wait(TimeSpan.FromSeconds(5));
        }
        finally
        {
            releaseFirstUpdate.Set();
        }

        await Task.WhenAll(firstSet, secondSet);

        Assert.True(secondCallbackOverlappedFirst);
        Assert.Collection(
            callbacks,
            callback =>
            {
                Assert.Null(callback.Previous);
                Assert.Same(initial, callback.Current);
            },
            callback =>
            {
                Assert.Same(initial, callback.Previous);
                Assert.Same(first, callback.Current);
            },
            callback =>
            {
                Assert.Same(first, callback.Previous);
                Assert.Same(second, callback.Current);
            });
    }

    [Fact]
    public void CompareAndSetRequiresCurrentReferenceAndInvokesCallbackOnlyOnSuccess()
    {
        var callbacks = new List<(WorldSession? Previous, WorldSession Current)>();
        var store = new WorldSessionStore((_, previous, current) => callbacks.Add((previous, current)));
        var connection = new ConquerTcpConnection(null, null);
        var initial = CreateSession(accountId: 0);
        var equalButDistinct = initial with { };
        var replacement = CreateSession(accountId: 1);
        store.Set(connection, initial);
        callbacks.Clear();

        Assert.Equal(initial, equalButDistinct);
        Assert.NotSame(initial, equalButDistinct);
        Assert.False(store.TryCompareAndSet(connection, equalButDistinct, replacement));
        Assert.Empty(callbacks);
        Assert.True(store.TryGet(connection, out var afterRejected));
        Assert.Same(initial, afterRejected);

        Assert.True(store.TryCompareAndSet(connection, initial, replacement));
        var callback = Assert.Single(callbacks);
        Assert.Same(initial, callback.Previous);
        Assert.Same(replacement, callback.Current);
        Assert.True(store.TryGet(connection, out var afterSuccess));
        Assert.Same(replacement, afterSuccess);

        Assert.False(store.TryCompareAndSet(connection, initial, CreateSession(accountId: 2)));
        Assert.Single(callbacks);
        Assert.True(store.TryGet(connection, out var afterStale));
        Assert.Same(replacement, afterStale);
    }

    [Fact]
    public async Task ConcurrentCompareAndSetAllowsExactlyOneWinner()
    {
        var callbacks = new ConcurrentQueue<(WorldSession? Previous, WorldSession Current)>();
        var store = new WorldSessionStore((_, previous, current) => callbacks.Enqueue((previous, current)));
        var connection = new ConquerTcpConnection(null, null);
        var initial = CreateSession(accountId: 0);
        var candidates = new[]
        {
            CreateSession(accountId: 1),
            CreateSession(accountId: 2),
        };
        store.Set(connection, initial);
        callbacks.Clear();
        using var ready = new CountdownEvent(candidates.Length);
        using var start = new ManualResetEventSlim();

        var attempts = candidates
            .Select(candidate => StartLongRunning(() =>
            {
                ready.Signal();
                start.Wait();
                return store.TryCompareAndSet(connection, initial, candidate);
            }))
            .ToArray();
        Assert.True(ready.Wait(TimeSpan.FromSeconds(5)));
        start.Set();
        var results = await Task.WhenAll(attempts);

        var winnerIndex = Assert.Single(
            results.Select((succeeded, index) => (succeeded, index)),
            result => result.succeeded).index;
        var callback = Assert.Single(callbacks);
        Assert.Same(initial, callback.Previous);
        Assert.Same(candidates[winnerIndex], callback.Current);
        Assert.True(store.TryGet(connection, out var current));
        Assert.Same(candidates[winnerIndex], current);
    }

    [Fact]
    public void TryRemoveReturnsLastPublishedSessionExactlyOnceWithoutCallback()
    {
        var callbacks = new List<(WorldSession? Previous, WorldSession Current)>();
        var store = new WorldSessionStore((_, previous, current) => callbacks.Add((previous, current)));
        var connection = new ConquerTcpConnection(null, null);
        var initial = CreateSession(accountId: 0);
        var replacement = CreateSession(accountId: 1);
        store.Set(connection, initial);
        store.Set(connection, replacement);
        callbacks.Clear();

        Assert.True(store.TryRemove(connection, out var removed));
        Assert.Same(replacement, removed);
        Assert.Empty(callbacks);
        Assert.False(store.TryGet(connection, out var afterRemoval));
        Assert.Null(afterRemoval);
        Assert.DoesNotContain(store.Snapshot(), active => ReferenceEquals(active.Connection, connection));

        Assert.False(store.TryRemove(connection, out var removedAgain));
        Assert.Null(removedAgain);
        Assert.Empty(callbacks);
    }

    [Fact]
    public void TryRemoveRejectsNullConnection()
    {
        var store = new WorldSessionStore();

        Assert.Throws<ArgumentNullException>(() => store.TryRemove(null!, out _));
    }

    [Fact]
    public async Task TryRemoveCanFinishWhileThePostPublicationObserverWaits()
    {
        using var callbackEntered = new ManualResetEventSlim();
        using var releaseCallback = new ManualResetEventSlim();
        using var removeStarted = new ManualResetEventSlim();
        var store = new WorldSessionStore((_, _, current) =>
        {
            if (current.AccountId != 1)
            {
                return;
            }

            callbackEntered.Set();
            if (!releaseCallback.Wait(TimeSpan.FromSeconds(5)))
            {
                throw new TimeoutException("Timed out waiting to release the set callback.");
            }
        });
        var connection = new ConquerTcpConnection(null, null);
        var initial = CreateSession(accountId: 0);
        var replacement = CreateSession(accountId: 1);
        store.Set(connection, initial);

        var set = StartLongRunning(() => store.Set(connection, replacement));
        Assert.True(callbackEntered.Wait(TimeSpan.FromSeconds(5)));
        var remove = StartLongRunning(() =>
        {
            removeStarted.Set();
            return (Succeeded: store.TryRemove(connection, out var removed), Removed: removed);
        });
        Assert.True(removeStarted.Wait(TimeSpan.FromSeconds(5)));

        (bool Succeeded, WorldSession? Removed) result;
        try
        {
            result = await remove.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(set.IsCompleted); // Observer is still inside its controlled barrier.
        }
        finally
        {
            releaseCallback.Set();
        }

        await set;
        Assert.True(result.Succeeded);
        Assert.Same(replacement, result.Removed);
        Assert.False(store.TryGet(connection, out _));
    }

    [Fact]
    public async Task ConcurrentSetAndRemoveProduceOnlyLinearizedOutcomes()
    {
        const int iterationCount = 500;

        for (var iteration = 0; iteration < iterationCount; iteration++)
        {
            var store = new WorldSessionStore();
            var connection = new ConquerTcpConnection(null, null);
            var initial = CreateSession(accountId: iteration * 2);
            var replacement = CreateSession(accountId: (iteration * 2) + 1);
            store.Set(connection, initial);
            using var ready = new CountdownEvent(2);
            using var start = new ManualResetEventSlim();

            var set = StartLongRunning(() =>
            {
                ready.Signal();
                start.Wait();
                store.Set(connection, replacement);
            });
            var remove = StartLongRunning(() =>
            {
                ready.Signal();
                start.Wait();
                return (Succeeded: store.TryRemove(connection, out var removed), Removed: removed);
            });

            Assert.True(ready.Wait(TimeSpan.FromSeconds(5)));
            start.Set();
            await Task.WhenAll(set, remove);
            var removal = await remove;
            Assert.True(removal.Succeeded);

            if (ReferenceEquals(removal.Removed, initial))
            {
                Assert.True(store.TryGet(connection, out var current));
                Assert.Same(replacement, current);
            }
            else
            {
                Assert.Same(replacement, removal.Removed);
                Assert.False(store.TryGet(connection, out var current));
                Assert.Null(current);
            }
        }
    }

    [Fact]
    public async Task TryGetNeverReportsMissingAfterInitialPublicationWhileUpdatesRace()
    {
        var store = new WorldSessionStore();
        var connection = new ConquerTcpConnection(null, null);
        store.Set(connection, CreateSession(accountId: 0));
        var missingReads = 0;

        var observationCount = await RunUpdateRaceAsync(
            store,
            connection,
            () =>
            {
                if (!store.TryGet(connection, out var session) || session is null)
                {
                    Interlocked.Increment(ref missingReads);
                }
            });

        Assert.True(observationCount > 0);
        Assert.Equal(0, missingReads);
    }

    [Fact]
    public async Task SnapshotNeverOmitsPublishedConnectionWhileUpdatesRace()
    {
        var store = new WorldSessionStore();
        var connection = new ConquerTcpConnection(null, null);
        store.Set(connection, CreateSession(accountId: 0));
        var omittedSnapshots = 0;

        var observationCount = await RunUpdateRaceAsync(
            store,
            connection,
            () =>
            {
                if (!store.Snapshot().Any(active => ReferenceEquals(active.Connection, connection)))
                {
                    Interlocked.Increment(ref omittedSnapshots);
                }
            });

        Assert.True(observationCount > 0);
        Assert.Equal(0, omittedSnapshots);
    }

    private static async Task<int> RunUpdateRaceAsync(
        WorldSessionStore store,
        ConquerTcpConnection connection,
        Action observe)
    {
        using var ready = new CountdownEvent(RaceWriterCount + 1);
        using var start = new ManualResetEventSlim();
        using var writersFinished = new CountdownEvent(RaceWriterCount);
        var writerSessions = Enumerable.Range(1, RaceWriterCount)
            .Select(CreateSession)
            .ToArray();
        var writers = writerSessions
            .Select(session => StartLongRunning(() =>
            {
                ready.Signal();
                start.Wait();
                try
                {
                    for (var update = 0; update < RaceUpdatesPerWriter; update++)
                    {
                        store.Set(connection, session);
                        if ((update & 15) == 0)
                        {
                            Thread.Yield();
                        }
                    }
                }
                finally
                {
                    writersFinished.Signal();
                }
            }))
            .ToArray();
        var observationCount = 0;
        var reader = StartLongRunning(() =>
        {
            ready.Signal();
            start.Wait();
            do
            {
                observe();
                observationCount++;
                Thread.Yield();
            }
            while (!writersFinished.IsSet);
        });

        Assert.True(ready.Wait(TimeSpan.FromSeconds(5)));
        start.Set();
        await Task.WhenAll(writers.Append(reader));
        return observationCount;
    }

    private static Task StartLongRunning(Action action)
    {
        return Task.Factory.StartNew(
            action,
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);
    }

    private static Task<TResult> StartLongRunning<TResult>(Func<TResult> action)
    {
        return Task.Factory.StartNew(
            action,
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);
    }

    private static WorldSession CreateSession(int accountId)
    {
        return new WorldSession(
            AccountId: accountId,
            Username: "session" + accountId,
            Character: new CharacterSnapshot(
                Uid: accountId,
                Name: "Session" + accountId,
                Body: 1003,
                Face: 0,
                Hair: 0,
                Class: 10,
                FirstClass: 10,
                SecondClass: 0,
                Reborn: 0,
                Level: 1,
                Experience: 0,
                MapId: 1002,
                X: 300,
                Y: 278,
                PreviousMapId: 1002,
                PreviousMapX: 300,
                PreviousMapY: 278,
                Strength: 0,
                Agility: 0,
                Vitality: 0,
                Spirit: 0,
                AttributePoints: 0,
                MinHitPoints: 1,
                MinMana: 0,
                Money: 0,
                ConquerPoints: 0,
                BoundConquerPoints: 0,
                BankConquerPoints: 0,
                PkPoints: 0,
                Online: 1,
                SourceFile: accountId + ".ini",
                Attributes: new Dictionary<string, string>(),
                PaidNobility: new Dictionary<string, string>()),
            PlayerItems: PlayerItemSnapshot.Empty,
            PlayerSpells: PlayerSpellSnapshot.Empty,
            PlayerProficiencies: PlayerProficiencySnapshot.Empty);
    }
}
