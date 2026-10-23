using System.Collections.Concurrent;
using System.Reflection;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyNativeRuneEquipStorageTests
{
    [Fact]
    public void RuneConstructorStoresNullableOwnerThenCreatesDistinctEmptyStorageCaches()
    {
        var first = new LegacyNativeRuneItemStorage(null);
        var second = new LegacyNativeRuneItemStorage(null);
        Assert.Null(first.Owner);
        Assert.Empty(first.Objects);
        Assert.NotSame(first.Objects, second.Objects);
        Assert.False(first.ContainsUID(0));
    }

    [Fact]
    public void RuneConstructionDoesNotAssignItselfToPhysicalClient()
    {
        var client = new LegacyNativeGameClientRuntime();
        var rune = new LegacyNativeRuneItemStorage(client);
        Assert.Same(client, rune.Owner);
        Assert.Null(client.Rune);
        Assert.Null(client.Equipment);
    }

    [Fact]
    public void RuneAddCachesTheSameObjectAndDuplicateKeepsOldCache()
    {
        var rune = new LegacyNativeRuneItemStorage(null);
        var item = Item(7, 101);
        Assert.True(rune.Add(item));
        var cache = rune.Objects;
        Assert.Same(item, Assert.Single(cache));
        Assert.False(rune.Add(Item(7, 211)));
        Assert.Same(cache, rune.Objects);
        Assert.Same(item, Assert.Single(rune.Objects));
    }

    [Fact]
    public void RuneNullItemFaultLeavesExistingDictionaryAndCacheUntouched()
    {
        var rune = new LegacyNativeRuneItemStorage(null);
        var item = Item(7, 212);
        Assert.True(rune.Add(item));
        var cache = rune.Objects;
        Assert.Throws<NullReferenceException>(() => rune.Add(null!));
        Assert.Same(cache, rune.Objects);
        Assert.Same(item, Assert.Single(cache));
        Assert.True(rune.ContainsUID(7));
    }

    [Fact]
    public void RuneCapacityCountsOnlyCachedPosition211AndBlocksAnyNewPositionAtOneHundred()
    {
        var rune = new LegacyNativeRuneItemStorage(null);
        for (uint uid = 1; uid <= 100; uid++) Assert.True(rune.Add(Item(uid, 212)));
        Assert.True(rune.Add(Item(101, 212)));
        foreach (var item in rune.Objects.Take(100)) item.Position = 211;
        var cache = rune.Objects;
        Assert.False(rune.Add(Item(102, 101)));
        Assert.Same(cache, rune.Objects);
        Assert.False(rune.ContainsUID(102));
        rune.Objects[0].Position = 212;
        Assert.True(rune.Add(Item(102, 101)));
    }

    [Fact]
    public void RuneExposedArrayMutationDoesNotRepairPrivateDictionaryUntilNextSuccessfulAdd()
    {
        var rune = new LegacyNativeRuneItemStorage(null);
        var original = Item(7, 101);
        Assert.True(rune.Add(original));
        var external = Item(9, 103);
        rune.Objects[0] = external;
        Assert.True(rune.ContainsUID(7));
        Assert.False(rune.TryGetItem(7u, out _));
        Assert.True(rune.TryGetItem(9u, out var observed));
        Assert.Same(external, observed);
        Assert.True(rune.Add(Item(11, 104)));
        Assert.Contains(original, rune.Objects);
        Assert.DoesNotContain(external, rune.Objects);
    }

    [Fact]
    public void RuneMutableUidLookupUsesCachedValueWhileContainsUsesOriginalDictionaryKey()
    {
        var rune = new LegacyNativeRuneItemStorage(null);
        var item = Item(7, 101);
        Assert.True(rune.Add(item));
        item.Uid = 99;
        Assert.True(rune.ContainsUID(7));
        Assert.False(rune.ContainsUID(99));
        Assert.False(rune.TryGetItem(7u, out _));
        Assert.True(rune.TryGetItem(99u, out var observed));
        Assert.Same(item, observed);
    }

    [Fact]
    public void RunePositionAndItemIdOverloadsKeepTheirDifferentNativePredicates()
    {
        var rune = new LegacyNativeRuneItemStorage(null);
        var bag = Item(7, 211);
        var worn = Item(8, 212);
        Assert.True(rune.Add(bag));
        Assert.True(rune.Add(worn));
        Assert.Same(bag, rune.TryGetItem((byte)211));
        Assert.Same(worn, rune.TryGetItem(181305u));
        Assert.Null(rune.TryGetItem((byte)103));
    }

    [Fact]
    public void RuneUidLookupSkipsNullCacheEntryButPositionQueryRetainsNativeNullFault()
    {
        var rune = new LegacyNativeRuneItemStorage(null);
        Assert.True(rune.Add(Item(7, 101)));
        rune.Objects[0] = null!;
        Assert.False(rune.TryGetItem(7u, out var observed));
        Assert.Null(observed);
        Assert.Throws<NullReferenceException>(() => rune.TryGetItem((byte)101));
    }

    [Fact]
    public void EquipConstructorStoresExactNullableOwnerAndCreatesDistinctSameItemDictionaries()
    {
        var client = new LegacyNativeGameClientRuntime();
        var first = new LegacyNativeEquipItemStorage(client);
        var second = new LegacyNativeEquipItemStorage(null);
        Assert.Same(client, ReadOwner(first));
        Assert.Null(ReadOwner(second));
        Assert.NotSame(first.ClientItems, second.ClientItems);
        Assert.NotSame(first.CurentEquip, second.CurentEquip);
        Assert.Empty(first.ClientItems);
        Assert.Empty(first.CurentEquip);
        Assert.Null(client.Equipment);
    }

    [Fact]
    public void EquipDeclarationDefaultsKeepCreateSpawnTrueAndAllOtherPrimitiveFieldsZero()
    {
        var equipment = new LegacyNativeEquipItemStorage(null);
        Assert.True(equipment.CreateSpawn);
        Assert.Null(equipment.Name);
        var fields = typeof(LegacyNativeEquipItemStorage).GetFields(BindingFlags.Instance | BindingFlags.Public);
        foreach (var field in fields)
        {
            if (field.Name == nameof(equipment.CreateSpawn)) continue;
            if (field.FieldType == typeof(bool)) Assert.False((bool)field.GetValue(equipment)!);
            else if (field.FieldType.IsPrimitive || field.FieldType.IsEnum)
                Assert.Equal(0L, Convert.ToInt64(field.GetValue(equipment)));
        }
        Assert.Empty(equipment.AllItems);
    }

    [Fact]
    public void EquipRawStorageKeepsDictionaryKeyDistinctFromSameItemsCurrentUid()
    {
        var equipment = new LegacyNativeEquipItemStorage(null);
        var item = Item(7, 4);
        Assert.True(equipment.ClientItems.TryAdd(99, item));
        Assert.Same(item, equipment.GetItem(99));
        Assert.Null(equipment.GetItem(7));
        Assert.Same(item, Assert.Single(equipment.AllItems));
        item.Uid = 88;
        Assert.Same(item, equipment.GetItem(99));
        Assert.Equal(88u, equipment.GetItem(99)!.Uid);
        Assert.Null(equipment.GetItem(88));
    }

    [Fact]
    public void EquipDuplicateTryAddDoesNotReplaceOrNormalizeCandidate()
    {
        var equipment = new LegacyNativeEquipItemStorage(null);
        var original = Item(7, 4);
        var candidate = Item(8, 54);
        Assert.True(equipment.ClientItems.TryAdd(99, original));
        Assert.False(equipment.ClientItems.TryAdd(99, candidate));
        Assert.Same(original, equipment.GetItem(99));
        Assert.Equal(8u, candidate.Uid);
        Assert.Equal((ushort)54, candidate.Position);
    }

    [Fact]
    public void EquipReplaceableDictionaryAndCurrentArrayAreNotSilentlyRepaired()
    {
        var equipment = new LegacyNativeEquipItemStorage(null);
        var item = Item(7, 4);
        equipment.ClientItems.TryAdd(99, item);
        var old = equipment.ClientItems;
        equipment.ClientItems = new ConcurrentDictionary<uint, LegacyNativeGameItemRuntime>();
        equipment.CurentEquip = [item, item];
        Assert.Null(equipment.GetItem(99));
        Assert.Same(item, old[99]);
        Assert.Same(item, equipment.CurentEquip[0]);
        Assert.Same(item, equipment.CurentEquip[1]);
        equipment.ClientItems = null!;
        Assert.Throws<NullReferenceException>(() => equipment.GetItem(99));
    }


    [Fact]
    public void RuneCountCountsEveryNonNullCacheEntryAndRetainsByteWrap()
    {
        var rune = new LegacyNativeRuneItemStorage(null);
        for (uint uid = 1; uid <= 256; uid++) Assert.True(rune.Add(Item(uid, 212)));
        Assert.Equal(256, rune.Objects.Length);
        Assert.Equal((byte)0, rune.Count);
        rune.Objects[0] = null!;
        Assert.Equal((byte)255, rune.Count);
    }

    [Fact]
    public void RuneFree211RetainsFallbackPositionQueryEvenAfterCountReachesOneHundred()
    {
        var rune = new LegacyNativeRuneItemStorage(null);
        for (uint uid = 1; uid <= 100; uid++) Assert.True(rune.Add(Item(uid, 212)));
        Assert.Equal((byte)100, rune.Count);
        Assert.True(rune.Free(211)); // Native final OR branch: no position211 item exists.
        rune.Objects[0].Position = 211;
        Assert.False(rune.Free(211));
        Assert.True(rune.Free(212));
    }

    [Fact]
    public void RuneFree212RetainsItsSpecialRuleWhileUnknownCacheSlotsFailClosed()
    {
        var rune = new LegacyNativeRuneItemStorage(null);
        Assert.True(rune.Add(Item(7, 101)));
        rune.Objects[0] = null!;
        Assert.True(rune.Free(212));
        Assert.False(rune.Free(211)); // Null cache data cannot grant a free slot.
        Assert.False(rune.Free(103));
    }

    private static LegacyNativeGameItemRuntime Item(uint uid, ushort position) =>
        new() { Uid = uid, ITEM_ID = 181305u, Position = position };

    private static LegacyNativeGameClientRuntime? ReadOwner(LegacyNativeEquipItemStorage equipment) =>
        (LegacyNativeGameClientRuntime?)typeof(LegacyNativeEquipItemStorage)
            .GetField("Owner", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(equipment);
}
