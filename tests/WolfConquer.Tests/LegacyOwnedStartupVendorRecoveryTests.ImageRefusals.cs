using System.Buffers.Binary;
using System.Text.Json.Nodes;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using Fixture = WolfConquer.Tests.LegacyOwnedStartupVendorRecoveryFixture;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedStartupVendorRecoveryTests
{
    [Theory]
    [InlineData("buyer-credit")] [InlineData("seller-debit")] [InlineData("mismatched-credit")]
    [InlineData("mixed-currencies")] [InlineData("negative-wallet")] [InlineData("cp-overflow")]
    [InlineData("active-before-escrow")] [InlineData("active-after-escrow")] [InlineData("invented-cleared-escrow")]
    [InlineData("unrelated-character")] [InlineData("warehouse-aliases")] [InlineData("misbound-character")]
    [InlineData("reverse-item-direction")] [InlineData("two-root-sale")] [InlineData("changed-deposit")]
    [InlineData("changed-perfection")] [InlineData("before-capacity")]
    [InlineData("uid-birth")] [InlineData("uid-above-high-water")] [InlineData("duplicate-uid")] [InlineData("lost-uid")]
    [InlineData("missing-items-before")] [InlineData("unsupported-version")] [InlineData("unknown-property")]
    [InlineData("participant-order")]
    public Task CorruptingAGenuineRetainedPurchaseCannotBroadenItsRecordedSettlementAuthority(string fault) =>
        WithFixture(async f =>
        {
            await f.StageAsync(4); await f.CloseAsync();
            var record = JsonNode.Parse(File.ReadAllBytes(f.PendingPath))!.AsObject();
            var buyer = Change(record, LegacyTradeJournalFileKind.Character, Fixture.BuyerUid);
            var seller = Change(record, LegacyTradeJournalFileKind.Character, Fixture.SellerUid);
            var buyerItems = Change(record, LegacyTradeJournalFileKind.Items, Fixture.BuyerUid);
            var sellerItems = Change(record, LegacyTradeJournalFileKind.Items, Fixture.SellerUid);
            var beforeBuyerItems = Items(buyerItems, "Before"); var beforeSellerItems = Items(sellerItems, "Before");
            var delivered = Items(buyerItems, "After"); var remaining = Items(sellerItems, "After");
            switch (fault)
            {
                case "buyer-credit": ReplaceImage(buyer, "After", "Money=1875\r\n", "Money=2125\r\n"); break;
                case "seller-debit": ReplaceImage(seller, "After", "Money=902\r\n", "Money=652\r\n"); break;
                case "mismatched-credit": ReplaceImage(seller, "After", "Money=902\r\n", "Money=903\r\n"); break;
                case "mixed-currencies":
                    ReplaceImage(buyer, "After", "ConquerPoints=60\r\n", "ConquerPoints=59\r\n");
                    ReplaceImage(seller, "After", "ConquerPoints=222\r\n", "ConquerPoints=223\r\n"); break;
                case "negative-wallet": ReplaceImage(buyer, "After", "Money=1875\r\n", "Money=-1\r\n"); break;
                case "cp-overflow": ReplaceImage(seller, "After", "ConquerPoints=222\r\n", "ConquerPoints=4294967296\r\n"); break;
                case "active-before-escrow": AddClaim(buyer, "Before", active: true); break;
                case "active-after-escrow": AddClaim(buyer, "After", active: true); break;
                case "invented-cleared-escrow": AddClaim(buyer, "After", active: false); break;
                case "unrelated-character": ReplaceImage(seller, "After", "KeepBytes=startup-owned-value\r\n", "KeepBytes=foreign\r\n"); break;
                case "warehouse-aliases":
                    foreach (var image in new[] { "Before", "After" }) ReplaceImage(seller, image,
                        "WHMoney=555\r\n", "WHMoney=555\r\nWarehouseMoney=556\r\n"); break;
                case "misbound-character": ReplaceImage(seller, "After", $"UID={Fixture.SellerUid}\r\n", $"UID={Fixture.BuyerUid}\r\n"); break;
                case "reverse-item-direction":
                    SetItems(buyerItems, "After", beforeBuyerItems with { InventoryItems = [] });
                    SetItems(sellerItems, "After", beforeSellerItems with
                        { InventoryItems = beforeSellerItems.InventoryItems.Concat(beforeBuyerItems.InventoryItems).ToArray() }); break;
                case "two-root-sale":
                    SetItems(buyerItems, "After", beforeBuyerItems with
                        { InventoryItems = beforeBuyerItems.InventoryItems.Concat(beforeSellerItems.InventoryItems).ToArray() });
                    SetItems(sellerItems, "After", beforeSellerItems with { InventoryItems = [] }); break;
                case "changed-deposit":
                    SetItems(buyerItems, "After", delivered with { InventoryItems = delivered.InventoryItems.Select(row => row.Uid == Fixture.SoldUid
                        ? row.WithDepositedItems([Assert.Single(row.DepositedItems).WithDurability(9)]) : row).ToArray() }); break;
                case "changed-perfection":
                    var sold = Assert.Single(delivered.InventoryItems, row => row.Uid == Fixture.SoldUid);
                    sold.OwnerName = "foreign-owner"; SetItems(buyerItems, "After", delivered); break;
                case "before-capacity":
                    var extras = Enumerable.Range(0, 39).Select(index => NewItem(checked(1_200_000U + (uint)index))).ToArray();
                    SetItems(buyerItems, "Before", beforeBuyerItems with { InventoryItems = beforeBuyerItems.InventoryItems.Concat(extras).ToArray() });
                    SetItems(buyerItems, "After", delivered with { InventoryItems = delivered.InventoryItems.Concat(extras).ToArray() });
                    Assert.Equal(40, Items(buyerItems, "Before").InventoryItems.Count); break;
                case "uid-birth":
                case "uid-above-high-water":
                    var uid = fault == "uid-birth" ? Fixture.SoldUid + 1_000 : LegacyOwnedAdmissionRuntimeFixture.HistoricalFloor + 1;
                    SetItems(buyerItems, "After", delivered with { InventoryItems = delivered.InventoryItems.Append(NewItem(uid)).ToArray() }); break;
                case "duplicate-uid": SetItems(buyerItems, "After", delivered with
                    { InventoryItems = delivered.InventoryItems.Append(delivered.InventoryItems[0]).ToArray() }); break;
                case "lost-uid": SetItems(sellerItems, "After", remaining with { InventoryItems = [] }); break;
                case "missing-items-before": sellerItems["Before"] = null; break;
                case "unsupported-version": record["Version"] = 12; break;
                case "unknown-property": record["AcceptedOwner"] = "invented"; break;
                case "participant-order":
                    var changes = record["Changes"]!.AsArray();
                    var first = changes[0]!.DeepClone(); var second = changes[1]!.DeepClone();
                    changes[0] = second; changes[1] = first; break;
                default: throw new ArgumentOutOfRangeException(nameof(fault));
            }
            WriteCorruptAfterState(f, record);
            f.AssertRefused();
        });

    [Theory]
    [InlineData("bound-root")] [InlineData("locked-root")] [InlineData("inscribed-root")] [InlineData("prohibited-root")]
    [InlineData("bound-deposit")] [InlineData("locked-deposit")]
    public Task VendorStaticRestrictionsApplyToTheConservedTransferredRootAndEveryDeposit(string fault) =>
        WithFixture(async f =>
        {
            await f.StageAsync(4); await f.CloseAsync();
            var record = JsonNode.Parse(File.ReadAllBytes(f.PendingPath))!.AsObject();
            foreach (var change in ItemChanges(record))
                foreach (var image in new[] { "Before", "After" })
                {
                    var snapshot = Items(change, image);
                    SetItems(change, image, snapshot with { InventoryItems = snapshot.InventoryItems.Select(row =>
                        row.Uid == Fixture.SoldUid ? RestrictVendorRoot(row, fault) : row).ToArray() });
                }
            var beforeRoot = Assert.Single(ItemChanges(record).SelectMany(change => Items(change, "Before").InventoryItems), row => row.Uid == Fixture.SoldUid);
            var afterRoot = Assert.Single(ItemChanges(record).SelectMany(change => Items(change, "After").InventoryItems), row => row.Uid == Fixture.SoldUid);
            Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(new([beforeRoot], [], [], [], [])),
                PlayerItemSnapshotLoader.ToLegacyBytes(new([afterRoot], [], [], [], [])));
            var beforeUids = CapturedUids(record, "Before"); var afterUids = CapturedUids(record, "After");
            Assert.Equal(beforeUids, afterUids); Assert.Equal(beforeUids.Length, beforeUids.Distinct().Count());
            WriteCorruptAfterState(f, record); f.AssertRefused();
        });

    private static JsonObject Change(JsonObject record, LegacyTradeJournalFileKind kind, uint uid) => record["Changes"]!.AsArray()
        .Select(row => row!.AsObject()).Single(row => row["Kind"]!.GetValue<int>() == (int)kind && row["FileId"]!.GetValue<long>() == uid);
    private static IEnumerable<JsonObject> ItemChanges(JsonObject record) => record["Changes"]!.AsArray().Select(row => row!.AsObject())
        .Where(row => row["Kind"]!.GetValue<int>() == (int)LegacyTradeJournalFileKind.Items);
    private static PlayerItemSnapshot Items(JsonObject change, string image) => PlayerItemSnapshotLoader.Parse(
        Convert.FromBase64String(change[image]!.GetValue<string>()));
    private static void SetItems(JsonObject change, string image, PlayerItemSnapshot items) =>
        change[image] = Convert.ToBase64String(PlayerItemSnapshotLoader.ToLegacyBytes(items));
    private static PlayerItemSnapshotItem NewItem(uint uid) => new()
    { Uid = uid, ItemId = Fixture.OrdinaryType, StackSize = 1, Durability = 10, MaximumDurability = 10, Color = 5 };
    private static uint[] CapturedUids(JsonObject record, string image) => ItemChanges(record).SelectMany(row =>
        LegacyItemUidRawCatalog.ReadAdmissionPlayerItemUids(Convert.FromBase64String(row[image]!.GetValue<string>()))).Order().ToArray();
    private static void ReplaceImage(JsonObject change, string image, string before, string after) => change[image] =
        Convert.ToBase64String(Fixture.Replace(Convert.FromBase64String(change[image]!.GetValue<string>()),
            new Dictionary<string, string> { [before] = after }));
    private static void AddClaim(JsonObject change, string image, bool active)
    {
        var fields = LegacyTradeEscrowClaims.IdKey + "=" + (active ? "8b3adeb7ad2b41f5b1a3c942a0cd922f" : string.Empty) + "\r\n" +
            LegacyTradeEscrowClaims.MoneyKey + "=" + (active ? "10" : "0") + "\r\n" + LegacyTradeEscrowClaims.CpKey + "=0\r\n";
        ReplaceImage(change, image, "WHMoney=555\r\n", fields + "WHMoney=555\r\n");
    }
    private static PlayerItemSnapshotItem RestrictVendorRoot(PlayerItemSnapshotItem row, string fault)
    {
        switch (fault)
        {
            case "bound-root": return row.WithBound(1);
            case "locked-root": return row.WithLocked(1);
            case "prohibited-root": return row.WithItemId(750000);
            case "inscribed-root":
                var bytes = PlayerItemSnapshotLoader.ToLegacyBytes(new([row], [], [], [], []));
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(sizeof(int) + 40, sizeof(ushort)), 1);
                return Assert.Single(PlayerItemSnapshotLoader.Parse(bytes).InventoryItems);
            case "bound-deposit": return row.WithDepositedItems([Assert.Single(row.DepositedItems).WithBound(1)]);
            case "locked-deposit": return row.WithDepositedItems([Assert.Single(row.DepositedItems).WithLocked(1)]);
            default: throw new ArgumentOutOfRangeException(nameof(fault));
        }
    }
    private static void WriteCorruptAfterState(Fixture f, JsonObject record)
    {
        // Only refusal tests alter a genuine producer WAL. Align all physical
        // targets with its corrupted AFTER so target matching cannot mask the
        // image validation being exercised; never fabricate a positive intent.
        foreach (var change in record["Changes"]!.AsArray())
        {
            var uid = checked((uint)change!["FileId"]!.GetValue<long>());
            var path = (LegacyTradeJournalFileKind)change["Kind"]!.GetValue<int>() == LegacyTradeJournalFileKind.Character
                ? f.CharacterPath(uid) : f.ItemsPath(uid);
            File.WriteAllBytes(path, Convert.FromBase64String(change["After"]!.GetValue<string>()));
        }
        File.WriteAllText(f.PendingPath, record.ToJsonString());
    }
}
