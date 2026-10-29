using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyNativeDbDictionaryCallerTests
{
    [Fact]
    public void ActualRankItemReplacementIsVisibleToSubsequentSavedValueReaders()
    {
        var rank = new LegacyNativeRankItems.Rank();
        var original = new LegacyNativeGameItemRuntime { Uid = 9871, ItemPoints = 10, Position = 0 };
        var replacement = new LegacyNativeGameItemRuntime { Uid = 9871, ItemPoints = 30, Position = 0 };
        rank.AddItem(original); var savedBefore = rank.Items.GetValues(); long revision = rank.Items.Revision;
        rank.AddItem(replacement);
        Assert.Same(replacement, rank.Items[9871]); Assert.Same(replacement, Assert.Single(rank.Items.GetValues()));
        Assert.Same(original, Assert.Single(savedBefore)); Assert.Equal(revision + 1, rank.Items.Revision);
        Assert.Same(replacement, Assert.Single(rank.GetRank50Items())); Assert.Equal(1u, rank.GetItemRank(9871));
    }

    [Fact]
    public void ActualBoothCollectionKeepsUpsertedRowsAndRejectsEscapedArrayPoisoning()
    {
        var booth = new LegacyNativeBooth(); var item = new LegacyNativeGameItemRuntime { Uid = 9872 };
        booth.ItemList.Add(item.Uid, new() { Item = item, Cost = 7 });
        var before = booth.ItemList.GetValues(); booth.ItemList.Add(item.Uid, new() { Item = item, Cost = 9 });
        var current = booth.ItemList.GetValues(); Assert.Single(booth.ItemList);
        Assert.Equal(7, before[0].Cost); Assert.Equal(9, current[0].Cost); Assert.Same(item, current[0].Item);
        current[0] = default; Assert.Equal(9, booth.ItemList.GetValues()[0].Cost);
        Assert.Same(item, booth.ItemList[item.Uid].Item);
    }
}