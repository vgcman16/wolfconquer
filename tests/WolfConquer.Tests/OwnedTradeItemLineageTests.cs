using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using Fixture = WolfConquer.Tests.OwnedEquipmentBehaviorFixture;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class OwnedTradeItemLineageTests
{
    private const uint OfferedUid = 2002, ExtraUid = 2003, ChildUid = 2010, SashType = 1100009;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task RegisteredWarehouseOutAndBackCannotResurrectAnIdenticalValueOldOffer(bool withChild) =>
        Run(withChild, extra: false, async f =>
        {
            await OpenAndOffer(f);
            var offer = Offer(f); var lineage = Assert.IsType<WorldSessionStore.TradeItemLineage>(offer.Lineage);
            var original = f.Current(); var originalRow = Assert.Single(original.PlayerItems.InventoryItems);
            var originalNative = original.NativeGameClient!.Inventory!.ClientItems[OfferedUid];
            Assert.True(lineage.MatchesCurrent(f.Store, f.Connection(Fixture.Actor), original, originalRow));
            var itemsBefore = File.ReadAllBytes(f.ItemPath()); var registryBefore = File.ReadAllBytes(f.Data.State);
            await Trade(f, Fixture.Actor, new(7) { Money = 300 });
            await Trade(f, Fixture.Actor, new(9));

            await Move(f, OfferedUid, LegacyWarehouseAction.DepositItem);
            Assert.DoesNotContain(f.Current().PlayerItems.InventoryItems, row => row.Uid == OfferedUid);
            Assert.Equal(8u, Assert.Single(f.Current().PlayerItems.WarehouseItems).WarehouseId);
            Assert.False(lineage.MatchesCurrent(f.Store, f.Connection(Fixture.Actor), f.Current(), originalRow));
            await Move(f, OfferedUid, LegacyWarehouseAction.WithdrawItem);

            var returned = Assert.Single(f.Current().PlayerItems.InventoryItems);
            Assert.Equal(itemsBefore, File.ReadAllBytes(f.ItemPath())); // Exact saved values/order restored.
            Assert.Equal(OfferedUid, returned.Uid); Assert.Empty(f.Current().PlayerItems.WarehouseItems);
            Assert.NotSame(originalRow, returned);
            Assert.NotSame(originalNative, f.Current().NativeGameClient!.Inventory!.ClientItems[OfferedUid]);
            Assert.False(lineage.MatchesCurrent(f.Store, f.Connection(Fixture.Actor), f.Current(), returned));
            if (withChild) Assert.Equal(ChildUid, Assert.Single(returned.DepositedItems).Uid);
            var actorStart = f.ActorFrames.Count; var peerStart = f.Frames(Fixture.Peer).Count;
            await Trade(f, Fixture.Peer, new(9));

            Assert.False(HasCompletion(f.ActorFrames.Skip(actorStart)));
            Assert.False(HasCompletion(f.Frames(Fixture.Peer).Skip(peerStart)));
            Assert.Contains("offeredItemChangedOrMissing:refunded", f.Log.ToString());
            Assert.False(f.Current().LegacyTradeActive); Assert.False(f.Current(Fixture.Peer).LegacyTradeActive);
            Assert.Equal(1000L, f.Current().Character.Money); Assert.Equal(2000L, f.Current(Fixture.Peer).Character.Money);
            Assert.Single(f.Current().PlayerItems.InventoryItems, row => row.Uid == OfferedUid);
            Assert.Empty(f.Current(Fixture.Peer).PlayerItems.InventoryItems);
            Assert.Equal(itemsBefore, File.ReadAllBytes(f.ItemPath())); Assert.Equal(registryBefore, File.ReadAllBytes(f.Data.State));
            Assert.False(f.Recovery.IsBlocked); Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
            var files = f.Data.Files();
            await Trade(f, Fixture.Actor, new(9)); await Trade(f, Fixture.Peer, new(9));
            f.Data.AssertFiles(files);
        });

    [Fact]
    public Task DetachedOldCanonicalRowsCannotPoisonUnchangedNativeOfferAfterAnotherRealItemMoves() =>
        Run(withChild: true, extra: true, async f =>
        {
            await OpenAndOffer(f);
            var offer = Offer(f); var lineage = Assert.IsType<WorldSessionStore.TradeItemLineage>(offer.Lineage);
            var oldRow = Assert.Single(f.Current().PlayerItems.InventoryItems, row => row.Uid == OfferedUid);
            var native = f.Current().NativeGameClient!.Inventory!.ClientItems[OfferedUid];
            var oldChild = Assert.Single(oldRow.DepositedItems);
            await Move(f, ExtraUid, LegacyWarehouseAction.DepositItem);
            await Move(f, ExtraUid, LegacyWarehouseAction.WithdrawItem);
            var current = Assert.Single(f.Current().PlayerItems.InventoryItems, row => row.Uid == OfferedUid);
            Assert.NotSame(oldRow, current); Assert.NotSame(oldChild, Assert.Single(current.DepositedItems));
            Assert.Same(native, f.Current().NativeGameClient!.Inventory!.ClientItems[OfferedUid]);
            oldRow.OwnerName = "DetachedOnly"; oldChild.Signature = "DetachedChildOnly";
            Assert.Equal("", current.OwnerName); Assert.Equal("", Assert.Single(current.DepositedItems).Signature);
            Assert.False(offer.Closure.MatchesSource(current));
            Assert.True(lineage.MatchesCurrent(f.Store, f.Connection(Fixture.Actor), f.Current(), current));
            var actorStart = f.ActorFrames.Count; var peerStart = f.Frames(Fixture.Peer).Count;

            await Trade(f, Fixture.Actor, new(9)); await Trade(f, Fixture.Peer, new(9));

            Assert.True(HasCompletion(f.ActorFrames.Skip(actorStart)), f.Log.ToString());
            Assert.True(HasCompletion(f.Frames(Fixture.Peer).Skip(peerStart)), f.Log.ToString());
            Assert.DoesNotContain(f.Current().PlayerItems.InventoryItems, row => row.Uid == OfferedUid);
            var received = Assert.Single(f.Current(Fixture.Peer).PlayerItems.InventoryItems);
            Assert.Equal(OfferedUid, received.Uid); Assert.Equal("", received.OwnerName);
            Assert.Equal(ChildUid, Assert.Single(received.DepositedItems).Uid);
            Assert.Equal("", Assert.Single(received.DepositedItems).Signature);
            Assert.Single(f.Current().PlayerItems.InventoryItems, row => row.Uid == ExtraUid);
            foreach (var uid in new[] { Fixture.Actor, Fixture.Peer })
                Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(f.Current(uid).PlayerItems), File.ReadAllBytes(f.ItemPath(uid)));
            Assert.False(LegacyTradeJournal.HasPending(f.Data.Users)); Assert.False(f.Recovery.IsBlocked);
        });

    [Fact]
    public Task ActualOfferRequiresItsPublishedRowAndCannotMintAnotherLineageOutsideThePairIssuer() =>
        Run(withChild: true, extra: false, async f =>
        {
            await OpenAndOffer(f);
            var current = f.Current(); var row = Assert.Single(current.PlayerItems.InventoryItems);
            var lineage = Assert.IsType<WorldSessionStore.TradeItemLineage>(Offer(f).Lineage);
            var copy = row.WithPosition(row.Position); Assert.NotSame(row, copy);
            var files = f.Data.Files(); var frames = f.ActorFrames.Count;
            Assert.True(lineage.MatchesCurrent(f.Store, f.Connection(Fixture.Actor), current, row));
            Assert.False(lineage.MatchesCurrent(f.Store, f.Connection(Fixture.Actor), current, copy));
            Assert.True(LegacyItemSaveGraph.TryCapture(current.PlayerItems, out var graph));
            Assert.True(graph.TryGetInventory(OfferedUid, out var closure));
            Assert.False(f.Store.TryCaptureTradeItemLineage(f.Connection(Fixture.Actor), current, closure, out var unissued));
            Assert.Null(unissued); Assert.Same(current, f.Current()); Assert.Equal(frames, f.ActorFrames.Count);
            f.Data.AssertFiles(files);
        });

    [Fact]
    public Task ActualStoreRetirementInvalidatesTheRetainedOfferInsteadOfMatchingItsUidAndValues() =>
        Run(withChild: true, extra: false, async f =>
        {
            await OpenAndOffer(f); await Trade(f, Fixture.Actor, new(9));
            var expected = f.Current(); var row = Assert.Single(expected.PlayerItems.InventoryItems);
            var binding = Assert.IsType<LegacyOrdinarySpellBinding>(expected.NativeGameClient!.OrdinarySpellBinding);
            var lineage = Assert.IsType<WorldSessionStore.TradeItemLineage>(Offer(f).Lineage);
            var files = f.Data.Files(); int peerStart = f.Frames(Fixture.Peer).Count;

            Assert.True(f.Store.TryRemove(f.Connection(Fixture.Actor), out var removed));

            Assert.Same(expected, removed); Assert.True(binding.IsRetired);
            Assert.False(lineage.MatchesCurrent(f.Store, f.Connection(Fixture.Actor), expected, row));
            await Trade(f, Fixture.Peer, new(9));
            Assert.False(HasCompletion(f.Frames(Fixture.Peer).Skip(peerStart)));
            Assert.Empty(f.Current(Fixture.Peer).PlayerItems.InventoryItems); f.Data.AssertFiles(files);
        });

    private static Task Run(bool withChild, bool extra, Func<Fixture, Task> body)
    {
        var child = new PlayerItemSnapshotItem { Uid = ChildUid, ItemId = 1000000, StackSize = 1, Durability = 1, MaximumDurability = 1 };
        var offered = new PlayerItemSnapshotItem { Uid = OfferedUid, ItemId = SashType, StackSize = 1, Durability = 1,
            MaximumDurability = 1, DepositedItems = withChild ? new[] { child } : Array.Empty<PlayerItemSnapshotItem>() };
        var rows = new List<PlayerItemSnapshotItem> { offered };
        if (extra) rows.Add(new() { Uid = ExtraUid, ItemId = 1088001, StackSize = 1, Durability = 1, MaximumDurability = 1 });
        var actor = Fixture.Character(minHitPoints: 1000, reborn: 0) with { Money = 1000, ConquerPoints = 100 };
        var peer = Fixture.Character(Fixture.Peer, "LineagePeer", x: 831, minHitPoints: 1000, reborn: 0)
            with { Money = 2000, ConquerPoints = 200 };
        var catalog = Fixture.Catalog(Fixture.Item(SashType), Fixture.Item(1000000), Fixture.Item(1088001));
        return Fixture.RunAsync(new(actor, new(rows, [], [], [], [])), catalog, body,
            new OwnedEquipmentBehaviorInput(peer, PlayerItemSnapshot.Empty));
    }

    private static async Task OpenAndOffer(Fixture f)
    {
        await Trade(f, Fixture.Actor, new(0) { UID = Fixture.Peer });
        await Trade(f, Fixture.Peer, new(1) { UID = Fixture.Actor });
        await Trade(f, Fixture.Actor, new(6) { ItemUID = OfferedUid });
        Assert.Equal(OfferedUid, Offer(f).Item.Uid);
    }

    private static LegacyTradeOffer Offer(Fixture f)
    {
        var trades = Assert.IsType<LegacyTradeStateStore>(typeof(WorldServerFrameHandler)
            .GetField("_tradeStates", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(f.Handler));
        lock (trades.SyncRoot)
        {
            Assert.True(trades.TryGet(f.Connection(Fixture.Actor), out var party));
            return Assert.Single(party.Items).Value;
        }
    }

    private static Task Trade(Fixture f, uint uid, LegacyTradePacket packet) => f.SendAsync(uid, packet.ToNativeFrameBytes());
    private static Task Move(Fixture f, uint uid, LegacyWarehouseAction action) => f.SendAsync(Fixture.Actor,
        new LegacyWarehousePacket(8, action, uid, 0, []).ToFrameBytes());
    private static bool HasCompletion(IEnumerable<byte[]> frames) => frames.Any(bytes =>
        LegacyTradePacket.TryParse(Fixture.ReadFrame(bytes), out var packet) && packet.Action == 4);
}
