using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyEquipmentDurabilityTests
{
    [Fact]
    public Task ActualFrameEquipThenUnequipPersistsExactPositionPatchBeforeSuccessAndPreservesOpaqueBytes() => Run(async f =>
    {
        Assert.Equal((ushort)2137, MsgItemUsagePacket.Opcode);
        var character = File.ReadAllBytes(f.CharacterPath);
        var initial = File.ReadAllBytes(f.ItemsPath);
        var registry = File.ReadAllBytes(f.Data.State);
        var inventoryRow = Assert.Single(f.Current.PlayerItems.InventoryItems, row => row.Uid == EquipmentFixture.GearUid);
        var expectedEquip = f.ExpectedPositions((EquipmentFixture.GearUid, 2));
        var calls = 0;
        f.BeforeSend = _ => { if (calls++ == 0) AssertCommittedBeforeOutput(f, expectedEquip, character); };
        await f.Dispatch();
        f.BeforeSend = null;
        Assert.True(calls >= 3);
        var equipFrames = f.Sent.ToArray();
        Assert.Equal(new MsgItemUsagePacket(EquipmentFixture.GearUid, 0, MsgItemUsageAction.RemoveInventory, 0, 0, 0, 0).ToFrameBytes(), equipFrames[0]);
        Assert.Equal(LegacyGameItemPacket.FromSnapshotItem(inventoryRow.WithPosition(2)).ToFrameBytes(), equipFrames[1]);
        Assert.Equal(LegacyGameItemPacket.FromSnapshotItem(inventoryRow.WithPosition(2), mode: LegacyGameItemPacket.UpdateItemMode).ToFrameBytes(), equipFrames[2]);
        Assert.Equal(expectedEquip, File.ReadAllBytes(f.ItemsPath));
        Assert.Equal(registry, File.ReadAllBytes(f.Data.State));

        f.Sent.Clear(); calls = 0;
        var expectedUnequip = f.ExpectedPositions((EquipmentFixture.GearUid, 0));
        f.BeforeSend = _ => { if (calls++ == 0) AssertCommittedBeforeOutput(f, expectedUnequip, character); };
        await f.Dispatch(unequip: true);
        f.BeforeSend = null;
        Assert.True(calls >= 2);
        var unequipFrames = f.Sent.ToArray();
        Assert.Equal(EquipmentFixture.Request(true), unequipFrames[0]);
        Assert.Equal(LegacyGameItemPacket.FromSnapshotItem(inventoryRow).ToFrameBytes(), unequipFrames[1]);
        Assert.Equal(initial, File.ReadAllBytes(f.ItemsPath));
        Assert.Equal(registry, File.ReadAllBytes(f.Data.State));
        Assert.Single(f.Current.PlayerItems.InventoryItems, row => row.Uid == EquipmentFixture.GearUid);
        Assert.DoesNotContain(f.Current.PlayerItems.EquipmentItems, row => row.Uid == EquipmentFixture.GearUid);
    });

    [Fact]
    public Task ReplacingRightWithTwoHandClearsRightThenLeftAndCommitsAllRowsBeforeOutput() => Run(async f =>
    {
        var before = f.Current.PlayerItems;
        var incoming = Assert.Single(before.InventoryItems, row => row.Uid == EquipmentFixture.GearUid);
        var right = Assert.Single(before.EquipmentItems, row => row.Uid == EquipmentFixture.RightUid);
        var left = Assert.Single(before.EquipmentItems, row => row.Uid == EquipmentFixture.LeftUid);
        Assert.Equal((byte)1, right.Bound); Assert.Equal((byte)1, right.Locked);
        Assert.Equal((byte)1, left.Bound); Assert.Equal((byte)1, left.Locked);
        var expected = f.ExpectedPositions((incoming.Uid, 4), (right.Uid, 0), (left.Uid, 0));
        var character = File.ReadAllBytes(f.CharacterPath);
        var registry = File.ReadAllBytes(f.Data.State);
        var calls = 0;
        f.BeforeSend = _ => { if (calls++ == 0) AssertCommittedBeforeOutput(f, expected, character); };
        await f.Dispatch(position: 4);
        f.BeforeSend = null;
        Assert.True(calls >= 6);
        byte[][] prefix =
        [
            new MsgItemUsagePacket(incoming.Uid, 0, MsgItemUsageAction.RemoveInventory, 0, 0, 0, 0).ToFrameBytes(),
            new MsgItemUsagePacket(right.Uid, 4, MsgItemUsageAction.Unequip, 0, 0, 0, 0).ToFrameBytes(),
            LegacyGameItemPacket.FromSnapshotItem(right.WithPosition(0)).ToFrameBytes(),
            new MsgItemUsagePacket(left.Uid, 5, MsgItemUsageAction.Unequip, 0, 0, 0, 0).ToFrameBytes(),
            LegacyGameItemPacket.FromSnapshotItem(left.WithPosition(0)).ToFrameBytes(),
            LegacyGameItemPacket.FromSnapshotItem(incoming.WithPosition(4)).ToFrameBytes(),
        ];
        Assert.Equal(prefix.Length, f.Sent.Take(prefix.Length).Count());
        for (var index = 0; index < prefix.Length; index++) Assert.Equal(prefix[index], f.Sent.ElementAt(index));
        var after = f.ReadItems();
        Assert.Equal(before.InventoryItems.Count + 1, after.InventoryItems.Count);
        Assert.Equal((ushort)4, Assert.Single(after.EquipmentItems).Position);
        Assert.Contains(after.InventoryItems, row => row.Uid == right.Uid && row.Bound == 1 && row.Locked == 1);
        Assert.Contains(after.InventoryItems, row => row.Uid == left.Uid && row.Bound == 1 && row.Locked == 1);
        Assert.Equal(before.InventoryItems.Concat(before.EquipmentItems).Select(row => row.Uid).Order(),
            after.InventoryItems.Concat(after.EquipmentItems).Select(row => row.Uid).Order());
        Assert.Equal(expected, File.ReadAllBytes(f.ItemsPath)); Assert.Equal(registry, File.ReadAllBytes(f.Data.State));
    }, twoHandReplacement: true);
}
