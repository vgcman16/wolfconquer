using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class OwnedEquipmentWeaponBehaviorTests
{
    [Theory]
    [InlineData("HandlerItemUsageEquipMovesInventoryItemToAlternateEquipmentSlotAndRefreshesActiveSpawn", (ushort)51, false)]
    [InlineData("HandlerItemUsageEquipReplacesOccupiedAlternateEquipmentSlotAndRefreshesActiveSpawn", (ushort)51, true)]
    [InlineData("HandlerItemUsageEquipReplacesOccupiedSimpleEquipmentSlot", (ushort)1, true)]
    public Task HeadMoveRetainsSelectedQueryAndNearbyAppearance(string original, ushort slot, bool occupied)
    {
        var equipment = occupied ? new[] { Row(3001, 112003, slot) } : Array.Empty<PlayerItemSnapshotItem>();
        var actor = new OwnedEquipmentBehaviorInput(OwnedEquipmentBehaviorFixture.Character(OwnedEquipmentBehaviorFixture.Peer,
            x: 840, y: 590), new PlayerItemSnapshot([Row(2001, 111003, 0)], [], [], equipment, []));
        var viewer = new OwnedEquipmentBehaviorInput(OwnedEquipmentBehaviorFixture.Character(OwnedEquipmentBehaviorFixture.Actor), PlayerItemSnapshot.Empty);
        return OwnedEquipmentBehaviorFixture.RunAsync(actor,
            OwnedEquipmentBehaviorFixture.Catalog(OwnedEquipmentBehaviorFixture.Item(111003), OwnedEquipmentBehaviorFixture.Item(112003)), async f =>
            {
                f.Log.WriteLine("Original case: " + original);
                var uid = OwnedEquipmentBehaviorFixture.Peer;
                if (slot == 51) { await f.ToggleAsync(uid); Assert.True(f.Current(uid).AlternateEquipmentActive); }
                var selfStart = f.Frames(uid).Count; var viewerStart = f.Frames(OwnedEquipmentBehaviorFixture.Actor).Count;
                await f.DispatchAsync(Equip(slot), uid);
                var frames = f.Frames(uid).Skip(selfStart).ToArray();
                Assert.Equal(slot == 51 ? (occupied ? 16 : 15) : 14, frames.Length);
                Usage(frames[0], 2001, 0, MsgItemUsageAction.RemoveInventory);
                var cursor = 1;
                if (occupied)
                {
                    Usage(frames[cursor++], 3001, slot, MsgItemUsageAction.Unequip);
                    ItemFrame(frames[cursor++], 3001, 112003, 0, LegacyGameItemPacket.AddItemMode);
                }
                if (slot == 51) Usage(frames[cursor++], 2001, slot, MsgItemUsageAction.Unequip);
                ItemFrame(frames[cursor++], 2001, 111003, (byte)slot, LegacyGameItemPacket.AddItemMode);
                if (!occupied) ItemFrame(frames[cursor++], 2001, 111003, (byte)slot, LegacyGameItemPacket.UpdateItemMode);
                if (slot == 51) LegacySelectedEquipmentQueryAssertions.AlternateNotification(frames[cursor++], 5, 2001, 51);
                var query = LegacySelectedEquipmentQueryAssertions.NeutralQuery(frames, cursor, uid, alternate: slot == 51);
                Assert.Equal(2001U, query.Equipment.EquipmentSlots.Head);
                Assert.Equal(viewerStart + 1, f.Frames(OwnedEquipmentBehaviorFixture.Actor).Count);
                Assert.True(LegacyPlayerSpawnPacket.TryParse(Frame(f.Frames(OwnedEquipmentBehaviorFixture.Actor)[^1]), out var spawn));
                Assert.Equal((ulong)uid, spawn.GetNumericField(2)); Assert.Equal(111003UL, spawn.GetNumericField(8));
                var saved = new PlayerItemSnapshotLoader(f.Data.Items).LoadByCharacterUid(uid);
                Assert.Contains(saved.EquipmentItems, item => item.Uid == 2001 && item.Position == slot);
                if (occupied) Assert.Contains(saved.InventoryItems, item => item.Uid == 3001);
                Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
            }, viewer);
    }

    [Theory]
    [InlineData("HandlerItemUsageEquipMovesInventoryItemToAlternateSimpleEquipmentSlot", 120001u, (ushort)2, (ushort)52, (ushort)52)]
    [InlineData("HandlerItemUsageEquipMovesInventoryItemToAlternateSimpleEquipmentSlot", 150000u, (ushort)6, (ushort)56, (ushort)56)]
    [InlineData("HandlerItemUsageEquipMovesInventoryItemToAlternateSimpleEquipmentSlot", 160013u, (ushort)8, (ushort)58, (ushort)58)]
    [InlineData("HandlerItemUsageEquipMovesInventoryItemToAlternateSimpleEquipmentSlot", 4100000u, (ushort)13, (ushort)63, (ushort)63)]
    [InlineData("HandlerItemUsageEquipAlternateSimpleSlotUsesItemPositionWhenRequestDoesNotMatch", 150000u, (ushort)6, (ushort)52, (ushort)56)]
    public Task AlternateSimpleSlotPreservesSourceFallbackAndItemFrames(string original, uint id, ushort basePosition, ushort requested, ushort expected)
    {
        Assert.Equal(basePosition, ItemType.GetLegacyEquipmentPosition(id));
        var actor = new OwnedEquipmentBehaviorInput(OwnedEquipmentBehaviorFixture.Character(), new PlayerItemSnapshot([Row(2001, id, 0)], [], [], [], []));
        return OwnedEquipmentBehaviorFixture.RunAsync(actor, OwnedEquipmentBehaviorFixture.Catalog(OwnedEquipmentBehaviorFixture.Item(id)), async f =>
        {
            f.Log.WriteLine("Original case: " + original);
            var start = f.ActorFrames.Count;
            await f.DispatchAsync(Equip(requested));
            var frames = f.ActorFrames.Skip(start).ToArray();
            Assert.Equal(6, frames.Length);
            Usage(frames[0], 2001, 0, MsgItemUsageAction.RemoveInventory);
            Usage(frames[1], 2001, expected, MsgItemUsageAction.Unequip);
            ItemFrame(frames[2], 2001, id, (byte)expected, LegacyGameItemPacket.AddItemMode);
            ItemFrame(frames[3], 2001, id, (byte)expected, LegacyGameItemPacket.UpdateItemMode);
            LegacySelectedEquipmentQueryAssertions.InactiveAlternate(frames, 4, 2001, expected);
            Assert.Contains(f.Current().PlayerItems.EquipmentItems, row => row.Uid == 2001 && row.Position == expected);
            var saved = new PlayerItemSnapshotLoader(f.Data.Items).LoadByCharacterUid(OwnedEquipmentBehaviorFixture.Actor);
            Assert.Contains(saved.EquipmentItems, row => row.Uid == 2001 && row.Position == expected);
        });
    }
}
