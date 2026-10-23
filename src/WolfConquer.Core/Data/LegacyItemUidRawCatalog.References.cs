using System.Buffers.Binary;
using WolfConquer.Core.World;

namespace WolfConquer.Core.Data;

internal static partial class LegacyItemUidRawCatalog
{
    private const int MailStrings = LegacyMailboxEntry.TitleByteLength + LegacyMailboxEntry.SenderNameByteLength +
        LegacyMailboxEntry.ContentByteLength;
    private const int MailItemOffset = MailStrings + sizeof(uint) + sizeof(ulong) + sizeof(uint);
    private const int MailRecordSize = MailStrings + (sizeof(uint) * 5) + sizeof(ulong) + sizeof(long);
    private const int DetainedRecordSize = 322; // Actual StructLayout.Size, not a marshaled bool projection.

    private static void ReadMailbox(byte[] bytes, Scan scan)
    {
        var offset = 0;
        var count = ReadCount(bytes, ref offset);
        if ((long)offset + ((long)count * MailRecordSize) != bytes.Length)
            throw new FormatException("Invalid mailbox record size.");
        var mailIds = new HashSet<uint>();
        for (var index = 0; index < count; index++)
        {
            var id = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset + MailStrings, sizeof(uint)));
            if (id == 0 || !mailIds.Add(id)) throw new FormatException("Ambiguous mailbox identity.");
            // A mailbox attachment is a reference to the same physical WH999 row,
            // not another item. Even expired references burn their known UID.
            scan.AddReference(BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset + MailItemOffset, sizeof(uint))));
            offset += MailRecordSize;
        }
    }

    private static void ReadDetained(byte[] bytes, string path, Scan scan)
    {
        var offset = 0;
        var count = ReadCount(bytes, ref offset);
        if ((long)offset + ((long)count * DetainedRecordSize) != bytes.Length)
            throw new FormatException("Invalid detained record size.");
        var recordIds = new HashSet<uint>();
        for (var index = 0; index < count; index++, offset += DetainedRecordSize)
        {
            var row = bytes.AsSpan(offset, DetainedRecordSize);
            var recordId = BinaryPrimitives.ReadUInt32LittleEndian(row);
            var itemUid = BinaryPrimitives.ReadUInt32LittleEndian(row[4..]);
            var itemId = BinaryPrimitives.ReadUInt32LittleEndian(row[8..]);
            var action = BinaryPrimitives.ReadUInt32LittleEndian(row[16..]);
            if (recordId == 0 || !recordIds.Add(recordId)) throw new FormatException("Duplicate detained record identity.");
            if (action == (uint)LegacyNativeMsgDetainedItem.ContainerType.RewardCps)
            { scan.AddReference(itemUid); continue; }
            if ((action != (uint)LegacyNativeMsgDetainedItem.ContainerType.DetainPage &&
                action != (uint)LegacyNativeMsgDetainedItem.ContainerType.ClaimPage) || itemUid == 0 || itemId == 0)
                throw new FormatException("Invalid detained physical identity.");
            if (scan.Escrow.TryGetValue(itemUid, out var previous))
            {
                // Source Save writes the same escrow row to Redeem AND Claim.
                // Only an exact cross-file alias is accepted; never deduplicate
                // different physical records or accept a repeated same-file row.
                if (Paths.Equals(previous.Path, path) || !row.SequenceEqual(previous.Bytes))
                    throw new FormatException("Conflicting detained UID alias.");
                continue;
            }
            scan.AddPhysical(itemUid);
            scan.Escrow.Add(itemUid, (path, row.ToArray()));
        }
    }
}
