using System.Collections.Concurrent;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyNativeWarehouseRuntimeTests
{
    [Fact]
    public void ClientBirthDoesNotEagerlyAllocateWarehouseOrInventory()
    {
        var client = new LegacyNativeGameClientRuntime();
        Assert.Null(client.Warehouse);
        Assert.Null(client.Inventory);
    }

    [Fact]
    public void ConstructorRetainsActualOwnerAndStartsFreshNativeStorage()
    {
        var client = new LegacyNativeGameClientRuntime();
        var warehouse = new LegacyNativeWarehouseRuntime(client);
        Assert.Same(client, warehouse.User);
        Assert.Empty(warehouse.ClientItems);
        Assert.Empty(warehouse.IsShow);
        Assert.Null(client.Warehouse); // Native member assignment is a separate DataCore statement.
    }

    [Fact]
    public void ConstructorAcceptsNullOwnerWithoutInventingGuardOrClient()
    {
        var warehouse = new LegacyNativeWarehouseRuntime(null);
        Assert.Null(warehouse.User);
        Assert.Empty(warehouse.ClientItems);
    }

    [Fact]
    public void ExplicitMemberAssignmentKeepsTheSameConcreteWarehouse()
    {
        var client = new LegacyNativeGameClientRuntime();
        var warehouse = new LegacyNativeWarehouseRuntime(client);
        client.Warehouse = warehouse;
        Assert.Same(warehouse, client.Warehouse);
        Assert.Same(client, client.Warehouse!.User);
    }

    [Fact]
    public void DifferentClientsAndWarehousesNeverShareContainerInstances()
    {
        var leftClient = new LegacyNativeGameClientRuntime();
        var rightClient = new LegacyNativeGameClientRuntime();
        var left = new LegacyNativeWarehouseRuntime(leftClient);
        var right = new LegacyNativeWarehouseRuntime(rightClient);
        Assert.NotSame(left.ClientItems, right.ClientItems);
        Assert.NotSame(left.IsShow, right.IsShow);
        left.AddItem(new LegacyNativeGameItemRuntime { Uid = 11u }, 7u);
        Assert.Empty(right.ClientItems);
    }

    [Fact]
    public void AddCreatesBankStoresSameItemAndSetsWarehouseIdOnlyAfterSuccess()
    {
        var warehouse = new LegacyNativeWarehouseRuntime(null);
        var item = new LegacyNativeGameItemRuntime { Uid = 11u, WH_ID = 999u };
        Assert.True(warehouse.AddItem(item, 7u));
        Assert.Same(item, warehouse.ClientItems[7u][11u]);
        Assert.Equal(7u, item.WH_ID);
        Assert.Equal(11u, item.Uid);
    }

    [Fact]
    public void DuplicateUidReturnsFalseAndDoesNotMutateOrReplaceTheCandidate()
    {
        var warehouse = new LegacyNativeWarehouseRuntime(null);
        var first = new LegacyNativeGameItemRuntime { Uid = 11u };
        var candidate = new LegacyNativeGameItemRuntime { Uid = 11u, WH_ID = 999u };
        Assert.True(warehouse.AddItem(first, 7u));
        Assert.False(warehouse.AddItem(candidate, 7u));
        Assert.Same(first, warehouse.ClientItems[7u][11u]);
        Assert.Equal(999u, candidate.WH_ID);
    }

    [Fact]
    public void SameUidInTwoBanksKeepsSeparateReferencesWithoutGlobalDeduplication()
    {
        var warehouse = new LegacyNativeWarehouseRuntime(null);
        var first = new LegacyNativeGameItemRuntime { Uid = 11u };
        var second = new LegacyNativeGameItemRuntime { Uid = 11u };
        Assert.True(warehouse.AddItem(first, 7u));
        Assert.True(warehouse.AddItem(second, 8u));
        Assert.Same(first, warehouse.ClientItems[7u][11u]);
        Assert.Same(second, warehouse.ClientItems[8u][11u]);
    }

    [Fact]
    public void NativeMaximumConstantDoesNotCreateAnAddCapacityLimit()
    {
        var warehouse = new LegacyNativeWarehouseRuntime(null);
        Assert.Equal((byte)60, LegacyNativeWarehouseRuntime.Max_Count);
        Assert.Equal((byte)60, warehouse.WHMaxSpace());
        for (uint uid = 1; uid <= 61; uid++)
            Assert.True(warehouse.AddItem(new LegacyNativeGameItemRuntime { Uid = uid }, 7u));
        Assert.Equal(61, warehouse.ClientItems[7u].Count);
    }

    [Fact]
    public void ZeroBankAndZeroUidAreStoredWithoutInventoryUidAdmission()
    {
        var warehouse = new LegacyNativeWarehouseRuntime(null);
        var item = new LegacyNativeGameItemRuntime { Uid = 0u };
        Assert.True(warehouse.AddItem(item, 0u));
        Assert.Same(item, warehouse.ClientItems[0u][0u]);
        Assert.Equal(0u, item.Uid);
        Assert.Equal(0u, item.WH_ID);
    }

    [Fact]
    public void NullAddFaultsAfterCreatingItsMissingBank()
    {
        var warehouse = new LegacyNativeWarehouseRuntime(null);
        Assert.Throws<NullReferenceException>(() => warehouse.AddItem(null!, 7u));
        Assert.True(warehouse.ClientItems.ContainsKey(7u));
        Assert.Empty(warehouse.ClientItems[7u]);
    }

    [Fact]
    public void TryGetReturnsTheExactStoredReferenceAndMissingPathsSetNull()
    {
        var warehouse = new LegacyNativeWarehouseRuntime(null);
        var item = new LegacyNativeGameItemRuntime { Uid = 11u };
        Assert.False(warehouse.TryGetItem(7u, 11u, out var missingBank));
        Assert.Null(missingBank);
        warehouse.AddItem(item, 7u);
        Assert.False(warehouse.TryGetItem(7u, 12u, out var missingUid));
        Assert.Null(missingUid);
        Assert.True(warehouse.TryGetItem(7u, 11u, out var found));
        Assert.Same(item, found);
    }

    [Fact]
    public void EmptyBankIsNotAnItemButAnyNonemptyBankIs()
    {
        var warehouse = new LegacyNativeWarehouseRuntime(null);
        Assert.False(warehouse.HaveItemsInBanks());
        warehouse.ClientItems.TryAdd(7u, new ConcurrentDictionary<uint, LegacyNativeGameItemRuntime>());
        Assert.False(warehouse.HaveItemsInBanks());
        warehouse.AddItem(new LegacyNativeGameItemRuntime { Uid = 11u }, 8u);
        Assert.True(warehouse.HaveItemsInBanks());
    }

    [Fact]
    public void RemoveInscribedMutatesSameItemsOnlyForTheExactOneValue()
    {
        var warehouse = new LegacyNativeWarehouseRuntime(null);
        var zero = new LegacyNativeGameItemRuntime { Uid = 10u, Inscribed = 0 };
        var one = new LegacyNativeGameItemRuntime { Uid = 11u, Inscribed = 1 };
        var two = new LegacyNativeGameItemRuntime { Uid = 12u, Inscribed = 2 };
        var maximum = new LegacyNativeGameItemRuntime { Uid = 13u, Inscribed = ushort.MaxValue };
        warehouse.AddItem(zero, 7u);
        warehouse.AddItem(one, 7u);
        warehouse.AddItem(two, 8u);
        warehouse.AddItem(maximum, 8u);
        warehouse.RemoveInscribedItems();
        Assert.Equal((ushort)0, zero.Inscribed);
        Assert.Equal((ushort)0, one.Inscribed);
        Assert.Equal((ushort)2, two.Inscribed);
        Assert.Equal(ushort.MaxValue, maximum.Inscribed);
        Assert.Same(one, warehouse.ClientItems[7u][11u]);
    }

    [Fact]
    public void DirectRawStoragePreservesKeyValueUidAndWarehouseMismatch()
    {
        var warehouse = new LegacyNativeWarehouseRuntime(null);
        var parent = new LegacyNativeGameItemRuntime { Uid = 11u, WH_ID = 2u };
        warehouse.ClientItems.TryAdd(7u, new ConcurrentDictionary<uint, LegacyNativeGameItemRuntime>());
        warehouse.ClientItems[7u].TryAdd(777u, parent); // Future native loader uses the last DBItem key, not parent.Uid.
        Assert.True(warehouse.TryGetItem(7u, 777u, out var found));
        Assert.Same(parent, found);
        Assert.Equal(11u, found!.Uid);
        Assert.Equal(2u, found.WH_ID); // Direct native raw route does not call AddItem's WH_ID writer.
        Assert.False(warehouse.TryGetItem(7u, 11u, out _));
    }

    [Fact]
    public void InventoryAndWarehouseStorageCanRetainOneConcreteItemIdentity()
    {
        var client = new LegacyNativeGameClientRuntime();
        var inventory = new LegacyNativeInventoryRuntime(client);
        var warehouse = new LegacyNativeWarehouseRuntime(client);
        client.Inventory = inventory;
        client.Warehouse = warehouse;
        var item = new LegacyNativeGameItemRuntime { Uid = 11u };
        inventory.AddDBItem(item);
        warehouse.AddItem(item, 7u); // Reference-sharing control, not a claim that native routes add to both.
        Assert.Same(inventory.ClientItems[11u], warehouse.ClientItems[7u][11u]);
        warehouse.ClientItems[7u][11u].PerfectionLevel = 18u;
        Assert.Equal(18u, inventory.ClientItems[11u].PerfectionLevel);
    }

    [Fact]
    public void ReplaceableFieldsRemainActualNativeReferencesNotRepairingProperties()
    {
        var warehouse = new LegacyNativeWarehouseRuntime(null);
        var replacement = new ConcurrentDictionary<uint, ConcurrentDictionary<uint, LegacyNativeGameItemRuntime>>();
        var list = new List<uint> { 7u, 7u };
        warehouse.ClientItems = replacement;
        warehouse.IsShow = list;
        Assert.Same(replacement, warehouse.ClientItems);
        Assert.Same(list, warehouse.IsShow);
        warehouse.ClientItems = null!;
        Assert.Throws<NullReferenceException>(() => warehouse.HaveItemsInBanks());
        Assert.Null(warehouse.ClientItems);
    }

    [Fact]
    public void StorageAndAddDoNotEmitModesChangePositionOrConsumeShowState()
    {
        var warehouse = new LegacyNativeWarehouseRuntime(null);
        var item = new LegacyNativeGameItemRuntime { Uid = 11u, Position = 17 };
        var mode = item.Mode;
        warehouse.IsShow.Add(7u);
        Assert.True(warehouse.AddItem(item, 7u));
        Assert.Equal((ushort)17, item.Position);
        Assert.Equal(mode, item.Mode);
        Assert.Equal(new uint[] { 7u }, warehouse.IsShow);
    }
}
