using System.Buffers;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class OwnedEquipmentBasicBehaviorTests
{
    [Fact]
    public Task HandlerItemUsageUnequipMovesEquipmentToInventoryAndRefreshesVisiblePlayerSpawn()
    {
        const int UidField = 2;
        const int RightWeaponField = 12;
        const int RightWeaponSoulField = 67;
        var catalog = Catalog(Item(500109));
        return OwnedEquipmentBehaviorFixture.RunAsync(new OwnedEquipmentBehaviorInput(OwnedEquipmentBehaviorFixture.Character(uid: 1293600, mapId: 1015, x: 830, y: 584), PlayerItemSnapshot.Empty), catalog, async f =>
        {
            var sent = f.ActorFrames;
            var log = f.Log;
            var itemsRoot = f.Data.Items;
            var otherSent = f.Frames(OwnedEquipmentBehaviorFixture.Peer);
            AssertRealOwner(f, OwnedEquipmentBehaviorFixture.Peer);
            var peerCountBefore = sent.Count;
            var selfCountBefore = otherSent.Count;
            await f.SendAsync(OwnedEquipmentBehaviorFixture.Peer, new MsgItemUsagePacket(
                    Id: 0,
                    DwParam: 4,
                    Action: MsgItemUsageAction.Unequip,
                    Timestamp: 1234,
                    DwParam2: 0,
                    DwParam3: 0,
                    DwParam4: 0).ToFrameBytes());

            Assert.Equal(selfCountBefore + 12, otherSent.Count);
            var sourceQuery = LegacySelectedEquipmentQueryAssertions.NeutralQuery(otherSent, selfCountBefore + 2, 1_293_601U);
            AssertItemUsage(
                otherSent[selfCountBefore],
                new MsgItemUsagePacket(
                    Id: 2001,
                    DwParam: 4,
                    Action: MsgItemUsageAction.Unequip,
                    Timestamp: 0,
                    DwParam2: 0,
                    DwParam3: 0,
                    DwParam4: 0));
            Assert.True(LegacyGameItemPacket.TryParse(ReadFrame(otherSent[selfCountBefore + 1]), out var movedItem));
            Assert.Equal(2001U, movedItem.Uid);
            Assert.Equal(500_109U, movedItem.ItemId);
            Assert.Equal(LegacyGameItemPacket.AddItemMode, movedItem.Mode);
            Assert.Equal((byte)0, movedItem.Position);
            var equipmentRefresh = sourceQuery.Equipment;
            Assert.Equal(LegacyShowEquipmentPacket.ShowAction, equipmentRefresh.Action);
            Assert.Equal(0U, equipmentRefresh.Alternante);
            Assert.Equal(0U, equipmentRefresh.EquipmentSlots.RightWeapon);

            Assert.Equal(peerCountBefore + 1, sent.Count);
            Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(sent[^1]), out var refreshedFrame, out _, out _));
            Assert.True(LegacyPlayerSpawnPacket.TryParse(refreshedFrame, out var refreshedSpawn));
            Assert.Equal(1_293_601UL, refreshedSpawn.GetNumericField(UidField));
            Assert.Equal(0UL, refreshedSpawn.GetNumericField(RightWeaponField));
            Assert.Equal(0UL, refreshedSpawn.GetNumericField(RightWeaponSoulField));
            var persistedItems = new PlayerItemSnapshotLoader(itemsRoot).LoadByCharacterUid(1_293_601);
            var persistedItem = Assert.Single(persistedItems.InventoryItems);
            Assert.Equal(2001U, persistedItem.Uid);
            Assert.Equal(500_109U, persistedItem.ItemId);
            Assert.Empty(persistedItems.EquipmentItems);
            Assert.Contains("handled MsgItemUsage unequip", log.ToString());
            Assert.Contains("persistedBeforeReply=True", log.ToString());
        }, new OwnedEquipmentBehaviorInput(OwnedEquipmentBehaviorFixture.Character(uid: 1293601, mapId: 1015, x: 840, y: 590), ParseItems(PlayerItemSnapshotLoaderTests.BuildPlayerItems(
                    [
                        PlayerItemSnapshotLoaderTests.DbItem(
                            uid: 2001,
                            itemId: 500109,
                            position: 4,
                            purificationItemId: 800004),
                    ],
                    []))));
    }

    [Fact]
    public Task HandlerItemUsageEquipMovesInventoryItemToEmptyEquipmentSlotAndRefreshesVisiblePlayerSpawn()
    {
        const int UidField = 2;
        const int HeadField = 8;
        const int HeadSoulField = 64;
        var catalog = Catalog(Item(111003));
        return OwnedEquipmentBehaviorFixture.RunAsync(new OwnedEquipmentBehaviorInput(OwnedEquipmentBehaviorFixture.Character(uid: 1293600, mapId: 1015, x: 830, y: 584), PlayerItemSnapshot.Empty), catalog, async f =>
        {
            var sent = f.ActorFrames;
            var log = f.Log;
            var otherSent = f.Frames(OwnedEquipmentBehaviorFixture.Peer);
            AssertRealOwner(f, OwnedEquipmentBehaviorFixture.Peer);
            var peerCountBefore = sent.Count;
            var selfCountBefore = otherSent.Count;
            await f.SendAsync(OwnedEquipmentBehaviorFixture.Peer, new MsgItemUsagePacket(
                    Id: 2001,
                    DwParam: 1,
                    Action: MsgItemUsageAction.Equip,
                    Timestamp: 1234,
                    DwParam2: 0,
                    DwParam3: 0,
                    DwParam4: 0).ToFrameBytes());

            Assert.Equal(selfCountBefore + 13, otherSent.Count);
            LegacySelectedEquipmentQueryAssertions.NeutralQuery(otherSent, selfCountBefore + 3, 1_293_601U);
            AssertItemUsage(
                otherSent[selfCountBefore],
                new MsgItemUsagePacket(
                    Id: 2001,
                    DwParam: 0,
                    Action: MsgItemUsageAction.RemoveInventory,
                    Timestamp: 0,
                    DwParam2: 0,
                    DwParam3: 0,
                    DwParam4: 0));
            Assert.True(LegacyGameItemPacket.TryParse(ReadFrame(otherSent[selfCountBefore + 1]), out var addedItem));
            Assert.Equal(2001U, addedItem.Uid);
            Assert.Equal(111_003U, addedItem.ItemId);
            Assert.Equal(LegacyGameItemPacket.AddItemMode, addedItem.Mode);
            Assert.Equal((byte)1, addedItem.Position);

            Assert.True(LegacyGameItemPacket.TryParse(ReadFrame(otherSent[selfCountBefore + 2]), out var updatedItem));
            Assert.Equal(2001U, updatedItem.Uid);
            Assert.Equal(111_003U, updatedItem.ItemId);
            Assert.Equal(LegacyGameItemPacket.UpdateItemMode, updatedItem.Mode);
            Assert.Equal((byte)1, updatedItem.Position);

            Assert.Equal(peerCountBefore + 1, sent.Count);
            Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(sent[^1]), out var refreshedFrame, out _, out _));
            Assert.True(LegacyPlayerSpawnPacket.TryParse(refreshedFrame, out var refreshedSpawn));
            Assert.Equal(1_293_601UL, refreshedSpawn.GetNumericField(UidField));
            Assert.Equal(111_003UL, refreshedSpawn.GetNumericField(HeadField));
            Assert.Equal(800_001UL, refreshedSpawn.GetNumericField(HeadSoulField));
            Assert.Contains("handled MsgItemUsage equip", log.ToString());
        }, new OwnedEquipmentBehaviorInput(OwnedEquipmentBehaviorFixture.Character(uid: 1293601, mapId: 1015, x: 840, y: 590), ParseItems(PlayerItemSnapshotLoaderTests.BuildPlayerItems(
                    [
                        PlayerItemSnapshotLoaderTests.DbItem(
                            uid: 2001,
                            itemId: 111003,
                            position: 0,
                            purificationItemId: 800001),
                    ],
                    []))));
    }

    [Fact]
    public Task HandlerItemUsageEquipArmorRefreshesVisibleBodyAppearance()
    {
        const int UidField = 2;
        const int ArmorField = 10;
        const int ArmorSoulField = 65;
        var catalog = Catalog(Item(130269));
        return OwnedEquipmentBehaviorFixture.RunAsync(new OwnedEquipmentBehaviorInput(OwnedEquipmentBehaviorFixture.Character(uid: 1293600, mapId: 1015, x: 830, y: 584), PlayerItemSnapshot.Empty), catalog, async f =>
        {
            var sent = f.ActorFrames;
            var log = f.Log;
            var otherSent = f.Frames(OwnedEquipmentBehaviorFixture.Peer);
            AssertRealOwner(f, OwnedEquipmentBehaviorFixture.Peer);
            var peerCountBefore = sent.Count;
            var selfCountBefore = otherSent.Count;
            await f.SendAsync(OwnedEquipmentBehaviorFixture.Peer, new MsgItemUsagePacket(
                    Id: 2001,
                    DwParam: 3,
                    Action: MsgItemUsageAction.Equip,
                    Timestamp: 1234,
                    DwParam2: 0,
                    DwParam3: 0,
                    DwParam4: 0).ToFrameBytes());

            Assert.Equal(selfCountBefore + 13, otherSent.Count);
            var sourceQuery = LegacySelectedEquipmentQueryAssertions.NeutralQuery(otherSent, selfCountBefore + 3, 1_293_601U);
            AssertItemUsage(
                otherSent[selfCountBefore],
                new MsgItemUsagePacket(
                    Id: 2001,
                    DwParam: 0,
                    Action: MsgItemUsageAction.RemoveInventory,
                    Timestamp: 0,
                    DwParam2: 0,
                    DwParam3: 0,
                    DwParam4: 0));
            var equipmentRefresh = sourceQuery.Equipment;
            Assert.Equal(2001U, equipmentRefresh.EquipmentSlots.Armor);

            Assert.Equal(peerCountBefore + 1, sent.Count);
            Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(sent[^1]), out var refreshedFrame, out _, out _));
            Assert.True(LegacyPlayerSpawnPacket.TryParse(refreshedFrame, out var refreshedSpawn));
            Assert.Equal(1_293_601UL, refreshedSpawn.GetNumericField(UidField));
            Assert.Equal(130_269UL, refreshedSpawn.GetNumericField(ArmorField));
            Assert.Equal(800_003UL, refreshedSpawn.GetNumericField(ArmorSoulField));

            var equipped = Assert.Single(f.Current(OwnedEquipmentBehaviorFixture.Peer).PlayerItems.EquipmentItems);
            Assert.Equal((ushort)3, equipped.Position);
        }, new OwnedEquipmentBehaviorInput(OwnedEquipmentBehaviorFixture.Character(uid: 1293601, mapId: 1015, x: 840, y: 590), ParseItems(PlayerItemSnapshotLoaderTests.BuildPlayerItems(
                    [
                        PlayerItemSnapshotLoaderTests.DbItem(
                            uid: 2001,
                            itemId: 130269,
                            position: 0,
                            purificationItemId: 800003),
                    ],
                    []))));
    }
}
