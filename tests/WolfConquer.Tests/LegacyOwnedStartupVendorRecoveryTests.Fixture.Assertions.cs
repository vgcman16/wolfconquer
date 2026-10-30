using System.Globalization;
using System.Text.Json.Nodes;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

internal sealed partial class LegacyOwnedStartupVendorRecoveryFixture
{
    internal LegacyOwnedAdmissionRuntime Open()
    {
        var success = LegacyOwnedAdmissionRuntime.TryOpen(Data.Loader, Data.Options(), out var opened, out var reason);
        if (opened is not null) runtimes.Add(opened);
        Assert.True(success, reason);
        var runtime = Assert.IsType<LegacyOwnedAdmissionRuntime>(opened);
        Assert.True(runtime.Ordinary.IsReconciliationComplete);
        Assert.True(runtime.Recovery.IsReadyForOrdinaryAdmission(Data.Options().Roots)); return runtime;
    }

    private void AssertListing()
    {
        Assert.True(Current(SellerUid).LegacyVendorListings!.TryGetListing(SoldUid, out var listing));
        Assert.Equal(Amount, listing.Amount);
        Assert.Equal(IsCp ? LegacyVendorListingCostMode.CPs : LegacyVendorListingCostMode.Gold, listing.CostMode);
    }

    private void AssertExpectedCapturedImages()
    {
        var expectedBuyer = Replace(Before[CharacterPath(BuyerUid)], new Dictionary<string, string>
        {
            ["Money=2000\r\n"] = "Money=" + (IsCp ? 2_000 : 2_000 - Amount).ToString(CultureInfo.InvariantCulture) + "\r\n",
            ["ConquerPoints=60\r\n"] = "ConquerPoints=" + (IsCp ? 60 - Amount : 60).ToString(CultureInfo.InvariantCulture) + "\r\n",
        });
        var expectedSeller = Replace(Before[CharacterPath(SellerUid)], new Dictionary<string, string>
        {
            ["Money=777\r\n"] = "Money=" + (IsCp ? 777 : 777 + Amount).ToString(CultureInfo.InvariantCulture) + "\r\n",
            ["ConquerPoints=222\r\n"] = "ConquerPoints=" + (IsCp ? 222 + Amount : 222).ToString(CultureInfo.InvariantCulture) + "\r\n",
        });
        Assert.Equal(expectedBuyer, After[CharacterPath(BuyerUid)]); Assert.Equal(expectedSeller, After[CharacterPath(SellerUid)]);
        foreach (var uid in new[] { BuyerUid, SellerUid })
        { AssertNoEscrow(ReadCharacter(Before[CharacterPath(uid)])); AssertNoEscrow(ReadCharacter(After[CharacterPath(uid)])); }
        var buyer = PlayerItemSnapshotLoader.Parse(Before[ItemsPath(BuyerUid)]);
        var seller = PlayerItemSnapshotLoader.Parse(Before[ItemsPath(SellerUid)]);
        var sold = Assert.Single(seller.InventoryItems, row => row.Uid == SoldUid);
        Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(buyer with { InventoryItems = buyer.InventoryItems.Append(sold).ToArray() }),
            After[ItemsPath(BuyerUid)]);
        Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(seller with { InventoryItems = seller.InventoryItems.Where(row => row.Uid != SoldUid).ToArray() }),
            After[ItemsPath(SellerUid)]);
    }

    internal void AssertRecoveredImages()
    {
        Assert.False(LegacyTradeJournal.HasPending(Data.Users));
        foreach (var (path, bytes) in After) Assert.Equal(bytes, File.ReadAllBytes(path));
        Assert.Equal(RegistryState, File.ReadAllBytes(Data.State));
        Assert.Equal(LegacyOwnedAdmissionRuntimeFixture.HistoricalFloor, JsonNode.Parse(RegistryState)!["HighWater"]!.GetValue<uint>());
        AssertExpectedCapturedImages();
        var buyer = new PlayerItemSnapshotLoader(Data.Items).LoadByCharacterUid(BuyerUid);
        var seller = new PlayerItemSnapshotLoader(Data.Items).LoadByCharacterUid(SellerUid);
        var root = Assert.Single(buyer.InventoryItems, row => row.Uid == SoldUid);
        Assert.Equal(DepositUid, Assert.Single(root.DepositedItems).Uid);
        Assert.Equal(1U, root.PerfectionLevel); Assert.Equal(SellerUid, root.OwnerUid);
        Assert.Equal("RecordedOwner", root.OwnerName); Assert.Equal("owned-vendor", root.Signature);
        Assert.DoesNotContain(seller.InventoryItems, row => row.Uid == SoldUid);
        Assert.Equal(SellerKeptUid, Assert.Single(seller.InventoryItems).Uid);
        foreach (var item in buyer.WarehouseItems.Concat(seller.WarehouseItems))
        { Assert.Equal((byte)1, item.Bound); Assert.Equal((byte)1, item.Locked); }
        var beforeUids = ReadUids(Before); var afterUids = ReadUids(After);
        Assert.Equal(beforeUids.Length, beforeUids.Distinct().Count());
        Assert.Equal(afterUids.Length, afterUids.Distinct().Count()); Assert.Equal(beforeUids, afterUids);
    }

    internal void AssertRefused(LegacyOwnedAdmissionOptions? options = null, bool leaseAvailable = true)
    {
        var files = Data.Files();
        var success = LegacyOwnedAdmissionRuntime.TryOpen(Data.Loader, options ?? Data.Options(), out var opened, out var reason);
        if (opened is not null) runtimes.Add(opened);
        Assert.False(success, reason); Assert.Null(opened); Assert.NotEmpty(reason);
        Data.AssertFiles(files); Assert.True(LegacyTradeJournal.HasPending(Data.Users));
        if (leaseAvailable) AssertLeaseAvailable();
    }
    internal void AssertLeaseAvailable()
    { using var lease = new FileStream(Data.OwnerLease, FileMode.Open, FileAccess.ReadWrite, FileShare.None); Assert.Equal(0L, lease.Length); }
    internal void Own(IDisposable resource) => resources.Add(resource);
    private uint[] ReadUids(Dictionary<string, byte[]> images) => new[] { BuyerUid, SellerUid }.SelectMany(uid =>
        LegacyItemUidRawCatalog.ReadAdmissionPlayerItemUids(images[ItemsPath(uid)])).Order().ToArray();
    private static CharacterSnapshot ReadCharacter(byte[] bytes) => CharacterSnapshotParser.ParseText(CharacterSnapshotFileEncoding.Decode(bytes, out _));
    private static void AssertNoEscrow(CharacterSnapshot character)
    {
        Assert.True(LegacyTradeEscrowClaims.TryRead(character.Attributes, out var claim, out var reason), reason); Assert.Null(claim);
        foreach (var key in new[] { LegacyTradeEscrowClaims.IdKey, LegacyTradeEscrowClaims.MoneyKey, LegacyTradeEscrowClaims.CpKey })
            Assert.False(character.Attributes.ContainsKey(key));
    }
}
