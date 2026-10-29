using System.Buffers.Binary;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal static partial class LegacyEquipmentMovePersistence
{
    private sealed record Sections(Dictionary<uint, (int Offset, uint Parent)> Items,
        Dictionary<uint, int> Perfections);

    private static bool TryPrepareItemImage(byte[] rawBefore, PlayerItemSnapshot before, PlayerItemSnapshot after,
        out byte[] rawAfter, out string reason)
    {
        rawAfter = []; reason = "equipmentMoveItemBaselineStaleOrInvalid";
        LegacyItemUidRawCatalog.ValidateAdmissionPlayerItems(rawBefore);
        var canonicalBefore = PlayerItemSnapshotLoader.ToLegacyBytes(PlayerItemSnapshotLoader.Parse(rawBefore));
        var expectedBefore = PlayerItemSnapshotLoader.ToLegacyBytes(before);
        var expectedAfter = PlayerItemSnapshotLoader.ToLegacyBytes(after);
        LegacyItemUidRawCatalog.ValidateAdmissionPlayerItems(canonicalBefore);
        LegacyItemUidRawCatalog.ValidateAdmissionPlayerItems(expectedBefore);
        LegacyItemUidRawCatalog.ValidateAdmissionPlayerItems(expectedAfter);
        if (!RecordsEqual(canonicalBefore, expectedBefore)) return false;
        var oldRows = ReadSections(expectedBefore);
        var newRows = ReadSections(expectedAfter);
        if (oldRows.Items.Count != newRows.Items.Count || !PerfectionsEqual(expectedBefore, oldRows, expectedAfter, newRows)) return false;
        var rawRows = ReadSections(rawBefore);
        var candidate = rawBefore.ToArray();
        var changed = 0;
        foreach (var (uid, row) in oldRows.Items)
        {
            if (!newRows.Items.TryGetValue(uid, out var next) || next.Parent != row.Parent ||
                !rawRows.Items.TryGetValue(uid, out var raw) || raw.Parent != row.Parent) return false;
            var first = expectedBefore.AsSpan(row.Offset, PlayerItemSnapshotLoader.LegacyDbItemSize);
            var second = expectedAfter.AsSpan(next.Offset, PlayerItemSnapshotLoader.LegacyDbItemSize);
            // Equipment moves cannot mutate a wallet, perfection, binding, lock,
            // durability, deposits, or any other modeled physical row field.
            if (!first[..12].SequenceEqual(second[..12]) || !first[14..].SequenceEqual(second[14..])) return false;
            var position = BinaryPrimitives.ReadUInt16LittleEndian(second[12..]);
            if (position == BinaryPrimitives.ReadUInt16LittleEndian(first[12..])) continue;
            if (row.Parent != 0) return false;
            BinaryPrimitives.WriteUInt16LittleEndian(candidate.AsSpan(raw.Offset + 12), position);
            changed++;
        }
        if (changed == 0) return false;
        LegacyItemUidRawCatalog.ValidateAdmissionPlayerItems(candidate);
        var canonicalAfter = PlayerItemSnapshotLoader.ToLegacyBytes(PlayerItemSnapshotLoader.Parse(candidate));
        if (!RecordsEqual(canonicalAfter, expectedAfter)) return false;
        // Exact original physical order and every uninterpreted byte survive;
        // only validated top-level Position words were patched.
        rawAfter = candidate; reason = string.Empty; return true;
    }

    private static bool RecordsEqual(byte[] first, byte[] second)
    {
        var left = ReadSections(first); var right = ReadSections(second);
        if (left.Items.Count != right.Items.Count || !PerfectionsEqual(first, left, second, right)) return false;
        foreach (var (uid, row) in left.Items)
            if (!right.Items.TryGetValue(uid, out var other) || row.Parent != other.Parent ||
                !first.AsSpan(row.Offset, PlayerItemSnapshotLoader.LegacyDbItemSize)
                    .SequenceEqual(second.AsSpan(other.Offset, PlayerItemSnapshotLoader.LegacyDbItemSize))) return false;
        return true;
    }

    private static bool PerfectionsEqual(byte[] first, Sections left, byte[] second, Sections right)
    {
        if (left.Perfections.Count != right.Perfections.Count) return false;
        foreach (var (uid, offset) in left.Perfections)
            if (!right.Perfections.TryGetValue(uid, out var other) ||
                !first.AsSpan(offset, PlayerItemSnapshotLoader.LegacyPerfectionSize)
                    .SequenceEqual(second.AsSpan(other, PlayerItemSnapshotLoader.LegacyPerfectionSize))) return false;
        return true;
    }

    // Called only after full raw catalog validation (or the real serializer).
    private static Sections ReadSections(byte[] image)
    {
        var items = new Dictionary<uint, (int, uint)>(); var perfections = new Dictionary<uint, int>();
        var count = BinaryPrimitives.ReadInt32LittleEndian(image); var offset = sizeof(int);
        for (var index = 0; index < count; index++)
        {
            var uid = BinaryPrimitives.ReadUInt32LittleEndian(image.AsSpan(offset));
            var children = BinaryPrimitives.ReadUInt32LittleEndian(image.AsSpan(offset + 136));
            items.Add(uid, (offset, 0)); offset = checked(offset + PlayerItemSnapshotLoader.LegacyDbItemSize);
            for (uint child = 0; child < children; child++)
            {
                var childUid = BinaryPrimitives.ReadUInt32LittleEndian(image.AsSpan(offset));
                items.Add(childUid, (offset, uid)); offset = checked(offset + PlayerItemSnapshotLoader.LegacyDbItemSize);
            }
        }
        var perfectionCount = BinaryPrimitives.ReadInt32LittleEndian(image.AsSpan(offset)); offset += sizeof(int);
        for (var index = 0; index < perfectionCount; index++)
        {
            perfections.Add(BinaryPrimitives.ReadUInt32LittleEndian(image.AsSpan(offset)), offset);
            offset = checked(offset + PlayerItemSnapshotLoader.LegacyPerfectionSize);
        }
        return new(items, perfections);
    }
}
