using System.Collections.Concurrent;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyNativeWardrobeRuntimeTests
{
    [Fact]
    public void ConstructorStoresTheActualOwnerAndCreatesTwoIndependentBuckets()
    {
        var client = new LegacyNativeGameClientRuntime();
        var player = client.Player;
        var wardrobe = new LegacyNativeWardrobeRuntime(client);
        Assert.Same(client, wardrobe.Owner);
        Assert.Same(player, client.Player);
        Assert.Equal(2, wardrobe.Items.Length);
        Assert.NotSame(wardrobe.Items[0], wardrobe.Items[1]);
        Assert.Empty(wardrobe.Items[0]);
        Assert.Empty(wardrobe.Items[1]);
    }

    [Fact]
    public void NullOwnerIsStoredWithoutCreatingOrValidatingAClient()
    {
        var wardrobe = new LegacyNativeWardrobeRuntime(null);
        Assert.Null(wardrobe.Owner);
        Assert.False(wardrobe.HaveItems());
    }

    [Fact]
    public void SeparateWardrobesHaveSeparateStorageEvenForTheSameOwner()
    {
        var client = new LegacyNativeGameClientRuntime();
        var first = new LegacyNativeWardrobeRuntime(client);
        var second = new LegacyNativeWardrobeRuntime(client);
        Assert.NotSame(first.Items, second.Items);
        Assert.NotSame(first.Items[0], second.Items[0]);
        Assert.NotSame(first.Items[1], second.Items[1]);
    }

    [Fact]
    public void NativeCategoryIsByteGarmentZeroMountOneAndCountTwo()
    {
        var wardrobe = new LegacyNativeWardrobeRuntime(null);
        Assert.Equal((byte)0, (byte)LegacyNativeWardrobeRuntime.ItemsType.Garment);
        Assert.Equal((byte)1, (byte)LegacyNativeWardrobeRuntime.ItemsType.Mount);
        Assert.Equal((byte)2, (byte)LegacyNativeWardrobeRuntime.ItemsType.Count);
        Assert.Equal(LegacyNativeWardrobeRuntime.ItemsType.Garment, wardrobe.GetItemType(181305u));
        Assert.Equal(LegacyNativeWardrobeRuntime.ItemsType.Mount, wardrobe.GetItemType(200000u));
        Assert.Equal(LegacyNativeWardrobeRuntime.ItemsType.Count, wardrobe.GetItemType(600000u));
    }

    [Fact]
    public void CompleteItemPositionRetainsNativeRangesExceptionsAndBranchPriority()
    {
        (uint Id, ushort Expected)[] cases =
        [
            (203003u, 18), (203009u, 18), (203002u, 0),
            (200000u, 17), (200726u, 17), (200727u, 0), (200731u, 17), (200772u, 17),
            (205003u, 17), (205902u, 17), (360323u, 15), (360401u, 15), (350001u, 15),
            (360291u, 15), (360292u, 0), (360297u, 15), (370001u, 16), (380046u, 16), (380047u, 0),
            (199185u, 9), (199255u, 9), (199325u, 9), (196885u, 9), (134155u, 9),
            (111003u, 1), (120001u, 2), (130003u, 3), (150000u, 6), (160013u, 8),
            (181305u, 9), (196785u, 9), (196786u, 0), (201003u, 10), (202003u, 11), (300000u, 12),
            (410003u, 4), (613429u, 4), (900000u, 5), (1050000u, 5), (2100025u, 7), (2168705u, 7),
            (204003u, 19), (4010001u, 101), (4020001u, 102), (4030001u, 103), (4100001u, 13),
            (616u, 4), (680000u, 5), (681000u, 4), (620003u, 4), (624000u, 4), (626003u, 4),
            (670000u, 4), (619000u, 5), (0u, 0), (uint.MaxValue, 0)
        ];
        foreach (var value in cases)
            Assert.Equal(value.Expected, LegacyNativeItemPosition.ItemPosition(value.Id));
    }

    [Fact]
    public void GarmentAddRetainsActualItemAndSetsOnlyWarehouseIdBeforeInsert()
    {
        var wardrobe = new LegacyNativeWardrobeRuntime(null);
        var item = new LegacyNativeGameItemRuntime { Uid = 11u, ITEM_ID = 181305u, WH_ID = 7u, Position = 9 };
        var mode = item.Mode;
        Assert.True(wardrobe.AddItem(item));
        Assert.Same(item, wardrobe.Items[0][11u]);
        Assert.Equal(100u, item.WH_ID);
        Assert.Equal((ushort)9, item.Position);
        Assert.Equal(mode, item.Mode);
    }

    [Fact]
    public void MountAddUsesSecondBucketAndKeepsTheSameActualItem()
    {
        var wardrobe = new LegacyNativeWardrobeRuntime(null);
        var item = new LegacyNativeGameItemRuntime { Uid = 11u, ITEM_ID = 200000u, Position = 17 };
        Assert.True(wardrobe.AddItem(item));
        Assert.Same(item, wardrobe.Items[1][11u]);
        Assert.Empty(wardrobe.Items[0]);
        Assert.Equal(100u, item.WH_ID);
        Assert.Equal((ushort)17, item.Position);
    }

    [Fact]
    public void UnsupportedItemIsRejectedWithoutChangingWarehouseOrStorage()
    {
        var wardrobe = new LegacyNativeWardrobeRuntime(null);
        var item = new LegacyNativeGameItemRuntime { Uid = 11u, ITEM_ID = 600000u, WH_ID = 7u };
        Assert.False(wardrobe.AddItem(item));
        Assert.Equal(7u, item.WH_ID);
        Assert.False(wardrobe.HaveItems());
    }

    [Fact]
    public void DuplicateInSelectedBucketDoesNotReplaceOrMutateTheCandidate()
    {
        var wardrobe = new LegacyNativeWardrobeRuntime(null);
        var first = new LegacyNativeGameItemRuntime { Uid = 11u, ITEM_ID = 181305u };
        var candidate = new LegacyNativeGameItemRuntime { Uid = 11u, ITEM_ID = 181305u, WH_ID = 7u };
        Assert.True(wardrobe.AddItem(first));
        Assert.False(wardrobe.AddItem(candidate));
        Assert.Same(first, wardrobe.Items[0][11u]);
        Assert.Equal(7u, candidate.WH_ID);
    }

    [Fact]
    public void SameUidMayExistInBothBucketsWithoutGlobalIdentityDeduplication()
    {
        var wardrobe = new LegacyNativeWardrobeRuntime(null);
        var garment = new LegacyNativeGameItemRuntime { Uid = 11u, ITEM_ID = 181305u };
        var mount = new LegacyNativeGameItemRuntime { Uid = 11u, ITEM_ID = 200000u };
        Assert.True(wardrobe.AddItem(garment));
        Assert.True(wardrobe.AddItem(mount));
        Assert.Equal(2, wardrobe.GetCountItems());
        Assert.Same(garment, wardrobe.Items[0][11u]);
        Assert.Same(mount, wardrobe.Items[1][11u]);
    }

    [Fact]
    public void TryGetSearchesGarmentBeforeMountAndMissingReturnsNull()
    {
        var wardrobe = new LegacyNativeWardrobeRuntime(null);
        var garment = new LegacyNativeGameItemRuntime { Uid = 11u, ITEM_ID = 181305u };
        var mount = new LegacyNativeGameItemRuntime { Uid = 11u, ITEM_ID = 200000u };
        wardrobe.AddItem(garment);
        wardrobe.AddItem(mount);
        Assert.True(wardrobe.TryGetItem(11u, out var found));
        Assert.Same(garment, found);
        Assert.False(wardrobe.TryGetItem(12u, out var missing));
        Assert.Null(missing);
    }

    [Fact]
    public void ContainQueriesInspectActualValuesWhileTryGetUsesDictionaryKeys()
    {
        var wardrobe = new LegacyNativeWardrobeRuntime(null);
        var item = new LegacyNativeGameItemRuntime { Uid = 11u, ITEM_ID = 181305u };
        wardrobe.Items[0].TryAdd(777u, item);
        Assert.True(wardrobe.Contain(181305u));
        Assert.True(wardrobe.ContainUID(11u));
        Assert.False(wardrobe.ContainUID(777u));
        Assert.True(wardrobe.TryGetItem(777u, out var found));
        Assert.Same(item, found);
        Assert.False(wardrobe.TryGetItem(11u, out _));
    }

    [Fact]
    public void RemoveReturnsSameItemAndResetsWarehouseAndPositionOnlyAfterRemoval()
    {
        var wardrobe = new LegacyNativeWardrobeRuntime(null);
        var item = new LegacyNativeGameItemRuntime { Uid = 11u, ITEM_ID = 181305u, Position = 9 };
        wardrobe.AddItem(item);
        var mode = item.Mode;
        Assert.True(wardrobe.RemoveItem(11u, out var removed));
        Assert.Same(item, removed);
        Assert.Equal(0u, item.WH_ID);
        Assert.Equal((ushort)0, item.Position);
        Assert.Equal(mode, item.Mode);
        Assert.False(wardrobe.HaveItems());
    }

    [Fact]
    public void MissingRemoveReturnsNullAndDoesNotMutateAnUnrelatedItem()
    {
        var wardrobe = new LegacyNativeWardrobeRuntime(null);
        var item = new LegacyNativeGameItemRuntime { Uid = 11u, ITEM_ID = 181305u, Position = 9 };
        wardrobe.AddItem(item);
        Assert.False(wardrobe.RemoveItem(12u, out var missing));
        Assert.Null(missing);
        Assert.Equal(100u, item.WH_ID);
        Assert.Equal((ushort)9, item.Position);
    }

    [Fact]
    public void DuplicateUidRemovalStopsAtTheFirstBucketOnly()
    {
        var wardrobe = new LegacyNativeWardrobeRuntime(null);
        var garment = new LegacyNativeGameItemRuntime { Uid = 11u, ITEM_ID = 181305u, Position = 9 };
        var mount = new LegacyNativeGameItemRuntime { Uid = 11u, ITEM_ID = 200000u, Position = 17 };
        wardrobe.AddItem(garment);
        wardrobe.AddItem(mount);
        Assert.True(wardrobe.RemoveItem(11u, out var removed));
        Assert.Same(garment, removed);
        Assert.Equal(0u, garment.WH_ID);
        Assert.Equal(100u, mount.WH_ID);
        Assert.Equal((ushort)17, mount.Position);
        Assert.True(wardrobe.TryGetItem(11u, out var remaining));
        Assert.Same(mount, remaining);
    }

    [Fact]
    public void EnumerationKeepsBucketOrderActualReferencesAndDuplicateUids()
    {
        var wardrobe = new LegacyNativeWardrobeRuntime(null);
        var garment = new LegacyNativeGameItemRuntime { Uid = 11u, ITEM_ID = 181305u };
        var mount = new LegacyNativeGameItemRuntime { Uid = 11u, ITEM_ID = 200000u };
        wardrobe.AddItem(garment);
        wardrobe.AddItem(mount);
        var all = wardrobe.GetAllItems().ToArray();
        Assert.Equal(2, wardrobe.GetCountItems());
        Assert.Equal(2, all.Length);
        Assert.Same(garment, all[0]);
        Assert.Same(mount, all[1]); // One item per bucket: no within-dictionary ordering assumption.
    }

    [Fact]
    public void ReplaceableItemsArrayIsNotRepairedOrSecretlyExpanded()
    {
        var wardrobe = new LegacyNativeWardrobeRuntime(null);
        ConcurrentDictionary<uint, LegacyNativeGameItemRuntime>[] replacement =
            [new ConcurrentDictionary<uint, LegacyNativeGameItemRuntime>()];
        wardrobe.Items = replacement;
        Assert.Same(replacement, wardrobe.Items);
        Assert.Throws<IndexOutOfRangeException>(() => wardrobe.TryGetItem(11u, out _));
        wardrobe.Items = null!;
        Assert.Throws<NullReferenceException>(() => wardrobe.HaveItems());
        Assert.Null(wardrobe.Items);
    }

    [Fact]
    public void NullAddFaultsBeforeAnyBucketMutationInsteadOfInventingAnItem()
    {
        var wardrobe = new LegacyNativeWardrobeRuntime(null);
        Assert.Throws<NullReferenceException>(() => wardrobe.AddItem(null!));
        Assert.False(wardrobe.HaveItems());
    }

    [Fact]
    public void ZeroUidIsStoredWithoutInventoryCounterAdmissionOrUidWrapper()
    {
        var wardrobe = new LegacyNativeWardrobeRuntime(null);
        var item = new LegacyNativeGameItemRuntime { Uid = 0u, ITEM_ID = 181305u };
        Assert.True(wardrobe.AddItem(item));
        Assert.Same(item, wardrobe.Items[0][0u]);
        Assert.Equal(0u, item.Uid);
    }

    [Fact]
    public void OwnerFieldRetainsMutableActualClientReferences()
    {
        var first = new LegacyNativeGameClientRuntime();
        var second = new LegacyNativeGameClientRuntime();
        var wardrobe = new LegacyNativeWardrobeRuntime(first);
        wardrobe.Owner = second;
        Assert.Same(second, wardrobe.Owner);
        wardrobe.Owner = null;
        Assert.Null(wardrobe.Owner);
    }
}
