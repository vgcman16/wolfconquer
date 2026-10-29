using System.Reflection;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyNativeContainerMemberBirthTests
{
    [Fact]
    public void ExistingPhysicalClientBirthLeavesEveryNewSlotUnassigned()
    {
        var client = new LegacyNativeGameClientRuntime();
        Assert.NotNull(client.Player);
        Assert.Null(client.GearMythsoulBag);
        Assert.Null(client.RelicFuse);
        Assert.Null(client.MyWardrobe);
        Assert.Null(client.Rune);
        Assert.Null(client.Inventory);
        Assert.Null(client.Equipment);
        Assert.Null(client.Warehouse);
        Assert.Null(client.RawEquipmentObservation);
    }

    [Fact]
    public void GearFieldSlotAssignsOnlyGearBagAndKeepsCurrentPlayer()
    {
        var client = new LegacyNativeGameClientRuntime();
        var player = client.Player;
        LegacyNativeItemContainerMemberPhases.InitializeGearMythsoulBagAtNativeFieldSlot(client);
        Assert.NotNull(client.GearMythsoulBag);
        Assert.Empty(client.GearMythsoulBag);
        Assert.Same(player, client.Player);
        Assert.Null(client.RelicFuse);
        Assert.Null(client.MyWardrobe);
        Assert.Null(client.Inventory);
        Assert.Null(client.Equipment);
        Assert.Null(client.Warehouse);
    }

    [Fact]
    public void RelicFieldSlotDoesNotInventPrecedingGearOrConstructorMembers()
    {
        var client = new LegacyNativeGameClientRuntime();
        LegacyNativeItemContainerMemberPhases.InitializeRelicFuseAtNativeFieldSlot(client);
        Assert.NotNull(client.RelicFuse);
        Assert.Empty(client.RelicFuse);
        Assert.Null(client.GearMythsoulBag);
        Assert.Null(client.MyWardrobe);
        Assert.Null(client.Rune);
    }

    [Fact]
    public void WardrobeSlotUsesPassedClientAndDoesNotInventOtherCtorEffects()
    {
        var client = new LegacyNativeGameClientRuntime();
        var player = client.Player;
        LegacyNativeItemContainerMemberPhases.AssignWardrobeAtNativeConstructorSlot(client);
        Assert.Same(client, client.MyWardrobe!.Owner);
        Assert.Equal(2, client.MyWardrobe.Items.Length);
        Assert.NotSame(client.MyWardrobe.Items[0], client.MyWardrobe.Items[1]);
        Assert.Same(player, client.Player);
        Assert.Null(client.GearMythsoulBag);
        Assert.Null(client.RelicFuse);
        Assert.Null(client.Warehouse);
    }

    [Fact]
    public void RepeatedGearSlotRetainsHeldOldBagItemsAndSnapshotContents()
    {
        var client = new LegacyNativeGameClientRuntime();
        LegacyNativeItemContainerMemberPhases.InitializeGearMythsoulBagAtNativeFieldSlot(client);
        var old = client.GearMythsoulBag!;
        var item = Item(7);
        old.Add(19, item);
        var cache = old.GetValues();
        long revision = old.Revision; object gate = old.SyncRoot;
        LegacyNativeItemContainerMemberPhases.InitializeGearMythsoulBagAtNativeFieldSlot(client);
        Assert.NotSame(old, client.GearMythsoulBag);
        Assert.Empty(client.GearMythsoulBag!);
        Assert.Same(item, old[19]);
        Assert.Same(item, Assert.Single(cache));
        var current = old.GetValues();
        Assert.NotSame(cache, current); Assert.Same(item, Assert.Single(current));
        Assert.Equal(revision, old.Revision); Assert.Same(gate, old.SyncRoot); Assert.False(old.Update);
    }

    [Fact]
    public void RepeatedRelicSlotDoesNotReplaceGearBag()
    {
        var client = new LegacyNativeGameClientRuntime();
        LegacyNativeItemContainerMemberPhases.InitializeGearMythsoulBagAtNativeFieldSlot(client);
        LegacyNativeItemContainerMemberPhases.InitializeRelicFuseAtNativeFieldSlot(client);
        var gear = client.GearMythsoulBag;
        var relic = client.RelicFuse;
        LegacyNativeItemContainerMemberPhases.InitializeRelicFuseAtNativeFieldSlot(client);
        Assert.Same(gear, client.GearMythsoulBag);
        Assert.NotSame(relic, client.RelicFuse);
    }

    [Fact]
    public void RepeatedWardrobeSlotRetainsHeldOldWardrobeAndItems()
    {
        var client = new LegacyNativeGameClientRuntime();
        LegacyNativeItemContainerMemberPhases.AssignWardrobeAtNativeConstructorSlot(client);
        var old = client.MyWardrobe!;
        var item = Item(7);
        Assert.True(old.AddItem(item));
        LegacyNativeItemContainerMemberPhases.AssignWardrobeAtNativeConstructorSlot(client);
        Assert.NotSame(old, client.MyWardrobe);
        Assert.Same(item, Assert.Single(old.GetAllItems()));
        Assert.Empty(client.MyWardrobe!.GetAllItems());
    }

    [Fact]
    public void DataCorePrefixCreatesFourGenuineOwnersWithoutPretendingFullLoadClient()
    {
        var client = new LegacyNativeGameClientRuntime();
        LegacyNativeItemContainerMemberPhases.AssignLoadClientItemOwnersPrefix(client.Player);
        Assert.Same(client, client.Rune!.Owner);
        Assert.Same(client, client.Inventory!.Owner);
        Assert.IsType<LegacyNativeEquipItemStorage>(client.Equipment);
        Assert.Same(client, ReadEquipOwner(client.Equipment!));
        Assert.Same(client, client.Warehouse!.User);
        Assert.Empty(client.Rune.Objects);
        Assert.Empty(client.Inventory.ClientItems);
        Assert.Empty(client.Equipment!.ClientItems);
        Assert.Empty(client.Warehouse.ClientItems);
        Assert.Null(client.MyWardrobe);
        Assert.Null(client.GearMythsoulBag);
        Assert.Null(client.RelicFuse);
    }

    [Fact]
    public void RepeatedDataCorePrefixReplacesFourOwnersButNeverClonesHeldItems()
    {
        var client = new LegacyNativeGameClientRuntime();
        LegacyNativeItemContainerMemberPhases.AssignLoadClientItemOwnersPrefix(client.Player);
        var rune = client.Rune!;
        var inventory = client.Inventory!;
        var equipment = client.Equipment!;
        var warehouse = client.Warehouse!;
        var item = Item(7);
        Assert.True(rune.Add(item));
        inventory.AddDBItem(item);
        Assert.True(equipment.ClientItems.TryAdd(99, item));
        Assert.True(warehouse.AddItem(item, 44));
        LegacyNativeItemContainerMemberPhases.AssignLoadClientItemOwnersPrefix(client.Player);
        Assert.NotSame(rune, client.Rune);
        Assert.NotSame(inventory, client.Inventory);
        Assert.NotSame(equipment, client.Equipment);
        Assert.NotSame(warehouse, client.Warehouse);
        Assert.Same(item, Assert.Single(rune.Objects));
        Assert.Same(item, inventory.ClientItems[7]);
        Assert.Same(item, equipment.GetItem(99));
        Assert.Same(item, warehouse.ClientItems[44][7]);
        Assert.Empty(client.Equipment!.ClientItems);
    }

    [Fact]
    public void DataCorePrefixUsesPassedPlayersOwnerNotClientsReplacementPlayer()
    {
        var first = new LegacyNativeGameClientRuntime();
        var second = new LegacyNativeGameClientRuntime();
        var held = first.Player;
        first.Player = second.Player;
        LegacyNativeItemContainerMemberPhases.AssignLoadClientItemOwnersPrefix(held);
        Assert.Same(first, first.Rune!.Owner);
        Assert.Same(first, first.Inventory!.Owner);
        Assert.Same(first, ReadEquipOwner(first.Equipment!));
        Assert.Same(first, first.Warehouse!.User);
        Assert.Same(second.Player, first.Player);
        Assert.Null(second.Rune);
        Assert.Null(second.Inventory);
        Assert.Null(second.Equipment);
        Assert.Null(second.Warehouse);
    }

    [Fact]
    public void NullPlayerFaultPrecedesAnyMutationOfPreviouslyConstructedOwners()
    {
        var client = new LegacyNativeGameClientRuntime();
        LegacyNativeItemContainerMemberPhases.AssignLoadClientItemOwnersPrefix(client.Player);
        var rune = client.Rune;
        var inventory = client.Inventory;
        var equipment = client.Equipment;
        var warehouse = client.Warehouse;
        Assert.Throws<NullReferenceException>(() =>
            LegacyNativeItemContainerMemberPhases.AssignLoadClientItemOwnersPrefix(null!));
        Assert.Same(rune, client.Rune);
        Assert.Same(inventory, client.Inventory);
        Assert.Same(equipment, client.Equipment);
        Assert.Same(warehouse, client.Warehouse);
    }

    [Fact]
    public void NullClientSlotFaultsAreNotNormalizedToSuccessfulEmptyOwners()
    {
        Assert.Throws<NullReferenceException>(() =>
            LegacyNativeItemContainerMemberPhases.InitializeGearMythsoulBagAtNativeFieldSlot(null!));
        Assert.Throws<NullReferenceException>(() =>
            LegacyNativeItemContainerMemberPhases.InitializeRelicFuseAtNativeFieldSlot(null!));
        Assert.Throws<NullReferenceException>(() =>
            LegacyNativeItemContainerMemberPhases.AssignWardrobeAtNativeConstructorSlot(null!));
    }

    [Fact]
    public void SameActualItemCanBeRetainedAcrossAllConcreteStoresWithoutIdentityAdapter()
    {
        var client = new LegacyNativeGameClientRuntime();
        LegacyNativeItemContainerMemberPhases.InitializeGearMythsoulBagAtNativeFieldSlot(client);
        LegacyNativeItemContainerMemberPhases.InitializeRelicFuseAtNativeFieldSlot(client);
        LegacyNativeItemContainerMemberPhases.AssignWardrobeAtNativeConstructorSlot(client);
        LegacyNativeItemContainerMemberPhases.AssignLoadClientItemOwnersPrefix(client.Player);
        var item = Item(7);
        client.GearMythsoulBag!.Add(900, item);
        client.RelicFuse!.Add(901, item);
        Assert.True(client.MyWardrobe!.AddItem(item));
        client.Inventory!.AddDBItem(item);
        Assert.True(client.Rune!.Add(item));
        Assert.True(client.Equipment!.ClientItems.TryAdd(902, item));
        Assert.True(client.Warehouse!.AddItem(item, 44));
        Assert.Equal(44u, item.WH_ID);
        Assert.Same(item, client.GearMythsoulBag[900]);
        Assert.Same(item, client.RelicFuse[901]);
        Assert.Same(item, Assert.Single(client.MyWardrobe.GetAllItems()));
        Assert.Same(item, client.Inventory.ClientItems[7]);
        Assert.Same(item, Assert.Single(client.Rune.Objects));
        Assert.Same(item, client.Equipment.GetItem(902));
        Assert.Same(item, client.Warehouse.ClientItems[44][7]);
        // Compatibility of physical references, NOT a claim native routing inserts into every store.
    }

    [Fact]
    public void GenuinePrefixDoesNotAdoptRawObservationAsEquipmentOrAuthorizeItsLookup()
    {
        var client = new LegacyNativeGameClientRuntime();
        var raw = new LegacyNativeEquipmentRuntime(client);
        client.RawEquipmentObservation = raw;
        LegacyNativeItemContainerMemberPhases.AssignLoadClientItemOwnersPrefix(client.Player);
        Assert.IsType<LegacyNativeEquipItemStorage>(client.Equipment);
        Assert.Same(raw, client.RawEquipmentObservation);
        Assert.False(raw.CanAuthorizeWeaponAdmission);
        Assert.False(raw.ObserveRawLoadedPosition(4).Available);
        Assert.Empty(client.Equipment!.ClientItems);
    }

    [Fact]
    public void ClientFieldDeclarationsRetainNativeContainerMemberRelativeOrderOnly()
    {
        var fields = typeof(LegacyNativeGameClientRuntime)
            .GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        var names = new[] { "GearMythsoulBag", "MyWardrobe", "Warehouse", "RelicFuse", "Equipment", "Inventory", "Rune" };
        var tokens = names.Select(name => fields.Single(field => field.Name == name).MetadataToken).ToArray();
        Assert.Equal(tokens.OrderBy(token => token).ToArray(), tokens);
        // Declaration metadata is NOT runtime proof of all native field/constructor effect order.
    }

    [Fact]
    public void OwnerConstructionPrefixDoesNotReadOrBootstrapItemCounter()
    {
        var previous = LegacyNativePoolItems.ItemCounter;
        try
        {
            LegacyNativePoolItems.ItemCounter = null!;
            var client = new LegacyNativeGameClientRuntime();
            LegacyNativeItemContainerMemberPhases.AssignLoadClientItemOwnersPrefix(client.Player);
            Assert.Null(LegacyNativePoolItems.ItemCounter);
            Assert.Empty(client.Equipment!.ClientItems);
        }
        finally { LegacyNativePoolItems.ItemCounter = previous; }
    }


    [Fact]
    public void NativeItemWrappersKeepExactGroupOrderSameReferencesAndCountExcludesBags()
    {
        var client = FullyConstructedContainerSubset();
        var inventory = Item(1);
        var equipment = Item(2);
        var warehouse = Item(3);
        var wardrobe = Item(4);
        var rune = Item(5);
        var relic = Item(6);
        var gear = Item(7);
        client.Inventory!.AddDBItem(inventory);
        client.Equipment!.ClientItems.TryAdd(2, equipment);
        Assert.True(client.Warehouse!.AddItem(warehouse, 44));
        Assert.True(client.MyWardrobe!.AddItem(wardrobe));
        Assert.True(client.Rune!.Add(rune));
        client.RelicFuse!.Add(6, relic);
        client.GearMythsoulBag!.Add(7, gear);
        var expected = new[] { inventory, equipment, warehouse, wardrobe, rune, relic, gear };
        var actual = client.AllMyItems().ToArray();
        Assert.Equal(expected.Length, actual.Length);
        for (int index = 0; index < expected.Length; index++) Assert.Same(expected[index], actual[index]);
        Assert.Equal(new[] { inventory, equipment }, client.GetAllMainItems().ToArray());
        Assert.Equal(5, client.GetItemsCount());
    }

    [Fact]
    public void NativeAllItemsNullEquipmentFaultIsDeferredUntilAfterInventoryYield()
    {
        var client = new LegacyNativeGameClientRuntime();
        var item = Item(7);
        client.Inventory = new LegacyNativeInventoryRuntime(client);
        client.Inventory.AddDBItem(item);
        var sequence = client.AllMyItems(); // Iterator call itself does not touch missing Equipment.
        using var enumerator = sequence.GetEnumerator();
        Assert.True(enumerator.MoveNext());
        Assert.Same(item, enumerator.Current);
        Assert.Throws<NullReferenceException>(() => enumerator.MoveNext());
        using var main = client.GetAllMainItems().GetEnumerator();
        Assert.True(main.MoveNext());
        Assert.Same(item, main.Current);
        Assert.Throws<NullReferenceException>(() => main.MoveNext());
    }

    [Fact]
    public void NativeAllItemsRetainsRuneArrayCapturedAtThatEnumerationPhase()
    {
        var client = FullyConstructedContainerSubset();
        var first = Item(7);
        var second = Item(8);
        Assert.True(client.Rune!.Add(first));
        using var enumerator = client.AllMyItems().GetEnumerator();
        Assert.True(enumerator.MoveNext());
        Assert.Same(first, enumerator.Current);
        Assert.True(client.Rune.Add(second)); // Replaces cache; running iterator retains its old array.
        Assert.False(enumerator.MoveNext());
        Assert.Equal(new[] { first, second }, client.AllMyItems().ToArray());
    }

    [Fact]
    public void NativeAllItemsKeepsEscapedSnapshotsAndEnumeratesCurrentBagValues()
    {
        var client = FullyConstructedContainerSubset();
        var old = Item(7);
        var replacement = Item(8);
        client.GearMythsoulBag!.Add(99, old);
        var cache = client.GearMythsoulBag.GetValues();
        long priorRevision = client.GearMythsoulBag.Revision;
        client.GearMythsoulBag[99] = replacement;
        long revision = client.GearMythsoulBag.Revision;
        Assert.Equal(priorRevision + 1, revision); Assert.True(client.GearMythsoulBag.Update);
        Assert.Same(old, Assert.Single(cache));
        Assert.Same(replacement, Assert.Single(client.AllMyItems()));
        Assert.Equal(0, client.GetItemsCount()); // Native counter omits both bags.
        Assert.Equal(revision, client.GearMythsoulBag.Revision); Assert.True(client.GearMythsoulBag.Update);
        var current = client.GearMythsoulBag.GetValues();
        Assert.NotSame(cache, current); Assert.Same(replacement, Assert.Single(current));
        Assert.False(client.GearMythsoulBag.Update); Assert.Equal(revision, client.GearMythsoulBag.Revision);
        Assert.Same(old, Assert.Single(cache));
    }

    [Fact]
    public void NativeItemCountKeepsGroupAliasesButEscapedRuneArrayCannotInjectNull()
    {
        var client = FullyConstructedContainerSubset();
        var item = Item(7);
        Assert.True(client.Rune!.Add(item));
        client.Inventory!.AddDBItem(item);
        client.Equipment!.ClientItems.TryAdd(99, item);
        Assert.Equal(3, client.GetItemsCount());
        client.Rune.Objects[0] = null!;
        Assert.Equal(3, client.GetItemsCount());
        var items = client.AllMyItems().ToArray();
        Assert.Equal(3, items.Length);
        Assert.Same(item, items[0]);
        Assert.Same(item, items[1]);
        Assert.Same(item, items[2]);
    }

    private static LegacyNativeGameClientRuntime FullyConstructedContainerSubset()
    {
        var client = new LegacyNativeGameClientRuntime();
        LegacyNativeItemContainerMemberPhases.InitializeGearMythsoulBagAtNativeFieldSlot(client);
        LegacyNativeItemContainerMemberPhases.InitializeRelicFuseAtNativeFieldSlot(client);
        LegacyNativeItemContainerMemberPhases.AssignWardrobeAtNativeConstructorSlot(client);
        LegacyNativeItemContainerMemberPhases.AssignLoadClientItemOwnersPrefix(client.Player);
        return client;
    }

    private static LegacyNativeGameItemRuntime Item(uint uid) =>
        new() { Uid = uid, ITEM_ID = 181305u, Position = 9 };

    private static LegacyNativeGameClientRuntime? ReadEquipOwner(LegacyNativeEquipItemStorage equipment) =>
        (LegacyNativeGameClientRuntime?)typeof(LegacyNativeEquipItemStorage)
            .GetField("Owner", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(equipment);
}
