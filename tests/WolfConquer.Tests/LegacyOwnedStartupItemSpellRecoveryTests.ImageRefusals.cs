using System.Buffers.Binary;
using System.Text.Json.Nodes;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using Fixture = WolfConquer.Tests.LegacyOwnedStartupItemSpellRecoveryFixture;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedStartupItemSpellRecoveryTests
{
    [Theory]
    [InlineData("wrong-mapping")] [InlineData("extra-grant")] [InlineData("lost-old-spell")]
    [InlineData("old-level")] [InlineData("old-opaque")] [InlineData("reordered-spells")]
    [InlineData("new-level")] [InlineData("new-experience")] [InlineData("new-previous")]
    [InlineData("new-soul")] [InlineData("new-use-soul")] [InlineData("new-opaque")]
    [InlineData("invalid-old-use-soul")] [InlineData("negative-old-experience")]
    [InlineData("zero-old-id")] [InlineData("duplicate-old-id")]
    [InlineData("spell-tail")] [InlineData("wrong-stack-delta")] [InlineData("locked-book")]
    [InlineData("zero-stack-book")] [InlineData("deposited-book")]
    [InlineData("wrong-book-type")] [InlineData("changed-held-item")]
    [InlineData("uid-birth")] [InlineData("uid-above-high-water")] [InlineData("duplicate-uid")]
    [InlineData("character-money")] [InlineData("character-hitpoints")] [InlineData("character-unrelated")]
    [InlineData("negative-character-money")] [InlineData("unequal-warehouse-aliases")]
    [InlineData("unresolved-escrow")] [InlineData("missing-item-before")]
    [InlineData("missing-character-before")] [InlineData("misbound-character-owner")]
    public Task WellFormedCapturedBytesCannotSubstituteAnotherSpellGrantConsumptionOrCharacterOutcome(string fault) =>
        WithFixture(async f =>
        {
            await f.StageAsync(3); await f.CloseAsync();
            var record = JsonNode.Parse(File.ReadAllBytes(f.PendingPath))!.AsObject();
            var character = Target(record, LegacyTradeJournalFileKind.Character);
            var items = Target(record, LegacyTradeJournalFileKind.Items);
            var spells = Target(record, LegacyTradeJournalFileKind.Spells);
            var beforeItems = ReadItems(items, "Before"); var afterItems = ReadItems(items, "After");
            var beforeSpells = ReadBytes(spells, "Before"); var afterSpells = ReadBytes(spells, "After");
            int added = beforeSpells.Length;
            switch (fault)
            {
                case "wrong-mapping": BinaryPrimitives.WriteUInt16LittleEndian(afterSpells.AsSpan(added), 1350); break;
                case "extra-grant":
                    afterSpells = afterSpells.Concat(new byte[12]).ToArray();
                    BinaryPrimitives.WriteInt32LittleEndian(afterSpells, 3);
                    BinaryPrimitives.WriteUInt16LittleEndian(afterSpells.AsSpan(afterSpells.Length - 12), 1350); break;
                case "lost-old-spell": afterSpells = new byte[4].Concat(afterSpells.Skip(added)).ToArray();
                    BinaryPrimitives.WriteInt32LittleEndian(afterSpells, 1); break;
                case "old-level": BinaryPrimitives.WriteUInt16LittleEndian(afterSpells.AsSpan(6), 5); break;
                case "old-opaque": afterSpells[15] ^= 1; break;
                case "reordered-spells":
                    afterSpells = afterSpells.Take(4).Concat(afterSpells.Skip(added)).Concat(afterSpells.Skip(4).Take(12)).ToArray(); break;
                case "new-level": BinaryPrimitives.WriteUInt16LittleEndian(afterSpells.AsSpan(added + 2), 1); break;
                case "new-experience": BinaryPrimitives.WriteInt32LittleEndian(afterSpells.AsSpan(added + 4), 1); break;
                case "new-previous": afterSpells[added + 8] = 1; break;
                case "new-soul": afterSpells[added + 9] = 1; break;
                case "new-use-soul": afterSpells[added + 10] = 1; break;
                case "new-opaque": afterSpells[added + 11] = 1; break;
                case "invalid-old-use-soul": beforeSpells[14] = 5; afterSpells[14] = 5; break;
                case "negative-old-experience":
                    BinaryPrimitives.WriteInt32LittleEndian(beforeSpells.AsSpan(8), -1);
                    BinaryPrimitives.WriteInt32LittleEndian(afterSpells.AsSpan(8), -1); break;
                case "zero-old-id":
                    BinaryPrimitives.WriteUInt16LittleEndian(beforeSpells.AsSpan(4), 0);
                    BinaryPrimitives.WriteUInt16LittleEndian(afterSpells.AsSpan(4), 0); break;
                case "duplicate-old-id":
                    beforeSpells = beforeSpells.Concat(beforeSpells.Skip(4).Take(12)).ToArray();
                    afterSpells = afterSpells.Take(added).Concat(afterSpells.Skip(4).Take(12)).Concat(afterSpells.Skip(added)).ToArray();
                    BinaryPrimitives.WriteInt32LittleEndian(beforeSpells, 2);
                    BinaryPrimitives.WriteInt32LittleEndian(afterSpells, 3); break;
                case "spell-tail": afterSpells = afterSpells.Concat(new byte[] { 0x71 }).ToArray(); break;
                case "wrong-stack-delta":
                    SetItems(items, "After", afterItems with { InventoryItems =
                        beforeItems.InventoryItems.Select(row => row.Uid == Fixture.ItemUid ? row.WithStackSize(2) : row).ToArray() }); break;
                case "locked-book": SetItems(items, "Before", MapBook(beforeItems, row => row.WithLocked(1))); break;
                case "zero-stack-book": SetItems(items, "Before", MapBook(beforeItems, row => row.WithStackSize(0))); break;
                case "deposited-book": SetItems(items, "Before", MapBook(beforeItems, row => row.WithDepositedItems([NewItem(Fixture.DepositUid + 1)]))); break;
                case "wrong-book-type": SetItems(items, "Before", MapBook(beforeItems, row => row.WithItemId(Fixture.OrdinaryType))); break;
                case "changed-held-item":
                    var held = Assert.Single(afterItems.WarehouseItems); held.OwnerName = "foreign";
                    SetItems(items, "After", afterItems); break;
                case "uid-birth":
                case "uid-above-high-water":
                    var uid = fault == "uid-birth" ? Fixture.ItemUid + 1_000 : LegacyOwnedAdmissionRuntimeFixture.HistoricalFloor + 1;
                    SetItems(items, "After", afterItems with { InventoryItems = afterItems.InventoryItems.Append(NewItem(uid)).ToArray() }); break;
                case "duplicate-uid": SetItems(items, "After", afterItems with
                    { InventoryItems = afterItems.InventoryItems.Append(afterItems.InventoryItems[0]).ToArray() }); break;
                case "character-money": ReplaceImage(character, "After", "Money=777\r\n", "Money=776\r\n"); break;
                case "character-hitpoints": ReplaceImage(character, "After", "MinHitPoints=1\r\n", "MinHitPoints=0\r\n"); break;
                case "character-unrelated": ReplaceImage(character, "After", "KeepBytes=startup-owned-value\r\n", "KeepBytes=foreign\r\n"); break;
                case "negative-character-money":
                    foreach (var image in new[] { "Before", "After" }) ReplaceImage(character, image, "Money=777\r\n", "Money=-1\r\n"); break;
                case "unequal-warehouse-aliases":
                    foreach (var image in new[] { "Before", "After" }) ReplaceImage(character, image,
                        "WHMoney=555\r\n", "WHMoney=555\r\nWarehouseMoney=556\r\n"); break;
                case "unresolved-escrow":
                    foreach (var image in new[] { "Before", "After" }) ReplaceImage(character, image, "WHMoney=555\r\n",
                        "WHMoney=555\r\nManagedTradeEscrowId=ae51e4557fd24b8e8582047dde810256\r\nManagedTradeEscrowMoney=10\r\nManagedTradeEscrowCP=0\r\n"); break;
                case "missing-item-before": items["Before"] = null; break;
                case "missing-character-before": character["Before"] = null; break;
                case "misbound-character-owner":
                    foreach (var image in new[] { "Before", "After" }) ReplaceImage(character, image,
                        $"UID={Fixture.Uid}\r\n", $"UID={Fixture.Uid + 1}\r\n"); break;
                default: throw new ArgumentOutOfRangeException(nameof(fault));
            }
            spells["Before"] = Convert.ToBase64String(beforeSpells); spells["After"] = Convert.ToBase64String(afterSpells);
            WriteCorruptAfterState(f, record); f.AssertRefused();
        });

    [Theory]
    [InlineData("locked")] [InlineData("stacked")] [InlineData("deposited")] [InlineData("equipped-locked")]
    [InlineData("reborn")] [InlineData("level")] [InlineData("wrong-family-position")]
    [InlineData("changed-spells")] [InlineData("changed-hitpoints")]
    public Task CapturedRuneOperationsStillRequireTheExactStaticTransitionAndRecordedOutcomes(string fault) =>
        WithFixture(async f =>
        {
            await f.StageAsync(3); await f.CloseAsync();
            var record = JsonNode.Parse(File.ReadAllBytes(f.PendingPath))!.AsObject();
            var items = Target(record, LegacyTradeJournalFileKind.Items);
            foreach (var image in new[] { "Before", "After" })
            {
                var snapshot = ReadItems(items, image);
                if (fault is "locked" or "stacked") SetItems(items, image, snapshot with
                { RuneItems = snapshot.RuneItems.Select(row => row.Uid != Fixture.ItemUid ? row : fault == "locked"
                    ? row.WithLocked(1) : row.WithStackSize(2)).ToArray() });
                else if (fault == "deposited") SetItems(items, image, snapshot with
                { RuneItems = snapshot.RuneItems.Select(row => row.Uid == Fixture.ItemUid
                    ? row.WithDepositedItems([NewItem(Fixture.DepositUid + 1)]) : row).ToArray() });
                else if (fault == "equipped-locked") SetItems(items, image, snapshot with
                { RuneItems = snapshot.RuneItems.Select(row => row.Uid == Fixture.ItemUid + 2
                    ? row.WithLocked(1).WithPosition(image == "Before" ? (ushort)102 : (ushort)212) : row).ToArray() });
                else if (fault == "wrong-family-position" && image == "After") SetItems(items, image, snapshot with
                { RuneItems = snapshot.RuneItems.Select(row => row.Uid == Fixture.ItemUid ? row.WithPosition(101) : row).ToArray() });
            }
            if (fault == "changed-spells")
            {
                var spells = Target(record, LegacyTradeJournalFileKind.Spells); var bytes = ReadBytes(spells, "After"); bytes[^1] ^= 1;
                spells["After"] = Convert.ToBase64String(bytes);
            }
            if (fault == "changed-hitpoints") ReplaceImage(Target(record, LegacyTradeJournalFileKind.Character),
                "After", "MinHitPoints=1\r\n", "MinHitPoints=0\r\n");
            if (fault is "reborn" or "level") foreach (var image in new[] { "Before", "After" })
                ReplaceImage(Target(record, LegacyTradeJournalFileKind.Character), image,
                    fault == "reborn" ? "Reborn=2\r\n" : "Level=140\r\n", fault == "reborn" ? "Reborn=1\r\n" : "Level=14\r\n");
            WriteCorruptAfterState(f, record); f.AssertRefused();
        }, mode: "rune-equip");

    [Theory]
    [InlineData("full-inventory")] [InlineData("active-vendor")]
    public Task CapturedRuneUnequipCannotBypassItsStableCapacityOrVendingRestriction(string fault) => WithFixture(async f =>
    {
        await f.StageAsync(3); await f.CloseAsync(); var record = JsonNode.Parse(File.ReadAllBytes(f.PendingPath))!.AsObject();
        foreach (var image in new[] { "Before", "After" })
            if (fault == "active-vendor") ReplaceImage(Target(record, LegacyTradeJournalFileKind.Character), image,
                "KeepBytes=startup-owned-value\r\n", "KeepBytes=startup-owned-value\r\nRuntimeVendorUid=9001\r\n");
            else
            {
                var items = Target(record, LegacyTradeJournalFileKind.Items); var snapshot = ReadItems(items, image);
                var kept = Enumerable.Range(0, 39).Select(index => NewItem(checked(1_100_000U + (uint)index)));
                var full = snapshot with { InventoryItems = snapshot.InventoryItems.Concat(kept).ToArray() };
                Assert.Equal(40, full.InventoryItems.Count); SetItems(items, image, full);
            }
        WriteCorruptAfterState(f, record); f.AssertRefused();
    }, mode: "rune-identity");

    private static JsonObject Target(JsonObject record, LegacyTradeJournalFileKind kind) => record["Changes"]!.AsArray()
        .Select(row => row!.AsObject()).Single(row => row["Kind"]!.GetValue<int>() == (int)kind);
    private static byte[] ReadBytes(JsonObject change, string image) => Convert.FromBase64String(change[image]!.GetValue<string>());
    private static PlayerItemSnapshot ReadItems(JsonObject change, string image) => PlayerItemSnapshotLoader.Parse(ReadBytes(change, image));
    private static void SetItems(JsonObject change, string image, PlayerItemSnapshot items) =>
        change[image] = Convert.ToBase64String(PlayerItemSnapshotLoader.ToLegacyBytes(items));
    private static PlayerItemSnapshot MapBook(PlayerItemSnapshot items, Func<PlayerItemSnapshotItem, PlayerItemSnapshotItem> change) =>
        items with { InventoryItems = items.InventoryItems.Select(row => row.Uid == Fixture.ItemUid ? change(row) : row).ToArray() };
    private static PlayerItemSnapshotItem NewItem(uint uid) => new()
    { Uid = uid, ItemId = Fixture.OrdinaryType, StackSize = 1, Durability = 10, MaximumDurability = 10, Color = 5 };
    private static void ReplaceImage(JsonObject target, string image, string before, string after)
    {
        var text = CharacterSnapshotFileEncoding.Decode(ReadBytes(target, image), out var encoding);
        Assert.Contains(before, text, StringComparison.Ordinal);
        target[image] = Convert.ToBase64String(encoding.GetPreamble().Concat(encoding.GetBytes(text.Replace(before, after, StringComparison.Ordinal))).ToArray());
    }
    private static void WriteCorruptAfterState(Fixture fixture, JsonObject record)
    {
        // Negative tests deliberately alter a genuine captured intent and
        // align physical AFTER files, so matching cannot mask image validation.
        foreach (var change in record["Changes"]!.AsArray())
        {
            var kind = (LegacyTradeJournalFileKind)change!["Kind"]!.GetValue<int>();
            var path = kind == LegacyTradeJournalFileKind.Character ? fixture.CharacterPath :
                kind == LegacyTradeJournalFileKind.Items ? fixture.ItemsPath : fixture.SpellsPath;
            File.WriteAllBytes(path, ReadBytes(change.AsObject(), "After"));
        }
        File.WriteAllText(fixture.PendingPath, record.ToJsonString());
    }
}
