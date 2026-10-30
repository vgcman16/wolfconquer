using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using Owners = WolfConquer.Core.World.LegacyNativeItemLoadOwners;
using D = WolfConquer.Core.World.LegacyNativeItemLoadOwners.Destination;
using P = WolfConquer.Core.World.LegacyNativeItemLoadOwners.Placement;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyNativeItemLoadOwnerTests
{
    private sealed class Scope : LegacyNativeRuneStorageTestOwner
    {
        internal Scope()
        {
            Client.Player.UID = 17;
            Client.Inventory = new(Client); Client.Warehouse = new(Client); Client.MyWardrobe = new(Client);
            Client.RelicFuse = new(); Client.GearMythsoulBag = new(); Client.DemonExterminator = new();
        }
        internal Owners Capture(D required) => Owners.Capture(Client, Client.Player, required);
    }

    private static LegacyNativeGameItemRuntime Item(uint uid, ushort position = 0, uint warehouse = 0, uint id = 410009) =>
        new() { Uid = uid, Position = position, WH_ID = warehouse, ITEM_ID = id };

    [Fact]
    public void MixedPreparedPublicationRetainsRealOwnersOldRowsAndNewWardrobeEquipmentAlias()
    {
        using var scope = new Scope(); var client = scope.Client;
        var inventory = client.Inventory; var equipment = client.Equipment; var wardrobe = client.MyWardrobe;
        var warehouse = client.Warehouse; var rune = client.Rune; var relic = client.RelicFuse; var gear = client.GearMythsoulBag;
        var old = Item(1); inventory!.AddDBItem(old);
        var indexed = Item(2, 213); relic!.Add(900, indexed); relic.GetValues(); long revision = relic.Revision;
        var oldRune = Item(3, 212, id:181305); Assert.True(rune!.Add(oldRune));
        var garment = Item(10, 9, 100, 181305); var carried = Item(11); var banked = Item(12, warehouse:900);
        var newRune = Item(13, 101, id:181305); var fused = Item(14, 213); var myth = Item(15, LegacyNativeGameItemRuntime.MythsoulBag);
        var jar = Item(77, id:750000); carried.Deposite.TryAdd(77, jar);
        var rows = new P[] { new(garment,D.WardrobeGarment), new(garment,D.Equipment), new(carried,D.Inventory),
            new(banked,D.Warehouse,900), new(newRune,D.Rune), new(fused,D.Relic), new(myth,D.Gear) };
        using (var owners = scope.Capture(D.WardrobeGarment|D.Equipment|D.Inventory|D.Warehouse|D.Rune|D.Relic|D.Gear))
        {
            owners.Prepare(rows,77); Assert.Same(old,Assert.Single(inventory.ClientItems).Value);
            Assert.False(equipment!.ClientItems.ContainsKey(10)); Assert.Equal(0u,client.DemonExterminator!.ItemUID);
            owners.RequireCurrent(); owners.PublishPrepared();
        }
        Assert.Same(inventory,client.Inventory); Assert.Same(equipment,client.Equipment); Assert.Same(wardrobe,client.MyWardrobe);
        Assert.Same(warehouse,client.Warehouse); Assert.Same(rune,client.Rune); Assert.Same(relic,client.RelicFuse); Assert.Same(gear,client.GearMythsoulBag);
        Assert.Same(old,inventory.ClientItems[1]); Assert.Same(carried,inventory.ClientItems[11]); Assert.Same(jar,carried.Deposite[77]);
        Assert.Same(garment,equipment!.ClientItems[10]); Assert.Same(garment,wardrobe!.Items[0][10]);
        Assert.Same(banked,warehouse!.ClientItems[900][12]); Assert.Equal(new[]{oldRune,newRune},rune!.Objects);
        Assert.Same(indexed,relic[900]); Assert.Same(fused,relic[14]); Assert.Same(myth,gear![15]);
        Assert.Equal(revision+1,relic.Revision); Assert.True(relic.Update); Assert.Equal(new[]{indexed,fused},relic.GetValues());
        Assert.Equal(77u,client.DemonExterminator!.ItemUID); Assert.Null(client.Socket); Assert.False(client.FullLoading);
    }

    [Theory]
    [InlineData("missing-rune")] [InlineData("foreign-wardrobe")] [InlineData("missing-bank-map")]
    public void AnyPlacementRequiresTheActualCompleteGlobalIdentityGraph(string invalid)
    {
        using var scope = new Scope(); var client=scope.Client; var map=client.Inventory!.ClientItems;
        if(invalid=="missing-rune") client.Rune=null;
        if(invalid=="foreign-wardrobe") client.MyWardrobe=new(new LegacyNativeGameClientRuntime());
        if(invalid=="missing-bank-map") client.Warehouse!.ClientItems=null!;
        Assert.Throws<InvalidDataException>(()=>scope.Capture(D.Inventory));
        Assert.Same(map,client.Inventory.ClientItems); Assert.Empty(map); Assert.Null(client.Socket);
        Assert.False(Monitor.IsEntered(client.SpellAdmissionSyncRoot));
    }

    [Fact]
    public void IncomingUidCollisionInUntouchedRuneRejectsAllDetachedMapAndMarkerChanges()
    {
        using var scope=new Scope(); var old=Item(7,212,id:181305); Assert.True(scope.Rune.Add(old));
        var inventory=scope.Client.Inventory!.ClientItems;
        using var owners=scope.Capture(D.Inventory);
        Assert.Throws<InvalidDataException>(()=>owners.Prepare(new[]{new P(Item(7),D.Inventory)},null));
        Assert.Same(inventory,scope.Client.Inventory.ClientItems); Assert.Empty(inventory);
        Assert.Same(old,Assert.Single(scope.Rune.Objects)); Assert.Equal(0u,scope.Client.DemonExterminator!.ItemUID);
    }

    [Fact]
    public void LateRuneAdmissionFailureDoesNotPublishEarlierPreparedInventory()
    {
        using var scope=new Scope(); var old=Item(1,101,id:181305); Assert.True(scope.Rune.Add(old));
        var inventory=scope.Client.Inventory!.ClientItems; var carried=Item(2);
        using var owners=scope.Capture(D.Inventory|D.Rune);
        Assert.Throws<InvalidDataException>(()=>owners.Prepare(new[]{new P(carried,D.Inventory),new P(Item(3,101,id:181305),D.Rune)},null));
        Assert.Same(inventory,scope.Client.Inventory.ClientItems); Assert.Empty(inventory); Assert.Same(old,Assert.Single(scope.Rune.Objects));
    }

    [Fact]
    public void DeliberateRawMembershipChangeIsDetectedWithoutPretendingToUndoTheExternalWrite()
    {
        using var scope=new Scope(); var map=scope.Client.Inventory!.ClientItems;
        using var owners=scope.Capture(D.Inventory); owners.Prepare(new[]{new P(Item(2),D.Inventory)},null);
        var external=Item(90); Assert.True(map.TryAdd(90,external));
        Assert.Throws<InvalidDataException>(owners.RequireCurrent);
        Assert.Same(map,scope.Client.Inventory.ClientItems); Assert.Same(external,Assert.Single(map).Value); Assert.False(map.ContainsKey(2));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void MissingQuestOwnerIsNotConsumedUntilARealLoadedJarMarkerIsRequested(bool jar)
    {
        using var scope=new Scope(); scope.Client.DemonExterminator=null;
        var item=Item(2,id:jar?750000u:410009u); var map=scope.Client.Inventory!.ClientItems;
        using var owners=scope.Capture(D.Inventory);
        if(jar)
        {
            Assert.Throws<InvalidDataException>(()=>owners.Prepare(new[]{new P(item,D.Inventory)},2));
            Assert.Same(map,scope.Client.Inventory.ClientItems); Assert.Empty(map);
        }
        else
        {
            owners.Prepare(new[]{new P(item,D.Inventory)},null); owners.RequireCurrent(); owners.PublishPrepared();
            Assert.Same(item,scope.Client.Inventory.ClientItems[2]);
        }
    }

    [Fact]
    public void NativeBagIndexCollisionDoesNotOverwriteAnOldDifferentUidOrPublishPreparedInventory()
    {
        using var scope=new Scope(); var bag=scope.Client.RelicFuse!; var kept=Item(2,213);
        bag.Add(900,kept); long revision=bag.Revision; var map=scope.Client.Inventory!.ClientItems;
        using var owners=scope.Capture(D.Inventory|D.Relic);
        Assert.Throws<ArgumentException>(()=>owners.Prepare(new[]{new P(Item(3),D.Inventory),new P(Item(900,213),D.Relic)},null));
        Assert.Same(kept,Assert.Single(bag).Value); Assert.Same(kept,bag[900]); Assert.Equal(revision,bag.Revision);
        Assert.Same(map,scope.Client.Inventory.ClientItems); Assert.Empty(map);
    }

    [Fact]
    public void RealBagRevisionExhaustionRefusesBeforeOtherPreparedOwnersPublish()
    {
        using var scope=new Scope(); var bag=scope.Client.RelicFuse!; var map=scope.Client.Inventory!.ClientItems;
        typeof(LegacyNativeDbSafeDictionary<uint,LegacyNativeGameItemRuntime>)
            .GetField("revision",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.SetValue(bag,long.MaxValue);
        using var owners=scope.Capture(D.Inventory|D.Relic);
        Assert.Throws<OverflowException>(()=>owners.Prepare(new[]{new P(Item(2),D.Inventory),new P(Item(3,213),D.Relic)},null));
        Assert.Same(map,scope.Client.Inventory.ClientItems); Assert.Empty(map); Assert.Empty(bag); Assert.Equal(long.MaxValue,bag.Revision);
    }
}
