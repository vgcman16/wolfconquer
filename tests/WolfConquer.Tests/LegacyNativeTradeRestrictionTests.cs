using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyNativeTradeRestrictionTests
{
    private static readonly uint[] Expected =
    {
        750000, 722700, 3306885, 3307142, 3600025, 3200000, 729549, 3008994, 3200005, 3600029,
        3007108, 3007109, 3007110, 3200004, 3600031, 3600027, 3600024
    };

    [Fact]
    public void NativeFieldStartsWithTheEntireOrderedSourceListNotAnInferredCategory()
    {
        Assert.Equal(Expected, LegacyNativeItemTypeRuneMetadata.unabletradeitem);
        Assert.Equal(17, LegacyNativeItemTypeRuneMetadata.unabletradeitem.Count);
        Assert.Equal(17, LegacyNativeItemTypeRuneMetadata.unabletradeitem.Distinct().Count());
    }

    [Fact]
    public void CallerVisibleReferenceRemainsReplaceableAndMutableWithListDuplicateSemantics()
    {
        var prior = LegacyNativeItemTypeRuneMetadata.unabletradeitem;
        try
        {
            var list = new List<uint>(Expected); LegacyNativeItemTypeRuneMetadata.unabletradeitem = list;
            list.Add(410000); list.Add(410000); Assert.True(LegacyNativeItemTypeRuneMetadata.unabletradeitem.Contains(410000));
            Assert.True(list.Remove(410000)); Assert.True(LegacyNativeItemTypeRuneMetadata.unabletradeitem.Contains(410000));
            Assert.True(list.Remove(410000)); Assert.False(LegacyNativeItemTypeRuneMetadata.unabletradeitem.Contains(410000));
            LegacyNativeItemTypeRuneMetadata.unabletradeitem = new();
            Assert.NotSame(list, LegacyNativeItemTypeRuneMetadata.unabletradeitem); Assert.Empty(LegacyNativeItemTypeRuneMetadata.unabletradeitem);
            Assert.Equal(Expected, list);
        }
        finally { LegacyNativeItemTypeRuneMetadata.unabletradeitem = prior; }
    }

    [Fact]
    public void NormalItemsAndNeighboursDoNotBecomeRestrictedByRangeInference()
    {
        foreach (uint id in new uint[] { 0, 410000, 749999, 750001, 722699, 722701, 3306884, 3306886, uint.MaxValue })
            Assert.False(LegacyNativeItemTypeRuneMetadata.unabletradeitem.Contains(id));
    }

    [Fact]
    public void SourceFieldCanRetainNullWithoutInventedLazyRestoration()
    {
        var prior = LegacyNativeItemTypeRuneMetadata.unabletradeitem;
        try
        {
            LegacyNativeItemTypeRuneMetadata.unabletradeitem = null!; Assert.Null(LegacyNativeItemTypeRuneMetadata.unabletradeitem);
            Assert.Throws<NullReferenceException>(() => LegacyNativeItemTypeRuneMetadata.unabletradeitem.Contains(410000));
        }
        finally { LegacyNativeItemTypeRuneMetadata.unabletradeitem = prior; }
    }
}
