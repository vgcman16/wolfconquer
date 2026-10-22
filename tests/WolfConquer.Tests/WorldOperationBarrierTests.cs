using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class WorldOperationBarrierTests
{
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task SharedOperationsOverlapAndPendingWriterBlocksLaterReaders()
    {
        var barrier = new WorldOperationBarrier();
        var first = await barrier.EnterSharedAsync();
        var second = await barrier.EnterSharedAsync();
        var writer = barrier.EnterExclusiveAsync().AsTask();
        var laterReader = barrier.EnterSharedAsync().AsTask();
        Assert.False(writer.IsCompleted);
        Assert.False(laterReader.IsCompleted);

        first.Dispose();
        Assert.False(writer.IsCompleted);
        second.Dispose();
        using (await writer.WaitAsync(Deadline))
        {
            Assert.False(laterReader.IsCompleted);
        }
        using var last = await laterReader.WaitAsync(Deadline);
    }

    [Fact]
    public async Task ExclusiveLeasesKeepFifoPositionAndExcludeAllOtherOperations()
    {
        var barrier = new WorldOperationBarrier();
        var held = await barrier.EnterExclusiveAsync();
        var secondWriter = barrier.EnterExclusiveAsync().AsTask();
        var reader = barrier.EnterSharedAsync().AsTask();
        var lastWriter = barrier.EnterExclusiveAsync().AsTask();
        held.Dispose();

        using (await secondWriter.WaitAsync(Deadline))
        {
            Assert.False(reader.IsCompleted);
            Assert.False(lastWriter.IsCompleted);
        }
        using (await reader.WaitAsync(Deadline))
        {
            Assert.False(lastWriter.IsCompleted);
        }
        using var final = await lastWriter.WaitAsync(Deadline);
    }

    [Fact]
    public async Task CancellingPendingWriterUnblocksReadersWithoutLeakingOwnership()
    {
        var barrier = new WorldOperationBarrier();
        using var cancellation = new CancellationTokenSource();
        var first = await barrier.EnterSharedAsync();
        var writer = barrier.EnterExclusiveAsync(cancellation.Token).AsTask();
        var laterReader = barrier.EnterSharedAsync().AsTask();
        Assert.False(laterReader.IsCompleted);

        cancellation.Cancel();

        await Assert.ThrowsAsync<TaskCanceledException>(async () => { await writer.WaitAsync(Deadline); });
        Task<IDisposable> nextWriter;
        using (await laterReader.WaitAsync(Deadline))
        {
            nextWriter = barrier.EnterExclusiveAsync().AsTask();
            first.Dispose();
            Assert.False(nextWriter.IsCompleted);
            // The remaining reader must release before the next writer can enter.
        }
        using var exclusive = await nextWriter.WaitAsync(Deadline);
    }

    [Fact]
    public async Task CancellingWaitingReaderDoesNotConsumeAWriterOrReaderLease()
    {
        var barrier = new WorldOperationBarrier();
        var writer = await barrier.EnterExclusiveAsync();
        using var cancellation = new CancellationTokenSource();
        var reader = barrier.EnterSharedAsync(cancellation.Token).AsTask();
        cancellation.Cancel();

        await Assert.ThrowsAsync<TaskCanceledException>(async () => { await reader.WaitAsync(Deadline); });
        writer.Dispose();

        using var nextWriter = await barrier.EnterExclusiveAsync().AsTask().WaitAsync(Deadline);
    }

    [Fact]
    public async Task LeaseDisposalIsOnceAndCancellationAfterGrantDoesNotRevokeLease()
    {
        var barrier = new WorldOperationBarrier();
        using var cancellation = new CancellationTokenSource();
        var writer = await barrier.EnterExclusiveAsync(cancellation.Token);
        cancellation.Cancel();
        var reader = barrier.EnterSharedAsync().AsTask();
        Assert.False(reader.IsCompleted);
        writer.Dispose();
        writer.Dispose();
        var readLease = await reader.WaitAsync(Deadline);
        readLease.Dispose();
        readLease.Dispose();
        using var nextWriter = await barrier.EnterExclusiveAsync().AsTask().WaitAsync(Deadline);
    }

    [Fact]
    public async Task AlreadyCancelledAcquisitionsLeaveBarrierAvailable()
    {
        var barrier = new WorldOperationBarrier();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => { _ = barrier.EnterSharedAsync(cancellation.Token); });
        Assert.Throws<OperationCanceledException>(() => { _ = barrier.EnterExclusiveAsync(cancellation.Token); });

        using var writer = await barrier.EnterExclusiveAsync().AsTask().WaitAsync(Deadline);
    }

    [Fact]
    public async Task OldSharedFullStateSetFinishesBeforeExclusiveWalletAndItemsCommit()
    {
        var barrier = new WorldOperationBarrier();
        var sessions = new WorldSessionStore();
        var connection = new ConquerTcpConnection(null, null);
        var initial = CreateSession();
        sessions.Set(connection, initial);
        var oldReader = await barrier.EnterSharedAsync();
        Assert.True(sessions.TryGet(connection, out var captured));
        var exclusive = barrier.EnterExclusiveAsync().AsTask();
        Assert.False(exclusive.IsCompleted);

        // Models an old async handler's full-record Set after its awaited send.
        sessions.Set(connection, captured! with { Character = captured!.Character with { X = 301 } });
        oldReader.Dispose();
        using (await exclusive.WaitAsync(Deadline))
        {
            Assert.True(sessions.TryGet(connection, out var current));
            Assert.Equal(301L, current!.Character.X);
            var tradedItem = new PlayerItemSnapshotItem { Uid = 22, ItemId = 1000000, StackSize = 1 };
            var items = current.PlayerItems with { InventoryItems = new[] { tradedItem } };
            sessions.Set(connection, current with
            {
                Character = current.Character with { Money = 250, ConquerPoints = 10 },
                PlayerItems = items
            });
        }

        using var nextReader = await barrier.EnterSharedAsync();
        Assert.True(sessions.TryGet(connection, out var final));
        Assert.Equal(301L, final!.Character.X);
        Assert.Equal(250L, final.Character.Money);
        Assert.Equal(10L, final.Character.ConquerPoints);
        Assert.Equal(22U, Assert.Single(final.PlayerItems.InventoryItems).Uid);
    }

    private static WorldSession CreateSession() => new(
        AccountId: 1, Username: "barrier-test",
        Character: new CharacterSnapshot(
            Uid: 1, Name: "Barrier", Body: 1003, Face: 0, Hair: 0,
            Class: 10, FirstClass: 10, SecondClass: 0, Reborn: 0, Level: 1,
            Experience: 0, MapId: 1002, X: 300, Y: 278,
            PreviousMapId: 1002, PreviousMapX: 300, PreviousMapY: 278,
            Strength: 0, Agility: 0, Vitality: 0, Spirit: 0, AttributePoints: 0,
            MinHitPoints: 1, MinMana: 0, Money: 100, ConquerPoints: 0,
            BoundConquerPoints: 0, BankConquerPoints: 0, PkPoints: 0, Online: 1,
            SourceFile: "1.ini", Attributes: new Dictionary<string, string>(),
            PaidNobility: new Dictionary<string, string>()),
        PlayerItems: PlayerItemSnapshot.Empty,
        PlayerSpells: PlayerSpellSnapshot.Empty,
        PlayerProficiencies: PlayerProficiencySnapshot.Empty);
}
