using System.Collections;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeItemBagRuntimeTests
{
    [Fact]
    public void InterfaceAddRejectsDuplicatesWhileNativeUpsertRetainsTheExactReplacement()
    {
        var bag = new LegacyNativeItemBagRuntime(); IDictionary<uint, LegacyNativeGameItemRuntime> contract = bag;
        IDictionary legacy = bag; ICollection<KeyValuePair<uint, LegacyNativeGameItemRuntime>> pairs = bag;
        var first = new LegacyNativeGameItemRuntime { Uid = 101 }; var replacement = new LegacyNativeGameItemRuntime { Uid = 102 };
        contract.Add(7, first); bag.GetValues(); long revision = bag.Revision;
        Assert.Throws<ArgumentException>(() => contract.Add(7, replacement));
        Assert.Throws<ArgumentException>(() => legacy.Add(7u, replacement));
        Assert.Throws<ArgumentException>(() => pairs.Add(new(7, replacement)));
        Assert.Throws<ArgumentException>(() => legacy.Add("bad-key", replacement));
        Assert.Throws<ArgumentException>(() => legacy[8u] = "bad-value");
        Assert.Equal(revision, bag.Revision); Assert.False(bag.Update); Assert.Same(first, bag[7]);
        bag.Add(7, replacement); Assert.Equal(revision + 1, bag.Revision); Assert.True(bag.Update);
        Assert.Same(replacement, Assert.Single(bag.GetValues())); Assert.Equal(102u, replacement.Uid);
        legacy.Remove(7u); Assert.Empty(bag.GetValues()); Assert.Equal(revision + 2, bag.Revision);
    }

    [Fact]
    public void ActualItemKeysValuesAndEnumerationAreOrderedDefensiveSnapshots()
    {
        var bag = new LegacyNativeItemBagRuntime();
        var first = new LegacyNativeGameItemRuntime { Uid = 201 }; var second = new LegacyNativeGameItemRuntime { Uid = 202 };
        bag.Add(7, first); bag.Add(4, second);
        var keys = bag.Keys; var values = bag.Values; using var entries = bag.GetEnumerator();
        Assert.Throws<NotSupportedException>(() => keys.Add(9));
        Assert.Throws<NotSupportedException>(() => values.Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<LegacyNativeGameItemRuntime>)values)[0] = second);
        bag.Remove(7); Assert.Equal(new uint[] { 7, 4 }, keys); Assert.Equal(new[] { first, second }, values);
        Assert.True(entries.MoveNext()); Assert.Equal(7u, entries.Current.Key); Assert.Same(first, entries.Current.Value);
        Assert.True(entries.MoveNext()); Assert.Equal(4u, entries.Current.Key); Assert.Same(second, entries.Current.Value);
        Assert.False(entries.MoveNext()); Assert.Same(second, Assert.Single(bag.GetValues()));
    }

    [Fact]
    public void ParallelBagIndexersInterfacesAndSnapshotsPreserveAllFinalActualItems()
    {
        var bag = new LegacyNativeItemBagRuntime(); const int workers = 6, rows = 60;
        Parallel.For(0, workers, worker =>
        {
            IDictionary<uint, LegacyNativeGameItemRuntime> contract = bag;
            for (uint offset = 0; offset < rows; offset++)
            {
                uint key = (uint)(worker * rows) + offset;
                contract.Add(key, new LegacyNativeGameItemRuntime { Uid = key + 1000 });
                var replacement = new LegacyNativeGameItemRuntime { Uid = key + 2000 };
                bag[key] = replacement;
                var snapshot = bag.GetValues(); Assert.InRange(snapshot.Length, 1, workers * rows);
                Assert.All(snapshot, item => Assert.NotNull(item));
                var entries = bag.ToArray(); Assert.Equal(entries.Length, entries.Select(pair => pair.Key).Distinct().Count());
                Assert.Same(replacement, bag[key]);
            }
        });
        Assert.Equal(workers * rows, bag.Count); Assert.Equal((long)workers * rows * 2, bag.Revision);
        foreach (var pair in bag) Assert.Equal(pair.Key + 2000, pair.Value.Uid);
        Assert.Equal(workers * rows, bag.GetValues().Length);
    }

    [Fact]
    public async Task BagInterfaceMutationAndSnapshotReadsUseTheSameStableGate()
    {
        var bag = new LegacyNativeItemBagRuntime(); var item = new LegacyNativeGameItemRuntime { Uid = 301 };
        bag.Add(7, item); object gate = bag.SyncRoot;
        foreach (Action operation in new Action[] { () => ((IDictionary<uint, LegacyNativeGameItemRuntime>)bag)[7] = item,
                     () => _ = bag.GetValues(), () => _ = bag.ToArray(), () => _ = bag.Values.Count })
        {
            using var started = new ManualResetEventSlim(); Task worker;
            lock (gate)
            {
                worker = Task.Run(() => { started.Set(); operation(); });
                Assert.True(started.Wait(TimeSpan.FromSeconds(5)));
                Assert.False(worker.Wait(TimeSpan.FromMilliseconds(100)));
                Assert.Same(gate, ((ICollection)bag).SyncRoot);
            }
            await worker.WaitAsync(TimeSpan.FromSeconds(5));
        }
        Assert.Same(gate, bag.SyncRoot); Assert.Same(item, bag[7]);
    }
}