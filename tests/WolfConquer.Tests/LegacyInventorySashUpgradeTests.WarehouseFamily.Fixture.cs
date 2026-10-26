using System.Text.Json;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyInventorySashUpgradeTests
{
    private const uint WarehouseFamilyNpc = 8;
    private const uint WarehouseFamilyItemUid = TransferItemUid;

    private static bool IsWarehouseFamilyWithdraw(LegacyWarehouseAction action) => action is
        LegacyWarehouseAction.WithdrawItem or LegacyWarehouseAction.WithdrawItem_WH_House or
        LegacyWarehouseAction.InventorySashWithdrawItem;

    private static uint WarehouseFamilyBank(LegacyWarehouseAction action) => action is
        LegacyWarehouseAction.InventorySashDepositItem or LegacyWarehouseAction.InventorySashWithdrawItem
            ? Fixture.Uid : WarehouseFamilyNpc;

    private static PlayerItemSnapshotItem WarehouseFamilyMovingItem() => TransferRow()
        .WithItemId(1_100_009).WithStackSize(1).WithDepositedItems([new PlayerItemSnapshotItem
        {
            Uid = 12, ItemId = 1_088_001, StackSize = 2, Durability = 3, MaximumDurability = 9,
            Bound = 2, Locked = 1, Inscribed = 1,
            OwnerUid = 78, OwnerName = "StoredChild", Signature = "complete child sentinel",
        }]);

    private sealed partial class Fixture
    {
        internal void SeedWarehouseFamily(LegacyWarehouseAction action, uint? storedBank = null,
            PlayerItemSnapshotItem? moving = null)
        {
            Seed(count: 2);
            var bank = storedBank ?? WarehouseFamilyBank(action);
            moving ??= WarehouseFamilyMovingItem();
            var original = Current.PlayerItems;
            var withdraw = IsWarehouseFamilyWithdraw(action);
            SaveTransferSetup(original with
            {
                InventoryItems = withdraw ? original.InventoryItems : original.InventoryItems.Append(moving)
                    .OrderBy(item => item.Uid).ToArray(),
                WarehouseItems = (withdraw ? original.WarehouseItems.Append(moving.WithWarehouseId(bank, position: 0))
                    : original.WarehouseItems).OrderBy(item => item.WarehouseId).ThenBy(item => item.Uid).ToArray(),
            });
        }

        internal Task InvokeWarehouseFamilyAsync(LegacyWarehouseAction action, uint? bank = null)
        {
            var request = new LegacyWarehousePacket(bank ?? WarehouseFamilyBank(action), action,
                WarehouseFamilyItemUid, 0, []);
            // The actual registered producer owns the item-mutation operation.
            // No caller-held Shared lease and no save after this invocation.
            var actual = Registry.HandleAsync(Context, ReadFrame(request.ToFrameBytes()), CancellationToken.None).AsTask();
            Calls.Add(actual);
            return ObserveAsync(actual);
        }

        internal void SetWarehouseFamilyCount(string value)
        {
            var current = Current;
            Assert.True(CharacterSnapshotFileCoordinator.TryUpdateCharacterAttributes(CharacterPath, Uid,
                new Dictionary<string, string> { ["InventorySashCount"] = value },
                detailedIoFailures: true, out var reason), reason);
            var parsed = CharacterSnapshotParser.ParseText(File.ReadAllText(CharacterPath), CharacterPath);
            Assert.Equal(value, parsed.Attributes["InventorySashCount"]);
            Sessions.Set(Connection, current with { Character = parsed });
            Context = Context with { Session = Current };
        }

        internal void FillWarehouseFamilyBank(uint bank, int count)
        {
            var filler = Enumerable.Range(0, count).Select(index => new PlayerItemSnapshotItem
            { Uid = checked((uint)(20_000 + index)), ItemId = 1_088_001, StackSize = 1, WarehouseId = bank });
            SaveTransferSetup(Current.PlayerItems with
            {
                WarehouseItems = Current.PlayerItems.WarehouseItems.Concat(filler)
                    .OrderBy(item => item.WarehouseId).ThenBy(item => item.Uid).ToArray(),
            });
        }
    }

    private static PlayerItemSnapshot ExpectedWarehouseFamilyItems(PlayerItemSnapshot before,
        LegacyWarehouseAction action, uint bank)
    {
        var withdraw = IsWarehouseFamilyWithdraw(action);
        var row = withdraw
            ? Assert.Single(before.WarehouseItems, item => item.Uid == WarehouseFamilyItemUid && item.WarehouseId == bank)
            : Assert.Single(before.InventoryItems, item => item.Uid == WarehouseFamilyItemUid);
        return before with
        {
            InventoryItems = withdraw ? before.InventoryItems.Append(row.WithWarehouseId(0, position: 0))
                .OrderBy(item => item.Uid).ToArray() : before.InventoryItems.Where(item => item.Uid != row.Uid).ToArray(),
            WarehouseItems = withdraw ? before.WarehouseItems.Where(item => item.Uid != row.Uid || item.WarehouseId != bank).ToArray()
                : before.WarehouseItems.Append(row.WithWarehouseId(bank, position: 0))
                    .OrderBy(item => item.WarehouseId).ThenBy(item => item.Uid).ToArray(),
        };
    }

    private static Dictionary<string, byte[]> ExpectedWarehouseFamilyFiles(Fixture fixture, Snapshot before,
        LegacyWarehouseAction action, uint bank)
    {
        var expected = new Dictionary<string, byte[]>(before.Files, StringComparer.OrdinalIgnoreCase)
        {
            [Path.GetRelativePath(fixture.Root, fixture.ItemPath)] = PlayerItemSnapshotLoader.ToLegacyBytes(
                ExpectedWarehouseFamilyItems(before.Owner.PlayerItems, action, bank)),
        };
        return expected;
    }

    private static void AssertWarehouseFamilyCommitted(Fixture fixture, Snapshot before,
        LegacyWarehouseAction action, uint? targetBank = null)
    {
        var bank = targetBank ?? WarehouseFamilyBank(action);
        var expectedItems = ExpectedWarehouseFamilyItems(before.Owner.PlayerItems, action, bank);
        var current = fixture.Current;
        Assert.NotNull(current.WarehouseBankPresence);
        Assert.Equal(before.Owner with { PlayerItems = current.PlayerItems,
            WarehouseBankPresence = current.WarehouseBankPresence }, current);
        Assert.Equal(before.Json, JsonSerializer.Serialize(before.Owner));
        Assert.Equal(JsonSerializer.Serialize(expectedItems), JsonSerializer.Serialize(current.PlayerItems));
        Assert.Same(before.Owner.PlayerItems.RelicFuseItems, current.PlayerItems.RelicFuseItems);
        Assert.Same(before.Owner.PlayerItems.MythsoulBagItems, current.PlayerItems.MythsoulBagItems);
        Assert.Same(before.Owner.PlayerItems.EquipmentItems, current.PlayerItems.EquipmentItems);
        Assert.Same(before.Owner.PlayerItems.RuneItems, current.PlayerItems.RuneItems);
        Assert.Same(before.Owner.PlayerItems.WardrobeItems, current.PlayerItems.WardrobeItems);
        fixture.AssertFiles(ExpectedWarehouseFamilyFiles(fixture, before, action, bank));
        var saved = new PlayerItemSnapshotLoader(fixture.Items).LoadByCharacterUid(Fixture.Uid);
        Assert.Equal(JsonSerializer.Serialize(expectedItems), JsonSerializer.Serialize(saved));
        Assert.True(fixture.Recovery.IsReadyFor(fixture.Users, fixture.Items));
        Assert.False(fixture.Recovery.IsBlocked);
        Assert.False(LegacyTradeJournal.HasPending(fixture.Users));
    }

    private static byte[][] ExpectedWarehouseFamilyFrames(Snapshot before, LegacyWarehouseAction action, uint bank)
    {
        var expected = ExpectedWarehouseFamilyItems(before.Owner.PlayerItems, action, bank);
        var withdraw = IsWarehouseFamilyWithdraw(action);
        var row = withdraw ? Assert.Single(expected.InventoryItems, item => item.Uid == WarehouseFamilyItemUid)
            : Assert.Single(expected.WarehouseItems, item => item.Uid == WarehouseFamilyItemUid && item.WarehouseId == bank);
        if (withdraw)
            return [LegacyGameItemPacket.FromSnapshotItem(row).ToFrameBytes(),
                new LegacyWarehousePacket(bank, action, WarehouseFamilyItemUid, 0, []).ToFrameBytes()];
        return [new MsgItemUsagePacket(WarehouseFamilyItemUid, 0, MsgItemUsageAction.RemoveInventory, 0, 0, 0, 0).ToFrameBytes(),
            new LegacyWarehousePacket(bank, action, 0, 0, [row]).ToFrameBytes(),
            TransferExtraFrame(),
            new LegacyItemLockPacket(WarehouseFamilyItemUid, LegacyItemLockPacket.UnlockDateType,
                LegacyItemLockPacket.UnlockDateLockedMode, 20_991_231).ToFrameBytes()];
    }

    private static void AssertWarehouseFamilyFrames(Fixture fixture, Snapshot before,
        LegacyWarehouseAction action, uint? targetBank = null)
    {
        var expected = ExpectedWarehouseFamilyFrames(before, action, targetBank ?? WarehouseFamilyBank(action));
        var actual = fixture.Sent.Skip(before.FrameCount).ToArray();
        Assert.Equal(expected.Length, actual.Length);
        for (var index = 0; index < expected.Length; index++) Assert.Equal(expected[index], actual[index]);
    }
}
