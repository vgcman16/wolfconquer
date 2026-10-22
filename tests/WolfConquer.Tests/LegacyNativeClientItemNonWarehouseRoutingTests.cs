using WolfConquer.Core.World;
using Routing = WolfConquer.Core.World.LegacyNativeClientItemNonWarehouseRouting;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyNativeClientItemNonWarehouseRoutingTests
{
    [Fact]
    public void DefaultMythsoulAddsSameItemButReturnsFalseForRemainingRuneCondition()
    {
        var client = Owners();
        client.Player = null!;
        var item = Item(71);
        var record = Record(900, 217);
        var before = record;

        Assert.False(Routing.TryCompleteNonWarehouseStoragePrefix(client, ref record, item));

        Assert.Same(item, client.GearMythsoulBag![71]);
        Assert.False(client.GearMythsoulBag.ContainsKey(900));
        Assert.Empty(client.RelicFuse!);
        Assert.Empty(client.Equipment!.ClientItems);
        Assert.Empty(client.Inventory!.ClientItems);
        Assert.Equal((ushort)9, item.Inscribed);
        Assert.Equal(before, record);
    }

    [Fact]
    public void MythsoulDuplicateOverwritesSameKeyAndInvalidatesCachedValues()
    {
        var client = Owners();
        var first = Item(71);
        client.GearMythsoulBag!.Add(71, first);
        var cached = client.GearMythsoulBag.GetValues();
        var incoming = Item(71);
        var record = Record(900, 217);

        Assert.False(Routing.TryCompleteNonWarehouseStoragePrefix(client, ref record, incoming));

        Assert.Same(incoming, client.GearMythsoulBag[71]);
        Assert.True(client.GearMythsoulBag.Update);
        Assert.Same(first, Assert.Single(cached));
        Assert.Same(incoming, Assert.Single(client.GearMythsoulBag.GetValues()));
        Assert.False(client.GearMythsoulBag.Update);
    }

    [Fact]
    public void MutableMythsoulRunePositionAddsThenReturnsFalseWithoutDroppingFallthrough()
    {
        ushort original = LegacyNativeGameItemRuntime.MythsoulBag;
        try
        {
            LegacyNativeGameItemRuntime.MythsoulBag = 101;
            var client = Owners();
            client.Player = null!;
            client.Rune = null;
            var item = Item(71);
            var record = Record(900, 101);

            Assert.False(Routing.TryCompleteNonWarehouseStoragePrefix(client, ref record, item));

            Assert.Same(item, client.GearMythsoulBag![71]);
            Assert.Equal((ushort)101, record.Position); // Future caller still owes native Rune condition.
            Assert.Equal((ushort)9, item.Inscribed);
        }
        finally { LegacyNativeGameItemRuntime.MythsoulBag = original; }
    }

    [Fact]
    public void IndependentMythsoulRunsBeforeRelicAndSharesTheActualItem()
    {
        ushort original = LegacyNativeGameItemRuntime.MythsoulBag;
        try
        {
            LegacyNativeGameItemRuntime.MythsoulBag = 213;
            var client = Owners();
            client.Player = null!;
            var item = Item(71);
            var record = Record(900, 213);
            Assert.True(Routing.TryCompleteNonWarehouseStoragePrefix(client, ref record, item));
            Assert.Same(item, client.GearMythsoulBag![71]);
            Assert.Same(item, client.RelicFuse![71]);
            Assert.Equal((ushort)9, item.Inscribed);
        }
        finally { LegacyNativeGameItemRuntime.MythsoulBag = original; }
    }

    [Fact]
    public void IndependentMythsoulMutationSurvivesTheFollowingRelicFault()
    {
        ushort original = LegacyNativeGameItemRuntime.MythsoulBag;
        try
        {
            LegacyNativeGameItemRuntime.MythsoulBag = 213;
            var client = Owners();
            client.RelicFuse = null;
            var item = Item(71);
            var record = Record(900, 213);
            Assert.Throws<NullReferenceException>(() =>
                Routing.TryCompleteNonWarehouseStoragePrefix(client, ref record, item));
            Assert.Same(item, client.GearMythsoulBag![71]);
            Assert.Equal((ushort)9, item.Inscribed);
        }
        finally { LegacyNativeGameItemRuntime.MythsoulBag = original; }
    }

    [Fact]
    public void MissingMythsoulFaultsBeforeRelicInsertion()
    {
        ushort original = LegacyNativeGameItemRuntime.MythsoulBag;
        try
        {
            LegacyNativeGameItemRuntime.MythsoulBag = 213;
            var client = Owners();
            client.GearMythsoulBag = null;
            var item = Item(71);
            var record = Record(900, 213);
            Assert.Throws<NullReferenceException>(() =>
                Routing.TryCompleteNonWarehouseStoragePrefix(client, ref record, item));
            Assert.Empty(client.RelicFuse!);
        }
        finally { LegacyNativeGameItemRuntime.MythsoulBag = original; }
    }

    [Fact]
    public void IndependentMythsoulSharesEquipmentReferenceBeforeInscriptionClear()
    {
        ushort original = LegacyNativeGameItemRuntime.MythsoulBag;
        try
        {
            LegacyNativeGameItemRuntime.MythsoulBag = 1;
            var client = Owners();
            var item = Item(71);
            var record = Record(900, 1);
            Assert.True(Routing.TryCompleteNonWarehouseStoragePrefix(client, ref record, item));
            Assert.Same(item, client.GearMythsoulBag![71]);
            Assert.Same(item, client.Equipment!.ClientItems[900]);
            Assert.Equal((ushort)0, client.GearMythsoulBag[71]!.Inscribed);
        }
        finally { LegacyNativeGameItemRuntime.MythsoulBag = original; }
    }

    [Fact]
    public void IndependentMythsoulSharesInventoryReferenceBeforeInscriptionClear()
    {
        ushort original = LegacyNativeGameItemRuntime.MythsoulBag;
        try
        {
            LegacyNativeGameItemRuntime.MythsoulBag = 0;
            var client = Owners();
            var item = Item(71);
            var record = Record(900, 0);
            Assert.True(Routing.TryCompleteNonWarehouseStoragePrefix(client, ref record, item));
            Assert.Same(item, client.GearMythsoulBag![71]);
            Assert.Same(item, client.Inventory!.ClientItems[71]);
            Assert.Equal((ushort)0, client.GearMythsoulBag[71]!.Inscribed);
        }
        finally { LegacyNativeGameItemRuntime.MythsoulBag = original; }
    }

    [Fact]
    public void RelicPositions213Through215UseItemUidNotEscapedRecordUid()
    {
        foreach (ushort position in new ushort[] { 213, 214, 215 })
        {
            var client = Owners();
            var item = Item(71);
            var record = Record(900, position);
            var before = record;
            Assert.True(Routing.TryCompleteNonWarehouseStoragePrefix(client, ref record, item));
            Assert.Same(item, client.RelicFuse![71]);
            Assert.False(client.RelicFuse.ContainsKey(900));
            Assert.Equal((ushort)9, item.Inscribed);
            Assert.Equal(before, record);
        }
    }

    [Fact]
    public void RelicDuplicateOverwritesInsteadOfThrowingOrRetainingFirst()
    {
        var client = Owners();
        var first = Item(71);
        client.RelicFuse!.Add(71, first);
        var cached = client.RelicFuse.GetValues();
        var incoming = Item(71);
        var record = Record(900, 215);
        Assert.True(Routing.TryCompleteNonWarehouseStoragePrefix(client, ref record, incoming));
        Assert.Same(incoming, client.RelicFuse[71]);
        Assert.True(client.RelicFuse.Update);
        Assert.Same(first, Assert.Single(cached));
        Assert.Same(incoming, Assert.Single(client.RelicFuse.GetValues()));
    }

    [Fact]
    public void RelicArmDoesNotReadPlayerEquipmentInventoryOrRune()
    {
        var client = Owners();
        client.Player = null!;
        client.Equipment = null;
        client.Inventory = null;
        client.Rune = null;
        var item = Item(71);
        var record = Record(900, 214);
        Assert.True(Routing.TryCompleteNonWarehouseStoragePrefix(client, ref record, item));
        Assert.Same(item, client.RelicFuse![71]);
        Assert.Equal((ushort)9, item.Inscribed);
    }

    [Fact]
    public void EquipmentPositionsOneThrough63UseEscapedRecordPositionAndUid()
    {
        foreach (ushort position in new ushort[] { 1, 59, 60, 63 })
        {
            var client = Owners();
            var item = Item(71);
            var child = Item(72);
            item.Deposite.TryAdd(72, child);
            var record = Record(900, position);
            var before = record;
            Assert.True(Routing.TryCompleteNonWarehouseStoragePrefix(client, ref record, item));
            Assert.Same(item, client.Equipment!.ClientItems[900]);
            Assert.False(client.Equipment.ClientItems.ContainsKey(71));
            Assert.Same(child, item.Deposite[72]);
            Assert.Equal((ushort)8, item.Position);
            Assert.Equal(7U, item.WH_ID);
            Assert.Equal(before, record);
        }
    }

    [Fact]
    public void EquipmentDuplicateRetainsFirstAfterIncomingInscriptionClear()
    {
        var client = Owners();
        var first = Item(72);
        client.Equipment!.ClientItems.TryAdd(900, first);
        var incoming = Item(71);
        var record = Record(900, 63);
        Assert.True(Routing.TryCompleteNonWarehouseStoragePrefix(client, ref record, incoming));
        Assert.Same(first, client.Equipment.ClientItems[900]);
        Assert.Equal((ushort)0, incoming.Inscribed);
        Assert.Equal((ushort)9, first.Inscribed);
    }

    [Fact]
    public void EquipmentGuildMembershipPreservesInscription()
    {
        var client = Owners();
        client.Player.GuildID = 123;
        var item = Item(71);
        var record = Record(900, 1);
        Assert.True(Routing.TryCompleteNonWarehouseStoragePrefix(client, ref record, item));
        Assert.Same(item, client.Equipment!.ClientItems[900]);
        Assert.Equal((ushort)9, item.Inscribed);
    }

    [Fact]
    public void MissingEquipmentFaultsAfterGuildZeroInscriptionClear()
    {
        var client = Owners();
        client.Equipment = null;
        var item = Item(71);
        var record = Record(900, 1);
        Assert.Throws<NullReferenceException>(() =>
            Routing.TryCompleteNonWarehouseStoragePrefix(client, ref record, item));
        Assert.Equal((ushort)0, item.Inscribed);
        Assert.Empty(client.Inventory!.ClientItems);
    }

    [Fact]
    public void MissingPlayerFaultsBeforeEquipmentAttemptOrInscriptionMutation()
    {
        var client = Owners();
        client.Player = null!;
        var item = Item(71);
        var record = Record(900, 1);
        Assert.Throws<NullReferenceException>(() =>
            Routing.TryCompleteNonWarehouseStoragePrefix(client, ref record, item));
        Assert.Equal((ushort)9, item.Inscribed);
        Assert.Empty(client.Equipment!.ClientItems);
    }

    [Fact]
    public void InventoryZeroPositionUsesItemUidAndDoesNotNormalizeItemPosition()
    {
        var client = Owners();
        var item = Item(71);
        var record = Record(900, 0);
        var before = record;
        Assert.True(Routing.TryCompleteNonWarehouseStoragePrefix(client, ref record, item));
        Assert.Same(item, client.Inventory!.ClientItems[71]);
        Assert.False(client.Inventory.ClientItems.ContainsKey(900));
        Assert.Equal((ushort)8, item.Position);
        Assert.Equal(7U, item.WH_ID);
        Assert.Equal((ushort)0, item.Inscribed);
        Assert.Equal(before, record);
    }

    [Fact]
    public void InventoryDuplicateRetainsFirstAfterIncomingInscriptionClear()
    {
        var client = Owners();
        var first = Item(71);
        client.Inventory!.AddDBItem(first);
        var incoming = Item(71);
        var record = Record(900, 0);
        Assert.True(Routing.TryCompleteNonWarehouseStoragePrefix(client, ref record, incoming));
        Assert.Same(first, client.Inventory.ClientItems[71]);
        Assert.Equal((ushort)0, incoming.Inscribed);
        Assert.Equal((ushort)9, first.Inscribed);
    }

    [Fact]
    public void InventoryGuildMembershipPreservesInscriptionWithoutTouchingEquipment()
    {
        var client = Owners();
        client.Player.GuildID = 123;
        client.Equipment = null;
        var item = Item(71);
        var record = Record(900, 0);
        Assert.True(Routing.TryCompleteNonWarehouseStoragePrefix(client, ref record, item));
        Assert.Same(item, client.Inventory!.ClientItems[71]);
        Assert.Equal((ushort)9, item.Inscribed);
    }

    [Fact]
    public void MissingInventoryFaultsAfterGuildZeroInscriptionClear()
    {
        var client = Owners();
        client.Inventory = null;
        var item = Item(71);
        var record = Record(900, 0);
        Assert.Throws<NullReferenceException>(() =>
            Routing.TryCompleteNonWarehouseStoragePrefix(client, ref record, item));
        Assert.Equal((ushort)0, item.Inscribed);
        Assert.Empty(client.Equipment!.ClientItems);
    }

    [Fact]
    public void MissingPlayerFaultsBeforeInventoryAttemptOrInscriptionMutation()
    {
        var client = Owners();
        client.Player = null!;
        var item = Item(71);
        var record = Record(900, 0);
        Assert.Throws<NullReferenceException>(() =>
            Routing.TryCompleteNonWarehouseStoragePrefix(client, ref record, item));
        Assert.Equal((ushort)9, item.Inscribed);
        Assert.Empty(client.Inventory!.ClientItems);
    }

    [Fact]
    public void UnrepresentedPositionsReturnFalseWithoutOwnerOrItemAccess()
    {
        foreach (ushort position in new ushort[] { 64, 100, 101, 135, 136, 211, 212, 216, ushort.MaxValue })
        {
            var record = Record(900, position);
            var before = record;
            Assert.False(Routing.TryCompleteNonWarehouseStoragePrefix(null!, ref record, null!));
            Assert.Equal(before, record);
        }
    }

    [Fact]
    public void ZeroUidIsStoredLiterallyWithoutCounterOrCheakUpFallback()
    {
        var client = Owners();
        var item = Item(0);
        var record = Record(uint.MaxValue, 0);
        Assert.True(Routing.TryCompleteNonWarehouseStoragePrefix(client, ref record, item));
        Assert.Equal(0U, item.Uid);
        Assert.Same(item, client.Inventory!.ClientItems[0]);
    }

    [Fact]
    public void SharedOuterReferenceSurvivesMultipleEscapedEquipmentKeys()
    {
        var client = Owners();
        client.Player.GuildID = 123;
        var item = Item(71);
        var record = Record(900, 1);
        Assert.True(Routing.TryCompleteNonWarehouseStoragePrefix(client, ref record, item));
        client.Player.GuildID = 0;
        record.UID = 901;
        record.Position = 63;
        Assert.True(Routing.TryCompleteNonWarehouseStoragePrefix(client, ref record, item));
        Assert.Same(item, client.Equipment!.ClientItems[900]);
        Assert.Same(item, client.Equipment.ClientItems[901]);
        Assert.Equal((ushort)0, client.Equipment.ClientItems[900].Inscribed);
        Assert.Equal(71U, item.Uid);
    }

    // Explicit real owner phases in a serialized fixture, not a native birth/caller implementation.
    private static LegacyNativeGameClientRuntime Owners()
    {
        var client = new LegacyNativeGameClientRuntime();
        LegacyNativeItemContainerMemberPhases.InitializeGearMythsoulBagAtNativeFieldSlot(client);
        LegacyNativeItemContainerMemberPhases.InitializeRelicFuseAtNativeFieldSlot(client);
        LegacyNativeItemContainerMemberPhases.AssignLoadClientItemOwnersPrefix(client.Player);
        return client;
    }

    private static LegacyNativeGameItemRuntime Item(uint uid) =>
        new() { Uid = uid, ITEM_ID = 181305, Position = 8, WH_ID = 7, Inscribed = 9 };

    private static LegacyNativeClientItemRecord Record(uint uid, ushort position) =>
        new() { UID = uid, Position = position, WH_ID = 0 };
}
