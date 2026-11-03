using System.Runtime.ExceptionServices;
using WolfConquer.Core.Data.SqlWorld;

namespace WolfConquer.Tests;

public sealed partial class ModernSqlAuthFileRoundtripTests
{
    [ModernSqlAuthFixtureFact]
    public Task ActualPairCancellationReleasesFirstOwnerWhileSecondSingleCommitIsHeld() => RunSqlWorldStateAsync(async (database, repository) =>
    {
        var initial = await SeedBundlePairAsync(database, repository);
        var exchanged = SqlWorldStateTestData.Exchanged();
        var firstImage = CompleteWorldImage(exchanged.First, 1); var secondImage = CompleteWorldImage(exchanged.Second, 2);
        var cancelledRequest = BundlePair(firstImage, secondImage);
        var cancelledDescriptor = cancelledRequest.CopyDescriptorBytes(); var cancelledHash = cancelledRequest.CopyDescriptorHash();
        var updatedSecond = CompleteWorldImage(SqlWorldStateTestData.Image(502, 9002, SqlWorldStateTestData.SecondItems(), 2011, 201), 1);
        var singleRequest = BundleRequest(SqlWorldStateOperationKind.Save, updatedSecond, 1);
        var singleDescriptor = singleRequest.CopyDescriptorBytes(); var singleHash = singleRequest.CopyDescriptorHash();
        await using var proxy = database.CreateCommitRequestGateProxy();
        var a = await repository.TryAcquireBundleCustodyAsync(initial.First.AccountId);
        Assert.True(a.IsAcquired); await using var first = Assert.IsType<SqlWorldBundleCustody>(a.Custody);
        var b = await proxy.CreateWorldStateRepository().TryAcquireBundleCustodyAsync(initial.Second.AccountId);
        Assert.True(b.IsAcquired); await using var second = Assert.IsType<SqlWorldBundleCustody>(b.Custody);
        var before = await database.SnapshotWorldBundleRowsAsync();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        using (var preCancelled = new CancellationTokenSource())
        {
            preCancelled.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => { await first.SavePairBundleAsync(second, cancelledRequest, preCancelled.Token); });
        }
        Assert.Equal(before, await database.SnapshotWorldBundleRowsAsync());
        Assert.Equal(cancelledDescriptor, cancelledRequest.CopyDescriptorBytes()); Assert.Equal(cancelledHash, cancelledRequest.CopyDescriptorHash());

        using var pairCancellation = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token);
        var single = second.SaveBundleAsync(singleRequest, deadline.Token).AsTask();
        Task<SqlWorldStateBundleCommitResult>? pair = null; Task<SqlWorldStateBundleReadResult>? probe = null;
        SqlWorldStateBundleCommitResult? singleResult = null; Exception? primary = null;
        var cleanup = new List<Exception>();
        try
        {
            var held = await proxy.WaitForHeldCommitRequestAsync().WaitAsync(deadline.Token);
            Assert.Equal(2, held.ObservedCommitQueries); Assert.Equal((byte)0, held.Sequence); Assert.Equal(7, held.PayloadBytes);
            Assert.False(single.IsCompleted);
            // Pair enters the lower account's real async gate before waiting on
            // the higher account's gate held by the actual single-owner save.
            pair = second.SavePairBundleAsync(first, cancelledRequest, pairCancellation.Token).AsTask();
            probe = first.ReadBundleAsync(deadline.Token).AsTask();
            Assert.False(pair.IsCompleted); Assert.False(probe.IsCompleted);
            pairCancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => { await pair; });
            var read = await probe;
            Assert.Equal(SqlWorldStateReadStatus.Found, read.Status);
            var firstRecord = Assert.IsType<SqlWorldStateBundleRecord>(read.Record);
            Assert.Equal(1, firstRecord.Revision); AssertBundleImage(initial.First, firstRecord.Image);
            Assert.False(single.IsCompleted); // The first owner is usable before release of the real 502 COMMIT.
            Assert.Equal(before, await database.SnapshotWorldBundleRowsAsync());
            Assert.Equal(cancelledDescriptor, cancelledRequest.CopyDescriptorBytes()); Assert.Equal(cancelledHash, cancelledRequest.CopyDescriptorHash());
            Assert.DoesNotContain(before, row => row.StartsWith("operation|" + Convert.ToHexString(cancelledRequest.OperationId.ToByteArray()) + "|", StringComparison.Ordinal));
        }
        catch (Exception error) { primary = error; }
        finally
        {
            try { pairCancellation.Cancel(); } catch (Exception error) { cleanup.Add(error); }
            try { proxy.ReleaseHeldCommitRequest(); } catch (Exception error) { cleanup.Add(error); }
            if (pair is not null)
                try { await pair; } catch (OperationCanceledException) when (pairCancellation.IsCancellationRequested) { } catch (Exception error) { cleanup.Add(error); }
            if (probe is not null)
                try { await probe; } catch (Exception error) { cleanup.Add(error); }
            try { singleResult = await single; } catch (Exception error) { cleanup.Add(error); }
        }
        if (primary is not null) cleanup.Insert(0, primary);
        if (cleanup.Count == 1) ExceptionDispatchInfo.Capture(cleanup[0]).Throw();
        if (cleanup.Count > 1) throw new AggregateException(cleanup);
        AssertBundleCommit(Assert.IsType<SqlWorldStateBundleCommitResult>(singleResult), SqlWorldStateCommitStatus.Committed, 2, updatedSecond);
        var forwarded = await proxy.WaitForForwardedCommitAsync().WaitAsync(deadline.Token);
        Assert.Equal(2, forwarded.ObservedCommitQueries); Assert.Equal((byte)1, forwarded.AckSequence);
        Assert.True(forwarded.AckPayloadBytes >= 7); Assert.Equal(0, forwarded.StatusFlags & (0x0001 | 0x0008));
        Assert.Equal(singleDescriptor, singleRequest.CopyDescriptorBytes()); Assert.Equal(singleHash, singleRequest.CopyDescriptorHash());
        await AssertBundleReadAsync(database, initial.First, 1); await AssertBundleReadAsync(database, updatedSecond, 2);
        var afterSingle = await database.SnapshotWorldBundleRowsAsync();
        Assert.DoesNotContain(afterSingle, row => row.StartsWith("operation|" + Convert.ToHexString(cancelledRequest.OperationId.ToByteArray()) + "|", StringComparison.Ordinal));
        var finalPair = BundlePair(firstImage, secondImage, 1, 2);
        var committed = await first.SavePairBundleAsync(second, finalPair, deadline.Token);
        Assert.Equal(SqlWorldStateCommitStatus.Committed, committed.Status); Assert.Equal(SqlWorldStateFailure.None, committed.Failure);
        var records = committed.Records.ToArray(); Assert.Equal(2, records.Length);
        Assert.Equal(2, records[0].Revision); AssertBundleImage(firstImage, records[0].Image);
        Assert.Equal(3, records[1].Revision); AssertBundleImage(secondImage, records[1].Image);
        await AssertBundleReadAsync(database, firstImage, 2); await AssertBundleReadAsync(database, secondImage, 3);
    });
}
