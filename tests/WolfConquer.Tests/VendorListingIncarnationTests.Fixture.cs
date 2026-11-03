using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

// Registered, detached in-process component only: real Store and owned files,
// no ordinary/native admission, SQL provider, TCP writer or live client claim.
public sealed partial class VendorListingIncarnationTests
{
    private sealed partial class ComponentFixture : IDisposable
    {
        internal const uint SoldUid = 910001;
        private static readonly PacketHandlerRegistry<WorldPacketHandlerContext> Registry =
            PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);
        internal readonly D13VendorTestHarness Harness = new();
        internal readonly ConquerTcpConnection SellerConnection, BuyerConnection;
        internal readonly bool Cp;
        internal readonly List<PlayerItemSnapshotItem> Children;
        internal PlayerItemSnapshotItem Root { get; private set; }
        internal long Price { get; private set; } = 125;
        internal uint BoothUid { get; private set; }
        internal WorldSession Seller => Harness.StoredSession(SellerConnection);
        internal WorldSession Buyer => Harness.StoredSession(BuyerConnection);
        internal string SellerItemsPath => ItemPath(D13VendorTestHarness.SellerCharacterUid);
        internal string BuyerItemsPath => ItemPath(D13VendorTestHarness.BuyerCharacterUid);

        internal ComponentFixture(bool cp)
        {
            Cp = cp;
            Children = [D13VendorTestHarness.Item(910002, 1088001), D13VendorTestHarness.Item(910003, 1088001)];
            Root = D13VendorTestHarness.Item(SoldUid, 1100009).WithDepositedItems(Children);
            SellerConnection = Harness.CreateConnection("seller"); BuyerConnection = Harness.CreateConnection("buyer");
            Harness.Sessions.Set(SellerConnection, Harness.CreateSeller(300, 50, D13VendorTestHarness.Items(Root)));
            Harness.Sessions.Set(BuyerConnection, Harness.CreateBuyer(1000, 200, PlayerItemSnapshot.Empty));
            Harness.SeedPersistence(Seller, Buyer);
            RequireDetachedCurrentOwners();
        }

        internal async Task StartAndListAsync()
        {
            Assert.False(Seller.Character.Attributes.ContainsKey("RuntimeVendorUid"));
            using var log = new StringWriter();
            var context = new WorldPacketHandlerContext(SellerConnection, log, Seller,
                Sessions: Harness.Sessions, PlayerItemsRoot: Harness.PlayerItemsRoot,
                ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13);
            await Registry.HandleAsync(context, D13VendorTestHarness.ReadFrame(new MsgActionPacket
            { ObjId = D13VendorTestHarness.SellerCharacterUid, Type = MsgActionType.StartVendor }.ToFrameBytes()));
            var reply = Assert.Single(Harness.Sent, sent => sent.Recipient == "seller" &&
                MsgActionPacket.TryParse(D13VendorTestHarness.ReadFrame(sent.Bytes), out var action) && action.Type == MsgActionType.StartVendor);
            Assert.True(MsgActionPacket.TryParse(D13VendorTestHarness.ReadFrame(reply.Bytes), out var started));
            BoothUid = started.DwParam; Assert.NotEqual(0U, BoothUid);
            Assert.Equal(BoothUid.ToString(CultureInfo.InvariantCulture), Seller.Character.Attributes["RuntimeVendorUid"]);
            Assert.Empty(Assert.IsType<LegacyVendorListingBook>(Seller.LegacyVendorListings).Listings);
            Harness.Sent.Clear();
            await ListAsync();
        }

        internal async Task ListAsync(long? amount = null)
        {
            long price = amount ?? Price;
            var request = new MsgItemUsagePacket(SoldUid, checked((ulong)price), Cp
                ? MsgItemUsageAction.AddVendingItemConquerPoints : MsgItemUsageAction.AddVendingItemGold, 3210, 0, 0, 0);
            Harness.Sent.Clear(); await SendAsync(false, request);
            var reply = Assert.Single(Harness.Sent); Assert.Equal("seller", reply.Recipient);
            Assert.Equal(request.ToFrameBytes(), reply.Bytes);
            Assert.True(Seller.LegacyVendorListings!.TryGetListing(SoldUid, out var listing));
            Assert.Equal(price, listing.Amount);
            Assert.Equal(Cp ? LegacyVendorListingCostMode.CPs : LegacyVendorListingCostMode.Gold, listing.CostMode);
            Price = price;
            Assert.Same(Root, Assert.Single(Seller.PlayerItems.InventoryItems));
            Harness.Sent.Clear();
        }

        internal MsgItemUsagePacket BuyRequest() => new(SoldUid, BoothUid, MsgItemUsageAction.BuyVendingItem, 3456, 2, 3, 4);
        internal Task<string> BuyAsync() => SendAsync(true, BuyRequest());
        internal Task<string> ShowAsync() => SendAsync(true, new(BoothUid, 0, MsgItemUsageAction.ShowVendingList, 4500, 0, 0, 0));
        internal Task<string> SendAsync(bool buyer, MsgItemUsagePacket packet) =>
            DispatchCatalogAsync(buyer, packet);

        internal async Task RemoveAsync()
        {
            var request = new MsgItemUsagePacket(SoldUid, 0, MsgItemUsageAction.RemoveVendingItem, 4567, 0, 0, 0);
            Harness.Sent.Clear(); await SendAsync(false, request);
            Assert.Equal(request.ToFrameBytes(), Assert.Single(Harness.Sent).Bytes);
            Assert.Empty(Seller.LegacyVendorListings!.Listings); Harness.Sent.Clear();
        }

        internal void ChangeCurrentItem(string mode)
        {
            var before = PlayerItemSnapshotLoader.ToLegacyBytes(Seller.PlayerItems);
            var oldBook = Seller.LegacyVendorListings;
            var oldRoot = Root;
            switch (mode)
            {
                case "different-type": Root = Root.WithItemId(1100006); break;
                case "equal-root": Root = Root.WithPosition(Root.Position); break;
                case "root-value": Root.PerfectionLevel++; break;
                case "child-value": Children[0].Signature = "ChangedAfterListing"; break;
                case "equal-child":
                    var oldChild = Children[0]; Children[0] = oldChild.WithPosition(oldChild.Position);
                    Assert.NotSame(oldChild, Children[0]); break;
                case "child-order": Children.Reverse(); break;
                case "child-added": Children.Add(D13VendorTestHarness.Item(910004, 1088001)); break;
                default: throw new ArgumentOutOfRangeException(nameof(mode));
            }
            if (!ReferenceEquals(oldRoot, Root))
            {
                Harness.Sessions.Set(SellerConnection, Seller with
                { PlayerItems = Seller.PlayerItems with { InventoryItems = new[] { Root } } });
                Assert.NotSame(oldRoot, Root);
            }
            Assert.Same(oldBook, Seller.LegacyVendorListings);
            Assert.Same(Root, Assert.Single(Seller.PlayerItems.InventoryItems)); Assert.Equal(SoldUid, Root.Uid);
            Assert.True(LegacyItemSaveGraph.TryCapture(Seller.PlayerItems, out _));
            foreach (var row in new[] { Root }.Concat(Root.DepositedItems))
            {
                Assert.Equal((byte)0, row.Bound); Assert.Equal((byte)0, row.Locked); Assert.Equal((ushort)0, row.Inscribed);
                Assert.False(LegacyNativeItemRestrictions.IsUnableTradeItem(row.ItemId));
            }
            var after = PlayerItemSnapshotLoader.ToLegacyBytes(Seller.PlayerItems);
            if (mode is "equal-root" or "equal-child") Assert.Equal(before, after);
            else Assert.NotEqual(Convert.ToHexString(before), Convert.ToHexString(after));
            new PlayerItemSnapshotLoader(Harness.PlayerItemsRoot).SaveByCharacterUid(Seller.Character.Uid, Seller.PlayerItems);
            RequireDetachedCurrentOwners();
        }

        internal void RequireDetachedCurrentOwners()
        {
            Assert.Null(Seller.NativeGameClient); Assert.Null(Buyer.NativeGameClient);
            Assert.Null(SellerConnection.NativeGameClient); Assert.Null(BuyerConnection.NativeGameClient);
            Assert.True(Harness.Sessions.IsUnboundTradeItemOwnerCurrent(SellerConnection, Seller));
            Assert.True(Harness.Sessions.IsUnboundTradeItemOwnerCurrent(BuyerConnection, Buyer));
        }

        internal Dictionary<string, byte[]> Files() => new[] { Harness.UsersRoot, Harness.PlayerItemsRoot }
            .SelectMany(root => Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            .ToDictionary(path => path, File.ReadAllBytes, StringComparer.OrdinalIgnoreCase);
        internal void RequireFiles(Dictionary<string, byte[]> expected)
        {
            var actual = Files(); Assert.Equal(expected.Keys.Order(), actual.Keys.Order());
            foreach (var file in expected) Assert.Equal(file.Value, actual[file.Key]);
        }
        private string ItemPath(uint uid) => Path.Combine(Harness.PlayerItemsRoot, uid.ToString(CultureInfo.InvariantCulture) + ".bin");
        public void Dispose() => Harness.Dispose();
    }

    private sealed record Before(WorldSession Seller, WorldSession Buyer, LegacyVendorListingBook Book,
        LegacyVendorListing[] Listings, byte[] SellerItems, byte[] BuyerItems, Dictionary<string, byte[]> Files)
    {
        internal static Before Capture(ComponentFixture f) => new(f.Seller, f.Buyer, f.Seller.LegacyVendorListings!,
            f.Seller.LegacyVendorListings!.Listings.ToArray(), PlayerItemSnapshotLoader.ToLegacyBytes(f.Seller.PlayerItems),
            PlayerItemSnapshotLoader.ToLegacyBytes(f.Buyer.PlayerItems), f.Files());
    }

    private static void AssertUnchanged(ComponentFixture f, Before before)
    {
        Assert.Same(before.Seller, f.Seller); Assert.Same(before.Buyer, f.Buyer);
        Assert.Same(before.Book, f.Seller.LegacyVendorListings); Assert.Equal(before.Listings, f.Seller.LegacyVendorListings!.Listings);
        Assert.Equal(before.SellerItems, PlayerItemSnapshotLoader.ToLegacyBytes(f.Seller.PlayerItems));
        Assert.Equal(before.BuyerItems, PlayerItemSnapshotLoader.ToLegacyBytes(f.Buyer.PlayerItems));
        Assert.Equal(before.SellerItems, File.ReadAllBytes(f.SellerItemsPath)); Assert.Equal(before.BuyerItems, File.ReadAllBytes(f.BuyerItemsPath));
        Assert.Equal((before.Seller.Character.Money, before.Seller.Character.ConquerPoints), f.Harness.ReadBalances(before.Seller.AccountId));
        Assert.Equal((before.Buyer.Character.Money, before.Buyer.Character.ConquerPoints), f.Harness.ReadBalances(before.Buyer.AccountId));
        f.RequireFiles(before.Files); f.RequireDetachedCurrentOwners();
        Assert.Empty(f.Harness.Sent); Assert.False(LegacyTradeJournal.HasPending(f.Harness.UsersRoot));
    }
}
