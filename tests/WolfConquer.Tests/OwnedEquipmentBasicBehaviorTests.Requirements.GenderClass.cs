using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class OwnedEquipmentBasicBehaviorTests
{
    [Fact]
    public Task HandlerItemUsageEquipSkipsItemWhenCharacterBodyDoesNotMatchRequiredGender()
    {
        var catalog = RequirementCatalog(150320);
        return OwnedEquipmentBehaviorFixture.RunAsync(new OwnedEquipmentBehaviorInput(OwnedEquipmentBehaviorFixture.Character(uid: 1293600, level: 20, reborn: 0, body: 1008), ParseItems(PlayerItemSnapshotLoaderTests.BuildPlayerItems(
                    [
                        PlayerItemSnapshotLoaderTests.DbItem(uid: 2001, itemId: 150320, position: 0),
                    ],
                    []))), catalog, async f =>
        {
            var sent = f.ActorFrames;
            var log = f.Log;
            var worldData = catalog;
            AssertRealOwner(f, OwnedEquipmentBehaviorFixture.Actor);
            var unchanged = f.Data.Files();
            var original = f.Current(OwnedEquipmentBehaviorFixture.Actor);
            AssertSingleFailedRequirement(f.Current().Character, catalog.ItemsById[150320][0], "gender");
            Assert.Equal(20u, worldData.ItemsById[150320][0].RequiredLevel);
            Assert.Equal(2u, worldData.ItemsById[150320][0].RequiredGender);
            Assert.Equal(0u, worldData.ItemsById[150320][0].RequiredStrength);
            Assert.Equal(0u, worldData.ItemsById[150320][0].RequiredAgility);
            Assert.Equal((ushort)6, ItemType.GetLegacyEquipmentPosition(150320));

            var countBeforeRequest = sent.Count;
            await f.SendAsync(OwnedEquipmentBehaviorFixture.Actor, new MsgItemUsagePacket(
                    Id: 2001,
                    DwParam: 6,
                    Action: MsgItemUsageAction.Equip,
                    Timestamp: 1234,
                    DwParam2: 0,
                    DwParam3: 0,
                    DwParam4: 0).ToFrameBytes());

            Assert.Equal(countBeforeRequest, sent.Count);
            Assert.Contains("skipped MsgItemUsage equip", log.ToString());
            Assert.Contains("reason=equipmentMoveRequirementsRefused;", log.ToString());
            Assert.Same(original, f.Current(OwnedEquipmentBehaviorFixture.Actor));
            f.Data.AssertFiles(unchanged);
            Assert.False(f.Recovery.IsBlocked);
            Assert.DoesNotContain("equipmentMoveOwnerUnavailable", log.ToString());
            Assert.DoesNotContain("equipmentMoveCatalogUnavailableOrInvalid", log.ToString());
        });
    }

    [Fact]
    public Task HandlerItemUsageEquipMovesItemWhenCharacterBodyMatchesRequiredGender()
    {
        var catalog = RequirementCatalog(150320);
        return OwnedEquipmentBehaviorFixture.RunAsync(new OwnedEquipmentBehaviorInput(OwnedEquipmentBehaviorFixture.Character(uid: 1293600, level: 20, reborn: 0, body: 2001), ParseItems(PlayerItemSnapshotLoaderTests.BuildPlayerItems(
                    [
                        PlayerItemSnapshotLoaderTests.DbItem(uid: 2001, itemId: 150320, position: 0),
                    ],
                    []))), catalog, async f =>
        {
            var sent = f.ActorFrames;
            var log = f.Log;
            var worldData = catalog;
            AssertRealOwner(f, OwnedEquipmentBehaviorFixture.Actor);
            Assert.Equal(20u, worldData.ItemsById[150320][0].RequiredLevel);
            Assert.Equal(2u, worldData.ItemsById[150320][0].RequiredGender);
            Assert.Equal(0u, worldData.ItemsById[150320][0].RequiredStrength);
            Assert.Equal(0u, worldData.ItemsById[150320][0].RequiredAgility);
            Assert.Equal((ushort)6, ItemType.GetLegacyEquipmentPosition(150320));

            var countBeforeRequest = sent.Count;
            await f.SendAsync(OwnedEquipmentBehaviorFixture.Actor, new MsgItemUsagePacket(
                    Id: 2001,
                    DwParam: 6,
                    Action: MsgItemUsageAction.Equip,
                    Timestamp: 1234,
                    DwParam2: 0,
                    DwParam3: 0,
                    DwParam4: 0).ToFrameBytes());

            Assert.Equal(countBeforeRequest + 14, sent.Count);
            LegacySelectedEquipmentQueryAssertions.PirateRankQuery(sent, countBeforeRequest + 3, 1_293_600U);
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

            Assert.True(LegacyGameItemPacket.TryParse(ReadFrame(sent[countBeforeRequest + 1]), out var addedItem));
            Assert.Equal(2001U, addedItem.Uid);
            Assert.Equal(150_320U, addedItem.ItemId);
            Assert.Equal(LegacyGameItemPacket.AddItemMode, addedItem.Mode);
            Assert.Equal((byte)6, addedItem.Position);

            Assert.True(LegacyGameItemPacket.TryParse(ReadFrame(sent[countBeforeRequest + 2]), out var updatedItem));
            Assert.Equal(2001U, updatedItem.Uid);
            Assert.Equal(150_320U, updatedItem.ItemId);
            Assert.Equal(LegacyGameItemPacket.UpdateItemMode, updatedItem.Mode);
            Assert.Equal((byte)6, updatedItem.Position);
            Assert.Contains("handled MsgItemUsage equip", log.ToString());
        });
    }

    [Fact]
    public Task HandlerItemUsageEquipSkipsItemWhenCharacterClassDoesNotMatchRequiredClass()
    {
        var catalog = RequirementCatalog(101000);
        return OwnedEquipmentBehaviorFixture.RunAsync(new OwnedEquipmentBehaviorInput(OwnedEquipmentBehaviorFixture.Character(uid: 1293600, level: 15, reborn: 0, classCode: 7051), ParseItems(PlayerItemSnapshotLoaderTests.BuildPlayerItems(
                    [
                        PlayerItemSnapshotLoaderTests.DbItem(uid: 2001, itemId: 101000, position: 0),
                    ],
                    []))), catalog, async f =>
        {
            var sent = f.ActorFrames;
            var log = f.Log;
            var worldData = catalog;
            AssertRealOwner(f, OwnedEquipmentBehaviorFixture.Actor);
            var unchanged = f.Data.Files();
            var original = f.Current(OwnedEquipmentBehaviorFixture.Actor);
            AssertSingleFailedRequirement(f.Current().Character, catalog.ItemsById[101000][0], "class");
            Assert.Equal(16000u, worldData.ItemsById[101000][0].RequiredClass);
            Assert.Equal(15u, worldData.ItemsById[101000][0].RequiredLevel);
            Assert.Equal(0u, worldData.ItemsById[101000][0].RequiredGender);
            Assert.Equal(0u, worldData.ItemsById[101000][0].RequiredStrength);
            Assert.Equal(0u, worldData.ItemsById[101000][0].RequiredAgility);
            Assert.Equal((ushort)3, ItemType.GetLegacyEquipmentPosition(101000));

            var countBeforeRequest = sent.Count;
            await f.SendAsync(OwnedEquipmentBehaviorFixture.Actor, new MsgItemUsagePacket(
                    Id: 2001,
                    DwParam: 3,
                    Action: MsgItemUsageAction.Equip,
                    Timestamp: 1234,
                    DwParam2: 0,
                    DwParam3: 0,
                    DwParam4: 0).ToFrameBytes());

            Assert.Equal(countBeforeRequest, sent.Count);
            Assert.Contains("skipped MsgItemUsage equip", log.ToString());
            Assert.Contains("reason=equipmentMoveRequirementsRefused;", log.ToString());
            Assert.Same(original, f.Current(OwnedEquipmentBehaviorFixture.Actor));
            f.Data.AssertFiles(unchanged);
            Assert.False(f.Recovery.IsBlocked);
            Assert.DoesNotContain("equipmentMoveOwnerUnavailable", log.ToString());
            Assert.DoesNotContain("equipmentMoveCatalogUnavailableOrInvalid", log.ToString());
        });
    }

    [Fact]
    public Task HandlerItemUsageEquipMovesItemWhenCharacterClassMatchesRequiredClass()
    {
        var catalog = RequirementCatalog(101000);
        return OwnedEquipmentBehaviorFixture.RunAsync(new OwnedEquipmentBehaviorInput(OwnedEquipmentBehaviorFixture.Character(uid: 1293600, level: 15, reborn: 0, classCode: 16057), ParseItems(PlayerItemSnapshotLoaderTests.BuildPlayerItems(
                    [
                        PlayerItemSnapshotLoaderTests.DbItem(uid: 2001, itemId: 101000, position: 0),
                    ],
                    []))), catalog, async f =>
        {
            var sent = f.ActorFrames;
            var log = f.Log;
            var itemsRoot = f.Data.Items;
            var worldData = catalog;
            AssertRealOwner(f, OwnedEquipmentBehaviorFixture.Actor);
            Assert.Equal(16000u, worldData.ItemsById[101000][0].RequiredClass);
            Assert.Equal(15u, worldData.ItemsById[101000][0].RequiredLevel);
            Assert.Equal(0u, worldData.ItemsById[101000][0].RequiredGender);
            Assert.Equal(0u, worldData.ItemsById[101000][0].RequiredStrength);
            Assert.Equal(0u, worldData.ItemsById[101000][0].RequiredAgility);
            Assert.Equal((ushort)3, ItemType.GetLegacyEquipmentPosition(101000));

            var countBeforeRequest = sent.Count;
            await f.SendAsync(OwnedEquipmentBehaviorFixture.Actor, new MsgItemUsagePacket(
                    Id: 2001,
                    DwParam: 3,
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

            Assert.True(LegacyGameItemPacket.TryParse(ReadFrame(sent[countBeforeRequest + 1]), out var addedItem));
            Assert.Equal(2001U, addedItem.Uid);
            Assert.Equal(101_000U, addedItem.ItemId);
            Assert.Equal(LegacyGameItemPacket.AddItemMode, addedItem.Mode);
            Assert.Equal((byte)3, addedItem.Position);

            Assert.True(LegacyGameItemPacket.TryParse(ReadFrame(sent[countBeforeRequest + 2]), out var updatedItem));
            Assert.Equal(2001U, updatedItem.Uid);
            Assert.Equal(101_000U, updatedItem.ItemId);
            Assert.Equal(LegacyGameItemPacket.UpdateItemMode, updatedItem.Mode);
            Assert.Equal((byte)3, updatedItem.Position);
            var persistedItems = new PlayerItemSnapshotLoader(itemsRoot).LoadByCharacterUid(1_293_600);
            Assert.Empty(persistedItems.InventoryItems);
            var persistedItem = Assert.Single(persistedItems.EquipmentItems);
            Assert.Equal(2001U, persistedItem.Uid);
            Assert.Equal(101_000U, persistedItem.ItemId);
            Assert.Equal((ushort)3, persistedItem.Position);
            Assert.Contains("handled MsgItemUsage equip", log.ToString());
            Assert.Contains("persistedBeforeReply=True", log.ToString());
        });
    }
}
