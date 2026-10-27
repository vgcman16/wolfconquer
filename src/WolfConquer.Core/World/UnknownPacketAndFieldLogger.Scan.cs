namespace WolfConquer.Core.World;

public sealed partial class UnknownPacketAndFieldLogger
{
    // This reads a flat tag stream only. LEN bodies remain opaque; no UTF8,
    // submessage recursion, field names, enum labels or scalar values are emitted.
    private static ScanResult ScanDailyTask(ReadOnlySpan<byte> bytes, bool capped, int maxFields)
    {
        var fields = new List<FieldDescription>();
        var offset = 0;
        var unexpected = false;
        while (offset < bytes.Length)
        {
            if (fields.Count == maxFields) return new("field_limit", fields.ToArray(), true);
            var start = offset;
            var tagRead = ReadWireVarint(bytes, ref offset, out var tag);
            if (tagRead != WireRead.Complete)
                return new(ReadStatus(tagRead, capped), fields.ToArray(), true);
            if (tag is 0 or > uint.MaxValue || tag >> 3 == 0)
                return new("malformed", fields.ToArray(), true);
            var number = (int)(tag >> 3);
            var wire = (int)(tag & 7);
            var valueOffset = offset;
            var known = IsKnownDailyTaskField(number, wire);
            unexpected |= !known;
            int? dataLength = null;
            var read = SkipWireValue(bytes, wire, ref offset, out dataLength);
            fields.Add(new(number, wire, start, valueOffset, offset - start, dataLength, known));
            if (read != WireRead.Complete)
            {
                var status = wire is 3 or 4 ? "unsupported_group" : ReadStatus(read, capped);
                return new(status, fields.ToArray(), true);
            }
        }
        return new(capped ? "scan_limit" : "complete", fields.ToArray(), unexpected || capped);
    }

    private enum WireRead { Complete, Truncated, Malformed }

    private static string ReadStatus(WireRead read, bool capped) =>
        read == WireRead.Truncated ? capped ? "scan_limit" : "truncated" : "malformed";

    private static WireRead SkipWireValue(ReadOnlySpan<byte> bytes, int wire, ref int offset, out int? dataLength)
    {
        dataLength = null;
        if (wire == 0) return ReadWireVarint(bytes, ref offset, out _);
        if (wire == 1) return SkipWireBytes(bytes, ref offset, 8);
        if (wire == 5) return SkipWireBytes(bytes, ref offset, 4);
        if (wire != 2) return WireRead.Malformed;
        var read = ReadWireVarint(bytes, ref offset, out var length);
        if (read != WireRead.Complete) return read;
        if (length > int.MaxValue) return WireRead.Malformed;
        dataLength = (int)length;
        return SkipWireBytes(bytes, ref offset, (int)length);
    }

    private static WireRead SkipWireBytes(ReadOnlySpan<byte> bytes, ref int offset, int length)
    {
        if (length > bytes.Length - offset)
        {
            offset = bytes.Length;
            return WireRead.Truncated;
        }
        offset += length;
        return WireRead.Complete;
    }

    private static WireRead ReadWireVarint(ReadOnlySpan<byte> bytes, ref int offset, out ulong value)
    {
        value = 0;
        for (var index = 0; index < 10; index++)
        {
            if (offset == bytes.Length) return WireRead.Truncated;
            var next = bytes[offset++];
            if (index == 9 && (next & 0xfe) != 0) return WireRead.Malformed;
            value |= (ulong)(next & 0x7f) << (index * 7);
            if ((next & 0x80) == 0) return WireRead.Complete;
        }
        return WireRead.Malformed;
    }
}
