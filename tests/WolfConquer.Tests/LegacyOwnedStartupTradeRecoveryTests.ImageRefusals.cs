using System.Buffers.Binary;
using System.Text.Json.Nodes;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using Fixture = WolfConquer.Tests.LegacyOwnedStartupTradeRecoveryFixture;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedStartupTradeRecoveryTests
{
    [Theory]
    [InlineData("bound-root")] [InlineData("inscribed-root")]
    [InlineData("unable-trade-root")] [InlineData("bound-deposit")]
    public Task ConservedCapturedClosuresStillRequireStaticPermissionForTheActualOwnerTransfer(string fault) =>
        WithFixture(async f =>
        {
            await f.StageAsync(4);
            await f.CloseAsync();
            var record = JsonNode.Parse(File.ReadAllBytes(f.PendingPath))!.AsObject();
            foreach (var target in record["Changes"]!.AsArray().Select(row => row!.AsObject()).Where(row =>
                row["Kind"]!.GetValue<int>() == (int)LegacyTradeJournalFileKind.Items))
                foreach (var image in new[] { "Before", "After" })
                {
                    var snapshot = PlayerItemSnapshotLoader.Parse(Convert.FromBase64String(target[image]!.GetValue<string>()));
                    var updated = snapshot with { InventoryItems = snapshot.InventoryItems.Select(row => row.Uid == Fixture.LeftItemUid
                        ? RestrictMovedClosure(row, fault) : row).ToArray() };
                    var bytes = PlayerItemSnapshotLoader.ToLegacyBytes(updated);
                    target[image] = Convert.ToBase64String(bytes);
                    if (image == "After") File.WriteAllBytes(f.ItemsPath(checked((uint)target["FileId"]!.GetValue<long>())), bytes);
                }
            File.WriteAllText(f.PendingPath, record.ToJsonString());
            RequireCapturedPairConservation(record);
            RequireEveryCurrentAfter(f, record);
            var before = FindCapturedInventoryRoot(record, "Before", Fixture.LeftItemUid);
            var after = FindCapturedInventoryRoot(record, "After", Fixture.LeftItemUid);
            Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(new([before], [], [], [], [])),
                PlayerItemSnapshotLoader.ToLegacyBytes(new([after], [], [], [], [])));
            f.AssertRefused();
        });

    [Fact]
    public Task AConservedFortySlotAfterImageCannotCreditOutgoingSpaceToAdmitAnIncomingTradeRoot() => WithFixture(async f =>
    {
        await f.StageAsync(4);
        await f.CloseAsync();
        var record = JsonNode.Parse(File.ReadAllBytes(f.PendingPath))!.AsObject();
        var target = record["Changes"]!.AsArray().Select(row => row!.AsObject()).Single(row =>
            row["Kind"]!.GetValue<int>() == (int)LegacyTradeJournalFileKind.Items &&
            row["FileId"]!.GetValue<long>() == Fixture.LeftUid);
        var retained = Enumerable.Range(0, 39).Select(index => new PlayerItemSnapshotItem
        {
            Uid = checked(1_100_000U + (uint)index), ItemId = Fixture.ItemId, Durability = 10,
            MaximumDurability = 10, StackSize = 1, Color = 5,
        }).ToArray();
        foreach (var image in new[] { "Before", "After" })
        {
            var snapshot = PlayerItemSnapshotLoader.Parse(Convert.FromBase64String(target[image]!.GetValue<string>()));
            var updated = snapshot with { InventoryItems = snapshot.InventoryItems.Concat(retained).ToArray() };
            Assert.Equal(40, updated.InventoryItems.Count);
            Assert.True(LegacyItemSaveGraph.TryCapture(updated, out _));
            var bytes = PlayerItemSnapshotLoader.ToLegacyBytes(updated);
            target[image] = Convert.ToBase64String(bytes);
            if (image == "After") File.WriteAllBytes(f.ItemsPath(Fixture.LeftUid), bytes);
        }
        File.WriteAllText(f.PendingPath, record.ToJsonString());
        RequireCapturedPairConservation(record);
        RequireEveryCurrentAfter(f, record);
        // The receiving owner already occupies40 before its one incoming root.
        // The one outgoing root leaves its AFTER count40, so checking only the
        // final count would accidentally accept this impossible producer state.
        f.AssertRefused();
    });

    [Theory]
    [InlineData("malformed-canonical")]
    [InlineData("negative-canonical")]
    [InlineData("malformed-alias")]
    [InlineData("negative-alias")]
    [InlineData("unequal-aliases")]
    public Task InvalidCapturedWarehouseCurrencyRemainsFencedWhenEveryCurrentTargetMatchesItsAfterImage(string fault) =>
        WithFixture(async f =>
        {
            await f.StageAsync(4);
            await f.CloseAsync();
            var record = JsonNode.Parse(File.ReadAllBytes(f.PendingPath))!.AsObject();
            var target = record["Changes"]!.AsArray().Select(row => row!.AsObject()).Single(row =>
                row["Kind"]!.GetValue<int>() == (int)LegacyTradeJournalFileKind.Character &&
                row["FileId"]!.GetValue<long>() == Fixture.LeftUid);
            var replacement = fault switch
            {
                "malformed-canonical" => "WHMoney=broken\r\n",
                "negative-canonical" => "WHMoney=-1\r\n",
                "malformed-alias" => "WHMoney=555\r\nWarehouseMoney=broken\r\n",
                "negative-alias" => "WHMoney=555\r\nWarehouseMoney=-1\r\n",
                "unequal-aliases" => "WHMoney=555\r\nWarehouseMoney=556\r\n",
                _ => throw new ArgumentOutOfRangeException(nameof(fault)),
            };
            foreach (var image in new[] { "Before", "After" })
            {
                var bytes = Fixture.Replace(Convert.FromBase64String(target[image]!.GetValue<string>()),
                    new Dictionary<string, string> { ["WHMoney=555\r\n"] = replacement });
                target[image] = Convert.ToBase64String(bytes);
                if (image == "After") File.WriteAllBytes(f.CharacterPath(Fixture.LeftUid), bytes);
            }
            // Both captured images carry the same deliberately corrupted
            // unrelated field, so the real trade projection remains coherent.
            // Current files still match all four AFTER images: the malformed
            // captured wallet cannot hide behind target-match preflight.
            File.WriteAllText(f.PendingPath, record.ToJsonString());
            foreach (var change in record["Changes"]!.AsArray())
            {
                var uid = checked((uint)change!["FileId"]!.GetValue<long>());
                var path = (LegacyTradeJournalFileKind)change["Kind"]!.GetValue<int>() == LegacyTradeJournalFileKind.Character
                    ? f.CharacterPath(uid) : f.ItemsPath(uid);
                Assert.Equal(Convert.FromBase64String(change["After"]!.GetValue<string>()), File.ReadAllBytes(path));
            }
            f.AssertRefused();
        });

    [Theory]
    [InlineData("uid-birth-below-high-water")]
    [InlineData("uid-above-high-water")]
    [InlineData("duplicate-uid")]
    [InlineData("lost-uid")]
    [InlineData("item-type")]
    [InlineData("item-durability")]
    [InlineData("warehouse-ownership")]
    [InlineData("character-owner")]
    [InlineData("character-unrelated-field")]
    [InlineData("currency-credit")]
    [InlineData("uncleared-escrow")]
    [InlineData("missing-items-before")]
    public Task ARealTradeWalDoesNotAuthorizeTamperedCapturedImagesEvenWhenUidCountsLookPlausible(string fault) =>
        WithFixture(async f =>
        {
            await f.StageAsync(0);
            await f.CloseAsync();
            var record = JsonNode.Parse(File.ReadAllBytes(f.PendingPath))!.AsObject();
            var changes = record["Changes"]!.AsArray();
            JsonObject Target(LegacyTradeJournalFileKind kind, uint uid) => changes.Select(row => row!.AsObject()).Single(row =>
                row["Kind"]!.GetValue<int>() == (int)kind && row["FileId"]!.GetValue<long>() == uid);
            var character = Target(LegacyTradeJournalFileKind.Character, Fixture.LeftUid);
            var items = Target(LegacyTradeJournalFileKind.Items, Fixture.LeftUid);
            var characterAfter = Convert.FromBase64String(character["After"]!.GetValue<string>());
            var itemAfter = PlayerItemSnapshotLoader.Parse(Convert.FromBase64String(items["After"]!.GetValue<string>()));
            var transferred = Assert.Single(itemAfter.InventoryItems);
            switch (fault)
            {
                case "uid-birth-below-high-water":
                case "uid-above-high-water":
                    var uid = fault == "uid-above-high-water" ? LegacyOwnedAdmissionRuntimeFixture.HistoricalFloor + 1 : Fixture.LeftItemUid + 1_000;
                    itemAfter = itemAfter with { InventoryItems = [transferred, new()
                        { Uid = uid, ItemId = Fixture.ItemId, StackSize = 1, Durability = 10, MaximumDurability = 10 }] }; break;
                case "duplicate-uid": itemAfter = itemAfter with { InventoryItems = [transferred, transferred] }; break;
                case "lost-uid": itemAfter = itemAfter with { InventoryItems = [] }; break;
                case "item-type": itemAfter = itemAfter with { InventoryItems = [transferred.WithItemId(Fixture.ItemId + 1)] }; break;
                case "item-durability": itemAfter = itemAfter with { InventoryItems = [transferred.WithDurability(9)] }; break;
                case "warehouse-ownership":
                    var other = Target(LegacyTradeJournalFileKind.Items, Fixture.RightUid);
                    var otherAfter = PlayerItemSnapshotLoader.Parse(Convert.FromBase64String(other["After"]!.GetValue<string>()));
                    var ownWarehouse = itemAfter.WarehouseItems; var otherWarehouse = otherAfter.WarehouseItems;
                    itemAfter = itemAfter with { WarehouseItems = otherWarehouse };
                    other["After"] = Convert.ToBase64String(PlayerItemSnapshotLoader.ToLegacyBytes(otherAfter with { WarehouseItems = ownWarehouse })); break;
                case "character-owner": characterAfter = Fixture.Replace(characterAfter,
                    new Dictionary<string, string> { [$"UID={Fixture.LeftUid}\r\n"] = $"UID={Fixture.RightUid}\r\n" }); break;
                case "character-unrelated-field": characterAfter = Fixture.Replace(characterAfter,
                    new Dictionary<string, string> { ["KeepBytes=startup-owned-value\r\n"] = "KeepBytes=foreign-change\r\n" }); break;
                case "currency-credit": characterAfter = Fixture.Replace(characterAfter,
                    new Dictionary<string, string> { ["Money=1177\r\n"] = "Money=1178\r\n" }); break;
                case "uncleared-escrow":
                    var before = CharacterSnapshotParser.ParseText(CharacterSnapshotFileEncoding.Decode(
                        Convert.FromBase64String(character["Before"]!.GetValue<string>()), out _));
                    characterAfter = Fixture.Replace(characterAfter, new Dictionary<string, string>
                    {
                        [LegacyTradeEscrowClaims.IdKey + "=\r\n"] = LegacyTradeEscrowClaims.IdKey + "=" + before.Attributes[LegacyTradeEscrowClaims.IdKey] + "\r\n",
                        [LegacyTradeEscrowClaims.MoneyKey + "=0\r\n"] = LegacyTradeEscrowClaims.MoneyKey + "=300\r\n",
                        [LegacyTradeEscrowClaims.CpKey + "=0\r\n"] = LegacyTradeEscrowClaims.CpKey + "=5\r\n",
                    }); break;
                case "missing-items-before": items["Before"] = null; break;
                default: throw new ArgumentOutOfRangeException(nameof(fault));
            }
            character["After"] = Convert.ToBase64String(characterAfter);
            items["After"] = Convert.ToBase64String(PlayerItemSnapshotLoader.ToLegacyBytes(itemAfter));
            // This is deliberate corruption of an actual producer WAL for a
            // refusal test; it never supplies a fabricated successful journal.
            File.WriteAllText(f.PendingPath, record.ToJsonString());
            f.AssertRefused();
        });

    [Theory]
    [InlineData("unknown-before-uid")]
    [InlineData("ambiguous-before-owner")]
    public Task RecordedBeforeImagesMustValidateEvenWhenAllFourCurrentFilesAlreadyMatchAfter(string fault) =>
        WithFixture(async f =>
        {
            await f.StageAsync(4);
            await f.CloseAsync();
            var record = JsonNode.Parse(File.ReadAllBytes(f.PendingPath))!.AsObject();
            var kind = fault == "unknown-before-uid" ? LegacyTradeJournalFileKind.Items : LegacyTradeJournalFileKind.Character;
            var target = record["Changes"]!.AsArray().Select(row => row!.AsObject()).Single(row =>
                row["Kind"]!.GetValue<int>() == (int)kind && row["FileId"]!.GetValue<long>() == Fixture.LeftUid);
            var before = Convert.FromBase64String(target["Before"]!.GetValue<string>());
            if (kind == LegacyTradeJournalFileKind.Items)
            {
                var snapshot = PlayerItemSnapshotLoader.Parse(before);
                before = PlayerItemSnapshotLoader.ToLegacyBytes(snapshot with { InventoryItems =
                    [new() { Uid = LegacyOwnedAdmissionRuntimeFixture.HistoricalFloor + 1, ItemId = Fixture.ItemId,
                        StackSize = 1, Durability = 10, MaximumDurability = 10 }] });
            }
            else before = Fixture.Replace(before, new Dictionary<string, string>
                { [$"UID={Fixture.LeftUid}\r\n"] = $"UID={Fixture.RightUid}\r\n" });
            target["Before"] = Convert.ToBase64String(before);
            File.WriteAllText(f.PendingPath, record.ToJsonString());
            f.AssertRefused();
        });

    private static PlayerItemSnapshotItem RestrictMovedClosure(PlayerItemSnapshotItem row, string fault)
    {
        switch (fault)
        {
            case "bound-root": return row.WithBound(1);
            case "unable-trade-root": return row.WithItemId(750000); // Actual exact-ID restriction table.
            case "inscribed-root":
                var image = PlayerItemSnapshotLoader.ToLegacyBytes(new([row], [], [], [], []));
                // Actual PlayerItemSnapshotLoader DBItem InscribedOffset40,
                // after the four-byte top-level count, preserving other bytes.
                BinaryPrimitives.WriteUInt16LittleEndian(image.AsSpan(sizeof(int) + 40, sizeof(ushort)), 1);
                return Assert.Single(PlayerItemSnapshotLoader.Parse(image).InventoryItems);
            case "bound-deposit":
                var child = new PlayerItemSnapshotItem { Uid = Fixture.LeftItemUid + 1_000, ItemId = Fixture.ItemId,
                    Durability = 10, MaximumDurability = 10, StackSize = 1, Color = 5 };
                return row.WithDepositedItems([child.WithBound(1)]);
            default: throw new ArgumentOutOfRangeException(nameof(fault));
        }
    }

    private static PlayerItemSnapshotItem FindCapturedInventoryRoot(JsonObject record, string image, uint uid) =>
        Assert.Single(record["Changes"]!.AsArray().Select(row => row!.AsObject()).Where(row =>
            row["Kind"]!.GetValue<int>() == (int)LegacyTradeJournalFileKind.Items).SelectMany(row =>
            PlayerItemSnapshotLoader.Parse(Convert.FromBase64String(row[image]!.GetValue<string>())).InventoryItems), row => row.Uid == uid);

    private static void RequireCapturedPairConservation(JsonObject record)
    {
        uint[] Read(string image) => record["Changes"]!.AsArray().Select(row => row!.AsObject()).Where(row =>
            row["Kind"]!.GetValue<int>() == (int)LegacyTradeJournalFileKind.Items).SelectMany(row =>
            LegacyItemUidRawCatalog.ReadAdmissionPlayerItemUids(Convert.FromBase64String(row[image]!.GetValue<string>()))).Order().ToArray();
        var before = Read("Before"); var after = Read("After");
        Assert.Equal(before.Length, before.Distinct().Count());
        Assert.Equal(after.Length, after.Distinct().Count());
        Assert.Equal(before, after);
    }

    private static void RequireEveryCurrentAfter(Fixture f, JsonObject record)
    {
        foreach (var change in record["Changes"]!.AsArray())
        {
            var uid = checked((uint)change!["FileId"]!.GetValue<long>());
            var path = (LegacyTradeJournalFileKind)change["Kind"]!.GetValue<int>() == LegacyTradeJournalFileKind.Character
                ? f.CharacterPath(uid) : f.ItemsPath(uid);
            Assert.Equal(Convert.FromBase64String(change["After"]!.GetValue<string>()), File.ReadAllBytes(path));
        }
    }
}
