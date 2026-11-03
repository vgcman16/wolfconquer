using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using Fixture = WolfConquer.Tests.OwnedEquipmentBehaviorFixture;

namespace WolfConquer.Tests;

public sealed partial class OwnedVendorListingIncarnationTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task ActualNativeListingRejectsSelectedOverCapRootOrChildBeforePublication(bool child)
    {
        var selected = new PlayerItemSnapshotItem { Uid = child ? Child : Listed, ItemId = Plain,
            StackSize = 2, Durability = 1, MaximumDurability = 1 };
        var root = child ? new PlayerItemSnapshotItem { Uid = Listed, ItemId = Sash, StackSize = 1,
            Durability = 1, MaximumDurability = 1, DepositedItems = [selected] } : selected;
        return RunMetadata(root, Fixture.Catalog(MetadataRow(Plain, "1"), MetadataRow(Sash, "1")), async f =>
        {
            await StartOnly(f);
            Assert.False(f.Store.IsUnboundTradeItemOwnerCurrent(f.Connection(Fixture.Actor), f.Current()));
            var before = Before.Capture(f); var log = f.Log.ToString().Length;
            await f.SendAsync(Fixture.Actor, Listing().ToFrameBytes());
            before.Unchanged(f); Assert.Empty(f.Current().LegacyVendorListings!.Listings);
            Assert.Contains("listing-item-metadata-invalid", f.Log.ToString()[log..], StringComparison.Ordinal);
        });
    }

    [Fact]
    public Task ActualNativeListingRejectsSelectedTypeMissingFromNonemptyMetadata()
    {
        var root = new PlayerItemSnapshotItem { Uid = Listed, ItemId = Plain, StackSize = 1,
            Durability = 1, MaximumDurability = 1 };
        return RunMetadata(root, Fixture.Catalog(MetadataRow(Sash, "1")), async f =>
        {
            await StartOnly(f); var before = Before.Capture(f); var log = f.Log.ToString().Length;
            await f.SendAsync(Fixture.Actor, Listing().ToFrameBytes());
            before.Unchanged(f); Assert.Empty(f.Current().LegacyVendorListings!.Listings);
            Assert.Contains("listing-item-metadata-invalid", f.Log.ToString()[log..], StringComparison.Ordinal);
        });
    }

    [Fact]
    public Task PublicCapMutationCannotIncreasePublishedMetadataAuthorityForNativeListing()
    {
        var row = MetadataRow(Plain, "1"); var catalog = Fixture.Catalog(row);
        var root = new PlayerItemSnapshotItem { Uid = Listed, ItemId = Plain, StackSize = 2,
            Durability = 1, MaximumDurability = 1 };
        return RunMetadata(root, catalog, async f =>
        {
            await StartOnly(f);
            var owner = Assert.IsType<LegacyTradeItemMetadata>(catalog.TradeItemMetadata);
            Assert.True(owner.IsCurrent(catalog)); Assert.False(owner.Allows(root));
            Assert.IsType<string[]>(row.Fields)[ItemType.StackSizeFieldIndex] = "2";
            Assert.Same(owner, catalog.TradeItemMetadata); Assert.False(owner.Allows(root));
            var before = Before.Capture(f); var log = f.Log.ToString().Length;
            await f.SendAsync(Fixture.Actor, Listing().ToFrameBytes());
            before.Unchanged(f); Assert.Empty(f.Current().LegacyVendorListings!.Listings);
            Assert.Contains("listing-item-metadata-invalid", f.Log.ToString()[log..], StringComparison.Ordinal);
        });
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public Task ActualNativeZeroOneAndFullCapStacksRemainSellable(int stack)
    {
        var root = new PlayerItemSnapshotItem { Uid = Listed, ItemId = Plain, StackSize = checked((ushort)stack),
            Durability = 1, MaximumDurability = 1 };
        return RunMetadata(root, Fixture.Catalog(MetadataRow(Plain, "2")), async f =>
        {
            var booth = await StartAndList(f); await AssertShown(f, booth);
            var before = Before.Capture(f); var request = Buy(booth);
            await f.SendAsync(Fixture.Peer, request.ToFrameBytes());
            var sold = Assert.Single(f.Current(Fixture.Peer).PlayerItems.InventoryItems);
            Assert.Equal(Listed, sold.Uid); Assert.Equal(checked((ushort)stack), sold.StackSize);
            Assert.Empty(f.Current().PlayerItems.InventoryItems);
            Assert.Empty(f.Current().LegacyVendorListings!.Listings);
            Assert.Equal(before.Buyer.Character.Money - 125, f.Current(Fixture.Peer).Character.Money);
            Assert.Equal(before.Seller.Character.Money + 125, f.Current().Character.Money);
            var buyerFrames = f.Frames(Fixture.Peer).Skip(before.BuyerFrames).ToArray();
            var sellerFrames = f.ActorFrames.Skip(before.SellerFrames).ToArray();
            Assert.Equal(3, buyerFrames.Length); Assert.Equal(3, sellerFrames.Length);
            Assert.Equal(LegacyGameItemPacket.FromSnapshotItem(
                Assert.Single(before.Seller.PlayerItems.InventoryItems)).ToFrameBytes(), buyerFrames[0]);
            Assert.Equal(request.ToFrameBytes(), buyerFrames[1]);
            Assert.Equal((request with { Action = MsgItemUsageAction.RemoveVendingItem }).ToFrameBytes(), sellerFrames[0]);
            Assert.Equal(new MsgItemUsagePacket(Listed, 0, MsgItemUsageAction.RemoveInventory, 0, 0, 0, 0).ToFrameBytes(), sellerFrames[1]);
            Assert.Equal(new LegacyMsgUpdatePacket(Fixture.Peer, [new(LegacyMsgUpdateDataType.Money,
                checked((ulong)f.Current(Fixture.Peer).Character.Money))]).ToFrameBytes(), buyerFrames[2]);
            Assert.Equal(new LegacyMsgUpdatePacket(Fixture.Actor, [new(LegacyMsgUpdateDataType.Money,
                checked((ulong)f.Current().Character.Money))]).ToFrameBytes(), sellerFrames[2]);
            AssertActors(f);
            var settled = Before.Capture(f);
            await f.SendAsync(Fixture.Peer, request.ToFrameBytes()); settled.Unchanged(f);
        });
    }

    private static ItemType MetadataRow(uint id, string cap) => Fixture.Item(id,
        fields: new Dictionary<int, string> { [ItemType.StackSizeFieldIndex] = cap });

    private static Task RunMetadata(PlayerItemSnapshotItem root, WorldDataCatalog catalog, Func<Fixture, Task> body)
    {
        var seller = Fixture.Character(minHitPoints: 1000, reborn: 0) with { Money = 1000, ConquerPoints = 100 };
        var buyer = Fixture.Character(Fixture.Peer, "VendorBuyer", x: 831, minHitPoints: 1000, reborn: 0)
            with { Money = 2000, ConquerPoints = 200 };
        return Fixture.RunAsync(new(seller, new([root], [], [], [], [])), catalog, body,
            new OwnedEquipmentBehaviorInput(buyer, PlayerItemSnapshot.Empty));
    }

    private static async Task StartOnly(Fixture f)
    {
        await f.SendAsync(Fixture.Actor, new MsgActionPacket { ObjId = Fixture.Actor, Type = MsgActionType.StartVendor }.ToFrameBytes());
        Assert.True(uint.TryParse(f.Current().Character.Attributes["RuntimeVendorUid"],
            NumberStyles.None, CultureInfo.InvariantCulture, out var booth));
        Assert.NotEqual(0U, booth); Assert.Empty(f.Current().LegacyVendorListings!.Listings); AssertActors(f);
    }
}
