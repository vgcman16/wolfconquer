using System.Text;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyPlayerBoothItemClosureTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task RegisteredBoothProtectedChildRequiresActualAnimaActivationBeforeListing(bool animaItemType) =>
        RunBoothSettlementAsync(async fixture =>
        {
            fixture.Seed(BoothItem(10, 1_100_009), animaItemType
                ? BoothItem(11, 4_200_001) : BoothItem(11).WithAnimaItemId(4_200_001));
            await fixture.DepositAsync(11);
            await fixture.StartAsync();
            await fixture.CreateBoothAnimaPinAsync();
            var before = fixture.Capture();

            await fixture.ListAsync();

            AssertBoothAnimaListingRefused(fixture, before);
            Assert.Empty(fixture.Seller.LegacyVendorListings!.Listings);
        });

    [Fact]
    public Task RegisteredBoothGenuineAnimaActivationAllowsTheSameProtectedChildAndExactPurchase() =>
        RunBoothSettlementAsync(async fixture =>
        {
            fixture.Seed(BoothItem(10, 1_100_009), BoothItem(11).WithAnimaItemId(4_200_001));
            await fixture.DepositAsync(11);
            await fixture.StartAsync();
            await fixture.CreateBoothAnimaPinAsync();
            var child = Assert.Single(Assert.Single(fixture.Seller.PlayerItems.InventoryItems).DepositedItems);
            var denied = fixture.Capture();
            await fixture.ListAsync();
            AssertBoothAnimaListingRefused(fixture, denied);

            await fixture.ActivateBoothAnimaPinAsync();
            Assert.Same(child, Assert.Single(Assert.Single(fixture.Seller.PlayerItems.InventoryItems).DepositedItems));
            await fixture.ListBoothAnimaItemAsync();
            var before = fixture.Capture();
            await fixture.BuyAsync();

            AssertOneSettledBoothPurchase(fixture, before);
            var received = Assert.Single(fixture.Buyer.PlayerItems.InventoryItems, row => row.Uid == 10);
            Assert.Equal(4_200_001U, Assert.Single(received.DepositedItems).AnimaItemId);
            Assert.NotSame(child, Assert.Single(received.DepositedItems));
        });

    [Fact]
    public Task RegisteredBoothPinCreatedAfterListingRequiresCurrentActivationAtPurchase() =>
        RunBoothSettlementAsync(async fixture =>
        {
            fixture.Seed(BoothItem(10, 1_100_009), BoothItem(11, 4_200_001));
            await fixture.DepositAsync(11);
            await fixture.StartAsync();
            Assert.False(fixture.Seller.Character.Attributes.ContainsKey("PinCodeAnima"));
            await fixture.ListBoothAnimaItemAsync();
            var listedBook = fixture.Seller.LegacyVendorListings;
            var listedRoot = Assert.Single(fixture.Seller.PlayerItems.InventoryItems);

            // Genuine permission change, not a fabricated TREPIN2=false write:
            // an unprotected account creates a PIN through registered NPC2 -> NPC3.
            await fixture.CreateBoothAnimaPinAsync();
            Assert.Same(listedBook, fixture.Seller.LegacyVendorListings);
            Assert.Same(listedRoot, Assert.Single(fixture.Seller.PlayerItems.InventoryItems));
            var before = fixture.Capture();
            await fixture.BuyAsync();

            fixture.AssertUnchanged(before);
            Assert.True(fixture.Seller.LegacyVendorListings!.Contains(10));
            Assert.False(fixture.SettlementRecovery.IsBlocked);
            // The SAME listing and deposited rows become eligible through the real
            // activation owner, not by reseeding files, book or authorization flags.
            await fixture.ActivateBoothAnimaPinAsync();
            var admitted = fixture.Capture();
            await fixture.BuyAsync();
            AssertOneSettledBoothPurchase(fixture, admitted);
        });

    private static void AssertBoothAnimaListingRefused(BoothFixture fixture, Snapshot before)
    {
        var prompt = Assert.Single(fixture.Harness.Sent.Skip(before.FrameCount));
        Assert.Equal("seller", prompt.Recipient);
        Assert.Equal(LegacyNpcReplyPacket.ForMessageBox("Please Active Pincode").ToFrameBytes(), prompt.Bytes);
        // The native rejection prompt is allowed; no listing echo or success output is.
        fixture.AssertUnchanged(before with { FrameCount = before.FrameCount + 1 });
    }

    private sealed partial class BoothFixture
    {
        private const uint BoothAnimaNpc = 489_123;

        private Task DispatchBoothAnimaAsync(byte optionId, string input = "") =>
            DispatchAsync(SellerConnection, LegacyNpcServerRequestPacket.Script(
                BoothAnimaNpc, optionId, interactType: 1, input: input).ToFrameBytes());

        internal async Task CreateBoothAnimaPinAsync()
        {
            var before = Capture();
            Assert.False(Seller.Character.Attributes.ContainsKey("PinCodeAnima"));
            Assert.False(Seller.Character.Attributes.ContainsKey("TREPIN2"));
            await DispatchBoothAnimaAsync(2, "ABC123");
            Assert.Equal("ABC123", Seller.LegacyAnimaSecurityPendingPin);
            Assert.False(Seller.LegacyAnimaSecurityOldPinVerified);
            AssertFiles(before.Files); // Staging does not persist or activate the PIN.
            var firstCreationFrame = Harness.Sent.Count;
            await DispatchBoothAnimaAsync(3);

            Assert.Equal("abc123", Seller.Character.Attributes["PinCodeAnima"]);
            Assert.False(Seller.Character.Attributes.ContainsKey("TREPIN2"));
            Assert.Same(before.Buyer, Buyer);
            Assert.Same(before.Seller.PlayerItems, Seller.PlayerItems);
            Assert.Same(before.Seller.LegacyVendorListings, Seller.LegacyVendorListings);
            Assert.Equal(before.Seller.Character.Money, Seller.Character.Money);
            Assert.Equal(before.Seller.Character.ConquerPoints, Seller.Character.ConquerPoints);
            var expected = new Dictionary<string, byte[]>(before.Files, StringComparer.OrdinalIgnoreCase);
            var key = Path.GetRelativePath(Root, CharacterPath(Seller));
            var original = Encoding.UTF8.GetString(before.Files[key]);
            Assert.DoesNotContain("PinCodeAnima=", original, StringComparison.Ordinal);
            Assert.Contains("[Unrelated]", original, StringComparison.Ordinal);
            expected[key] = new UTF8Encoding(false).GetBytes(original.Replace("[Unrelated]",
                "PinCodeAnima=abc123" + Environment.NewLine + "[Unrelated]", StringComparison.Ordinal));
            AssertFiles(expected); // Only that real saved PIN line is allowed to change.
            var frames = Harness.Sent.Skip(firstCreationFrame).ToArray();
            Assert.Equal(3, frames.Length);
            Assert.All(frames, frame => Assert.Equal("seller", frame.Recipient));
            Assert.Equal(LegacyNpcReplyPacket.ForMessageBox("Completed ABC123").ToFrameBytes(), frames[0].Bytes);
            Assert.Equal(LegacyNpcReplyPacket.ForAvatar(286).ToFrameBytes(), frames[1].Bytes);
            Assert.Equal(LegacyNpcReplyPacket.ForFinish().ToFrameBytes(), frames[2].Bytes);
        }

        internal async Task ActivateBoothAnimaPinAsync()
        {
            var before = Capture();
            await DispatchBoothAnimaAsync(80, "ABC123");
            Assert.Equal("true", Seller.Character.Attributes["TREPIN2"]);
            Assert.Equal("abc123", Seller.Character.Attributes["PinCodeAnima"]);
            Assert.Same(before.Buyer, Buyer);
            Assert.Same(before.Seller.PlayerItems, Seller.PlayerItems);
            Assert.Same(before.Seller.LegacyVendorListings, Seller.LegacyVendorListings);
            Assert.Equal(before.Seller.Character.Money, Seller.Character.Money);
            Assert.Equal(before.Seller.Character.ConquerPoints, Seller.Character.ConquerPoints);
            AssertFiles(before.Files); // Real activation is transient, never an invented disk authority.
            var frames = Harness.Sent.Skip(before.FrameCount).ToArray();
            Assert.Equal(2, frames.Length);
            Assert.All(frames, frame => Assert.Equal("seller", frame.Recipient));
            Assert.Equal(LegacyNpcReplyPacket.ForMessageBox("Account activated").ToFrameBytes(), frames[0].Bytes);
            Assert.Equal(LegacyNpcReplyPacket.ForFinish().ToFrameBytes(), frames[1].Bytes);
        }

        internal async Task ListBoothAnimaItemAsync()
        {
            var before = Capture();
            await ListAsync();
            var response = Assert.Single(Harness.Sent.Skip(before.FrameCount));
            Assert.Equal("seller", response.Recipient);
            Assert.Equal(ListingRequest().ToFrameBytes(), response.Bytes);
            Assert.Single(Seller.LegacyVendorListings!.Listings);
            Assert.True(Seller.LegacyVendorListings.Contains(10));
            AssertFiles(before.Files);
        }
    }
}
