using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace WolfConquer.Core.World;

// Complete file and canonical item identities only. Filtering and routing are later phases.
internal sealed class LegacyNativeItemLoadImage
{
    internal IReadOnlyList<Root> Roots { get; }
    internal IReadOnlyList<LegacyNativeClientItemPerfectionRecord> Perfections { get; }

    private LegacyNativeItemLoadImage(Root[] roots,
        LegacyNativeClientItemPerfectionRecord[] perfections)
    {
        Roots = Array.AsReadOnly(roots);
        Perfections = Array.AsReadOnly(perfections);
    }

    internal sealed class Root
    {
        internal LegacyNativeClientItemRecord Record { get; }
        internal IReadOnlyList<LegacyNativeClientItemRecord> Deposits { get; }

        internal Root(LegacyNativeClientItemRecord record, LegacyNativeClientItemRecord[] deposits)
        {
            Record = record;
            Deposits = Array.AsReadOnly(deposits);
        }
    }

    internal static LegacyNativeItemLoadImage Parse(ReadOnlySpan<byte> bytes)
    {
        if (!BitConverter.IsLittleEndian)
            throw new PlatformNotSupportedException("Native item images require the little-endian record layout.");

        var reader = new ImageReader(bytes);
        int rootCount = reader.ReadCount();
        int recordSize = Unsafe.SizeOf<LegacyNativeClientItemRecord>();
        reader.RequireRecords((uint)rootCount, recordSize, sizeof(int));
        var roots = new Root[rootCount];
        var identities = new HashSet<uint>();
        for (int index = 0; index < roots.Length; index++)
        {
            var record = reader.Read<LegacyNativeClientItemRecord>();
            AdmitIdentity(record.UID, identities);
            int reserved = checked((rootCount - index - 1) * recordSize + sizeof(int));
            int childCount = reader.RequireRecords(record.DepositeCount, recordSize, reserved);
            var deposits = new LegacyNativeClientItemRecord[childCount];
            for (int child = 0; child < deposits.Length; child++)
            {
                var deposited = reader.Read<LegacyNativeClientItemRecord>();
                if (deposited.DepositeCount != 0)
                    throw new InvalidDataException("Nested native item deposits have no supported file grammar.");
                AdmitIdentity(deposited.UID, identities);
                deposits[child] = deposited;
            }
            roots[index] = new Root(record, deposits);
        }

        int perfectionCount = reader.ReadCount();
        reader.RequireRecords((uint)perfectionCount,
            Unsafe.SizeOf<LegacyNativeClientItemPerfectionRecord>(), 0);
        var perfections = new LegacyNativeClientItemPerfectionRecord[perfectionCount];
        var perfectionIdentities = new HashSet<uint>();
        for (int index = 0; index < perfections.Length; index++)
        {
            var perfection = reader.Read<LegacyNativeClientItemPerfectionRecord>();
            if (perfection.ItemUID == 0 || !identities.Contains(perfection.ItemUID) ||
                !perfectionIdentities.Add(perfection.ItemUID))
                throw new InvalidDataException("Native perfection UIDs must uniquely reference serialized items.");
            perfections[index] = perfection;
        }
        if (reader.Remaining != 0)
            throw new InvalidDataException("Trailing bytes are not part of the native item-file grammar.");
        return new LegacyNativeItemLoadImage(roots, perfections);
    }

    private static void AdmitIdentity(uint uid, HashSet<uint> identities)
    {
        if (uid == 0 || !identities.Add(uid))
            throw new InvalidDataException("Native item UIDs must be nonzero and globally unique.");
    }

    private ref struct ImageReader
    {
        private readonly ReadOnlySpan<byte> bytes;
        private int offset;

        internal ImageReader(ReadOnlySpan<byte> bytes)
        {
            this.bytes = bytes;
            offset = 0;
        }

        internal int Remaining => bytes.Length - offset;

        internal int ReadCount()
        {
            if (Remaining < sizeof(int)) throw new EndOfStreamException("Incomplete native item count.");
            int count = BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(offset, sizeof(int)));
            offset += sizeof(int);
            if (count < 0) throw new InvalidDataException("Native item counts cannot be negative.");
            return count;
        }

        internal int RequireRecords(uint count, int recordSize, int reservedBytes)
        {
            if (Remaining < reservedBytes || count > (uint)((Remaining - reservedBytes) / recordSize))
                throw new EndOfStreamException("Native item record count exceeds the complete supplied input.");
            return checked((int)count);
        }

        internal T Read<T>() where T : unmanaged
        {
            int size = Unsafe.SizeOf<T>();
            if (Remaining < size) throw new EndOfStreamException("Incomplete native item record.");
            T record = MemoryMarshal.Read<T>(bytes.Slice(offset, size));
            offset += size;
            return record;
        }
    }
}
