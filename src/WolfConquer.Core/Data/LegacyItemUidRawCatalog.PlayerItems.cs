using System.Buffers.Binary;

namespace WolfConquer.Core.Data;

internal static partial class LegacyItemUidRawCatalog
{
    private const int DepositCountOffset = 136; // Exact actual DBItem layout.
    private const int MaximumRowsPerFile = ushort.MaxValue;

    private static void ReadPlayerItems(byte[] bytes, Scan scan)
    {
        var offset = 0;
        var topCount = ReadCount(bytes, ref offset);
        var ids = new HashSet<uint>();
        for (var index = 0; index < topCount; index++)
        {
            var deposits = ReadPhysicalRow(bytes, ref offset, scan, ids);
            if (deposits > MaximumRowsPerFile || deposits > (bytes.Length - offset) / PlayerItemSnapshotLoader.LegacyDbItemSize)
                throw new FormatException("Invalid deposited item count.");
            for (var child = 0U; child < deposits; child++)
                if (ReadPhysicalRow(bytes, ref offset, scan, ids) != 0)
                    throw new FormatException("Nested item deposits are unsupported by the actual save format.");
        }
        var perfectionCount = ReadCount(bytes, ref offset);
        if (perfectionCount > ids.Count ||
            (long)offset + (long)perfectionCount * PlayerItemSnapshotLoader.LegacyPerfectionSize != bytes.Length)
            throw new FormatException("Invalid perfection table or trailing item bytes.");
        var perfectionIds = new HashSet<uint>();
        for (var index = 0; index < perfectionCount; index++)
        {
            var uid = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, sizeof(uint)));
            if (!ids.Contains(uid) || !perfectionIds.Add(uid))
                throw new FormatException("Ambiguous or foreign perfection UID.");
            offset += PlayerItemSnapshotLoader.LegacyPerfectionSize;
        }
    }

    private static uint ReadPhysicalRow(byte[] bytes, ref int offset, Scan scan, HashSet<uint> fileIds)
    {
        if (offset > bytes.Length - PlayerItemSnapshotLoader.LegacyDbItemSize)
            throw new FormatException("Truncated raw item row.");
        var row = bytes.AsSpan(offset, PlayerItemSnapshotLoader.LegacyDbItemSize);
        var uid = BinaryPrimitives.ReadUInt32LittleEndian(row);
        var itemId = BinaryPrimitives.ReadUInt32LittleEndian(row[sizeof(uint)..]);
        if (itemId == 0 || fileIds.Count == MaximumRowsPerFile || !fileIds.Add(uid))
            throw new FormatException("Invalid raw physical item identity.");
        scan.AddPhysical(uid); // Includes rows the ordinary DTO would hide by location.
        offset += PlayerItemSnapshotLoader.LegacyDbItemSize;
        return BinaryPrimitives.ReadUInt32LittleEndian(row.Slice(DepositCountOffset, sizeof(uint)));
    }

    private static int ReadCount(byte[] bytes, ref int offset)
    {
        if (offset > bytes.Length - sizeof(int)) throw new FormatException("Missing raw record count.");
        var count = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(offset, sizeof(int)));
        offset += sizeof(int);
        if (count is < 0 or > MaximumRowsPerFile) throw new FormatException("Invalid raw record count.");
        return count;
    }
}
