using System.Collections.Concurrent;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

// Actual native item/client methods only. Owners are explicitly supplied fixtures,
// not new GameClient birth wiring or proof of SaveClientItems/SQL/client integration.
[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyNativePerfectionEnumerationTests
{
    [Fact]
    public void IsEquipUsesNativeItemIdClassificationRatherThanStoredPosition()
    {
        var item = new LegacyNativeGameItemRuntime();
        (uint Id, bool Expected)[] cases =
        [
            (0, false), (1000000, false), (uint.MaxValue, false),
            (111003, true), (410003, true), (350001, true),
            (300000, true), (203003, true), (200000, true),
        ];
        foreach (var testCase in cases)
        {
            item.ITEM_ID = testCase.Id;
            item.Position = testCase.Expected ? (ushort)0 : (ushort)9;
            Assert.Equal(testCase.Expected, item.IsEquip);
            item.Position = ushort.MaxValue;
            Assert.Equal(testCase.Expected, item.IsEquip);
        }
    }

    [Fact]
    public void EveryPredicateCombinationIsAppliedToEveryParticipatingRoot()
    {
        foreach (uint progress in new uint[] { 0, uint.MaxValue })
        foreach (uint level in new uint[] { 0, uint.MaxValue })
        foreach (uint ownerUid in new uint[] { 0, uint.MaxValue })
        foreach (bool isEquip in new[] { false, true })
        {
            var client = Owners();
            var inventory = Item(1);
            var equipment = Item(2);
            var warehouse = Item(3);
            foreach (var item in new[] { inventory, equipment, warehouse })
            {
                item.PerfectionProgress = progress;
                item.PerfectionLevel = level;
                item.OwnerUID = ownerUid;
                item.ITEM_ID = isEquip ? 181305u : 1000000u;
            }
            client.Inventory!.ClientItems[1] = inventory;
            client.Equipment!.ClientItems[2] = equipment;
            client.Warehouse!.ClientItems[44] = Bank(3, warehouse);
            bool expected = (progress != 0 || level != 0) && ownerUid != 0 && isEquip;

            var actual = client.AllPerfectionItems().ToArray();

            Assert.Equal(expected ? 3 : 0, actual.Length);
            Assert.Equal(expected ? 3 : 0, client.GetPerfectionItemsCount());
            if (expected)
            {
                Assert.Same(inventory, actual[0]);
                Assert.Same(equipment, actual[1]);
                Assert.Same(warehouse, actual[2]);
            }
        }
    }

    [Fact]
    public void NativeRootsAreStrictlyInventoryThenEquipmentThenWarehouseWithoutUidSorting()
    {
        var client = Owners();
        var inventory = Item(90);
        var equipment = Item(10);
        var warehouse = Item(30);
        client.Inventory!.ClientItems[991] = inventory;
        client.Equipment!.ClientItems[992] = equipment;
        client.Warehouse!.ClientItems[44] = Bank(993, warehouse);

        var actual = client.AllPerfectionItems().ToArray();

        Assert.Equal(3, actual.Length);
        Assert.Same(inventory, actual[0]);
        Assert.Same(equipment, actual[1]);
        Assert.Same(warehouse, actual[2]);
        Assert.Equal(3, client.GetPerfectionItemsCount());
        Assert.Equal(90u, inventory.Uid);
        Assert.Equal(10u, equipment.Uid);
        Assert.Equal(30u, warehouse.Uid);
    }

    [Fact]
    public void RepeatedReferencesAndEqualUidsAreNotDeduplicatedAcrossOrWithinRoots()
    {
        var client = Owners();
        var shared = Item(7);
        var equalUid = Item(7);
        client.Inventory!.ClientItems[1] = shared;
        client.Equipment!.ClientItems[2] = shared;
        client.Warehouse!.ClientItems[44] = new ConcurrentDictionary<uint, LegacyNativeGameItemRuntime>
        {
            [3] = shared,
            [4] = equalUid,
        };

        var actual = client.AllPerfectionItems().ToArray();

        Assert.Equal(4, actual.Length);
        Assert.Equal(3, actual.Count(item => ReferenceEquals(shared, item)));
        Assert.Equal(1, actual.Count(item => ReferenceEquals(equalUid, item)));
        Assert.Equal(4, client.GetPerfectionItemsCount());
    }

    [Fact]
    public void IteratorDefersReadsAndUsesCurrentOwnerReplacementsAtEachRootPhase()
    {
        var client = new LegacyNativeGameClientRuntime();
        var sequence = client.AllPerfectionItems();
        using var enumerator = sequence.GetEnumerator();
        var inventory = Item(90);
        var equipment = Item(10);
        var warehouse = Item(30);
        client.Inventory = new LegacyNativeInventoryRuntime(client);
        client.Inventory.ClientItems[1] = inventory;
        Assert.True(enumerator.MoveNext());
        Assert.Same(inventory, enumerator.Current);

        client.Equipment = new LegacyNativeEquipItemStorage(client);
        client.Equipment.ClientItems[2] = equipment;
        Assert.True(enumerator.MoveNext());
        Assert.Same(equipment, enumerator.Current);

        client.Warehouse = new LegacyNativeWarehouseRuntime(client);
        client.Warehouse.ClientItems[44] = Bank(3, warehouse);
        Assert.True(enumerator.MoveNext());
        Assert.Same(warehouse, enumerator.Current);
        Assert.False(enumerator.MoveNext());

        var replacement = Item(80);
        client.Inventory = new LegacyNativeInventoryRuntime(client);
        client.Inventory.ClientItems[4] = replacement;
        Assert.Same(replacement, sequence.First());
        Assert.Equal(3, client.GetPerfectionItemsCount());
    }

    [Fact]
    public void WardrobeRuneRelicAndGearOwnersAreNotVisitedOrCounted()
    {
        var client = Owners();
        Assert.Empty(client.AllPerfectionItems());
        Assert.Equal(0, client.GetPerfectionItemsCount());
        // Missing excluded owners do not fault. Filling them still contributes nothing.
        client.MyWardrobe = new LegacyNativeWardrobeRuntime(client);
        Assert.True(client.MyWardrobe.AddItem(Item(1)));
        client.Rune = new LegacyNativeRuneItemStorage(client);
        Assert.True(client.Rune.Add(Item(2)));
        client.RelicFuse = new LegacyNativeItemBagRuntime();
        client.RelicFuse.Add(3, Item(3));
        client.GearMythsoulBag = new LegacyNativeItemBagRuntime();
        client.GearMythsoulBag.Add(4, Item(4));

        Assert.Empty(client.AllPerfectionItems());
        Assert.Equal(0, client.GetPerfectionItemsCount());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void MissingParticipatingOwnerFaultsAtItsNativePhaseWithoutRepair(int missingRoot)
    {
        var client = new LegacyNativeGameClientRuntime();
        var inventory = Item(90);
        var equipment = Item(10);
        if (missingRoot > 0)
        {
            client.Inventory = new LegacyNativeInventoryRuntime(client);
            client.Inventory.ClientItems[1] = inventory;
        }
        if (missingRoot > 1)
        {
            client.Equipment = new LegacyNativeEquipItemStorage(client);
            client.Equipment.ClientItems[2] = equipment;
        }
        var sequence = client.AllPerfectionItems();
        using var enumerator = sequence.GetEnumerator();
        if (missingRoot > 0)
        {
            Assert.True(enumerator.MoveNext());
            Assert.Same(inventory, enumerator.Current);
        }
        if (missingRoot > 1)
        {
            Assert.True(enumerator.MoveNext());
            Assert.Same(equipment, enumerator.Current);
        }

        Assert.Throws<NullReferenceException>(() => enumerator.MoveNext());
        Assert.Throws<NullReferenceException>(() => client.GetPerfectionItemsCount());
        if (missingRoot == 0) Assert.Null(client.Inventory);
        if (missingRoot <= 1) Assert.Null(client.Equipment);
        Assert.Null(client.Warehouse);
    }

    [Fact]
    public void CountAndReenumerationReevaluateCurrentPredicateFieldsWithoutCaching()
    {
        var client = Owners();
        var item = Item(7);
        client.Inventory!.ClientItems[7] = item;
        var sequence = client.AllPerfectionItems();
        Assert.Same(item, Assert.Single(sequence));
        Assert.Equal(1, client.GetPerfectionItemsCount());

        item.PerfectionProgress = 0;
        Assert.Empty(sequence);
        Assert.Equal(0, client.GetPerfectionItemsCount());
        item.PerfectionLevel = 1;
        Assert.Same(item, Assert.Single(sequence));
        Assert.Equal(1, client.GetPerfectionItemsCount());
        item.OwnerUID = 0;
        Assert.Empty(sequence);
        Assert.Equal(0, client.GetPerfectionItemsCount());
        item.OwnerUID = 1;
        item.ITEM_ID = 1000000;
        Assert.Empty(sequence);
        Assert.Equal(0, client.GetPerfectionItemsCount());
        item.ITEM_ID = 181305;
        Assert.Same(item, Assert.Single(sequence));
        Assert.Equal(1, client.GetPerfectionItemsCount());
    }

    [Fact]
    public void NullStoredItemIsNotSilentlySkippedByEnumerationOrCount()
    {
        var client = Owners();
        client.Inventory!.ClientItems[7] = null!;

        Assert.Throws<NullReferenceException>(() => client.AllPerfectionItems().ToArray());
        Assert.Throws<NullReferenceException>(() => client.GetPerfectionItemsCount());
        Assert.Null(client.Inventory.ClientItems[7]);
    }

    private static LegacyNativeGameClientRuntime Owners()
    {
        var client = new LegacyNativeGameClientRuntime();
        client.Inventory = new LegacyNativeInventoryRuntime(client);
        client.Equipment = new LegacyNativeEquipItemStorage(client);
        client.Warehouse = new LegacyNativeWarehouseRuntime(client);
        return client;
    }

    private static LegacyNativeGameItemRuntime Item(uint uid) => new()
    {
        Uid = uid,
        ITEM_ID = 181305,
        PerfectionProgress = 1,
        OwnerUID = 17,
    };

    private static ConcurrentDictionary<uint, LegacyNativeGameItemRuntime> Bank(
        uint key, LegacyNativeGameItemRuntime item) => new()
    {
        [key] = item,
    };
}
