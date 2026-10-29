using System.Collections;
using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeDbDictionarySafetyTests
{
    [Fact]
    public void ParallelMutationsAndSnapshotsRetainEveryOwnedFinalValue()
    {
        var owner = new LegacyNativeDbSafeDictionary<int, int>();
        const int workers = 8, rows = 80;
        Parallel.For(0, workers, worker =>
        {
            IDictionary<int, int> contract = owner;
            for (int offset = 0; offset < rows; offset++)
            {
                int key = worker * rows + offset;
                contract.Add(key, key); owner[key] = key + 10_000;
                var values = owner.GetValues(); Assert.InRange(values.Length, 1, workers * rows);
                var pairs = owner.ToArray(); Assert.Equal(pairs.Length, pairs.Select(pair => pair.Key).Distinct().Count());
                if (offset % 4 == 0) { Assert.True(contract.Remove(key)); Assert.True(owner.TryAdd(key, key + 10_000)); }
            }
        });
        Assert.Equal(workers * rows, owner.Count);
        foreach (var pair in owner) Assert.Equal(pair.Key + 10_000, pair.Value);
        Assert.Equal(owner.Count, owner.GetValues().Length);
        Assert.Equal(workers * (rows * 2 + (rows / 4) * 2), owner.Revision);
        IDictionary legacy = owner; legacy.Clear(); Assert.Empty(owner.GetValues());
    }

    [Fact]
    public async Task StableGateSerializesConcreteAndInterfaceReadsAndWrites()
    {
        var owner = new LegacyNativeDbSafeDictionary<int, int>(); owner.Add(1, 1); owner.GetValues();
        foreach (Action operation in new Action[] { () => owner[1] = 2, () => ((IDictionary<int, int>)owner).Add(3, 3),
                     () => _ = owner.GetValues(), () => _ = owner.TryGetValue(1, out _),
                     () => _ = owner.Values.Count, () => _ = owner.ToArray() })
        {
            using var started = new ManualResetEventSlim(); Task worker;
            lock (owner.SyncRoot)
            {
                worker = Task.Run(() => { started.Set(); operation(); });
                Assert.True(started.Wait(TimeSpan.FromSeconds(5)));
                Assert.False(worker.Wait(TimeSpan.FromMilliseconds(100)));
                Assert.Same(owner.SyncRoot, ((System.Collections.ICollection)owner).SyncRoot);
            }
            await worker.WaitAsync(TimeSpan.FromSeconds(5));
        }
        Assert.Equal(2, owner[1]); Assert.Equal(3, owner[3]);
    }
}