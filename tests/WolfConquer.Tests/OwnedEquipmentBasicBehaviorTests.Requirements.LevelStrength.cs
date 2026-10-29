using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class OwnedEquipmentBasicBehaviorTests
{
    [Fact]
    public Task HandlerItemUsageEquipSkipsItemWhenCharacterLevelIsBelowRequiredLevel()
    {
        var catalog = RequirementCatalog(410003);
        return OwnedEquipmentBehaviorFixture.RunAsync(new OwnedEquipmentBehaviorInput(OwnedEquipmentBehaviorFixture.Character(uid: 1293600, level: 4, reborn: 0), ParseItems(PlayerItemSnapshotLoaderTests.BuildPlayerItems(
                    [
                        PlayerItemSnapshotLoaderTests.DbItem(uid: 2001, itemId: 410003, position: 0),
                    ],
                    []))), catalog, async f =>
        {
            var sent = f.ActorFrames;
            var log = f.Log;
            var worldData = catalog;
            AssertRealOwner(f, OwnedEquipmentBehaviorFixture.Actor);
            var unchanged = f.Data.Files();
            var original = f.Current(OwnedEquipmentBehaviorFixture.Actor);
            AssertSingleFailedRequirement(f.Current().Character, catalog.ItemsById[410003][0], "level");
            Assert.Equal(5u, worldData.ItemsById[410003][0].RequiredLevel);
            Assert.Equal((ushort)4, ItemType.GetLegacyEquipmentPosition(410003));

            var countBeforeRequest = sent.Count;
            await f.SendAsync(OwnedEquipmentBehaviorFixture.Actor, new MsgItemUsagePacket(
                    Id: 2001,
                    DwParam: 4,
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
    public Task HandlerItemUsageEquipMovesItemWhenCharacterLevelMeetsRequiredLevel()
    {
        var catalog = RequirementCatalog(410003);
        return OwnedEquipmentBehaviorFixture.RunAsync(new OwnedEquipmentBehaviorInput(OwnedEquipmentBehaviorFixture.Character(uid: 1293600, level: 5, reborn: 0), ParseItems(PlayerItemSnapshotLoaderTests.BuildPlayerItems(
                    [
                        PlayerItemSnapshotLoaderTests.DbItem(uid: 2001, itemId: 410003, position: 0),
                    ],
                    []))), catalog, async f =>
        {
            var sent = f.ActorFrames;
            var log = f.Log;
            var worldData = catalog;
            AssertRealOwner(f, OwnedEquipmentBehaviorFixture.Actor);
            Assert.Equal(5u, worldData.ItemsById[410003][0].RequiredLevel);
            Assert.Equal((ushort)4, ItemType.GetLegacyEquipmentPosition(410003));

            var countBeforeRequest = sent.Count;
            await f.SendAsync(OwnedEquipmentBehaviorFixture.Actor, new MsgItemUsagePacket(
                    Id: 2001,
                    DwParam: 4,
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
            Assert.Equal(410_003U, addedItem.ItemId);
            Assert.Equal(LegacyGameItemPacket.AddItemMode, addedItem.Mode);
            Assert.Equal((byte)4, addedItem.Position);

            Assert.True(LegacyGameItemPacket.TryParse(ReadFrame(sent[countBeforeRequest + 2]), out var updatedItem));
            Assert.Equal(2001U, updatedItem.Uid);
            Assert.Equal(410_003U, updatedItem.ItemId);
            Assert.Equal(LegacyGameItemPacket.UpdateItemMode, updatedItem.Mode);
            Assert.Equal((byte)4, updatedItem.Position);
            Assert.Contains("handled MsgItemUsage equip", log.ToString());
        });
    }

    [Fact]
    public Task HandlerItemUsageEquipSkipsItemWhenCharacterStrengthIsBelowRequiredStrength()
    {
        var catalog = RequirementCatalog(410023);
        return OwnedEquipmentBehaviorFixture.RunAsync(new OwnedEquipmentBehaviorInput(OwnedEquipmentBehaviorFixture.Character(uid: 1293600, level: 15, reborn: 1, strength: 27), ParseItems(PlayerItemSnapshotLoaderTests.BuildPlayerItems(
                    [
                        PlayerItemSnapshotLoaderTests.DbItem(uid: 2001, itemId: 410023, position: 0),
                    ],
                    []))), catalog, async f =>
        {
            var sent = f.ActorFrames;
            var log = f.Log;
            var worldData = catalog;
            AssertRealOwner(f, OwnedEquipmentBehaviorFixture.Actor);
            var unchanged = f.Data.Files();
            var original = f.Current(OwnedEquipmentBehaviorFixture.Actor);
            AssertSingleFailedRequirement(f.Current().Character, catalog.ItemsById[410023][0], "strength");
            Assert.Equal(15u, worldData.ItemsById[410023][0].RequiredLevel);
            Assert.Equal(28u, worldData.ItemsById[410023][0].RequiredStrength);
            Assert.Equal(0u, worldData.ItemsById[410023][0].RequiredAgility);
            Assert.Equal((ushort)4, ItemType.GetLegacyEquipmentPosition(410023));

            var countBeforeRequest = sent.Count;
            await f.SendAsync(OwnedEquipmentBehaviorFixture.Actor, new MsgItemUsagePacket(
                    Id: 2001,
                    DwParam: 4,
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
    public Task HandlerItemUsageEquipMovesItemWhenCharacterStatsMeetRequirements()
    {
        var catalog = RequirementCatalog(410023);
        return OwnedEquipmentBehaviorFixture.RunAsync(new OwnedEquipmentBehaviorInput(OwnedEquipmentBehaviorFixture.Character(uid: 1293600, level: 15, reborn: 1, strength: 28), ParseItems(PlayerItemSnapshotLoaderTests.BuildPlayerItems(
                    [
                        PlayerItemSnapshotLoaderTests.DbItem(uid: 2001, itemId: 410023, position: 0),
                    ],
                    []))), catalog, async f =>
        {
            var sent = f.ActorFrames;
            var log = f.Log;
            var worldData = catalog;
            AssertRealOwner(f, OwnedEquipmentBehaviorFixture.Actor);
            Assert.Equal(15u, worldData.ItemsById[410023][0].RequiredLevel);
            Assert.Equal(28u, worldData.ItemsById[410023][0].RequiredStrength);
            Assert.Equal(0u, worldData.ItemsById[410023][0].RequiredAgility);
            Assert.Equal((ushort)4, ItemType.GetLegacyEquipmentPosition(410023));

            var countBeforeRequest = sent.Count;
            await f.SendAsync(OwnedEquipmentBehaviorFixture.Actor, new MsgItemUsagePacket(
                    Id: 2001,
                    DwParam: 4,
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
            Assert.Equal(410_023U, addedItem.ItemId);
            Assert.Equal(LegacyGameItemPacket.AddItemMode, addedItem.Mode);
            Assert.Equal((byte)4, addedItem.Position);

            Assert.True(LegacyGameItemPacket.TryParse(ReadFrame(sent[countBeforeRequest + 2]), out var updatedItem));
            Assert.Equal(2001U, updatedItem.Uid);
            Assert.Equal(410_023U, updatedItem.ItemId);
            Assert.Equal(LegacyGameItemPacket.UpdateItemMode, updatedItem.Mode);
            Assert.Equal((byte)4, updatedItem.Position);
            Assert.Contains("handled MsgItemUsage equip", log.ToString());
        });
    }
}
