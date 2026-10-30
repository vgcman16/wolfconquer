using System.Buffers.Binary;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyItemRepairPersistence
{
    // Pure raw projection. Price/permission belongs to the actual repair owner.
    internal static byte[] PrepareItemImage(byte[] raw, PlayerItemSnapshot before, PlayerItemSnapshot after,
        uint targetUid, LegacyItemRepairPayment payment)
    {
        ArgumentNullException.ThrowIfNull(raw);
        if (!Enum.IsDefined(payment) || targetUid == 0 ||
            !LegacyItemSaveGraph.TryCapture(before, out var beforeGraph) || !LegacyItemSaveGraph.TryCapture(after, out var afterGraph))
            throw Invalid("itemGraphInvalid");
        var actual = ReadSections(raw);
        var first = PlayerItemSnapshotLoader.ToLegacyBytes(before); var left = ReadSections(first);
        var second = PlayerItemSnapshotLoader.ToLegacyBytes(after); var right = ReadSections(second);
        if (!beforeGraph.HasBytes(first) || !afterGraph.HasBytes(second)) throw Invalid("capturedItemGraphChanged");
        var parsed = PlayerItemSnapshotLoader.ToLegacyBytes(PlayerItemSnapshotLoader.Parse(raw)); var modeled = ReadSections(parsed);
        if (!ModeledEqual(parsed, modeled, first, left) || actual.Items.Count != modeled.Items.Count ||
            actual.Items.Any(pair => !modeled.Items.TryGetValue(pair.Key, out var row) || row.Parent != pair.Value.Parent))
            throw Invalid("itemBaselineStaleOrHidden");
        if (!left.Items.TryGetValue(targetUid, out var target) || target.Parent != 0 ||
            !right.Items.TryGetValue(targetUid, out var repaired) || repaired.Parent != 0 ||
            !IsRepairTarget(before, targetUid)) throw Invalid("repairTargetNotOwnedRoot");
        var oldTarget = first.AsSpan(target.Offset, PlayerItemSnapshotLoader.LegacyDbItemSize);
        var newTarget = second.AsSpan(repaired.Offset, PlayerItemSnapshotLoader.LegacyDbItemSize);
        ushort durability = BinaryPrimitives.ReadUInt16LittleEndian(oldTarget[8..]);
        ushort maximum = BinaryPrimitives.ReadUInt16LittleEndian(oldTarget[10..]);
        if (maximum == 0 || durability >= maximum ||
            (payment == LegacyItemRepairPayment.Money ? durability == 0 : durability != 0) ||
            BinaryPrimitives.ReadUInt16LittleEndian(newTarget[8..]) != maximum ||
            !oldTarget[..8].SequenceEqual(newTarget[..8]) || !oldTarget[10..].SequenceEqual(newTarget[10..]))
            throw Invalid("repairTargetDeltaInvalid");
        var patched = raw.ToArray(); var removed = new HashSet<uint>(); long debit = 0;
        BinaryPrimitives.WriteUInt16LittleEndian(patched.AsSpan(actual.Items[targetUid].Offset + 8), maximum);
        foreach (var (uid, row) in left.Items)
        {
            if (uid == targetUid) continue;
            if (!right.Items.TryGetValue(uid, out var next))
            {
                if (payment != LegacyItemRepairPayment.Meteors || !IsMaterialRoot(first, row))
                    throw Invalid("unrelatedItemRemoved");
                ushort count = BinaryPrimitives.ReadUInt16LittleEndian(first.AsSpan(row.Offset + 52));
                if (count == 0) throw Invalid("emptyMaterialRemoved");
                debit = checked(debit + count); removed.Add(uid); continue;
            }
            if (next.Parent != row.Parent) throw Invalid("itemParentChanged");
            var oldRow = first.AsSpan(row.Offset, PlayerItemSnapshotLoader.LegacyDbItemSize);
            var newRow = second.AsSpan(next.Offset, PlayerItemSnapshotLoader.LegacyDbItemSize);
            if (oldRow.SequenceEqual(newRow)) continue;
            ushort oldStack = BinaryPrimitives.ReadUInt16LittleEndian(oldRow[52..]);
            ushort newStack = BinaryPrimitives.ReadUInt16LittleEndian(newRow[52..]);
            if (payment != LegacyItemRepairPayment.Meteors || !IsMaterialRoot(first, row) ||
                newStack == 0 || newStack >= oldStack || !oldRow[..52].SequenceEqual(newRow[..52]) ||
                !oldRow[54..].SequenceEqual(newRow[54..])) throw Invalid("unrelatedItemChanged");
            debit = checked(debit + oldStack - newStack);
            BinaryPrimitives.WriteUInt16LittleEndian(patched.AsSpan(actual.Items[uid].Offset + 52), newStack);
        }
        if (right.Items.Keys.Any(uid => !left.Items.ContainsKey(uid)) ||
            (payment == LegacyItemRepairPayment.Meteors ? debit != 5 : debit != 0)) throw Invalid("materialDebitInvalid");
        foreach (var (uid, offset) in left.Perfections)
        {
            if (removed.Contains(uid))
            { if (right.Perfections.ContainsKey(uid)) throw Invalid("removedMaterialPerfectionRetained"); continue; }
            if (!right.Perfections.TryGetValue(uid, out int next) ||
                !first.AsSpan(offset, PlayerItemSnapshotLoader.LegacyPerfectionSize)
                    .SequenceEqual(second.AsSpan(next, PlayerItemSnapshotLoader.LegacyPerfectionSize)))
                throw Invalid("perfectionChanged");
        }
        if (right.Perfections.Keys.Any(uid => !left.Perfections.ContainsKey(uid))) throw Invalid("perfectionAdded");
        var result = RemoveRows(patched, actual, removed);
        var resultModel = PlayerItemSnapshotLoader.ToLegacyBytes(PlayerItemSnapshotLoader.Parse(result));
        if (!ModeledEqual(resultModel, ReadSections(resultModel), second, right)) throw Invalid("itemProjectionMismatch");
        // Includes opaque raw perfection rows that have no modeled nonzero fields.
        ReadSections(result); return result;
    }

    private static bool IsRepairTarget(PlayerItemSnapshot before, uint uid) =>
        before.InventoryItems.Any(row => row.Uid == uid && row.Position == 0 && row.WarehouseId == 0) ||
        before.EquipmentItems.Any(row => row.Uid == uid);
    private static bool IsMaterialRoot(byte[] image, RawRow row)
    {
        if (row.Parent != 0 || row.Children != 0) return false;
        var bytes = image.AsSpan(row.Offset);
        uint id = BinaryPrimitives.ReadUInt32LittleEndian(bytes[4..]);
        return (id is 1088001 or 1088002) && BinaryPrimitives.ReadUInt16LittleEndian(bytes[12..]) == 0 &&
            BinaryPrimitives.ReadUInt32LittleEndian(bytes[56..]) == 0;
    }
}
