using System.Reflection;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

// Four original DBItem caches plus native Refinary metadata. No cache-populating loader is invoked.
[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyNativeItemTypeCacheOwnerTests
{
    [Fact]
    public void DeclaredCachesHaveExactMutableStaticDictionaryTypesAndNativeOrder()
    {
        var fields = typeof(LegacyNativeItemTypeRuneMetadata)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .OrderBy(field => field.MetadataToken).ToArray();
        Assert.Equal(new[] { "PurificationItems", "Accessorys", "Refinary", "Garments", "SteedMounts" },
            fields.Select(field => field.Name));
        Assert.Equal(typeof(Dictionary<ushort, Dictionary<uint, LegacyNativeItemRuneMetadata>>), fields[0].FieldType);
        Assert.Equal(typeof(Dictionary<uint, LegacyNativeItemRuneMetadata>), fields[1].FieldType);
        Assert.Equal(typeof(Dictionary<uint, Dictionary<uint, LegacyNativeRifinery.Item>>), fields[2].FieldType);
        foreach (var field in fields.Skip(3))
            Assert.Equal(typeof(Dictionary<uint, LegacyNativeItemRuneMetadata>), field.FieldType);
        foreach (var field in fields)
        {
            Assert.True(field.IsPublic);
            Assert.True(field.IsStatic);
            Assert.False(field.IsInitOnly);
            Assert.False(field.IsLiteral);
        }
        Assert.True(typeof(LegacyNativeItemTypeRuneMetadata).IsSealed);
        Assert.Equal(typeof(Dictionary<uint, LegacyNativeItemRuneMetadata>),
            typeof(LegacyNativeItemTypeRuneMetadata).BaseType);
    }

    [Fact]
    public void UnpopulatedProgramOwnersStartNonNullEmptyAndIndependent()
    {
        // No production cache writer is wired; all mutating fixtures restore originals.
        Assert.NotNull(LegacyNativeItemTypeRuneMetadata.PurificationItems);
        Assert.NotNull(LegacyNativeItemTypeRuneMetadata.Accessorys);
        Assert.NotNull(LegacyNativeItemTypeRuneMetadata.Garments);
        Assert.NotNull(LegacyNativeItemTypeRuneMetadata.SteedMounts);
        Assert.Empty(LegacyNativeItemTypeRuneMetadata.PurificationItems);
        Assert.Empty(LegacyNativeItemTypeRuneMetadata.Accessorys);
        Assert.Empty(LegacyNativeItemTypeRuneMetadata.Garments);
        Assert.Empty(LegacyNativeItemTypeRuneMetadata.SteedMounts);
        Assert.NotSame(LegacyNativeItemTypeRuneMetadata.Accessorys, LegacyNativeItemTypeRuneMetadata.Garments);
        Assert.NotSame(LegacyNativeItemTypeRuneMetadata.Accessorys, LegacyNativeItemTypeRuneMetadata.SteedMounts);
        Assert.NotSame(LegacyNativeItemTypeRuneMetadata.Garments, LegacyNativeItemTypeRuneMetadata.SteedMounts);
    }

    [Fact]
    public void NewMainTableInstancesDoNotResetOrRecreateAnyCacheOwner()
    {
        WithEmptyCaches(() =>
        {
            var purification = LegacyNativeItemTypeRuneMetadata.PurificationItems;
            var accessory = LegacyNativeItemTypeRuneMetadata.Accessorys;
            var garments = LegacyNativeItemTypeRuneMetadata.Garments;
            var mounts = LegacyNativeItemTypeRuneMetadata.SteedMounts;
            var row = new LegacyNativeItemRuneMetadata { ID = 42 };
            var bucket = new Dictionary<uint, LegacyNativeItemRuneMetadata> { [42] = row };
            purification.Add(2, bucket);
            accessory.Add(42, row);
            garments.Add(42, row);
            mounts.Add(42, row);
            var first = new LegacyNativeItemTypeRuneMetadata();
            var second = new LegacyNativeItemTypeRuneMetadata();
            Assert.NotSame(first, second);
            Assert.Empty(first);
            Assert.Empty(second);
            Assert.Same(purification, LegacyNativeItemTypeRuneMetadata.PurificationItems);
            Assert.Same(accessory, LegacyNativeItemTypeRuneMetadata.Accessorys);
            Assert.Same(garments, LegacyNativeItemTypeRuneMetadata.Garments);
            Assert.Same(mounts, LegacyNativeItemTypeRuneMetadata.SteedMounts);
            Assert.Same(row, purification[2][42]);
            Assert.Same(row, accessory[42]);
            Assert.Same(row, garments[42]);
            Assert.Same(row, mounts[42]);
        });
    }

    [Fact]
    public void ReplacingOwnersPreservesEveryEscapedOldDictionaryAlias()
    {
        WithEmptyCaches(() =>
        {
            var oldPurification = LegacyNativeItemTypeRuneMetadata.PurificationItems;
            var oldAccessory = LegacyNativeItemTypeRuneMetadata.Accessorys;
            var oldGarments = LegacyNativeItemTypeRuneMetadata.Garments;
            var oldMounts = LegacyNativeItemTypeRuneMetadata.SteedMounts;
            var row = new LegacyNativeItemRuneMetadata { RuneSpellID = 17 };
            var bucket = new Dictionary<uint, LegacyNativeItemRuneMetadata> { [7] = row };
            oldPurification[3] = bucket;
            oldAccessory[7] = row;
            oldGarments[7] = row;
            oldMounts[7] = row;
            LegacyNativeItemTypeRuneMetadata.PurificationItems = new();
            LegacyNativeItemTypeRuneMetadata.Accessorys = new();
            LegacyNativeItemTypeRuneMetadata.Garments = new();
            LegacyNativeItemTypeRuneMetadata.SteedMounts = new();
            Assert.Same(bucket, oldPurification[3]);
            Assert.Same(row, oldAccessory[7]);
            Assert.Same(row, oldGarments[7]);
            Assert.Same(row, oldMounts[7]);
            Assert.Empty(LegacyNativeItemTypeRuneMetadata.PurificationItems);
            Assert.Empty(LegacyNativeItemTypeRuneMetadata.Accessorys);
            Assert.Empty(LegacyNativeItemTypeRuneMetadata.Garments);
            Assert.Empty(LegacyNativeItemTypeRuneMetadata.SteedMounts);
            row.RuneSpellID = 99;
            Assert.Equal(99U, oldPurification[3][7].RuneSpellID);
            Assert.Equal(99U, oldAccessory[7].RuneSpellID);
        });
    }

    [Fact]
    public void NullOwnersRemainNullAcrossTableConstructionAndPureIsRuneCalls()
    {
        WithEmptyCaches(() =>
        {
            LegacyNativeItemTypeRuneMetadata.PurificationItems = null!;
            LegacyNativeItemTypeRuneMetadata.Accessorys = null!;
            LegacyNativeItemTypeRuneMetadata.Garments = null!;
            LegacyNativeItemTypeRuneMetadata.SteedMounts = null!;
            Assert.Empty(new LegacyNativeItemTypeRuneMetadata());
            Assert.True(LegacyNativeItemTypeRuneMetadata.IsRune(4010001));
            Assert.False(LegacyNativeItemTypeRuneMetadata.IsRune(1));
            Assert.Null(LegacyNativeItemTypeRuneMetadata.PurificationItems);
            Assert.Null(LegacyNativeItemTypeRuneMetadata.Accessorys);
            Assert.Null(LegacyNativeItemTypeRuneMetadata.Garments);
            Assert.Null(LegacyNativeItemTypeRuneMetadata.SteedMounts);
        });
    }

    [Fact]
    public void EmptyPurificationOwnerDoesNotManufactureLevelBuckets()
    {
        WithEmptyCaches(() =>
        {
            var outer = LegacyNativeItemTypeRuneMetadata.PurificationItems;
            Assert.False(outer.ContainsKey(1));
            Assert.Throws<KeyNotFoundException>(() => outer[1]);
            Assert.Empty(outer);
            outer.Add(0, new Dictionary<uint, LegacyNativeItemRuneMetadata>());
            Assert.Single(outer);
            Assert.Empty(outer[0]);
            Assert.False(outer.ContainsKey(1));
        });
    }

    [Fact]
    public void PurificationBucketsKeepExactReplaceableMutableDictionaryReferences()
    {
        WithEmptyCaches(() =>
        {
            var row = new LegacyNativeItemRuneMetadata();
            var original = new Dictionary<uint, LegacyNativeItemRuneMetadata> { [uint.MaxValue] = row };
            var replacement = new Dictionary<uint, LegacyNativeItemRuneMetadata>();
            var outer = LegacyNativeItemTypeRuneMetadata.PurificationItems;
            outer.Add(ushort.MaxValue, original);
            Assert.Same(original, outer[ushort.MaxValue]);
            outer[ushort.MaxValue] = replacement;
            Assert.Same(replacement, outer[ushort.MaxValue]);
            Assert.Same(row, original[uint.MaxValue]);
            Assert.Empty(replacement);
            original[uint.MaxValue].Class = uint.MaxValue;
            Assert.Equal(uint.MaxValue, row.Class);
        });
    }

    [Fact]
    public void OneConcreteRowCanBeSharedByEveryCacheAndTheMainTable()
    {
        WithEmptyCaches(() =>
        {
            var row = new LegacyNativeItemRuneMetadata { ID = 42, RuneSpellID = 4010001 };
            var main = new LegacyNativeItemTypeRuneMetadata { [42] = row };
            LegacyNativeItemTypeRuneMetadata.PurificationItems[3] =
                new Dictionary<uint, LegacyNativeItemRuneMetadata> { [42] = row };
            LegacyNativeItemTypeRuneMetadata.Accessorys[42] = row;
            LegacyNativeItemTypeRuneMetadata.Garments[42] = row;
            LegacyNativeItemTypeRuneMetadata.SteedMounts[42] = row;
            Assert.Same(row, main[42]);
            Assert.Same(row, LegacyNativeItemTypeRuneMetadata.PurificationItems[3][42]);
            Assert.Same(row, LegacyNativeItemTypeRuneMetadata.Accessorys[42]);
            Assert.Same(row, LegacyNativeItemTypeRuneMetadata.Garments[42]);
            Assert.Same(row, LegacyNativeItemTypeRuneMetadata.SteedMounts[42]);
        });
    }

    [Fact]
    public void SharedExpandedRowAndGenuinePlusElementMutationsStayLiveInAllCaches()
    {
        WithEmptyCaches(() =>
        {
            var element = new LegacyNativeItemTypePlus { ID = 42, Plus = 2, Agility = 7 };
            var slots = new LegacyNativeItemTypePlus[13];
            slots[2] = element;
            var row = new LegacyNativeItemRuneMetadata { Plus = slots };
            LegacyNativeItemTypeRuneMetadata.PurificationItems[4] =
                new Dictionary<uint, LegacyNativeItemRuneMetadata> { [42] = row };
            LegacyNativeItemTypeRuneMetadata.Accessorys[42] = row;
            LegacyNativeItemTypeRuneMetadata.Garments[42] = row;
            LegacyNativeItemTypeRuneMetadata.SteedMounts[42] = row;
            LegacyNativeItemTypeRuneMetadata.Garments[42].Class = uint.MaxValue;
            LegacyNativeItemTypeRuneMetadata.SteedMounts[42].RuneSpellID = 17;
            element.Agility = ushort.MaxValue;
            foreach (var alias in new[] { LegacyNativeItemTypeRuneMetadata.PurificationItems[4][42],
                         LegacyNativeItemTypeRuneMetadata.Accessorys[42],
                         LegacyNativeItemTypeRuneMetadata.Garments[42],
                         LegacyNativeItemTypeRuneMetadata.SteedMounts[42] })
            {
                Assert.Same(row, alias);
                Assert.Same(slots, alias.Plus);
                Assert.Same(element, alias.Plus![2]);
                Assert.Equal(ushort.MaxValue, alias.Plus[2].Vigor);
                Assert.Equal(uint.MaxValue, alias.Class);
                Assert.Equal(17U, alias.RuneSpellID);
                Assert.Null(alias.Plus[0]);
            }
        });
    }

    [Fact]
    public void MainTableAndCacheAdmissionsDoNotAutomaticallyPopulateEachOther()
    {
        WithEmptyCaches(() =>
        {
            var main = new LegacyNativeItemTypeRuneMetadata();
            var first = new LegacyNativeItemRuneMetadata();
            var second = new LegacyNativeItemRuneMetadata();
            main.Add(1, first);
            Assert.Empty(LegacyNativeItemTypeRuneMetadata.PurificationItems);
            Assert.Empty(LegacyNativeItemTypeRuneMetadata.Accessorys);
            Assert.Empty(LegacyNativeItemTypeRuneMetadata.Garments);
            Assert.Empty(LegacyNativeItemTypeRuneMetadata.SteedMounts);
            LegacyNativeItemTypeRuneMetadata.Accessorys.Add(2, second);
            Assert.False(main.ContainsKey(2));
            Assert.False(LegacyNativeItemTypeRuneMetadata.Garments.ContainsKey(2));
            Assert.False(LegacyNativeItemTypeRuneMetadata.SteedMounts.ContainsKey(2));
            Assert.Empty(LegacyNativeItemTypeRuneMetadata.PurificationItems);
            Assert.Same(first, main[1]);
            Assert.Same(second, LegacyNativeItemTypeRuneMetadata.Accessorys[2]);
        });
    }

    [Fact]
    public void CacheKeysRemainIndependentOfTheSameMutableRowsItemId()
    {
        WithEmptyCaches(() =>
        {
            var row = new LegacyNativeItemRuneMetadata { ID = 99 };
            LegacyNativeItemTypeRuneMetadata.Accessorys[uint.MaxValue] = row;
            LegacyNativeItemTypeRuneMetadata.Garments[0] = row;
            LegacyNativeItemTypeRuneMetadata.SteedMounts[1] = row;
            LegacyNativeItemTypeRuneMetadata.PurificationItems[0] =
                new Dictionary<uint, LegacyNativeItemRuneMetadata> { [2] = row };
            row.ID = 7;
            Assert.Same(row, LegacyNativeItemTypeRuneMetadata.Accessorys[uint.MaxValue]);
            Assert.Same(row, LegacyNativeItemTypeRuneMetadata.Garments[0]);
            Assert.Same(row, LegacyNativeItemTypeRuneMetadata.SteedMounts[1]);
            Assert.Same(row, LegacyNativeItemTypeRuneMetadata.PurificationItems[0][2]);
            Assert.False(LegacyNativeItemTypeRuneMetadata.Accessorys.ContainsKey(7));
            Assert.False(LegacyNativeItemTypeRuneMetadata.Garments.ContainsKey(7));
        });
    }

    [Fact]
    public void DuplicateAddsThrowAndPreserveOriginalRowsAndSiblingCaches()
    {
        WithEmptyCaches(() =>
        {
            var original = new LegacyNativeItemRuneMetadata();
            var replacement = new LegacyNativeItemRuneMetadata();
            var bucket = new Dictionary<uint, LegacyNativeItemRuneMetadata> { [1] = original };
            LegacyNativeItemTypeRuneMetadata.PurificationItems.Add(2, bucket);
            LegacyNativeItemTypeRuneMetadata.Accessorys.Add(1, original);
            LegacyNativeItemTypeRuneMetadata.Garments.Add(1, original);
            LegacyNativeItemTypeRuneMetadata.SteedMounts.Add(1, original);
            Assert.Throws<ArgumentException>(() => LegacyNativeItemTypeRuneMetadata.PurificationItems.Add(2, new()));
            Assert.Throws<ArgumentException>(() => bucket.Add(1, replacement));
            Assert.Throws<ArgumentException>(() => LegacyNativeItemTypeRuneMetadata.Accessorys.Add(1, replacement));
            Assert.Throws<ArgumentException>(() => LegacyNativeItemTypeRuneMetadata.Garments.Add(1, replacement));
            Assert.Throws<ArgumentException>(() => LegacyNativeItemTypeRuneMetadata.SteedMounts.Add(1, replacement));
            Assert.Same(bucket, LegacyNativeItemTypeRuneMetadata.PurificationItems[2]);
            Assert.Same(original, bucket[1]);
            Assert.Same(original, LegacyNativeItemTypeRuneMetadata.Accessorys[1]);
            Assert.Same(original, LegacyNativeItemTypeRuneMetadata.Garments[1]);
            Assert.Same(original, LegacyNativeItemTypeRuneMetadata.SteedMounts[1]);
        });
    }

    [Fact]
    public void RemovingAndClearingCachesDoesNotDestroyEscapedRowsOrSiblingEntries()
    {
        WithEmptyCaches(() =>
        {
            var row = new LegacyNativeItemRuneMetadata { Name = "retained" };
            var bucket = new Dictionary<uint, LegacyNativeItemRuneMetadata> { [1] = row };
            LegacyNativeItemTypeRuneMetadata.PurificationItems[2] = bucket;
            LegacyNativeItemTypeRuneMetadata.Accessorys[1] = row;
            LegacyNativeItemTypeRuneMetadata.Garments[1] = row;
            LegacyNativeItemTypeRuneMetadata.SteedMounts[1] = row;
            Assert.True(LegacyNativeItemTypeRuneMetadata.Accessorys.Remove(1));
            LegacyNativeItemTypeRuneMetadata.PurificationItems.Clear();
            Assert.Same(row, bucket[1]);
            Assert.Same(row, LegacyNativeItemTypeRuneMetadata.Garments[1]);
            Assert.Same(row, LegacyNativeItemTypeRuneMetadata.SteedMounts[1]);
            Assert.Equal("retained", row.Name);
            Assert.Empty(LegacyNativeItemTypeRuneMetadata.Accessorys);
            Assert.Empty(LegacyNativeItemTypeRuneMetadata.PurificationItems);
        });
    }

    [Fact]
    public void DistinctPurificationLevelKeysCanShareOneConcreteBucket()
    {
        WithEmptyCaches(() =>
        {
            var bucket = new Dictionary<uint, LegacyNativeItemRuneMetadata>();
            var outer = LegacyNativeItemTypeRuneMetadata.PurificationItems;
            outer.Add(0, bucket);
            outer.Add(ushort.MaxValue, bucket);
            var row = new LegacyNativeItemRuneMetadata { Class = 19000 };
            outer[0][uint.MaxValue] = row;
            Assert.Same(bucket, outer[ushort.MaxValue]);
            Assert.Same(row, outer[ushort.MaxValue][uint.MaxValue]);
            Assert.Equal(19000U, outer[ushort.MaxValue][uint.MaxValue].Class);
        });
    }

    [Fact]
    public void NullRowAndNullNestedBucketValuesAreNotNormalizedOrRejected()
    {
        WithEmptyCaches(() =>
        {
            LegacyNativeItemTypeRuneMetadata.PurificationItems[1] = null!;
            LegacyNativeItemTypeRuneMetadata.Accessorys[1] = null!;
            LegacyNativeItemTypeRuneMetadata.Garments[1] = null!;
            LegacyNativeItemTypeRuneMetadata.SteedMounts[1] = null!;
            Assert.Null(LegacyNativeItemTypeRuneMetadata.PurificationItems[1]);
            Assert.Null(LegacyNativeItemTypeRuneMetadata.Accessorys[1]);
            Assert.Null(LegacyNativeItemTypeRuneMetadata.Garments[1]);
            Assert.Null(LegacyNativeItemTypeRuneMetadata.SteedMounts[1]);
            Assert.Throws<NullReferenceException>(() => LegacyNativeItemTypeRuneMetadata.PurificationItems[1].Add(2, new()));
            Assert.Single(LegacyNativeItemTypeRuneMetadata.PurificationItems);
        });
    }

    [Fact]
    public void NullOwnerDereferenceFaultsWithoutRepairingItOrMutatingSiblingOwners()
    {
        WithEmptyCaches(() =>
        {
            var row = new LegacyNativeItemRuneMetadata();
            var garments = LegacyNativeItemTypeRuneMetadata.Garments;
            var mounts = LegacyNativeItemTypeRuneMetadata.SteedMounts;
            LegacyNativeItemTypeRuneMetadata.Accessorys = null!;
            Assert.Throws<NullReferenceException>(() => LegacyNativeItemTypeRuneMetadata.Accessorys.Add(1, row));
            Assert.Null(LegacyNativeItemTypeRuneMetadata.Accessorys);
            Assert.Same(garments, LegacyNativeItemTypeRuneMetadata.Garments);
            Assert.Same(mounts, LegacyNativeItemTypeRuneMetadata.SteedMounts);
            Assert.Empty(garments);
            Assert.Empty(mounts);
            Assert.Empty(LegacyNativeItemTypeRuneMetadata.PurificationItems);
        });
    }

    private static void WithEmptyCaches(Action body)
    {
        var purification = LegacyNativeItemTypeRuneMetadata.PurificationItems;
        var accessory = LegacyNativeItemTypeRuneMetadata.Accessorys;
        var garments = LegacyNativeItemTypeRuneMetadata.Garments;
        var mounts = LegacyNativeItemTypeRuneMetadata.SteedMounts;
        try
        {
            LegacyNativeItemTypeRuneMetadata.PurificationItems = new();
            LegacyNativeItemTypeRuneMetadata.Accessorys = new();
            LegacyNativeItemTypeRuneMetadata.Garments = new();
            LegacyNativeItemTypeRuneMetadata.SteedMounts = new();
            body();
        }
        finally
        {
            LegacyNativeItemTypeRuneMetadata.PurificationItems = purification;
            LegacyNativeItemTypeRuneMetadata.Accessorys = accessory;
            LegacyNativeItemTypeRuneMetadata.Garments = garments;
            LegacyNativeItemTypeRuneMetadata.SteedMounts = mounts;
        }
    }
}
