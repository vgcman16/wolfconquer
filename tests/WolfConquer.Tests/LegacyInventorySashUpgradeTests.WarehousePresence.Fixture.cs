using System.Text.Json;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyInventorySashUpgradeTests
{
    private sealed partial class Fixture
    {
        internal Task InvokePresenceWithContextAsync(WorldPacketHandlerContext context)
        {
            var request = new LegacyWarehousePacket(ShowNpc, LegacyWarehouseAction.Show, 0, 0, []);
            var actual = Registry.HandleAsync(context, ReadFrame(request.ToFrameBytes()), CancellationToken.None).AsTask();
            Calls.Add(actual);
            return ObserveAsync(actual);
        }
    }

    private static LegacyWarehouseAction PresenceDeposit(bool ownUid) => ownUid
        ? LegacyWarehouseAction.InventorySashDepositItem : LegacyWarehouseAction.DepositItem;

    private static LegacyWarehouseAction PresenceWithdraw(bool ownUid) => ownUid
        ? LegacyWarehouseAction.InventorySashWithdrawItem : LegacyWarehouseAction.WithdrawItem;

    private static LegacyWarehouseAction PresenceShow(bool ownUid) => ownUid
        ? LegacyWarehouseAction.ShowInventorySash : LegacyWarehouseAction.Show;

    private static async Task CommitPresenceTransferAsync(Fixture fixture, LegacyWarehouseAction action,
        uint? target = null)
    {
        var bank = target ?? WarehouseFamilyBank(action);
        var before = fixture.Capture();
        await fixture.InvokeWarehouseFamilyAsync(action, bank);
        var current = fixture.Current;
        var expectedItems = ExpectedWarehouseFamilyItems(before.Owner.PlayerItems, action, bank);
        Assert.NotSame(before.Owner, current);
        Assert.Equal(JsonSerializer.Serialize(expectedItems), JsonSerializer.Serialize(current.PlayerItems));
        // The internal, nonserialized presence proof may change at this commit.
        // Preserve every public session value and the actual physical owner.
        Assert.Equal(JsonSerializer.Serialize(before.Owner with { PlayerItems = current.PlayerItems }),
            JsonSerializer.Serialize(current));
        Assert.Same(before.Owner.NativeGameClient, current.NativeGameClient);
        Assert.Same(before.Owner.PlayerSpells, current.PlayerSpells);
        Assert.Same(before.Owner.PlayerProficiencies, current.PlayerProficiencies);
        Assert.Same(before.Owner.PlayerItems.RelicFuseItems, current.PlayerItems.RelicFuseItems);
        Assert.Same(before.Owner.PlayerItems.MythsoulBagItems, current.PlayerItems.MythsoulBagItems);
        Assert.Same(before.Owner.PlayerItems.EquipmentItems, current.PlayerItems.EquipmentItems);
        Assert.Same(before.Owner.PlayerItems.RuneItems, current.PlayerItems.RuneItems);
        Assert.Same(before.Owner.PlayerItems.WardrobeItems, current.PlayerItems.WardrobeItems);
        Assert.Equal(before.Json, JsonSerializer.Serialize(before.Owner));
        fixture.AssertFiles(ExpectedWarehouseFamilyFiles(fixture, before, action, bank));
        var loaded = new PlayerItemSnapshotLoader(fixture.Items).LoadByCharacterUid(Fixture.Uid);
        Assert.Equal(JsonSerializer.Serialize(expectedItems), JsonSerializer.Serialize(loaded));
        Assert.True(fixture.Recovery.IsReadyFor(fixture.Users, fixture.Items));
        Assert.False(fixture.Recovery.IsBlocked);
        Assert.False(LegacyTradeJournal.HasPending(fixture.Users));
        AssertWarehouseFamilyFrames(fixture, before, action, bank);
    }

    private static async Task CreateEmptyPresenceBankAsync(Fixture fixture, bool ownUid = false)
    {
        fixture.SeedWarehouseFamily(PresenceDeposit(ownUid));
        var bank = WarehouseFamilyBank(PresenceDeposit(ownUid));
        Assert.DoesNotContain(fixture.Current.PlayerItems.WarehouseItems, row => row.WarehouseId == bank);
        await CommitPresenceTransferAsync(fixture, PresenceDeposit(ownUid));
        Assert.Single(fixture.Current.PlayerItems.WarehouseItems, row => row.WarehouseId == bank);
        await CommitPresenceTransferAsync(fixture, PresenceWithdraw(ownUid));
        Assert.DoesNotContain(fixture.Current.PlayerItems.WarehouseItems, row => row.WarehouseId == bank);
    }

    private static async Task AssertPresenceShowAsync(Fixture fixture, bool present,
        bool ownUid = false, uint? target = null)
    {
        var action = PresenceShow(ownUid);
        var bank = target ?? ShowTarget(action);
        var before = fixture.Capture();
        Assert.DoesNotContain(before.Owner.PlayerItems.WarehouseItems, row => row.WarehouseId == bank);
        await fixture.InvokeWarehouseShowAsync(action, bank);
        AssertWarehouseShowReadOnly(fixture, before);
        var frames = fixture.Sent.Skip(before.FrameCount).ToArray();
        if (!present)
        {
            Assert.Empty(frames);
            return;
        }
        var capacity = ownUid ? ShowPurchasedCapacity : LegacyWarehousePacket.DefaultCapacity;
        Assert.Equal(new LegacyWarehousePacket(bank, action, 0, capacity, []).ToFrameBytes(), Assert.Single(frames));
    }
}
