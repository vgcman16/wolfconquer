using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using Fixture = WolfConquer.Tests.WorldServerFrameHandlerTests.NativeItemSpellCallerFixture;

namespace WolfConquer.Tests;

public sealed partial class OwnedNativeItemJoinTests
{
    [Fact]
    public async Task ActualMailboxAttachmentMovesFromRealNativeWarehouseIntoInventoryAndDeliversFrames()
    {
        var item = new PlayerItemSnapshotItem { Uid = Fixture.ItemUid, ItemId = 725016,
            WarehouseId = 1000, Durability = 1, MaximumDurability = 1, StackSize = 1 };
        var saved = PlayerItemSnapshot.Empty with { WarehouseItems = new[] { item } };
        await WorldServerFrameHandlerTests.WithNativeItemSpellCallerAsync(false, async f =>
        {
            var mailboxes = new LegacyMailboxStore(f.Root);
            mailboxes.Add(Fixture.Uid, new LegacyMailboxEntry { Id = 41, ItemId = Fixture.ItemUid,
                Title = "Owned attachment", SenderName = "Owned fixture", Content = "Existing saved item",
                Timer = new DateTime(2099, 1, 1).ToBinary() });
            mailboxes.Save(Fixture.Uid);
            await f.Login(); var before = f.Current; var binding = RequireGraph(f);
            var inventory = f.Native.Inventory; var warehouse = f.Native.Warehouse;
            Assert.Empty(inventory!.ClientItems);
            Assert.Equal(Fixture.ItemUid, Assert.Single(warehouse!.ClientItems[1000]).Key);
            int first = f.Sent.Count;

            await f.Send(new LegacyMailboxModePacket(LegacyMailboxModeType.ClaimItem, 41, []).ToFrameBytes());

            Assert.NotSame(before, f.Current); Assert.True(f.Sent.Count > first, f.Log.ToString());
            Assert.Same(inventory, f.Native.Inventory); Assert.Same(warehouse, f.Native.Warehouse);
            Assert.Same(binding, f.Native.OrdinarySpellBinding);
            Assert.Equal(Fixture.ItemUid, Assert.Single(inventory.ClientItems).Key);
            Assert.DoesNotContain(warehouse.ClientItems.Values.SelectMany(bank => bank.Values), row => row.Uid == Fixture.ItemUid);
            Assert.Equal(0u, Assert.Single(f.Current.PlayerItems.InventoryItems).WarehouseId);
            Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(f.Current.PlayerItems), File.ReadAllBytes(f.ItemsPath));
            RequireGraph(f);
        }, playerItems: saved);
    }

    [Fact]
    public async Task ActualEquipUnequipAndBoundDropRefreshTheSameNativeOwnersWithoutRelogin()
    {
        var item = new PlayerItemSnapshotItem { Uid = Fixture.ItemUid, ItemId = 132009,
            Bound = 1, Durability = 1, MaximumDurability = 1, StackSize = 1 };
        var saved = PlayerItemSnapshot.Empty with { InventoryItems = new[] { item } };
        await WorldServerFrameHandlerTests.WithNativeItemSpellCallerAsync(false, async f =>
        {
            await f.Login(); var binding = RequireGraph(f);
            var inventory = f.Native.Inventory; var equipment = f.Native.Equipment;
            int first = f.Sent.Count;

            await f.Send(new MsgItemUsagePacket(Fixture.ItemUid, 3, MsgItemUsageAction.Equip, 0, 0, 0, 0).ToFrameBytes());

            Assert.True(f.Sent.Count > first, f.Log.ToString()); Assert.Empty(inventory!.ClientItems);
            Assert.Equal((ushort)3, Assert.Single(equipment!.ClientItems).Value.Position); RequireGraph(f);
            first = f.Sent.Count;
            await f.Send(new MsgItemUsagePacket(Fixture.ItemUid, 3, MsgItemUsageAction.Unequip, 0, 0, 0, 0).ToFrameBytes());

            Assert.True(f.Sent.Count > first, f.Log.ToString()); Assert.Empty(equipment.ClientItems);
            Assert.Equal((ushort)0, Assert.Single(inventory.ClientItems).Value.Position); RequireGraph(f);
            first = f.Sent.Count;
            await f.Send(new MsgItemUsagePacket(Fixture.ItemUid, 0, MsgItemUsageAction.DropItem, 0, 0, 0, 0).ToFrameBytes());

            Assert.True(f.Sent.Count > first, f.Log.ToString()); Assert.Empty(inventory.ClientItems);
            Assert.Empty(f.Current.PlayerItems.InventoryItems); RequireGraph(f);
            Assert.Same(binding, f.Native.OrdinarySpellBinding);
            Assert.Same(inventory, f.Native.Inventory); Assert.Same(equipment, f.Native.Equipment);
            Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(f.Current.PlayerItems), File.ReadAllBytes(f.ItemsPath));
            Assert.DoesNotContain("ownerChangedAfterCommit", f.Log.ToString(), StringComparison.Ordinal);
            Assert.False(LegacyTradeJournal.HasPending(f.Users));
        }, playerItems: saved);
    }
}
