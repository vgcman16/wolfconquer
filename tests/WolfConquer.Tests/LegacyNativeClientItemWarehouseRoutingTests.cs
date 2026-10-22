using System.Collections.Concurrent;
using WolfConquer.Core.World;
using Routing = WolfConquer.Core.World.LegacyNativeClientItemWarehouseRouting;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyNativeClientItemWarehouseRoutingTests
{
    [Fact]
    public void WhZeroIsUnhandledWithoutDereferencingClientItemOrOwners()
    {
        var record = Record(0, 900, 101);
        var before = record;
        Assert.False(Routing.TryRouteWarehouseRecord(null!, ref record, null!));
        Assert.Equal(before, record);
    }

    [Fact]
    public void WhZeroDoesNotRouteUsingTheItemsWarehouseOrPosition()
    {
        var client = new LegacyNativeGameClientRuntime();
        var item = Item(71, 181305, wh: 100, position: 1);
        var record = Record(0, 900, 0);
        Assert.False(Routing.TryRouteWarehouseRecord(client, ref record, item));
        Assert.Equal(100U, item.WH_ID);
        Assert.Equal((ushort)1, item.Position);
        Assert.Null(client.MyWardrobe);
        Assert.Null(client.Warehouse);
        Assert.Null(client.Equipment);
    }

    [Fact]
    public void WardrobeUsesItemClassifierButEscapedRecordKeyAndPosition()
    {
        var client = Owners();
        client.Player = null!; // This native branch never reads GuildID/Player.
        var item = Item(71, 181305, wh: 7, position: 0);
        var child = Item(72);
        item.Deposite.TryAdd(child.Uid, child);
        var record = Record(100, 900, 59);
        record.ITEM_ID = 200000; // Escaped child classifies as mount; outer object is garment.
        var before = record;

        Assert.True(Routing.TryRouteWarehouseRecord(client, ref record, item));

        Assert.Same(item, client.MyWardrobe!.Items[0][71]);
        Assert.Empty(client.MyWardrobe.Items[1]);
        Assert.Same(item, client.Equipment!.ClientItems[900]);
        Assert.False(client.Equipment.ClientItems.ContainsKey(71));
        Assert.Same(child, item.Deposite[72]);
        Assert.Equal(100U, item.WH_ID);
        Assert.Equal((ushort)0, item.Position);
        Assert.Equal((ushort)9, item.Inscribed);
        Assert.Equal(before, record);
        Assert.Empty(client.Warehouse!.ClientItems);
    }

    [Fact]
    public void WardrobeMountUsesRealMountBucketWithoutPositionRewrite()
    {
        var client = Owners();
        client.Equipment = null;
        var item = Item(71, 200000, wh: 7, position: 8);
        var record = Record(100, 900, 0);

        Assert.True(Routing.TryRouteWarehouseRecord(client, ref record, item));

        Assert.Same(item, client.MyWardrobe!.Items[1][71]);
        Assert.Empty(client.MyWardrobe.Items[0]);
        Assert.Equal(100U, item.WH_ID);
        Assert.Equal((ushort)8, item.Position);
        Assert.Equal((ushort)9, item.Inscribed);
    }

    [Fact]
    public void WardrobeEquipmentBoundsUseRecordPositionsOneThrough59()
    {
        foreach (ushort position in new ushort[] { 0, 1, 59, 60, 63, ushort.MaxValue })
        {
            var client = Owners();
            var item = Item(71, 181305, position: 217);
            var record = Record(100, 900, position);
            Assert.True(Routing.TryRouteWarehouseRecord(client, ref record, item));
            Assert.Same(item, client.MyWardrobe!.Items[0][71]);
            Assert.Equal(position > 0 && position <= 59,
                client.Equipment!.ClientItems.ContainsKey(900));
            Assert.Equal((ushort)217, item.Position);
        }
    }

    [Fact]
    public void WardrobeRejectionDoesNotPreventEligibleEquipmentInsertion()
    {
        var client = Owners();
        var item = Item(71, 3001044, wh: 7, position: 0);
        var record = Record(100, 900, 1);

        Assert.True(Routing.TryRouteWarehouseRecord(client, ref record, item));

        Assert.False(client.MyWardrobe!.HaveItems());
        Assert.Same(item, client.Equipment!.ClientItems[900]);
        Assert.Equal(7U, item.WH_ID);
        Assert.Equal((ushort)9, item.Inscribed);
        Assert.Empty(client.Warehouse!.ClientItems);
    }

    [Fact]
    public void DuplicateWardrobeKeepsFirstButEquipmentStoresIncomingReference()
    {
        var client = Owners();
        var first = Item(71);
        Assert.True(client.MyWardrobe!.AddItem(first));
        var incoming = Item(71, wh: 7);
        var record = Record(100, 900, 1);

        Assert.True(Routing.TryRouteWarehouseRecord(client, ref record, incoming));

        Assert.Same(first, client.MyWardrobe.Items[0][71]);
        Assert.Same(incoming, client.Equipment!.ClientItems[900]);
        Assert.Equal(7U, incoming.WH_ID); // Duplicate AddItem returned before its WH_ID write.
        Assert.Equal(100U, first.WH_ID);
    }

    [Fact]
    public void DuplicateEquipmentKeepsFirstAfterWardrobeSideEffects()
    {
        var client = Owners();
        var first = Item(72);
        Assert.True(client.Equipment!.ClientItems.TryAdd(900, first));
        var incoming = Item(71, wh: 7);
        var record = Record(100, 900, 1);

        Assert.True(Routing.TryRouteWarehouseRecord(client, ref record, incoming));

        Assert.Same(first, client.Equipment.ClientItems[900]);
        Assert.Same(incoming, client.MyWardrobe!.Items[0][71]);
        Assert.Equal(100U, incoming.WH_ID);
    }

    [Fact]
    public void MissingWardrobeFaultsBeforeEquipmentOrWarehouseFallback()
    {
        var client = Owners();
        client.MyWardrobe = null;
        var item = Item(71, wh: 7);
        var record = Record(100, 900, 1);

        Assert.Throws<NullReferenceException>(() =>
            Routing.TryRouteWarehouseRecord(client, ref record, item));

        Assert.Equal(7U, item.WH_ID);
        Assert.Empty(client.Equipment!.ClientItems);
        Assert.Empty(client.Warehouse!.ClientItems);
    }

    [Fact]
    public void WardrobeMutationPrecedesMissingEquipmentFault()
    {
        var client = Owners();
        client.Equipment = null;
        var item = Item(71, wh: 7);
        var record = Record(100, 900, 1);

        Assert.Throws<NullReferenceException>(() =>
            Routing.TryRouteWarehouseRecord(client, ref record, item));

        Assert.Same(item, client.MyWardrobe!.Items[0][71]);
        Assert.Equal(100U, item.WH_ID);
        Assert.Empty(client.Warehouse!.ClientItems);
    }

    [Fact]
    public void RegularBankUsesEscapedKeysWithoutRewritingItemIdentityOrWh()
    {
        var client = Owners();
        var item = Item(71, wh: 7, position: 8);
        var record = Record(73, 900, 59);
        var before = record;

        Assert.True(Routing.TryRouteWarehouseRecord(client, ref record, item));

        Assert.Same(item, client.Warehouse!.ClientItems[73][900]);
        Assert.False(client.Warehouse.ClientItems[73].ContainsKey(71));
        Assert.Equal(71U, item.Uid);
        Assert.Equal(7U, item.WH_ID); // Do NOT substitute Warehouse.AddItem.
        Assert.Equal((ushort)8, item.Position);
        Assert.Equal((ushort)0, item.Inscribed);
        Assert.Equal(before, record);
        Assert.False(client.MyWardrobe!.HaveItems());
        Assert.Empty(client.Equipment!.ClientItems);
        Assert.Empty(client.Inventory!.ClientItems);
    }

    [Fact]
    public void ExistingBankAndDuplicateKeyKeepFirstAfterInscriptionClear()
    {
        var client = Owners();
        var bank = new ConcurrentDictionary<uint, LegacyNativeGameItemRuntime>();
        var first = Item(72);
        bank.TryAdd(900, first);
        client.Warehouse!.ClientItems.TryAdd(73, bank);
        var incoming = Item(71);
        var record = Record(73, 900, 0);

        Assert.True(Routing.TryRouteWarehouseRecord(client, ref record, incoming));

        Assert.Same(bank, client.Warehouse.ClientItems[73]);
        Assert.Same(first, bank[900]);
        Assert.Equal((ushort)0, incoming.Inscribed);
        Assert.Equal((ushort)9, first.Inscribed);
    }

    [Fact]
    public void NonzeroGuildRetainsInscriptionAndDoesNotTouchOtherOwners()
    {
        var client = Owners();
        client.Player.GuildID = 123;
        client.MyWardrobe = null;
        client.Equipment = null;
        client.Inventory = null;
        client.Rune = null;
        var item = Item(71);
        var record = Record(73, 900, 101);

        Assert.True(Routing.TryRouteWarehouseRecord(client, ref record, item));

        Assert.Same(item, client.Warehouse!.ClientItems[73][900]);
        Assert.Equal((ushort)9, item.Inscribed);
    }

    [Fact]
    public void NewBankCreationPrecedesMissingPlayerFault()
    {
        var client = Owners();
        client.Player = null!;
        var item = Item(71);
        var record = Record(73, 900, 0);

        Assert.Throws<NullReferenceException>(() =>
            Routing.TryRouteWarehouseRecord(client, ref record, item));

        Assert.True(client.Warehouse!.ClientItems.ContainsKey(73));
        Assert.Empty(client.Warehouse.ClientItems[73]);
        Assert.Equal((ushort)9, item.Inscribed);
    }

    [Fact]
    public void MissingWarehouseFaultsBeforePlayerAccessOrItemMutation()
    {
        var client = Owners();
        client.Warehouse = null;
        client.Player = null!;
        var item = Item(71);
        var record = Record(73, 900, 0);

        Assert.Throws<NullReferenceException>(() =>
            Routing.TryRouteWarehouseRecord(client, ref record, item));

        Assert.Equal((ushort)9, item.Inscribed);
    }

    [Fact]
    public void ZeroUidAndMaximumWarehouseRemainLiteralKeys()
    {
        var client = Owners();
        var item = Item(0, wh: 7, position: 8);
        var record = Record(uint.MaxValue, 0, 0);

        Assert.True(Routing.TryRouteWarehouseRecord(client, ref record, item));

        Assert.Same(item, client.Warehouse!.ClientItems[uint.MaxValue][0]);
        Assert.Equal(0U, item.Uid);
        Assert.Equal(7U, item.WH_ID);
    }

    [Fact]
    public void RegularRoutingAddsBeyondSixtyWithoutCapacitySubstitution()
    {
        var client = Owners();
        var bank = new ConcurrentDictionary<uint, LegacyNativeGameItemRuntime>();
        for (uint i = 1; i <= 60; i++) bank.TryAdd(i, Item(i));
        client.Warehouse!.ClientItems.TryAdd(73, bank);
        var item = Item(71);
        var record = Record(73, 900, 0);

        Assert.True(Routing.TryRouteWarehouseRecord(client, ref record, item));

        Assert.Equal(61, bank.Count);
        Assert.Same(item, bank[900]);
    }

    [Fact]
    public void SameOuterItemReferenceCanBeStoredUnderTwoEscapedBankKeys()
    {
        var client = Owners();
        client.Player.GuildID = 1;
        var item = Item(71);
        var record = Record(73, 900, 0);
        Assert.True(Routing.TryRouteWarehouseRecord(client, ref record, item));
        client.Player.GuildID = 0;
        record.WH_ID = 74;
        record.UID = 901;

        Assert.True(Routing.TryRouteWarehouseRecord(client, ref record, item));

        Assert.Same(item, client.Warehouse!.ClientItems[73][900]);
        Assert.Same(item, client.Warehouse.ClientItems[74][901]);
        Assert.Equal((ushort)0, client.Warehouse.ClientItems[73][900].Inscribed);
    }

    // Explicit concrete prerequisite calls in this fixture do NOT prove full native birth.
    private static LegacyNativeGameClientRuntime Owners()
    {
        var client = new LegacyNativeGameClientRuntime();
        LegacyNativeItemContainerMemberPhases.AssignWardrobeAtNativeConstructorSlot(client);
        LegacyNativeItemContainerMemberPhases.AssignLoadClientItemOwnersPrefix(client.Player);
        return client;
    }

    private static LegacyNativeGameItemRuntime Item(
        uint uid, uint id = 181305, uint wh = 7, ushort position = 8) =>
        new() { Uid = uid, ITEM_ID = id, WH_ID = wh, Position = position, Inscribed = 9 };

    private static LegacyNativeClientItemRecord Record(uint wh, uint uid, ushort position) =>
        new() { WH_ID = wh, UID = uid, Position = position };
}
