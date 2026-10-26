using System.Text.Json;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyInventorySashUpgradeTests
{
    private const uint ShowNpc = 8;
    private const ushort ShowPurchasedCapacity = 2;

    private static uint ShowTarget(LegacyWarehouseAction action) => action switch
    {
        LegacyWarehouseAction.ShashShow => TransferSashUid,
        LegacyWarehouseAction.ShowInventorySash => Fixture.Uid,
        _ => ShowNpc,
    };

    private sealed partial class Fixture
    {
        internal void SeedWarehouseShow(LegacyWarehouseAction action, uint? target = null)
        {
            Seed(count: ShowPurchasedCapacity);
            // The real loader canonicalizes warehouse/child order. First seed a
            // lossless file through the unchanged strict save/reload assertion.
            var rows = new uint[] { 20, 21, 22, 23, 24 }.Select(uid => TransferRow(uid)).ToArray();
            var original = Current.PlayerItems;
            if (action == LegacyWarehouseAction.ShashShow)
            {
                var sash = new PlayerItemSnapshotItem
                { Uid = TransferSashUid, ItemId = 1_100_009, StackSize = 1, Durability = 1, MaximumDurability = 1 };
                SaveTransferSetup(original with
                { InventoryItems = original.InventoryItems.Append(sash.WithDepositedItems(rows)).ToArray() });
            }
            else
            {
                var bank = target ?? ShowTarget(action);
                SaveTransferSetup(original with
                {
                    WarehouseItems = original.WarehouseItems.Concat(rows.Select(row => row.WithWarehouseId(bank)))
                        .OrderBy(row => row.WarehouseId).ThenBy(row => row.Uid).ToArray(),
                });
            }
            PublishWarehouseShowInputOrder(action, target);
        }

        internal void PublishWarehouseShowInputOrder(LegacyWarehouseAction action, uint? target = null,
            bool keepStaleContext = false)
        {
            var parsed = Current;
            var files = Files();
            uint[] inputOrder = [24, 20, 23, 21, 22];
            PlayerItemSnapshot projected;
            if (action == LegacyWarehouseAction.ShashShow)
            {
                var sash = Assert.Single(parsed.PlayerItems.InventoryItems, row => row.Uid == TransferSashUid);
                Assert.Equal(inputOrder.Length, sash.DepositedItems.Count);
                var children = inputOrder.Select(uid => Assert.Single(sash.DepositedItems, row => row.Uid == uid)).ToArray();
                projected = parsed.PlayerItems with
                {
                    InventoryItems = parsed.PlayerItems.InventoryItems.Select(row =>
                        ReferenceEquals(row, sash) ? row.WithDepositedItems(children) : row).ToArray(),
                };
                Assert.Equal(inputOrder, Assert.Single(projected.InventoryItems,
                    row => row.Uid == TransferSashUid).DepositedItems.Select(row => row.Uid));
            }
            else
            {
                var bank = target ?? ShowTarget(action);
                var rows = parsed.PlayerItems.WarehouseItems.Where(row => row.WarehouseId == bank).ToArray();
                Assert.Equal(inputOrder.Length, rows.Length);
                projected = parsed.PlayerItems with
                {
                    WarehouseItems = inputOrder.Select(uid => Assert.Single(rows, row => row.Uid == uid))
                        .Concat(parsed.PlayerItems.WarehouseItems.Where(row => row.WarehouseId != bank)).ToArray(),
                };
                Assert.Equal(inputOrder, projected.WarehouseItems.Where(row => row.WarehouseId == bank).Select(row => row.Uid));
            }
            // Show-only preaction input: reuse actual parsed rows, publish through
            // the real Store, and never rewrite the canonical file after projection.
            Session = parsed with { PlayerItems = projected };
            Sessions.Set(Connection, Session);
            Context = Context with { Session = keepStaleContext ? Context.Session : Session };
            AssertFiles(files);
        }

        internal Task InvokeWarehouseShowAsync(LegacyWarehouseAction action, uint? target = null)
        {
            var request = new LegacyWarehousePacket(target ?? ShowTarget(action), action, 0, 0, []);
            // Keep Context unchanged: the producer must resolve the CURRENT Store.
            // No save callback, post-invocation saver, or caller-owned operation lease.
            var actual = Registry.HandleAsync(Context, ReadFrame(request.ToFrameBytes()), CancellationToken.None).AsTask();
            Calls.Add(actual);
            return ObserveAsync(actual);
        }
    }

    private static byte[][] ExpectedWarehouseShowFrames(WorldSession owner, LegacyWarehouseAction action)
    {
        var target = ShowTarget(action);
        if (action != LegacyWarehouseAction.ShashShow)
        {
            var capacity = target == Fixture.Uid ? ShowPurchasedCapacity : LegacyWarehousePacket.DefaultCapacity;
            var frames = new List<byte[]>();
            foreach (var chunk in owner.PlayerItems.WarehouseItems.Where(row => row.WarehouseId == target)
                .OrderBy(row => row.Uid).Chunk(4))
            {
                frames.Add(new LegacyWarehousePacket(target, action, 0, capacity, chunk).ToFrameBytes());
                frames.AddRange(chunk.Select(row => new LegacyItemLockPacket(row.Uid,
                    LegacyItemLockPacket.UnlockDateType, LegacyItemLockPacket.UnlockDateLockedMode, 20_991_231).ToFrameBytes()));
                frames.Add(WarehouseShowExtraFrame(chunk));
            }
            return frames.ToArray();
        }
        var sash = Assert.Single(owner.PlayerItems.InventoryItems, row => row.Uid == target);
        var rows = sash.DepositedItems.OrderBy(row => row.Uid).ToArray();
        return [new LegacyWarehousePacket(target, action, 0, 12, rows).ToFrameBytes(), WarehouseShowExtraFrame(rows)];
    }

    private static byte[] WarehouseShowExtraFrame(IReadOnlyList<PlayerItemSnapshotItem> rows)
    {
        var purifications = rows.Select(row => new LegacyItemExtraPurification(row.Uid,
            LegacyItemExtraPacket.StabilizationType, 800001, 3, 0, 0, 0)).ToArray();
        var refineries = rows.Select(row => new LegacyItemExtraRefinery(row.Uid,
            LegacyItemExtraPacket.PermanentRefineryType, 301, 2, 10, 20, 0)).ToArray();
        return new LegacyItemExtraPacket(purifications, refineries).ToFrameBytes();
    }

    private static void AssertWarehouseShowReadOnly(Fixture fixture, Snapshot before)
    {
        Assert.Same(before.Owner, fixture.Current);
        Assert.Equal(before.Json, JsonSerializer.Serialize(fixture.Current));
        fixture.AssertFiles(before.Files);
    }

    private static void AssertWarehouseShowFrames(Fixture fixture, Snapshot before, LegacyWarehouseAction action)
    {
        var expected = ExpectedWarehouseShowFrames(before.Owner, action);
        var actual = fixture.Sent.Skip(before.FrameCount).ToArray();
        Assert.Equal(expected.Length, actual.Length);
        for (var index = 0; index < expected.Length; index++) Assert.Equal(expected[index], actual[index]);
    }
}
