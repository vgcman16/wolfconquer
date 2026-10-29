using System.Collections;
using System.Reflection;
using System.Threading;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyNativeItemBagRuntimeTests
{
    [Fact]
    public void ConstructorsKeepIndependentStableGatesAndEmptyOwnedState()
    {
        var first = new LegacyNativeItemBagRuntime(); var second = new LegacyNativeItemBagRuntime(16);
        Assert.NotSame(first.SyncRoot, second.SyncRoot); Assert.NotNull(first.SyncRoot); Assert.NotNull(second.SyncRoot);
        Assert.False(first.Update); Assert.False(second.Update); Assert.Equal(0L, first.Revision);
        Assert.Empty(first.MyArray); Assert.Empty(second.MyArray); Assert.Empty(first); Assert.Empty(second);
        Assert.Same(first.SyncRoot, ((ICollection)first).SyncRoot);
    }

    [Fact]
    public void CapacityConstructorStillRejectsNegativeCapacity()
        => Assert.Throws<ArgumentOutOfRangeException>(() => new LegacyNativeItemBagRuntime(-1));

    [Fact]
    public void MissingNativeIndexerReturnsNullWhileDictionaryInterfaceThrowsWithoutInsertion()
    {
        var bag = new LegacyNativeItemBagRuntime(); Assert.Null(bag[11u]);
        Assert.Throws<KeyNotFoundException>(() => _ = ((IDictionary<uint, LegacyNativeGameItemRuntime>)bag)[11u]);
        Assert.Empty(bag); Assert.False(bag.Update); Assert.Equal(0L, bag.Revision);
    }

    [Fact]
    public void NativeAddKeepsActualItemUpsertWithoutNormalizingSlotKeysOrUids()
    {
        var bag = new LegacyNativeItemBagRuntime();
        var first = new LegacyNativeGameItemRuntime { Uid = 11u }; var replacement = new LegacyNativeGameItemRuntime { Uid = 99u };
        bag.Add(7u, first); bag.Add(7u, replacement);
        Assert.Single(bag); Assert.Same(replacement, bag[7u]); Assert.Equal(99u, replacement.Uid);
        Assert.Equal(7u, Assert.Single(bag.Keys)); Assert.True(bag.Update); Assert.Equal(2L, bag.Revision);
    }

    [Fact]
    public void GetValuesReturnsIndependentCurrentArraysWithTheActualItemReferences()
    {
        var bag = new LegacyNativeItemBagRuntime(); var item = new LegacyNativeGameItemRuntime { Uid = 11u };
        bag.Add(7u, item); var values = bag.GetValues(); var other = bag.GetValues();
        Assert.False(bag.Update); Assert.Single(values); Assert.Same(item, values[0]);
        Assert.NotSame(values, other); Assert.NotSame(values, bag.MyArray); Assert.Equal(values, other);
        values[0] = null!; Assert.Same(item, bag.GetValues()[0]); Assert.Same(item, bag.MyArray[0]);
    }

    [Fact]
    public void DefensiveArraysKeepLegitimateMutationsToTheSameActualItemVisible()
    {
        var bag = new LegacyNativeItemBagRuntime(); var item = new LegacyNativeGameItemRuntime { Uid = 11u };
        bag.Add(7u, item); var values = bag.GetValues(); long revision = bag.Revision;
        item.PerfectionLevel = 18u;
        Assert.Same(item, values[0]); Assert.Equal(18u, bag.GetValues()[0].PerfectionLevel);
        Assert.Equal(revision, bag.Revision); Assert.False(bag.Update);
    }

    [Fact]
    public void IndexerReplacementInvalidatesTheCurrentValueSnapshot()
    {
        var bag = new LegacyNativeItemBagRuntime(); var old = new LegacyNativeGameItemRuntime { Uid = 11u };
        var replacement = new LegacyNativeGameItemRuntime { Uid = 99u };
        bag.Add(7u, old); var cached = bag.GetValues(); long revision = bag.Revision;
        bag[7u] = replacement;
        Assert.True(bag.Update); Assert.Equal(revision + 1, bag.Revision);
        Assert.Same(replacement, bag[7u]); Assert.Same(replacement, Assert.Single(bag.GetValues()));
        Assert.Same(old, Assert.Single(cached)); Assert.Same(replacement, Assert.Single(bag.Values));
    }

    [Fact]
    public void IndexerFirstInsertImmediatelyParticipatesInCurrentSnapshots()
    {
        var bag = new LegacyNativeItemBagRuntime(); var item = new LegacyNativeGameItemRuntime { Uid = 11u };
        bag[7u] = item; Assert.Single(bag); Assert.True(bag.Update); Assert.Equal(1L, bag.Revision);
        Assert.Same(item, Assert.Single(bag.GetValues())); Assert.False(bag.Update);
    }

    [Fact]
    public void MissingRemoveDoesNotInventADirtyMutationOrAdvanceRevision()
    {
        var bag = new LegacyNativeItemBagRuntime(); var item = new LegacyNativeGameItemRuntime { Uid = 11u };
        bag.Add(7u, item); var before = bag.GetValues(); long revision = bag.Revision;
        Assert.False(bag.Remove(8u)); Assert.False(bag.Update); Assert.Equal(revision, bag.Revision);
        Assert.Equal(before, bag.GetValues()); Assert.Same(item, Assert.Single(before));
    }

    [Fact]
    public void ExistingRemoveInvalidatesTheOwnerButLeavesEscapedOldCopiesIntact()
    {
        var bag = new LegacyNativeItemBagRuntime(); var item = new LegacyNativeGameItemRuntime { Uid = 11u };
        bag.Add(7u, item); var cached = bag.GetValues();
        Assert.True(bag.Remove(7u)); Assert.Empty(bag); Assert.True(bag.Update);
        Assert.Same(item, Assert.Single(cached)); Assert.Empty(bag.GetValues()); Assert.Equal(2L, bag.Revision);
    }

    [Fact]
    public void ClearUsesTheStableOwnerAndEmptyClearDoesNotInventAMutation()
    {
        var bag = new LegacyNativeItemBagRuntime(); var item = new LegacyNativeGameItemRuntime { Uid = 11u };
        bag.Add(7u, item); var cached = bag.GetValues(); bag.Clear();
        Assert.Empty(bag); Assert.Same(item, Assert.Single(cached)); Assert.True(bag.Update);
        Assert.False(Monitor.IsEntered(bag.SyncRoot)); Assert.Empty(bag.GetValues()); Assert.False(bag.Update);
        long revision = bag.Revision; bag.Clear(); Assert.Equal(revision, bag.Revision); Assert.False(bag.Update);
    }

    [Fact]
    public void DictionaryInterfaceRemovalCannotBypassInvalidationOrRevision()
    {
        var bag = new LegacyNativeItemBagRuntime(); var item = new LegacyNativeGameItemRuntime { Uid = 11u };
        bag.Add(7u, item); var cached = bag.GetValues(); long revision = bag.Revision;
        Assert.True(((IDictionary<uint, LegacyNativeGameItemRuntime>)bag).Remove(7u));
        Assert.True(bag.Update); Assert.Equal(revision + 1, bag.Revision); Assert.Empty(bag.GetValues());
        Assert.Same(item, Assert.Single(cached)); Assert.Empty(bag);
    }

    [Fact]
    public void ExternalArrayReplacementCannotPublishForeignItemsIntoTheBag()
    {
        var bag = new LegacyNativeItemBagRuntime(); var item = new LegacyNativeGameItemRuntime { Uid = 11u };
        bag.Add(7u, item); bag.GetValues(); long revision = bag.Revision;
        var escaped = bag.MyArray; escaped[0] = new() { Uid = 999u };
        var values = bag.GetValues(); values[0] = null!;
        Assert.Same(item, bag[7u]); Assert.Same(item, Assert.Single(bag.GetValues()));
        Assert.Equal(revision, bag.Revision); Assert.False(bag.Update);
    }

    [Fact]
    public void NullRowsStayActualStoredValuesWithoutAPlaceholderOrCollectionEligibilityRule()
    {
        var bag = new LegacyNativeItemBagRuntime(); bag.Add(7u, null!);
        Assert.True(bag.ContainsKey(7u)); Assert.Null(bag[7u]); Assert.Null(Assert.Single(bag.GetValues()));
        Assert.Single(bag); Assert.Equal(1L, bag.Revision);
    }

    [Fact]
    public void CacheDirtyAndGateCannotBeExternallyReassigned()
    {
        var type = typeof(LegacyNativeItemBagRuntime);
        foreach (string property in new[] { "MyArray", "Update", "SyncRoot", "Revision" })
        {
            Assert.Null(type.GetField(property)); Assert.NotNull(type.GetProperty(property));
            Assert.Null(type.GetProperty(property)!.SetMethod);
        }
        Assert.Equal(typeof(LegacyNativeDbSafeDictionary<uint, LegacyNativeGameItemRuntime>), type.BaseType);
        Assert.Empty(type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly));
        Assert.False(typeof(Dictionary<uint, LegacyNativeGameItemRuntime>).IsAssignableFrom(type));
    }

    [Fact]
    public void MutationsAndSnapshotsRetainAnAlreadyHeldOuterReentrantLock()
    {
        var bag = new LegacyNativeItemBagRuntime(); Monitor.Enter(bag.SyncRoot);
        try
        {
            bag.Add(7u, new LegacyNativeGameItemRuntime()); Assert.True(Monitor.IsEntered(bag.SyncRoot));
            bag.GetValues(); Assert.True(Monitor.IsEntered(bag.SyncRoot));
            bag.Remove(7u); Assert.True(Monitor.IsEntered(bag.SyncRoot));
            bag.Clear(); Assert.True(Monitor.IsEntered(bag.SyncRoot));
        }
        finally { Monitor.Exit(bag.SyncRoot); }
        Assert.False(Monitor.IsEntered(bag.SyncRoot));
    }

    [Fact]
    public void IndependentBagsMayShareOneActualItemWithoutSharingMembershipOrArrays()
    {
        var mythsoul = new LegacyNativeItemBagRuntime(); var relic = new LegacyNativeItemBagRuntime();
        var item = new LegacyNativeGameItemRuntime { Uid = 11u }; mythsoul.Add(7u, item); relic.Add(8u, item);
        Assert.Same(mythsoul[7u], relic[8u]); Assert.NotSame(mythsoul.GetValues(), relic.GetValues());
        relic[8u].Plus = 12; Assert.Equal((byte)12, mythsoul[7u].Plus);
        relic.Clear(); Assert.Empty(relic); Assert.Same(item, mythsoul[7u]);
    }
}