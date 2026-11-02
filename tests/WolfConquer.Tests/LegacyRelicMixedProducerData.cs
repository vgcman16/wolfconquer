using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

// Declared DATA before real bootstrap. The actual Runtime/Frame RuneBag and
// relic producers alone allocate identities and publish their own journals.
internal static class LegacyRelicMixedProducerData
{
    internal const uint CharacterUid = LegacyOwnedAdmissionRuntimeFixture.Uid;
    internal const uint FirstRelicUid = 1_047_001, SecondRelicUid = 1_047_002, KeptUid = 1_047_003;
    internal const uint StoredRuneUidA = 1_047_004, StoredRuneUidB = 1_047_005;
    internal const uint StoredRuneUid = StoredRuneUidA;
    internal const uint EssenceItemIdA = 4_040_001, StoredRuneItemIdB = 4_020_517;
    internal const uint BlueRuneSpellId = 1_416_016;

    internal static PlayerItemSnapshot InitialItems() => PlayerItemSnapshot.Empty with
    {
        InventoryItems = [Relic(FirstRelicUid, 4_100_001), Relic(SecondRelicUid, 4_100_002),
            new() { Uid = KeptUid, ItemId = 1_000_000, StackSize = 1, Durability = 10, MaximumDurability = 10, Color = 5 }],
        // Different item types deliberately have no compatible inventory
        // receiver. Preserve the essence's real 10000 cap; no cap forces a UID.
        RuneItems = [Stored(StoredRuneUidA, EssenceItemIdA, 1), Stored(StoredRuneUidB, StoredRuneItemIdB, 10)],
    };

    internal static WorldDataCatalog Catalog()
    {
        var rows = new uint[] { 4_100_001, 4_100_002, 4_100_003, 4_100_004, 4_100_005 }
            .Select(id => LegacyFullAccountItemMutationFixture.ItemType(id, "MixedRelic" + id, durability: 10_000)).ToList();
        rows.Add(LegacyFullAccountItemMutationFixture.ItemType(1_000_000, "MixedKept", durability: 10, stackLimit: 1));
        rows.Add(LegacyFullAccountItemMutationFixture.ItemType(EssenceItemIdA, "MixedEssence", durability: 1, stackLimit: 10_000));
        rows.Add(BlueRuneMetadata());
        var caps = Enumerable.Range(1, 13).Select(type => XuanbaoParser.ParseComposeAttributeLimitRow(
            "fixture://mixed-relic-caps", type, FormattableString.Invariant($"{type}@@0@@1@@400@@"))).ToArray();
        // The stored 211 RETURN path performs no skill/progress/stat query.
        // It reads selected raw 46 and the blue rune's wire display mapping.
        return LegacyItemWorldDataFixture.Create(rows.ToArray(), [], xuanbaoComposeAttributeLimits: caps);
    }

    internal static byte[] RuneReturnRequest(uint storedUid) => new MsgItemUsagePacket(
        storedUid, 1, MsgItemUsageAction.Rune, 511_043_495, 112_244, 55, 66).ToFrameBytes();

    private static ItemType BlueRuneMetadata()
    {
        // Same declared stored blue-rune metadata as the genuine accepted
        // LegacyOwnedRuneBagTests fixture. Raw 0 is its existing unit stack cap.
        var row = LegacyFullAccountItemMutationFixture.ItemType(StoredRuneItemIdB, "MixedStoredBlueRune", durability: 10);
        var fields = row.Fields.ToArray();
        fields[ItemType.RequiredClassFieldIndex] = "6000";
        fields[ItemType.RuneSpellIdFieldIndex] = BlueRuneSpellId.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return new(row.Id, row.Name, fields);
    }

    private static PlayerItemSnapshotItem Relic(uint uid, uint itemId) => new()
    {
        Uid = uid, ItemId = itemId, StackSize = 1, Durability = 10_000, MaximumDurability = 10_000,
        Attribute1 = 100_003, Attribute2 = 110_004, Attribute3 = 120_005, Attribute4 = 130_009, Attribute5 = 100_114,
    };

    private static PlayerItemSnapshotItem Stored(uint uid, uint itemId, ushort durability) => new()
    {
        Uid = uid, ItemId = itemId, Position = 211, StackSize = 1, Durability = durability,
        MaximumDurability = durability, Locked = 1, Bound = 1, Color = 5,
    };
}
