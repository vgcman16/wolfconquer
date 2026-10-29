using System.Buffers.Binary;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class OwnedEquipmentWeaponBehaviorTests
{
    [Theory]
    [InlineData("HandlerItemUsageEquipAlternateTwoHandWeaponClearsAlternateLeftHandSlot", (ushort)54, false)]
    [InlineData("HandlerItemUsageEquipTwoHandWeaponClearsOccupiedLeftHandSlot", (ushort)4, false)]
    [InlineData("HandlerItemUsageEquipTwoHandWeaponIntoOccupiedRightHandSlotKeepsLeftHandEquipped", (ushort)4, true)]
    public Task TwoHandEquipDisplacesLeftAndOccupiedRightInSourceOrder(string original, ushort target, bool occupiedRight)
    {
        AssertItemClassifications(500109, 410003, 410003);
        var alternate = target == 54;
        var leftSlot = (ushort)(alternate ? 55 : 5);
        var leftUid = occupiedRight ? 4001u : 3001u;
        var equipped = new List<PlayerItemSnapshotItem> { Row(leftUid, 410003, leftSlot) };
        if (occupiedRight) equipped.Add(Row(3001, 410003, 4));
        var graph = new PlayerItemSnapshot([Row(2001, 500109, 0)], [], [], equipped.ToArray(), []);
        return OwnedEquipmentBehaviorFixture.RunAsync(
            new(OwnedEquipmentBehaviorFixture.Character(), graph),
            OwnedEquipmentBehaviorFixture.Catalog(OwnedEquipmentBehaviorFixture.Item(500109), OwnedEquipmentBehaviorFixture.Item(410003)), async f =>
            {
                f.Log.WriteLine("Original case: " + original);
                var start = f.ActorFrames.Count;
                var expectedBytes = File.ReadAllBytes(f.ItemPath());
                var count = BinaryPrimitives.ReadInt32LittleEndian(expectedBytes);
                var found = new HashSet<uint>();
                for (var index = 0; index < count; index++)
                {
                    var offset = 4 + index * PlayerItemSnapshotLoader.LegacyDbItemSize;
                    var uid = BinaryPrimitives.ReadUInt32LittleEndian(expectedBytes.AsSpan(offset, 4));
                    if (uid != 2001 && uid != leftUid && (!occupiedRight || uid != 3001)) continue;
                    Assert.True(found.Add(uid));
                    BinaryPrimitives.WriteUInt16LittleEndian(expectedBytes.AsSpan(offset + 12, 2), uid == 2001 ? target : (ushort)0);
                }
                Assert.Equal(occupiedRight ? 3 : 2, found.Count);
                f.BeforeSend = (uid, _) => { if (uid == OwnedEquipmentBehaviorFixture.Actor) Assert.Equal(expectedBytes, File.ReadAllBytes(f.ItemPath())); };
                await f.DispatchAsync(Equip(target));
                f.BeforeSend = null;
                Assert.Equal(expectedBytes, File.ReadAllBytes(f.ItemPath()));
                var frames = f.ActorFrames.Skip(start).ToArray();
                Assert.Equal(occupiedRight ? 16 : alternate ? 8 : 15, frames.Length);
                Usage(frames[0], 2001, 0, MsgItemUsageAction.RemoveInventory);
                var cursor = 1;
                if (occupiedRight)
                {
                    Usage(frames[cursor++], 3001, 4, MsgItemUsageAction.Unequip);
                    ItemFrame(frames[cursor++], 3001, 410003, 0, LegacyGameItemPacket.AddItemMode);
                }
                Usage(frames[cursor++], leftUid, leftSlot, MsgItemUsageAction.Unequip);
                ItemFrame(frames[cursor++], leftUid, 410003, 0, LegacyGameItemPacket.AddItemMode);
                if (alternate) Usage(frames[cursor++], 2001, target, MsgItemUsageAction.Unequip);
                ItemFrame(frames[cursor++], 2001, 500109, (byte)target, LegacyGameItemPacket.AddItemMode);
                if (!occupiedRight) ItemFrame(frames[cursor++], 2001, 500109, (byte)target, LegacyGameItemPacket.UpdateItemMode);
                if (alternate) LegacySelectedEquipmentQueryAssertions.InactiveAlternate(frames, cursor, 2001, target);
                else LegacySelectedEquipmentQueryAssertions.NeutralQuery(frames, cursor, OwnedEquipmentBehaviorFixture.Actor);
                var current = f.Current().PlayerItems;
                Assert.Contains(current.EquipmentItems, item => item.Uid == 2001 && item.Position == target);
                Assert.Contains(current.InventoryItems, item => item.Uid == leftUid && item.Position == 0);
                if (occupiedRight) Assert.Contains(current.InventoryItems, item => item.Uid == 3001 && item.Position == 0);
                Assert.DoesNotContain(current.EquipmentItems, item => item.Position == leftSlot);
                var saved = new PlayerItemSnapshotLoader(f.Data.Items).LoadByCharacterUid(OwnedEquipmentBehaviorFixture.Actor);
                Assert.Contains(saved.EquipmentItems, item => item.Uid == 2001 && item.Position == target);
                Assert.Contains(saved.InventoryItems, item => item.Uid == leftUid);
                if (occupiedRight) Assert.Contains(saved.InventoryItems, item => item.Uid == 3001);
                Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
            });
    }
}
