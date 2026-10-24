using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyTradeRuntimeTests
{
    [Theory]
    [InlineData("inventory")]
    [InlineData("relic")]
    [InlineData("mythsoul")]
    [InlineData("equipment")]
    [InlineData("rune")]
    [InlineData("wardrobe")]
    [InlineData("warehouse")]
    [InlineData("deposited")]
    public Task ItemClosureIncomingDescendantCollisionAtFinalConfirmationRefundsWithoutMovingEitherGraph(string store) =>
        RunItemClosureAsync(async () =>
        {
            var left = AddItemClosurePlayer(1_297_041, 1_000, 50,
                new[] { ItemClosureSash(1_100_009, Item(11)) });
            var right = AddItemClosurePlayer(1_297_042, 2_000, 60, new[] { Item(20) });
            await OpenItemClosureAsync(left, right);
            await SendItemClosureAsync(left, new(6) { ItemUID = 10 });
            Assert.True(trades.TryGet(left.Connection, out var party));
            Assert.Single(party.Items);
            await SendItemClosureAsync(left, new(7) { Money = 300 });
            await SendItemClosureAsync(left, new(9));
            var current = Current(right);
            var items = current.PlayerItems;
            items = store switch
            {
                "inventory" => items with { InventoryItems = items.InventoryItems.Concat(new[] { Item(11) }).ToArray() },
                "relic" => items with { RelicFuseItems = items.RelicFuseItems.Concat(new[] { Item(11, position: 213) }).ToArray() },
                "mythsoul" => items with { MythsoulBagItems = items.MythsoulBagItems.Concat(new[] { Item(11, position: 217) }).ToArray() },
                "equipment" => items with { EquipmentItems = items.EquipmentItems.Concat(new[] { Item(11, position: 5) }).ToArray() },
                "rune" => items with { RuneItems = items.RuneItems.Concat(new[] { Item(11, position: 212) }).ToArray() },
                "wardrobe" => items with { WardrobeItems = items.WardrobeItems.Concat(new[] { Item(11, warehouse: 100) }).ToArray() },
                "warehouse" => items with { WarehouseItems = items.WarehouseItems.Concat(new[] { Item(11, warehouse: 1000) })
                    .OrderBy(item => item.WarehouseId).ThenBy(item => item.Uid).ToArray() },
                "deposited" => items with { InventoryItems = new[] { Item(20).WithItemId(1_100_006).WithDepositedItems(new[] { Item(11) }) } },
                _ => throw new ArgumentOutOfRangeException(nameof(store)),
            };
            new PlayerItemSnapshotLoader(ItemsRoot).SaveByCharacterUid(right.Uid, items);
            sessions.Set(right.Connection, current with { PlayerItems = items });
            var leftItems = Current(left).PlayerItems;
            var leftBytes = File.ReadAllBytes(ItemPath(left));
            var rightBytes = File.ReadAllBytes(ItemPath(right));
            var leftCount = left.Sent.Count;
            var rightCount = right.Sent.Count;

            await SendItemClosureAsync(right, new(9));

            AssertItemClosureSettled(left, right, 1_000, 2_000);
            Assert.Same(leftItems, Current(left).PlayerItems);
            Assert.Same(items, Current(right).PlayerItems);
            Assert.Equal(leftBytes, File.ReadAllBytes(ItemPath(left)));
            Assert.Equal(rightBytes, File.ReadAllBytes(ItemPath(right)));
            AssertItemClosureNoFinish(left, leftCount);
            AssertItemClosureNoFinish(right, rightCount);
            AssertItemClosureNoIncoming(right, rightCount, 10);
        });

    [Theory]
    [InlineData("duplicateChild")]
    [InlineData("duplicateAcrossStores")]
    [InlineData("zeroChild")]
    [InlineData("nullForeignTail")]
    [InlineData("secondLevel")]
    public Task ItemClosureMalformedWholeGraphRejectsOfferBeforePreviewOrAnyWrite(string corruption) =>
        RunItemClosureAsync(async () =>
        {
            var left = AddItemClosurePlayer(1_297_051, 1_000, 50,
                new[] { ItemClosureSash(1_100_009, Item(11)) });
            var right = AddItemClosurePlayer(1_297_052, 2_000, 60, new[] { Item(20) });
            await OpenItemClosureAsync(left, right);
            var current = Current(left);
            var items = current.PlayerItems;
            // Deliberate malformed live-state fixtures, introduced only after real
            // parsed birth. Do not serialize nulls or recursively walk a cycle in
            // the test oracle, and never manufacture a successful save callback.
            items = corruption switch
            {
                "duplicateChild" => items with { InventoryItems = new[] { ItemClosureSash(1_100_009, Item(11), Item(11)) } },
                "duplicateAcrossStores" => items with { WarehouseItems = items.WarehouseItems.Concat(new[] { Item(11, warehouse: 1000) }).ToArray() },
                "zeroChild" => items with { InventoryItems = new[] { ItemClosureSash(1_100_009, Item(0)) } },
                "nullForeignTail" => items with { WarehouseItems = items.WarehouseItems.Concat(new PlayerItemSnapshotItem[] { null! }).ToArray() },
                "secondLevel" => items with { InventoryItems = new[] { ItemClosureSash(1_100_009, Item(11).WithItemId(1_100_003).WithDepositedItems(new[] { Item(12) })) } },
                _ => throw new ArgumentOutOfRangeException(nameof(corruption)),
            };
            sessions.Set(left.Connection, current with { PlayerItems = items });
            var before = CaptureItemClosure(left);
            var other = CaptureItemClosure(right);

            await SendItemClosureAsync(left, new(6) { ItemUID = 10 });

            AssertItemClosureUnchanged(left, before);
            AssertItemClosureUnchanged(right, other);
            Assert.Equal(other.SentCount, right.Sent.Count);
            Assert.True(trades.TryGet(left.Connection, out var party));
            Assert.Empty(party.Items);
            AssertItemClosureNoFinish(left, before.SentCount);
            AssertItemClosureNoFinish(right, other.SentCount);
        });

    [Theory]
    [InlineData("nullForeignTail")]
    [InlineData("duplicateChild")]
    public Task ItemClosureMalformedFinalGraphRetainsEscrowThenRealRepairAllowsSamePairRetry(string corruption) =>
        RunItemClosureAsync(async () =>
        {
            var left = AddItemClosurePlayer(1_297_055, 1_000, 50,
                new[] { ItemClosureSash(1_100_009, Item(11)) });
            var right = AddItemClosurePlayer(1_297_056, 2_000, 60, new[] { Item(20) });
            await OpenItemClosureAsync(left, right);
            await SendItemClosureAsync(left, new(6) { ItemUID = 10 });
            await SendItemClosureAsync(left, new(7) { Money = 300 });
            await SendItemClosureAsync(left, new(9));
            var healthy = Current(left);
            var corruptedItems = corruption == "nullForeignTail"
                ? healthy.PlayerItems with
                {
                    WarehouseItems = healthy.PlayerItems.WarehouseItems.Concat(new PlayerItemSnapshotItem[] { null! }).ToArray(),
                }
                : healthy.PlayerItems with
                {
                    InventoryItems = new[] { ItemClosureSash(1_100_009, Item(11), Item(11)) },
                };
            sessions.Set(left.Connection, healthy with { PlayerItems = corruptedItems });
            var own = CaptureItemClosure(left);
            var other = CaptureItemClosure(right);

            await SendItemClosureAsync(right, new(9));

            AssertItemClosureUnchanged(left, own);
            AssertItemClosureUnchanged(right, other);
            AssertWallet(left, 700, 50);
            AssertWallet(right, 2_000, 60);
            Assert.True(Current(left).LegacyTradeActive);
            Assert.True(Current(right).LegacyTradeActive);
            Assert.True(trades.TryGet(left.Connection, out var party));
            Assert.False(party.Confirmed);
            Assert.False(party.Other.Confirmed);
            AssertItemClosureNoFinish(left, own.SentCount);
            AssertItemClosureNoFinish(right, other.SentCount);
            AssertItemClosureNoIncoming(right, other.SentCount, 10);
            Assert.Contains("offeredItemGraphInvalid:escrowRetained", log.ToString(), StringComparison.Ordinal);

            // Restore ONLY the test-corrupted live reference. Real durable files,
            // escrow claim and offer remain unchanged, with no reseed/reset owner.
            sessions.Set(left.Connection, healthy);
            await SendItemClosureAsync(left, new(9));
            await SendItemClosureAsync(right, new(9));
            AssertItemClosureSettled(left, right, 700, 2_300);
            AssertInventory(left);
            AssertInventory(right, 10, 20);
            Assert.Equal(11U, Assert.Single(Assert.Single(Current(right).PlayerItems.InventoryItems,
                item => item.Uid == 10).DepositedItems).Uid);
        });

    [Fact]
    public Task ItemClosureRecipientCorruptForeignTailCannotBeTreatedAsValidVacancy() =>
        RunItemClosureAsync(async () =>
        {
            var left = AddItemClosurePlayer(1_297_061, 1_000, 50,
                new[] { ItemClosureSash(1_100_009, Item(11)) });
            var right = AddItemClosurePlayer(1_297_062, 2_000, 60, new[] { Item(20) });
            await OpenItemClosureAsync(left, right);
            var current = Current(right);
            sessions.Set(right.Connection, current with
            {
                PlayerItems = current.PlayerItems with
                {
                    WarehouseItems = current.PlayerItems.WarehouseItems.Concat(new PlayerItemSnapshotItem[] { null! }).ToArray(),
                },
            });
            var leftBefore = CaptureItemClosure(left);
            var rightBefore = CaptureItemClosure(right);

            await SendItemClosureAsync(left, new(6) { ItemUID = 10 });

            AssertItemClosureUnchanged(left, leftBefore);
            AssertItemClosureUnchanged(right, rightBefore);
            Assert.Equal(rightBefore.SentCount, right.Sent.Count);
            Assert.True(trades.TryGet(left.Connection, out var party));
            Assert.Empty(party.Items);
        });
}
