using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class OwnedEquipmentBasicBehaviorTests
{
    [Fact]
    public Task HandlerItemUsageEquipUsesNaturalSlotWhenClientRequestsZero()
    {
        var catalog = Catalog(Item(410003));
        return OwnedEquipmentBehaviorFixture.RunAsync(new OwnedEquipmentBehaviorInput(OwnedEquipmentBehaviorFixture.Character(uid: 1293600), ParseItems(PlayerItemSnapshotLoaderTests.BuildPlayerItems(
                    [
                        PlayerItemSnapshotLoaderTests.DbItem(uid: 2001, itemId: 410003, position: 0),
                    ],
                    []))), catalog, async f =>
        {
            var sent = f.ActorFrames;
            var log = f.Log;
            AssertRealOwner(f, OwnedEquipmentBehaviorFixture.Actor);
            var countBeforeRequest = sent.Count;
            await f.SendAsync(OwnedEquipmentBehaviorFixture.Actor, new MsgItemUsagePacket(
                    Id: 2001,
                    DwParam: 0,
                    Action: MsgItemUsageAction.Equip,
                    Timestamp: 1234,
                    DwParam2: 0,
                    DwParam3: 0,
                    DwParam4: 0).ToFrameBytes());

            Assert.Equal(countBeforeRequest + 13, sent.Count);
            LegacySelectedEquipmentQueryAssertions.NeutralQuery(sent, countBeforeRequest + 3, 1_293_600U);
            AssertItemUsage(
                sent[countBeforeRequest],
                new MsgItemUsagePacket(
                    Id: 2001,
                    DwParam: 0,
                    Action: MsgItemUsageAction.RemoveInventory,
                    Timestamp: 0,
                    DwParam2: 0,
                    DwParam3: 0,
                    DwParam4: 0));

            var addedItem = ParseLegacyGameItem(sent[countBeforeRequest + 1]);
            Assert.Equal(2001U, addedItem.Uid);
            Assert.Equal(410_003U, addedItem.ItemId);
            Assert.Equal(LegacyGameItemPacket.AddItemMode, addedItem.Mode);
            Assert.Equal((byte)4, addedItem.Position);

            var updatedItem = ParseLegacyGameItem(sent[countBeforeRequest + 2]);
            Assert.Equal(2001U, updatedItem.Uid);
            Assert.Equal(410_003U, updatedItem.ItemId);
            Assert.Equal(LegacyGameItemPacket.UpdateItemMode, updatedItem.Mode);
            Assert.Equal((byte)4, updatedItem.Position);

            Assert.Contains("handled MsgItemUsage equip", log.ToString());
            Assert.Equal((ushort)4, Assert.Single(f.Current().PlayerItems.EquipmentItems).Position);
            Assert.DoesNotContain("requestedSlot=0", log.ToString());
        });
    }

    [Fact]
    public Task HandlerItemUsageEquipDefersClickedNonEquipmentItemUseWhenClientRequestsZero()
    {
        var catalog = Catalog(Item(3335125), Item(3321108));
        return OwnedEquipmentBehaviorFixture.RunAsync(new OwnedEquipmentBehaviorInput(OwnedEquipmentBehaviorFixture.Character(uid: 1293600), ParseItems(PlayerItemSnapshotLoaderTests.BuildPlayerItems(
                    [
                        PlayerItemSnapshotLoaderTests.DbItem(uid: 2001, itemId: 3_335_125, position: 0),
                        PlayerItemSnapshotLoaderTests.DbItem(uid: 2002, itemId: 3_321_108, position: 0),
                    ],
                    []))), catalog, async f =>
        {
            var sent = f.ActorFrames;
            var log = f.Log;
            AssertRealOwner(f, OwnedEquipmentBehaviorFixture.Actor);
            var unchanged = f.Data.Files();
            var original = f.Current(OwnedEquipmentBehaviorFixture.Actor);
            foreach (var (uid, itemTypeId) in new[] { (2001U, 3_335_125U), (2002U, 3_321_108U) })
            {
                Assert.Equal((ushort)0, ItemType.GetLegacyEquipmentPosition(itemTypeId));
                var countBeforeRequest = sent.Count;
                await f.SendAsync(OwnedEquipmentBehaviorFixture.Actor, new MsgItemUsagePacket(
                        Id: uid,
                        DwParam: 0,
                        Action: MsgItemUsageAction.Equip,
                        Timestamp: 1234,
                        DwParam2: 0,
                        DwParam3: 0,
                        DwParam4: 0).ToFrameBytes());

                Assert.Equal(countBeforeRequest, sent.Count);
            }

            var output = log.ToString();
            Assert.Contains("deferred MsgItemUsage item use", output);
            Assert.Contains("itemId=3335125", output);
            Assert.Contains("itemId=3321108", output);
            Assert.DoesNotContain("requestedSlot=0", output);
            Assert.Same(original, f.Current(OwnedEquipmentBehaviorFixture.Actor));
            f.Data.AssertFiles(unchanged);
            Assert.False(f.Recovery.IsBlocked);
            Assert.DoesNotContain("equipmentMoveOwnerUnavailable", log.ToString());
            Assert.DoesNotContain("equipmentMoveCatalogUnavailableOrInvalid", log.ToString());
        });
    }

    [Fact]
    public Task HandlerItemUsageUnequipSkipsSpecialSlots()
    {
        var catalog = Catalog(Item(181325), Item(200000));
        return OwnedEquipmentBehaviorFixture.RunAsync(new OwnedEquipmentBehaviorInput(OwnedEquipmentBehaviorFixture.Character(uid: 1293600), ParseItems(PlayerItemSnapshotLoaderTests.BuildPlayerItems(
                    [
                        PlayerItemSnapshotLoaderTests.DbItem(uid: 2001, itemId: 181325, position: 9),
                        PlayerItemSnapshotLoaderTests.DbItem(uid: 2002, itemId: 200000, position: 17),
                    ],
                    []))), catalog, async f =>
        {
            var sent = f.ActorFrames;
            var log = f.Log;
            AssertRealOwner(f, OwnedEquipmentBehaviorFixture.Actor);
            var unchanged = f.Data.Files();
            var original = f.Current(OwnedEquipmentBehaviorFixture.Actor);
            var countBeforeRequests = sent.Count;
            await f.SendAsync(OwnedEquipmentBehaviorFixture.Actor, new MsgItemUsagePacket(
                    Id: 0,
                    DwParam: 9,
                    Action: MsgItemUsageAction.Unequip,
                    Timestamp: 1234,
                    DwParam2: 0,
                    DwParam3: 0,
                    DwParam4: 0).ToFrameBytes());
            await f.SendAsync(OwnedEquipmentBehaviorFixture.Actor, new MsgItemUsagePacket(
                    Id: 0,
                    DwParam: 17,
                    Action: MsgItemUsageAction.Unequip,
                    Timestamp: 1235,
                    DwParam2: 0,
                    DwParam3: 0,
                    DwParam4: 0).ToFrameBytes());

            Assert.Equal(countBeforeRequests, sent.Count);
            Assert.Contains("reason=equipmentMoveSlotInvalid;", log.ToString());
            Assert.Same(original, f.Current(OwnedEquipmentBehaviorFixture.Actor));
            f.Data.AssertFiles(unchanged);
            Assert.False(f.Recovery.IsBlocked);
            Assert.DoesNotContain("equipmentMoveOwnerUnavailable", log.ToString());
            Assert.DoesNotContain("equipmentMoveCatalogUnavailableOrInvalid", log.ToString());
        });
    }
}
