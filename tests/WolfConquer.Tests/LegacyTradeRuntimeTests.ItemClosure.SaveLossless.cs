using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyTradeRuntimeTests
{
    [Theory]
    [InlineData("inventoryPlacement")]
    [InlineData("equipmentPlacement")]
    [InlineData("nullWarehouse")]
    [InlineData("nullChildren")]
    [InlineData("zeroItemId")]
    public Task ItemClosureSaveLosslessStoreAndChildFailuresRejectBeforeAnyOffer(string invalidKind) =>
        RunItemClosureAsync(async () =>
        {
            var left = AddItemClosurePlayer(1_297_121, 1_000, 50,
                new[] { ItemClosureSash(1_100_009, Item(11)) });
            var right = AddItemClosurePlayer(1_297_122, 2_000, 60, new[] { Item(20) });
            await OpenItemClosureAsync(left, right);
            var current = Current(left);
            var items = current.PlayerItems;
            var sash = Assert.Single(items.InventoryItems);
            items = invalidKind switch
            {
                // Saving/reloading either row would move it to another store.
                "inventoryPlacement" => items with { InventoryItems = new[] { sash.WithPosition(4) } },
                "equipmentPlacement" => items with
                {
                    EquipmentItems = new[] { Assert.Single(items.EquipmentItems).WithPosition(0) },
                },
                "nullWarehouse" => items with { WarehouseItems = null! },
                "nullChildren" => items with
                {
                    InventoryItems = new[] { sash.WithDepositedItems(new[]
                    {
                        new PlayerItemSnapshotItem
                        {
                            Uid = 11, ItemId = 1_088_001, Durability = 5,
                            MaximumDurability = 10, StackSize = 1, DepositedItems = null!,
                        },
                    }) },
                },
                "zeroItemId" => items with
                {
                    InventoryItems = new[] { sash.WithDepositedItems(new[] { Item(11).WithItemId(0) }) },
                },
                _ => throw new ArgumentOutOfRangeException(nameof(invalidKind)),
            };
            // Fault only the live graph AFTER genuine parsed birth; retain the
            // exact ordinary saved image, not a fake successful invalid save.
            sessions.Set(left.Connection, current with { PlayerItems = items });

            await AssertItemClosureSaveLosslessOfferRefusalAsync(left, right);
        });

    [Theory]
    [InlineData("OwnerName", "utf8Overflow")]
    [InlineData("OwnerName", "nul")]
    [InlineData("OwnerName", "surrogate")]
    [InlineData("Signature", "utf8Overflow")]
    [InlineData("Signature", "nul")]
    [InlineData("Signature", "surrogate")]
    public Task ItemClosureSaveLosslessPerfectionTextCannotBeTruncatedOrReplacedByTradeSave(
        string field, string invalidKind) => RunItemClosureAsync(async () =>
        {
            var left = AddItemClosurePlayer(1_297_131, 1_000, 50,
                new[] { ItemClosureSash(1_100_009, Item(11)) });
            var right = AddItemClosurePlayer(1_297_132, 2_000, 60, new[] { Item(20) });
            await OpenItemClosureAsync(left, right);
            var child = Assert.Single(Assert.Single(Current(left).PlayerItems.InventoryItems).DepositedItems);
            var invalid = invalidKind switch
            {
                "utf8Overflow" => new string('\u00e9', 17), // 17 UTF-16 units, 34 UTF-8 bytes.
                "nul" => "prefix\0unpreserved-tail",
                "surrogate" => "\uD800",
                _ => throw new ArgumentOutOfRangeException(nameof(invalidKind)),
            };
            Assert.True(invalid.Length <= 32);
            if (invalidKind == "utf8Overflow") Assert.Equal(34, Encoding.UTF8.GetByteCount(invalid));
            if (invalidKind == "surrogate") Assert.True(char.IsHighSurrogate(Assert.Single(invalid)));
            if (field == "OwnerName") child.OwnerName = invalid;
            else child.Signature = invalid;

            await AssertItemClosureSaveLosslessOfferRefusalAsync(left, right);

            // JSON may escape/replace invalid Unicode for diagnostics; independently
            // retain exact original UTF-16 text instead of treating normalization as proof.
            Assert.Equal(invalid, field == "OwnerName" ? child.OwnerName : child.Signature);
        });

    [Fact]
    public Task ItemClosureCommittedIncomingChildIsDetachedFromRetiredSourcePerfectionAlias() =>
        RunItemClosureAsync(async () =>
        {
            var left = AddItemClosurePlayer(1_297_141, 1_000, 50,
                new[] { ItemClosureSash(1_100_009, Item(11)) });
            var right = AddItemClosurePlayer(1_297_142, 2_000, 60, new[] { Item(20) });
            var retiredRoot = Assert.Single(Current(left).PlayerItems.InventoryItems);
            var retiredChild = Assert.Single(retiredRoot.DepositedItems);
            var originalChildImage = PlayerItemSnapshotLoader.ToLegacyBytes(
                new PlayerItemSnapshot(new[] { retiredChild }, [], [], [], []));
            await OpenItemClosureAsync(left, right);
            await SendItemClosureAsync(left, new(6) { ItemUID = 10 });
            await SendItemClosureAsync(left, new(7) { Money = 300 });
            await SendItemClosureAsync(left, new(9));
            await SendItemClosureAsync(right, new(9));
            AssertItemClosureSettled(left, right, 700, 2_300);
            AssertInventory(left);
            AssertInventory(right, 10, 20);
            var receivedRoot = Assert.Single(Current(right).PlayerItems.InventoryItems, item => item.Uid == 10);
            var receivedChild = Assert.Single(receivedRoot.DepositedItems);
            Assert.NotSame(retiredRoot, receivedRoot);
            Assert.NotSame(retiredRoot.DepositedItems, receivedRoot.DepositedItems);
            Assert.NotSame(retiredChild, receivedChild);
            AssertItemUnchanged(retiredChild, receivedChild);
            var leftCommitted = CaptureItemClosure(left);
            var rightCommitted = CaptureItemClosure(right);

            // The old offered root is no longer physically owned by either live
            // inventory. A retained source alias must not edit the recipient.
            retiredChild.PerfectionLevel = 17;
            retiredChild.PerfectionProgress = 123;
            retiredChild.OwnerUid = 7_777;
            retiredChild.OwnerName = "RetiredOnly";
            retiredChild.Signature = "retired-source-mutation";

            AssertItemClosureUnchanged(left, leftCommitted);
            AssertItemClosureUnchanged(right, rightCommitted);
            Assert.Equal(leftCommitted.SentCount, left.Sent.Count);
            Assert.Equal(rightCommitted.SentCount, right.Sent.Count);
            Assert.Equal(originalChildImage, PlayerItemSnapshotLoader.ToLegacyBytes(
                new PlayerItemSnapshot(new[] { receivedChild }, [], [], [], [])));
            var reloaded = new PlayerItemSnapshotLoader(ItemsRoot).LoadByCharacterUid(right.Uid);
            var persistedChild = Assert.Single(Assert.Single(reloaded.InventoryItems, item => item.Uid == 10).DepositedItems);
            AssertItemUnchanged(receivedChild, persistedChild);
            AssertPersisted(left);
            AssertPersisted(right);
        });

    private async Task AssertItemClosureSaveLosslessOfferRefusalAsync(Player left, Player right)
    {
        var own = CaptureItemClosure(left);
        var other = CaptureItemClosure(right);

        await SendItemClosureAsync(left, new(6) { ItemUID = 10 });

        AssertItemClosureUnchanged(left, own);
        AssertItemClosureUnchanged(right, other);
        AssertWallet(left, 1_000, 50);
        AssertWallet(right, 2_000, 60);
        Assert.Equal(own.SentCount, left.Sent.Count);
        Assert.Equal(other.SentCount, right.Sent.Count);
        Assert.True(trades.TryGet(left.Connection, out var party));
        Assert.Empty(party.Items);
        Assert.Empty(party.Other.Items);
        Assert.Equal(0L, party.Money);
        Assert.Equal(0U, party.ConquerPoints);
        Assert.False(party.Confirmed);
        Assert.False(party.Other.Confirmed);
        Assert.False(LegacyTradeJournal.HasPending(UsersRoot));
        Assert.Contains("result=tradeItemGraphInvalid", log.ToString(), StringComparison.Ordinal);
        // Do not attempt Close or a lossy serializer on malformed live authority.
    }
}
