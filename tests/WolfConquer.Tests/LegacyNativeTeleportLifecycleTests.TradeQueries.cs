using System.Collections.Concurrent;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeTeleportLifecycleTests
{
    [Fact]
    public Task TradeMembershipUsesUidAcrossSameTypeItemsAndDifferentObjectAliases() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var owner = new LegacyNativeGameClientRuntime();
        var trade = new LegacyNativeTradeRuntime(owner);
        var offered = Item(100, 410000);
        var sameType = Item(200, 410000);
        var sameUid = Item(100, 420000);
        Assert.True(trade.Items.TryAdd(offered.Uid, offered));
        var before = new TradeQueryImage(trade);

        Assert.True(trade.ItemInTrade(offered));
        Assert.True(trade.ItemInTrade(sameUid));
        Assert.False(trade.ItemInTrade(sameType));

        before.RequireUnchanged();
        Assert.Null(owner.Inventory); Assert.Null(owner.MyTrade); Assert.Null(trade.Target);
        Assert.False(trade.WindowOpen); Assert.False(trade.Confirmed);
        Assert.Equal(100u, sameUid.Uid); Assert.Equal(420000u, sameUid.ITEM_ID);
        Assert.Equal(200u, sameType.Uid); Assert.Equal(410000u, sameType.ITEM_ID);
    });

    [Fact]
    public Task DuplicateOfferKeyRefusesReplacementWithoutChangingTheExistingUidMember() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var owner = new LegacyNativeGameClientRuntime();
        owner.Inventory = new LegacyNativeInventoryRuntime(owner);
        var trade = new LegacyNativeTradeRuntime(owner);
        var offered = Item(100); var other = Item(200);
        Assert.True(owner.Inventory.ClientItems.TryAdd(offered.Uid, offered));
        Assert.True(trade.Items.TryAdd(offered.Uid, offered));
        var before = new TradeQueryImage(trade);

        Assert.False(trade.Items.TryAdd(offered.Uid, other));
        Assert.True(trade.ItemInTrade(offered)); Assert.False(trade.ItemInTrade(other));
        Assert.True(trade.ValidItems());

        before.RequireUnchanged();
        Assert.Equal(200u, other.Uid); Assert.Equal(410000u, other.ITEM_ID);
    });

    [Fact]
    public Task NullAndZeroUidMembershipArgumentsRejectWithoutChangingTheOffer() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var trade = new LegacyNativeTradeRuntime(new LegacyNativeGameClientRuntime());
        var offered = Item(100); Assert.True(trade.Items.TryAdd(offered.Uid, offered));
        var zero = Item(0); var before = new TradeQueryImage(trade);

        Assert.Throws<ArgumentNullException>(() => trade.ItemInTrade(null!));
        Assert.ThrowsAny<ArgumentException>(() => trade.ItemInTrade(zero));

        before.RequireUnchanged(); Assert.Equal(0u, zero.Uid); Assert.Equal(410000u, zero.ITEM_ID);
    });

    [Theory]
    [InlineData("null")]
    [InlineData("zero-key")]
    [InlineData("type-key")]
    [InlineData("changed-uid")]
    [InlineData("duplicate-alias")]
    [InlineData("duplicate-uid")]
    public Task MalformedOfferCannotBecomeEitherPresentOrPermissionLikeAbsent(string corruption) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var owner = new LegacyNativeGameClientRuntime();
        owner.Inventory = new LegacyNativeInventoryRuntime(owner);
        var trade = new LegacyNativeTradeRuntime(owner);
        var offered = Item(100); var tail = Item(200);
        Assert.True(owner.Inventory.ClientItems.TryAdd(offered.Uid, offered));
        Assert.True(trade.Items.TryAdd(offered.Uid, offered));
        switch (corruption)
        {
            case "null": trade.Items[200] = null!; break;
            case "zero-key": trade.Items[0] = Item(0); break;
            case "type-key": trade.Items[tail.ITEM_ID] = tail; break;
            case "changed-uid": trade.Items[tail.Uid] = tail; tail.Uid = 201; break;
            case "duplicate-alias": trade.Items[200] = offered; break;
            case "duplicate-uid": trade.Items[200] = Item(offered.Uid, 420000); break;
            default: throw new ArgumentOutOfRangeException(nameof(corruption));
        }
        var before = new TradeQueryImage(trade);

        Assert.Throws<InvalidOperationException>(() => trade.ItemInTrade(offered));
        Assert.Throws<InvalidOperationException>(() => trade.ItemInTrade(Item(300)));
        Assert.False(trade.ValidItems());

        before.RequireUnchanged();
    });

    [Fact]
    public Task EmptyOfferNeedsARealBoundInventoryButNoTargetOrActiveWindow() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var owner = new LegacyNativeGameClientRuntime();
        var trade = new LegacyNativeTradeRuntime(owner);
        Assert.False(trade.ValidItems()); Assert.Null(owner.Inventory);
        owner.Inventory = new LegacyNativeInventoryRuntime(owner);
        var before = new TradeQueryImage(trade);

        Assert.True(trade.ValidItems());
        Assert.False(trade.ItemInTrade(Item(100)));

        before.RequireUnchanged(); Assert.Empty(trade.Items); Assert.Empty(owner.Inventory.ClientItems);
        Assert.Null(owner.MyTrade); Assert.Null(trade.Target); Assert.False(trade.WindowOpen);
    });

    [Fact]
    public Task DistinctOwnedUidsOfOneItemTypeRemainValidWithoutSubstitutingTheirObjects() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var owner = new LegacyNativeGameClientRuntime();
        owner.Inventory = new LegacyNativeInventoryRuntime(owner);
        var trade = new LegacyNativeTradeRuntime(owner);
        var first = Item(100, 410000); var second = Item(200, 410000);
        foreach (var item in new[] { first, second })
        {
            Assert.True(owner.Inventory.ClientItems.TryAdd(item.Uid, item));
            Assert.True(trade.Items.TryAdd(item.Uid, item));
        }
        var before = new TradeQueryImage(trade);

        Assert.True(trade.ValidItems());
        Assert.True(trade.ItemInTrade(first)); Assert.True(trade.ItemInTrade(second));

        before.RequireUnchanged(); Assert.NotSame(first, second);
        Assert.Null(owner.MyTrade); Assert.Null(trade.Target); Assert.False(trade.WindowOpen);
    });

    [Theory]
    [InlineData("missing-player")]
    [InlineData("foreign-player")]
    [InlineData("missing-inventory")]
    [InlineData("foreign-inventory")]
    [InlineData("missing-map")]
    public Task TradeValidityRefusesMissingOrForeignPhysicalOwnersWithoutRepair(string corruption) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var owner = new LegacyNativeGameClientRuntime();
        var foreign = new LegacyNativeGameClientRuntime();
        owner.Inventory = new LegacyNativeInventoryRuntime(owner);
        var trade = new LegacyNativeTradeRuntime(owner);
        var item = Item(100);
        Assert.True(owner.Inventory.ClientItems.TryAdd(item.Uid, item));
        Assert.True(trade.Items.TryAdd(item.Uid, item));
        switch (corruption)
        {
            case "missing-player": owner.Player = null!; break;
            case "foreign-player": owner.Player = foreign.Player; break;
            case "missing-inventory": owner.Inventory = null; break;
            case "foreign-inventory":
                owner.Inventory = new LegacyNativeInventoryRuntime(foreign);
                Assert.True(owner.Inventory.ClientItems.TryAdd(item.Uid, item));
                break;
            case "missing-map": owner.Inventory.ClientItems = null!; break;
            default: throw new ArgumentOutOfRangeException(nameof(corruption));
        }
        var before = new TradeQueryImage(trade);

        Assert.False(trade.ValidItems());

        before.RequireUnchanged(); Assert.Same(item, trade.Items[100]);
    });

    [Theory]
    [InlineData("null")]
    [InlineData("zero-key")]
    [InlineData("rekeyed")]
    [InlineData("duplicate-alias")]
    [InlineData("duplicate-uid")]
    [InlineData("missing-offer")]
    [InlineData("substituted")]
    public Task TradeValidityInspectsTheCompleteInventoryAndExactOfferedObject(string corruption) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var owner = new LegacyNativeGameClientRuntime();
        owner.Inventory = new LegacyNativeInventoryRuntime(owner);
        var trade = new LegacyNativeTradeRuntime(owner);
        var offered = Item(100); var tail = Item(200);
        Assert.True(owner.Inventory.ClientItems.TryAdd(offered.Uid, offered));
        Assert.True(trade.Items.TryAdd(offered.Uid, offered));
        switch (corruption)
        {
            case "null": owner.Inventory.ClientItems[200] = null!; break;
            case "zero-key": owner.Inventory.ClientItems[0] = Item(0); break;
            case "rekeyed": owner.Inventory.ClientItems[201] = tail; break;
            case "duplicate-alias": owner.Inventory.ClientItems[200] = offered; break;
            case "duplicate-uid": owner.Inventory.ClientItems[200] = Item(offered.Uid, 420000); break;
            case "missing-offer": Assert.True(owner.Inventory.ClientItems.TryRemove(offered.Uid, out _)); break;
            case "substituted": owner.Inventory.ClientItems[offered.Uid] = Item(offered.Uid, offered.ITEM_ID); break;
            default: throw new ArgumentOutOfRangeException(nameof(corruption));
        }
        var before = new TradeQueryImage(trade);

        Assert.False(trade.ValidItems());
        Assert.True(trade.ItemInTrade(Item(offered.Uid, 420000)));

        before.RequireUnchanged();
    });

    [Fact]
    public Task ChangedPhysicalUidInvalidatesTheEarlierValidOfferWithoutRekeyingEitherMap() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var owner = new LegacyNativeGameClientRuntime();
        owner.Inventory = new LegacyNativeInventoryRuntime(owner);
        var trade = new LegacyNativeTradeRuntime(owner);
        var item = Item(100);
        Assert.True(owner.Inventory.ClientItems.TryAdd(item.Uid, item));
        Assert.True(trade.Items.TryAdd(item.Uid, item)); Assert.True(trade.ValidItems());
        item.Uid = 101;
        var before = new TradeQueryImage(trade);

        Assert.Throws<InvalidOperationException>(() => trade.ItemInTrade(Item(100)));
        Assert.False(trade.ValidItems());

        before.RequireUnchanged();
        Assert.Same(item, trade.Items[100]); Assert.Same(item, owner.Inventory.ClientItems[100]);
        Assert.False(trade.Items.ContainsKey(101)); Assert.False(owner.Inventory.ClientItems.ContainsKey(101));
    });

    private sealed class TradeQueryImage
    {
        private readonly LegacyNativeTradeRuntime trade;
        private readonly LegacyNativeGameClientRuntime owner;
        private readonly LegacyNativePlayerRuntime? player;
        private readonly LegacyNativeInventoryRuntime? inventory;
        private readonly ConcurrentDictionary<uint, LegacyNativeGameItemRuntime> offers;
        private readonly ConcurrentDictionary<uint, LegacyNativeGameItemRuntime>? inventoryMap;
        private readonly KeyValuePair<uint, LegacyNativeGameItemRuntime>[] offerRows, inventoryRows;
        private readonly (LegacyNativeGameItemRuntime Item, uint Uid, uint Type)[] rows;
        private readonly LegacyNativeGameClientRuntime? target;
        private readonly long money;
        private readonly uint conquerPoints;
        private readonly bool windowOpen, confirmed;

        internal TradeQueryImage(LegacyNativeTradeRuntime trade)
        {
            this.trade = trade; owner = trade.Owner; player = owner.Player; inventory = owner.Inventory;
            offers = trade.Items; inventoryMap = inventory?.ClientItems;
            offerRows = offers.ToArray(); inventoryRows = inventoryMap?.ToArray() ?? [];
            rows = offerRows.Concat(inventoryRows).Select(pair => pair.Value).Where(item => item is not null)
                .Distinct<LegacyNativeGameItemRuntime>(ReferenceEqualityComparer.Instance)
                .Select(item => (item, item.Uid, item.ITEM_ID)).ToArray();
            target = trade.Target; money = trade.Money; conquerPoints = trade.ConquerPoints;
            windowOpen = trade.WindowOpen; confirmed = trade.Confirmed;
        }

        internal void RequireUnchanged()
        {
            Assert.Same(owner, trade.Owner); Assert.Same(player, owner.Player); Assert.Same(inventory, owner.Inventory);
            Assert.Same(offers, trade.Items);
            if (inventory is not null) Assert.Same(inventoryMap, inventory.ClientItems);
            RequireRows(offerRows, offers);
            if (inventoryMap is not null) RequireRows(inventoryRows, inventoryMap);
            foreach (var row in rows) { Assert.Equal(row.Uid, row.Item.Uid); Assert.Equal(row.Type, row.Item.ITEM_ID); }
            Assert.Same(target, trade.Target); Assert.Equal(money, trade.Money); Assert.Equal(conquerPoints, trade.ConquerPoints);
            Assert.Equal(windowOpen, trade.WindowOpen); Assert.Equal(confirmed, trade.Confirmed);
        }

        private static void RequireRows(KeyValuePair<uint, LegacyNativeGameItemRuntime>[] expected,
            ConcurrentDictionary<uint, LegacyNativeGameItemRuntime> current)
        {
            Assert.Equal(expected.Length, current.Count);
            foreach (var row in expected)
            {
                Assert.True(current.TryGetValue(row.Key, out var item));
                Assert.Same(row.Value, item);
            }
        }
    }
}
