using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

// Data-image validation only. A valid image is not inventory ownership or a
// journal/native publication capability.
internal static class LegacyRelicFuseImages
{
    internal static bool TryCapture(PlayerItemSnapshot before, bool addsResult,
        out byte[] image, out string reason)
    {
        image = []; reason = "relicFuseItemGraphNotPersistable";
        try
        {
            if (!LegacyItemSaveGraph.TryCapture(before, out var saved)) return false;
            var bytes = PlayerItemSnapshotLoader.ToLegacyBytes(before);
            if (!saved.IsCurrent(before) || !saved.HasBytes(bytes) ||
                addsResult && bytes.Length > CharacterSnapshotFileBaseline.MaximumBytes - PlayerItemSnapshotLoader.LegacyDbItemSize)
                return false;
            var copied = PlayerItemSnapshotLoader.Parse(bytes);
            var sourceRows = WorldPacketHandlers.IndexRuneBagPhysicalItems(before);
            var copiedRows = WorldPacketHandlers.IndexRuneBagPhysicalItems(copied);
            if (addsResult && sourceRows.Count >= LegacyRelicFuseInputs.MaximumRows ||
                sourceRows.Count != copiedRows.Count ||
                !PlayerItemSnapshotLoader.ToLegacyBytes(copied).AsSpan().SequenceEqual(bytes) ||
                sourceRows.Any(pair => !copiedRows.TryGetValue(pair.Key, out var row) ||
                    !string.Equals(pair.Value.OwnerName, row.OwnerName, StringComparison.Ordinal) ||
                    !string.Equals(pair.Value.Signature, row.Signature, StringComparison.Ordinal)) ||
                !saved.IsCurrent(before)) return false;
            image = bytes;
            reason = string.Empty;
            return true;
        }
        catch (Exception error) when (error is ArgumentException or InvalidDataException or
            IndexOutOfRangeException or InvalidOperationException or OverflowException)
        { return false; }
    }
}
