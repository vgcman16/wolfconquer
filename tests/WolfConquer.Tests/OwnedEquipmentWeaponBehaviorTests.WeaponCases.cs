using System.Buffers.Binary;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class OwnedEquipmentWeaponBehaviorTests
{
    [Theory]
    [MemberData(nameof(AcceptedWeaponCases))]
    public Task AcceptedWeaponCasePreservesOriginalFramesAndSavedOwnership(string original, uint itemId, long classCode,
        ushort requested, uint right, ushort rightSlot, uint left, ushort leftSlot, ushort expected)
    {
        AssertItemClassifications(itemId, right, left);
        var worn = new List<PlayerItemSnapshotItem>();
        if (right != 0) worn.Add(Row(3001, right, rightSlot));
        if (left != 0) worn.Add(Row(3002, left, leftSlot));
        var inventory = new[] { Row(2001, itemId, 0) };
        var graph = new PlayerItemSnapshot(inventory, [], [], worn.ToArray(), []);
        var data = new OwnedEquipmentBehaviorInput(OwnedEquipmentBehaviorFixture.Character(classCode: classCode), graph);
        var metadata = worn.Select(row => row.ItemId).Append(itemId).Distinct().Select(id => OwnedEquipmentBehaviorFixture.Item(id)).ToArray();
        return OwnedEquipmentBehaviorFixture.RunAsync(data, OwnedEquipmentBehaviorFixture.Catalog(metadata), async f =>
        {
            f.Log.WriteLine("Original case: " + original);
            var start = f.ActorFrames.Count;
            var expectedBytes = File.ReadAllBytes(f.ItemPath());
            var rowCount = BinaryPrimitives.ReadInt32LittleEndian(expectedBytes);
            var matches = 0;
            for (var index = 0; index < rowCount; index++)
            {
                var offset = 4 + index * PlayerItemSnapshotLoader.LegacyDbItemSize;
                if (BinaryPrimitives.ReadUInt32LittleEndian(expectedBytes.AsSpan(offset, 4)) != 2001) continue;
                BinaryPrimitives.WriteUInt16LittleEndian(expectedBytes.AsSpan(offset + 12, 2), expected); matches++;
            }
            Assert.Equal(1, matches);
            f.BeforeSend = (uid, _) => { if (uid == OwnedEquipmentBehaviorFixture.Actor) Assert.Equal(expectedBytes, File.ReadAllBytes(f.ItemPath())); };
            await f.DispatchAsync(Equip(requested));
            f.BeforeSend = null;
            Assert.Equal(expectedBytes, File.ReadAllBytes(f.ItemPath()));
            var frames = f.ActorFrames.Skip(start).ToArray();
            var alternate = expected >= 50;
            Assert.Equal(alternate ? 6 : 13, frames.Length);
            Usage(frames[0], 2001, 0, MsgItemUsageAction.RemoveInventory);
            var firstItem = 1;
            if (alternate) { Usage(frames[1], 2001, expected, MsgItemUsageAction.Unequip); firstItem = 2; }
            ItemFrame(frames[firstItem], 2001, itemId, (byte)expected, LegacyGameItemPacket.AddItemMode);
            ItemFrame(frames[firstItem + 1], 2001, itemId, (byte)expected, LegacyGameItemPacket.UpdateItemMode);
            if (alternate) LegacySelectedEquipmentQueryAssertions.InactiveAlternate(frames, 4, 2001, expected);
            else LegacySelectedEquipmentQueryAssertions.NeutralQuery(frames, 3, OwnedEquipmentBehaviorFixture.Actor);
            Assert.Empty(f.Current().PlayerItems.InventoryItems);
            var moved = Assert.Single(f.Current().PlayerItems.EquipmentItems, row => row.Uid == 2001);
            Assert.Equal(expected, moved.Position); Assert.Equal(itemId, moved.ItemId);
            foreach (var previous in worn)
                Assert.Contains(f.Current().PlayerItems.EquipmentItems, row => row.Uid == previous.Uid && row.Position == previous.Position && row.ItemId == previous.ItemId);
            var saved = new PlayerItemSnapshotLoader(f.Data.Items).LoadByCharacterUid(OwnedEquipmentBehaviorFixture.Actor);
            Assert.Contains(saved.EquipmentItems, row => row.Uid == 2001 && row.Position == expected && row.ItemId == itemId);
            Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
            Assert.Contains("persistedBeforeReply=True", f.Log.ToString(), StringComparison.Ordinal);
            Assert.True(LegacySecondaryPasswordRuntime.IsAdmitted(f.Connection(OwnedEquipmentBehaviorFixture.Actor), f.Current()));
        });
    }

    [Theory]
    [MemberData(nameof(RefusedWeaponCases))]
    public Task RefusedWeaponCaseHitsSpecificRuleWithoutMutation(string original, uint itemId, long classCode,
        ushort requested, uint right, ushort rightSlot, uint left, ushort leftSlot)
    {
        AssertItemClassifications(itemId, right, left);
        var worn = new List<PlayerItemSnapshotItem>();
        if (right != 0) worn.Add(Row(3001, right, rightSlot));
        if (left != 0) worn.Add(Row(3002, left, leftSlot));
        var graph = new PlayerItemSnapshot([Row(2001, itemId, 0)], [], [], worn.ToArray(), []);
        var data = new OwnedEquipmentBehaviorInput(OwnedEquipmentBehaviorFixture.Character(classCode: classCode), graph);
        var metadata = worn.Select(row => row.ItemId).Append(itemId).Distinct().Select(id => OwnedEquipmentBehaviorFixture.Item(id)).ToArray();
        return OwnedEquipmentBehaviorFixture.RunAsync(data, OwnedEquipmentBehaviorFixture.Catalog(metadata), async f =>
        {
            f.Log.WriteLine("Original case: " + original);
            var before = f.Data.Files();
            var owner = f.Current();
            var count = f.ActorFrames.Count;
            Assert.True(LegacySecondaryPasswordRuntime.IsAdmitted(f.Connection(OwnedEquipmentBehaviorFixture.Actor), owner));
            await f.DispatchAsync(Equip(requested));
            Assert.Contains("reason=equipmentMoveSlotOrWeaponRuleRefused", f.Log.ToString(), StringComparison.Ordinal);
            Assert.Equal(count, f.ActorFrames.Count);
            Assert.Same(owner, f.Current());
            Assert.True(LegacySecondaryPasswordRuntime.IsAdmitted(f.Connection(OwnedEquipmentBehaviorFixture.Actor), f.Current()));
            f.Data.AssertFiles(before);
            Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
        });
    }
}
