using System.Buffers.Binary;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    // Pure exact raw-image projection. Real admission/current-owner/WAL authority
    // stays with the caller; this function cannot publish or authorize a use.
    internal static byte[] PrepareOwnedConsumableItemImage(byte[] raw, PlayerItemSnapshot before,
        PlayerItemSnapshot after, uint uid, LegacyConsumableItemKind expectedKind)
    {
        LegacyItemUidRawCatalog.ValidateAdmissionPlayerItems(raw);
        var modeledBefore = PlayerItemSnapshotLoader.ToLegacyBytes(before);
        var canonicalBefore = PlayerItemSnapshotLoader.ToLegacyBytes(PlayerItemSnapshotLoader.Parse(raw));
        if (!ItemSpellRawRecordsMatch(canonicalBefore, modeledBefore)) throw InvalidEquipmentMove();
        var sections = ItemSpellRawSections(raw);
        var canonicalSections = ItemSpellRawSections(canonicalBefore);
        if (sections.Items.Count != canonicalSections.Items.Count ||
            sections.Items.Any(pair => !canonicalSections.Items.TryGetValue(pair.Key, out var canonicalRow) || canonicalRow.Parent != pair.Value.Parent) ||
            !sections.Perfections.Keys.Order().SequenceEqual(canonicalSections.Perfections.Keys.Order())) throw InvalidEquipmentMove();
        var selected = before.InventoryItems.Single(row => row.Uid == uid);
        if (selected.Position != 0 || selected.WarehouseId != 0 || selected.DepositedItems.Count != 0 ||
            expectedKind is not (LegacyConsumableItemKind.Hitpoints or LegacyConsumableItemKind.Mana or LegacyConsumableItemKind.FullRestore) ||
            LegacyConsumableItemUse.Classify(selected.ItemId) != expectedKind) throw InvalidEquipmentMove();
        var row = sections.Items[uid];
        if (row.Parent != 0) throw InvalidEquipmentMove();
        byte[] candidate;
        if (selected.StackSize > 1)
        {
            candidate = raw.ToArray();
            BinaryPrimitives.WriteUInt16LittleEndian(candidate.AsSpan(row.Offset + 52), checked((ushort)(selected.StackSize - 1)));
        }
        else
        {
            var removed = new List<(int Offset, int Length)> { (row.Offset, PlayerItemSnapshotLoader.LegacyDbItemSize) };
            var removePerfection = sections.Perfections.TryGetValue(uid, out var perfection);
            if (removePerfection) removed.Add(perfection);
            var ranges = removed.OrderBy(range => range.Offset).ToArray();
            candidate = new byte[checked(raw.Length - ranges.Sum(range => range.Length))];
            int source = 0, destination = 0;
            foreach (var range in ranges)
            {
                raw.AsSpan(source, range.Offset - source).CopyTo(candidate.AsSpan(destination));
                destination += range.Offset - source; source = range.Offset + range.Length;
            }
            raw.AsSpan(source).CopyTo(candidate.AsSpan(destination));
            BinaryPrimitives.WriteInt32LittleEndian(candidate, checked(BinaryPrimitives.ReadInt32LittleEndian(raw) - 1));
            var oldPerfectionHeader = checked(sizeof(int) + sections.Items.Count * PlayerItemSnapshotLoader.LegacyDbItemSize);
            var newPerfectionHeader = oldPerfectionHeader - PlayerItemSnapshotLoader.LegacyDbItemSize;
            BinaryPrimitives.WriteInt32LittleEndian(candidate.AsSpan(newPerfectionHeader),
                checked(BinaryPrimitives.ReadInt32LittleEndian(raw.AsSpan(oldPerfectionHeader)) - (removePerfection ? 1 : 0)));
        }
        LegacyItemUidRawCatalog.ValidateAdmissionPlayerItems(candidate);
        var canonicalAfter = PlayerItemSnapshotLoader.ToLegacyBytes(PlayerItemSnapshotLoader.Parse(candidate));
        if (!ItemSpellRawRecordsMatch(canonicalAfter, PlayerItemSnapshotLoader.ToLegacyBytes(after))) throw InvalidEquipmentMove();
        return candidate;
    }
}
