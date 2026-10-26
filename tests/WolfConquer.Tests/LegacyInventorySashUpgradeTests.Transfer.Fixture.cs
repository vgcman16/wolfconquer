using System.Text.Json;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

// Artifact-only proposal. These partials join the CURRENT upgrade fixture;
// they neither replace its lifetime owner nor claim Frame/native-client proof.
public sealed partial class LegacyInventorySashUpgradeTests
{
    private const uint TransferSashUid = 10;
    private const uint TransferItemUid = 11;

    private static PlayerItemSnapshotItem TransferRow(uint uid = TransferItemUid) => new()
    {
        Uid = uid, ItemId = 1_088_001, Durability = 5, MaximumDurability = 10,
        StackSize = 3, Bound = 1, Locked = 2, UnlockTimer = 20_991_231,
        PurificationItemId = 800001, PurificationLevel = 3,
        RefineryEffectId = 724440, RefineryEffectLevel = 2,
        RefineryEffectPercent = 10, RefineryEffectPercent2 = 20,
        OwnerUid = 77, OwnerName = "TransferOwner", Signature = "sash sentinel",
        PerfectionLevel = 2, PerfectionProgress = 17,
    };

    private sealed partial class Fixture
    {
        internal void SeedTransfer(bool deposited = false, uint sashItemId = 1_100_009,
            PlayerItemSnapshotItem? child = null)
        {
            Seed();
            child ??= TransferRow();
            var sash = new PlayerItemSnapshotItem
            { Uid = TransferSashUid, ItemId = sashItemId, Durability = 1, MaximumDurability = 1, StackSize = 1 };
            var inventory = deposited
                ? new[] { sash.WithDepositedItems(new[] { child }) }
                : new[] { sash, child };
            // Preserve all SIX other genuine stores seeded by the existing fixture.
            SaveTransferSetup(Current.PlayerItems with { InventoryItems = inventory });
        }

        internal void SaveTransferSetup(PlayerItemSnapshot items, bool keepStaleContext = false)
        {
            // Setup only. No save callback or save after a transfer invocation.
            var loader = new PlayerItemSnapshotLoader(Items);
            loader.SaveByCharacterUid(Uid, items);
            var loaded = loader.LoadByCharacterUid(Uid);
            Assert.Equal(File.ReadAllBytes(ItemPath), PlayerItemSnapshotLoader.ToLegacyBytes(loaded));
            Session = Current with
            {
                Character = CharacterSnapshotParser.ParseText(File.ReadAllText(CharacterPath), CharacterPath),
                PlayerItems = loaded,
            };
            Sessions.Set(Connection, Session);
            // Real readiness is established AFTER the final complete files/reload.
            Recovery = new(Users, Items);
            Assert.True(Recovery.TryEnsureInitialized(out var reason), reason);
            Assert.True(Recovery.IsReadyFor(Users, Items));
            Assert.False(Recovery.IsBlocked);
            Context = Context with { Session = keepStaleContext ? Context.Session : Session, TradeRecovery = Recovery };
        }

        internal Task InvokeTransferAsync(bool withdraw = false) =>
            InvokeSashPacketAsync(withdraw ? LegacyWarehouseAction.ShashWithdrawItem : LegacyWarehouseAction.ShashDepositItem);

        internal Task InvokeSashPacketAsync(LegacyWarehouseAction action)
        {
            var request = new LegacyWarehousePacket(TransferSashUid, action,
                action == LegacyWarehouseAction.ShashShow ? 0 : TransferItemUid, 0, []);
            // All warehouse routes must select the actual current Store, even
            // when this original context still contains a retired snapshot.
            // The actual registered transfer owns Exclusive. Never wrap in Shared.
            var actual = Registry.HandleAsync(Context, ReadFrame(request.ToFrameBytes()), CancellationToken.None).AsTask();
            Calls.Add(actual);
            return ObserveAsync(actual);
        }

        internal void HoldTransferItemFile() =>
            heldFiles.Add(new FileStream(ItemPath, FileMode.Open, FileAccess.Read, FileShare.None));
    }

    private static PlayerItemSnapshot ExpectedTransferItems(PlayerItemSnapshot before, bool withdraw)
    {
        var sash = Assert.Single(before.InventoryItems, item => item.Uid == TransferSashUid);
        var child = withdraw
            ? Assert.Single(sash.DepositedItems, item => item.Uid == TransferItemUid)
            : Assert.Single(before.InventoryItems, item => item.Uid == TransferItemUid);
        var moved = child.WithWarehouseId(0, position: 0);
        var updatedSash = sash.WithDepositedItems(withdraw
            ? sash.DepositedItems.Where(item => item.Uid != TransferItemUid).ToArray()
            : sash.DepositedItems.Append(moved).OrderBy(item => item.Uid).ToArray());
        var inventory = before.InventoryItems.Where(item => item.Uid != TransferItemUid)
            .Select(item => item.Uid == TransferSashUid ? updatedSash : item);
        if (withdraw) inventory = inventory.Append(moved);
        return before with { InventoryItems = inventory.OrderBy(item => item.Uid).ToArray() };
    }

    private static Dictionary<string, byte[]> ExpectedTransferFiles(Fixture fixture, Snapshot before, bool withdraw)
    {
        var files = new Dictionary<string, byte[]>(before.Files, StringComparer.OrdinalIgnoreCase);
        files[Path.GetRelativePath(fixture.Root, fixture.ItemPath)] =
            PlayerItemSnapshotLoader.ToLegacyBytes(ExpectedTransferItems(before.Owner.PlayerItems, withdraw));
        return files;
    }

    private static void AssertTransferCommitted(Fixture fixture, Snapshot before, bool withdraw)
    {
        var expectedItems = ExpectedTransferItems(before.Owner.PlayerItems, withdraw);
        Assert.Equal(JsonSerializer.Serialize(expectedItems), JsonSerializer.Serialize(fixture.Current.PlayerItems));
        Assert.Equal(before.Owner with { PlayerItems = fixture.Current.PlayerItems }, fixture.Current);
        Assert.Equal(before.Json, JsonSerializer.Serialize(before.Owner)); // No mutation of retired rows.
        fixture.AssertFiles(ExpectedTransferFiles(fixture, before, withdraw));
        var reloaded = new PlayerItemSnapshotLoader(fixture.Items).LoadByCharacterUid(Fixture.Uid);
        Assert.Equal(JsonSerializer.Serialize(expectedItems), JsonSerializer.Serialize(reloaded));
        Assert.True(fixture.Recovery.IsReadyFor(fixture.Users, fixture.Items));
        Assert.False(fixture.Recovery.IsBlocked);
        Assert.False(LegacyTradeJournal.HasPending(fixture.Users));
    }

    private static byte[] TransferExtraFrame() => new LegacyItemExtraPacket(
        [new(TransferItemUid, LegacyItemExtraPacket.StabilizationType, 800001, 3, 0, 0, 0)],
        [new(TransferItemUid, LegacyItemExtraPacket.PermanentRefineryType, 301, 2, 10, 20, 0)]).ToFrameBytes();

    private static byte[][] ExpectedTransferFrames(Snapshot before, bool withdraw)
    {
        var expected = ExpectedTransferItems(before.Owner.PlayerItems, withdraw);
        var sash = Assert.Single(expected.InventoryItems, item => item.Uid == TransferSashUid);
        var child = withdraw ? Assert.Single(expected.InventoryItems, item => item.Uid == TransferItemUid)
            : Assert.Single(sash.DepositedItems, item => item.Uid == TransferItemUid);
        if (withdraw)
            return
            [
                LegacyGameItemPacket.FromSnapshotItem(child).ToFrameBytes(),
                new LegacyWarehousePacket(TransferSashUid, LegacyWarehouseAction.ShashWithdrawItem,
                    TransferItemUid, 0, []) { DeclaredItemCount = 1 }.ToFrameBytes(),
            ];
        return
        [
            new MsgItemUsagePacket(TransferItemUid, 0, MsgItemUsageAction.RemoveInventory, 0, 0, 0, 0).ToFrameBytes(),
            new LegacyWarehousePacket(TransferSashUid, LegacyWarehouseAction.ShashDepositItem, 0, 0, [child]).ToFrameBytes(),
            TransferExtraFrame(),
            new LegacyItemLockPacket(TransferItemUid, LegacyItemLockPacket.UnlockDateType,
                LegacyItemLockPacket.UnlockDateLockedMode, 20_991_231).ToFrameBytes(),
        ];
    }

    private static void AssertTransferFrames(Fixture fixture, Snapshot before, bool withdraw)
    {
        var actual = fixture.Sent.Skip(before.FrameCount).ToArray();
        var expected = ExpectedTransferFrames(before, withdraw);
        Assert.Equal(expected.Length, actual.Length);
        for (var index = 0; index < expected.Length; index++) Assert.Equal(expected[index], actual[index]);
    }

    private static async Task AssertTransferShowAsync(Fixture fixture, ushort capacity, bool empty)
    {
        var before = fixture.Capture();
        var sash = Assert.Single(before.Owner.PlayerItems.InventoryItems, item => item.Uid == TransferSashUid);
        await fixture.InvokeSashPacketAsync(LegacyWarehouseAction.ShashShow);
        Assert.Same(before.Owner, fixture.Current);
        Assert.Equal(before.Json, JsonSerializer.Serialize(fixture.Current));
        fixture.AssertFiles(before.Files);
        var frames = fixture.Sent.Skip(before.FrameCount).ToArray();
        Assert.Equal(empty ? 1 : 2, frames.Length);
        Assert.Equal(new LegacyWarehousePacket(TransferSashUid, LegacyWarehouseAction.ShashShow,
            0, capacity, sash.DepositedItems).ToFrameBytes(), frames[0]);
        if (!empty) Assert.Equal(TransferExtraFrame(), frames[1]);
    }
}
