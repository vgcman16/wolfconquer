using System.Threading;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyNativeItemBagRuntimeTests
{
    [Fact]
    public void NativeConstructorsCreateIndependentMonitorAndEmptyCacheState()
    {
        var first = new LegacyNativeItemBagRuntime();
        var second = new LegacyNativeItemBagRuntime(16);
        Assert.NotSame(first.SyncRoot, second.SyncRoot);
        Assert.NotNull(first.SyncRoot);
        Assert.NotNull(second.SyncRoot);
        Assert.False(first.Update);
        Assert.False(second.Update);
        Assert.Empty(first.MyArray);
        Assert.Empty(second.MyArray);
        Assert.Empty(first);
        Assert.Empty(second);
    }

    [Fact]
    public void CapacityConstructorKeepsNativeBaseDictionaryNegativeCapacityFault()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new LegacyNativeItemBagRuntime(-1));
    }

    [Fact]
    public void MissingHiddenIndexerReturnsNullWhileBaseIndexerStillThrows()
    {
        var bag = new LegacyNativeItemBagRuntime();
        Assert.Null(bag[11u]);
        var direct = (Dictionary<uint, LegacyNativeGameItemRuntime>)bag;
        Assert.Throws<KeyNotFoundException>(() => { _ = direct[11u]; });
    }

    [Fact]
    public void AddOverwritesDuplicateKeyWithSameActualItemReferenceAndMarksDirty()
    {
        var bag = new LegacyNativeItemBagRuntime();
        var first = new LegacyNativeGameItemRuntime { Uid = 11u };
        var replacement = new LegacyNativeGameItemRuntime { Uid = 99u };
        bag.Add(7u, first);
        bag.Add(7u, replacement);
        Assert.Single(bag);
        Assert.Same(replacement, bag[7u]);
        Assert.Equal(99u, replacement.Uid); // Dictionary key is not an identity normalizer.
        Assert.True(bag.Update);
    }

    [Fact]
    public void GetValuesRebuildsOnceAndReturnsTheSameCachedArrayUntilDirty()
    {
        var bag = new LegacyNativeItemBagRuntime();
        var item = new LegacyNativeGameItemRuntime { Uid = 11u };
        bag.Add(7u, item);
        var values = bag.GetValues();
        Assert.False(bag.Update);
        Assert.Single(values);
        Assert.Same(item, values[0]);
        Assert.Same(values, bag.MyArray);
        Assert.Same(values, bag.GetValues());
    }

    [Fact]
    public void CachedValuesRetainActualItemMutationsWithoutCopyingItems()
    {
        var bag = new LegacyNativeItemBagRuntime();
        var item = new LegacyNativeGameItemRuntime { Uid = 11u };
        bag.Add(7u, item);
        var values = bag.GetValues();
        item.PerfectionLevel = 18u;
        Assert.Same(item, values[0]);
        Assert.Equal(18u, values[0].PerfectionLevel);
    }

    [Fact]
    public void HiddenIndexerSetterDoesNotInvalidateTheNativeCachedArray()
    {
        var bag = new LegacyNativeItemBagRuntime();
        var old = new LegacyNativeGameItemRuntime { Uid = 11u };
        var replacement = new LegacyNativeGameItemRuntime { Uid = 99u };
        bag.Add(7u, old);
        var cached = bag.GetValues();
        bag[7u] = replacement;
        Assert.False(bag.Update);
        Assert.Same(replacement, bag[7u]);
        Assert.Same(cached, bag.GetValues());
        Assert.Same(old, bag.GetValues()[0]);
        Assert.Same(replacement, bag.Values.Single());
    }

    [Fact]
    public void IndexerOnlyFirstInsertCanLeaveGetValuesEmptyUntilExplicitDirtyFlag()
    {
        var bag = new LegacyNativeItemBagRuntime();
        var item = new LegacyNativeGameItemRuntime { Uid = 11u };
        bag[7u] = item;
        Assert.Single(bag);
        Assert.False(bag.Update);
        Assert.Empty(bag.GetValues());
        bag.Update = true;
        Assert.Same(item, bag.GetValues().Single());
    }

    [Fact]
    public void RemoveMissingKeyStillMarksDirtyAndRebuildsTheCache()
    {
        var bag = new LegacyNativeItemBagRuntime();
        bag.Add(7u, new LegacyNativeGameItemRuntime { Uid = 11u });
        var before = bag.GetValues();
        bag.Remove(8u);
        Assert.True(bag.Update);
        var after = bag.GetValues();
        Assert.NotSame(before, after);
        Assert.Single(after);
        Assert.False(bag.Update);
    }

    [Fact]
    public void RemoveExistingKeyLeavesOldCacheAvailableUntilGetValuesRefresh()
    {
        var bag = new LegacyNativeItemBagRuntime();
        var item = new LegacyNativeGameItemRuntime { Uid = 11u };
        bag.Add(7u, item);
        var cached = bag.GetValues();
        bag.Remove(7u);
        Assert.Empty(bag);
        Assert.Same(item, cached[0]);
        Assert.True(bag.Update);
        Assert.Empty(bag.GetValues());
    }

    [Fact]
    public void ClearLocksClearsBaseStorageAndMarksDirtyAfterItsLock()
    {
        var bag = new LegacyNativeItemBagRuntime();
        var item = new LegacyNativeGameItemRuntime { Uid = 11u };
        bag.Add(7u, item);
        var cached = bag.GetValues();
        bag.Clear();
        Assert.Empty(bag);
        Assert.Same(item, cached[0]);
        Assert.True(bag.Update);
        Assert.False(Monitor.IsEntered(bag.SyncRoot));
        Assert.Empty(bag.GetValues());
        Assert.False(bag.Update);
    }

    [Fact]
    public void BaseTypedMutationBypassesHiddenMethodsAndCanLeaveCacheStale()
    {
        var bag = new LegacyNativeItemBagRuntime();
        var item = new LegacyNativeGameItemRuntime { Uid = 11u };
        bag.Add(7u, item);
        var cached = bag.GetValues();
        var direct = (Dictionary<uint, LegacyNativeGameItemRuntime>)bag;
        Assert.True(direct.Remove(7u));
        Assert.False(bag.Update);
        Assert.Empty(bag);
        Assert.Same(cached, bag.GetValues());
        Assert.Same(item, cached[0]);
    }

    [Fact]
    public void PublicCacheAndDirtyFieldsAreNotReplacedByARepairingView()
    {
        var bag = new LegacyNativeItemBagRuntime();
        var item = new LegacyNativeGameItemRuntime { Uid = 11u };
        var manual = new[] { item };
        bag.MyArray = manual;
        Assert.Same(manual, bag.GetValues());
        Assert.Empty(bag); // Cache may intentionally disagree with inherited dictionary.
        bag.Update = true;
        Assert.Empty(bag.GetValues());
        bag.MyArray = null!;
        Assert.Null(bag.GetValues());
    }

    [Fact]
    public void NativeNullValuesRemainStoredWithoutUidWrapperOrFallbackObject()
    {
        var bag = new LegacyNativeItemBagRuntime();
        bag.Add(7u, null!);
        Assert.True(bag.ContainsKey(7u));
        Assert.Null(bag[7u]);
        var values = bag.GetValues();
        Assert.Single(values);
        Assert.Null(values[0]);
    }

    [Fact]
    public void MutableSyncRootIsUsedDirectlyAndIsNotSilentlyRepaired()
    {
        var bag = new LegacyNativeItemBagRuntime();
        bag.SyncRoot = null!;
        Assert.Throws<ArgumentNullException>(() => bag.Add(7u, new LegacyNativeGameItemRuntime()));
        Assert.Empty(bag);
        Assert.False(bag.Update);
        bag.Update = true;
        Assert.Throws<ArgumentNullException>(() => bag.GetValues());
        Assert.True(bag.Update);
        Assert.Null(bag.SyncRoot);
    }

    [Fact]
    public void MonitorEnterExitRetainsAnAlreadyHeldOuterReentrantLock()
    {
        var bag = new LegacyNativeItemBagRuntime();
        Monitor.Enter(bag.SyncRoot);
        try
        {
            bag.Add(7u, new LegacyNativeGameItemRuntime());
            Assert.True(Monitor.IsEntered(bag.SyncRoot));
            _ = bag.GetValues();
            Assert.True(Monitor.IsEntered(bag.SyncRoot));
            bag.Remove(7u);
            Assert.True(Monitor.IsEntered(bag.SyncRoot));
            bag.Clear();
            Assert.True(Monitor.IsEntered(bag.SyncRoot));
        }
        finally { Monitor.Exit(bag.SyncRoot); }
        Assert.False(Monitor.IsEntered(bag.SyncRoot));
    }

    [Fact]
    public void TwoBagInstancesMayShareOneActualItemButNotStorageOrCache()
    {
        var mythsoul = new LegacyNativeItemBagRuntime();
        var relic = new LegacyNativeItemBagRuntime();
        var item = new LegacyNativeGameItemRuntime { Uid = 11u };
        mythsoul.Add(7u, item);
        relic.Add(8u, item);
        Assert.Same(mythsoul[7u], relic[8u]);
        Assert.NotSame(mythsoul.GetValues(), relic.GetValues());
        relic[8u]!.Plus = 12;
        Assert.Equal((byte)12, mythsoul[7u]!.Plus);
    }
}
