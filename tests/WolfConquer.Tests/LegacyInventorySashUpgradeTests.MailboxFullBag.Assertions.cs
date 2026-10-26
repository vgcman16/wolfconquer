using System.Reflection;
using System.Text.Json;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyInventorySashUpgradeTests
{
    // Assertion evidence only; these actual owners are never replaced or injected.
    private sealed record MailboxFullBagSnapshot(Snapshot Disk, WorldSession Owner, string Json,
        LegacyMailboxStore Mailboxes, List<LegacyMailboxEntry> Cache, LegacyMailboxEntry[] Rows);

    private static MailboxFullBagSnapshot CaptureMailboxFullBag(Fixture fixture)
    {
        var owner = fixture.ReadEnteredMailboxPackageSession();
        var mailboxes = fixture.ReadEnteredMailboxFullBagStore();
        var rows = fixture.ReadEnteredMailboxFullBagRows();
        return new(fixture.CaptureTransferFrame(), owner, JsonSerializer.Serialize(owner),
            mailboxes, rows, rows.ToArray());
    }

    private static void AssertMailboxFullBagState(Fixture fixture, MailboxFullBagSnapshot before)
    {
        Assert.Same(before.Owner, fixture.ReadEnteredMailboxPackageSession());
        Assert.Equal(before.Json, JsonSerializer.Serialize(before.Owner));
        fixture.AssertFiles(before.Disk.Files);
        AssertMailboxFullBagCache(fixture, before);
        Assert.False(LegacyTradeJournal.HasPending(fixture.Users));
    }

    private static void AssertMailboxFullBagCache(Fixture fixture, MailboxFullBagSnapshot before)
    {
        Assert.Same(before.Mailboxes, fixture.ReadEnteredMailboxFullBagStore());
        var current = fixture.ReadEnteredMailboxFullBagRows();
        Assert.Same(before.Cache, current);
        Assert.Equal(before.Rows.Length, current.Count);
        for (var index = 0; index < before.Rows.Length; index++) Assert.Same(before.Rows[index], current[index]);
    }

    private static void AssertMailboxFullBagOnlyError(Fixture fixture, int first, int logStart)
    {
        var frames = fixture.Sent.Skip(first).ToArray();
        Assert.Equal(LegacyMsgMessagePacket.SystemTalk("STR_BAG_FULL@@").ToFrameBytes(), Assert.Single(frames));
        Assert.DoesNotContain(frames, frame => ReadFrame(frame).Header.Opcode is
            WorldPacketOpcodes.LegacyMailboxMode or WorldPacketOpcodes.LegacyMailList);
        var output = fixture.Log.ToString()[logStart..];
        Assert.Contains("committed=False ack=False", output);
    }

    private static async Task FreeMailboxFullBagSlotThroughWarehouseAsync(Fixture fixture, MailboxFullBagSnapshot before)
    {
        const uint movingUid = 10_000;
        const uint bank = 8; // Existing known warehouse admission, not a fabricated owner.
        var moving = Assert.Single(before.Owner.PlayerItems.InventoryItems, item => item.Uid == movingUid);
        var stored = moving.WithWarehouseId(bank, position: 0);
        var expectedItems = before.Owner.PlayerItems with
        {
            InventoryItems = before.Owner.PlayerItems.InventoryItems.Where(item => item.Uid != movingUid).ToArray(),
            WarehouseItems = before.Owner.PlayerItems.WarehouseItems.Append(stored)
                .OrderBy(item => item.WarehouseId).ThenBy(item => item.Uid).ToArray(),
        };
        var expected = new Dictionary<string, byte[]>(before.Disk.Files, StringComparer.OrdinalIgnoreCase)
        {
            [Path.GetRelativePath(fixture.Root, fixture.ItemPath)] = PlayerItemSnapshotLoader.ToLegacyBytes(expectedItems),
        };
        var first = fixture.Sent.Count;
        fixture.SendObserver = _ => fixture.AssertFiles(expected);

        await fixture.FrameAsync(new LegacyWarehousePacket(bank, LegacyWarehouseAction.DepositItem, movingUid, 0, []).ToFrameBytes());

        fixture.SendObserver = null;
        fixture.AssertFiles(expected);
        var current = fixture.ReadEnteredMailboxPackageSession();
        Assert.Equal(39, current.PlayerItems.InventoryItems.Count);
        Assert.Equal(JsonSerializer.Serialize(expectedItems), JsonSerializer.Serialize(current.PlayerItems));
        Assert.Equal(before.Owner with { PlayerItems = current.PlayerItems,
            WarehouseBankPresence = current.WarehouseBankPresence }, current);
        Assert.Equal(before.Json, JsonSerializer.Serialize(before.Owner));
        AssertMailboxFullBagCache(fixture, before);
        var frames = fixture.Sent.Skip(first).ToArray();
        Assert.Equal(2, frames.Length);
        Assert.Equal(new MsgItemUsagePacket(movingUid, 0, MsgItemUsageAction.RemoveInventory, 0, 0, 0, 0).ToFrameBytes(), frames[0]);
        Assert.Equal(new LegacyWarehousePacket(bank, LegacyWarehouseAction.DepositItem, 0, 0, [stored]).ToFrameBytes(), frames[1]);
        Assert.False(LegacyTradeJournal.HasPending(fixture.Users));
    }

    private sealed partial class Fixture
    {
        internal LegacyMailboxStore ReadEnteredMailboxFullBagStore()
        {
            Assert.NotNull(frameOwner);
            var field = typeof(WorldServerFrameHandler).GetField("_mailboxes", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field);
            return Assert.IsType<LegacyMailboxStore>(field!.GetValue(frameOwner));
        }

        internal List<LegacyMailboxEntry> ReadEnteredMailboxFullBagRows()
        {
            // Do not call List here: that public API republishes its filtered list.
            // Observe the actual cache only; these fixture actions are serialized.
            var store = ReadEnteredMailboxFullBagStore();
            var field = typeof(LegacyMailboxStore).GetField("mailboxByPlayerUid", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field);
            var cache = Assert.IsType<Dictionary<uint, List<LegacyMailboxEntry>>>(field!.GetValue(store));
            Assert.True(cache.TryGetValue(Uid, out var rows));
            return Assert.IsType<List<LegacyMailboxEntry>>(rows);
        }
    }
}
