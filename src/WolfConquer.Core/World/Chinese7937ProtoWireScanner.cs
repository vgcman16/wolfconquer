namespace WolfConquer.Core.World;

internal enum Chinese7937ProtoScope { Item2350, Echo2132, EchoChild2132 }
internal enum Chinese7937ProtoFailure { None, InputBounds, InvalidWire, ExtensionBounds }

internal readonly record struct Chinese7937ProtoRange(
    int FieldNumber, int WireType, int Offset, int Length, int DataOffset, int DataLength);

/// <summary>Finite structural delimiting for three established schemas; opaque groups never expose nested fields.</summary>
internal static class Chinese7937ProtoWireScanner
{
    internal const int MaximumPayloadBytes = ushort.MaxValue - 4;
    internal const int MaximumExtensions = 1024;
    internal const int MaximumGroupDepth = 16;

    internal static bool TryScan(ReadOnlySpan<byte> payload, Chinese7937ProtoScope scope,
        out Chinese7937ProtoRange[] ranges, out Chinese7937ProtoFailure failure)
    {
        ranges = [];
        var lastKnown = scope switch
        {
            Chinese7937ProtoScope.Item2350 => 43,
            Chinese7937ProtoScope.Echo2132 => 10,
            Chinese7937ProtoScope.EchoChild2132 => 2,
            _ => 0,
        };
        if (lastKnown == 0) { failure = Chinese7937ProtoFailure.InvalidWire; return false; }
        if (payload.Length > MaximumPayloadBytes)
        { failure = Chinese7937ProtoFailure.InputBounds; return false; }
        var reader = new Cursor(payload);
        List<Chinese7937ProtoRange> prepared = [];
        var extensions = 0;
        while (!reader.End)
        {
            var start = reader.Offset;
            if (!reader.TryReadTag(out var field, out var wire) || wire == 4)
            { failure = Chinese7937ProtoFailure.InvalidWire; return false; }
            if (field > lastKnown && ++extensions > MaximumExtensions)
            { failure = Chinese7937ProtoFailure.ExtensionBounds; return false; }
            if (!reader.TrySkipValue(field, wire, 0, out var dataOffset, out var dataLength, out failure)) return false;
            prepared.Add(new(field, wire, start, reader.Offset - start, dataOffset, dataLength));
        }
        ranges = prepared.ToArray();
        failure = Chinese7937ProtoFailure.None;
        return true;
    }

    private ref struct Cursor(ReadOnlySpan<byte> payload)
    {
        private readonly ReadOnlySpan<byte> bytes = payload;
        private int offset;
        internal readonly int Offset => offset;
        internal readonly bool End => offset == bytes.Length;

        internal bool TryReadTag(out int field, out int wire)
        {
            field = 0; wire = 0;
            if (!TryReadVarint(out var tag) || tag is 0 or > uint.MaxValue || tag >> 3 == 0) return false;
            field = (int)(tag >> 3);
            wire = (int)(tag & 7);
            return true;
        }

        internal bool TrySkipValue(int field, int wire, int groupDepth,
            out int dataOffset, out int dataLength, out Chinese7937ProtoFailure failure)
        {
            dataOffset = -1; dataLength = 0;
            failure = Chinese7937ProtoFailure.InvalidWire;
            switch (wire)
            {
                case 0: return TryReadVarint(out _);
                case 1: return TrySkipBytes(sizeof(ulong));
                case 2:
                    if (!TryReadVarint(out var length) || length > (ulong)(bytes.Length - offset)) return false;
                    dataOffset = offset; dataLength = (int)length;
                    return TrySkipBytes(dataLength);
                case 3:
                    if (groupDepth == MaximumGroupDepth)
                    { failure = Chinese7937ProtoFailure.ExtensionBounds; return false; }
                    return TrySkipGroup(field, groupDepth + 1, out failure);
                case 5: return TrySkipBytes(sizeof(uint));
                default: return false;
            }
        }

        private bool TrySkipGroup(int openingField, int groupDepth, out Chinese7937ProtoFailure failure)
        {
            failure = Chinese7937ProtoFailure.InvalidWire;
            while (!End)
            {
                if (!TryReadTag(out var field, out var wire)) return false;
                if (wire == 4) return field == openingField;
                if (!TrySkipValue(field, wire, groupDepth, out _, out _, out failure)) return false;
            }
            return false;
        }

        private bool TrySkipBytes(int length)
        {
            if (length < 0 || length > bytes.Length - offset) return false;
            offset += length;
            return true;
        }

        private bool TryReadVarint(out ulong value)
        {
            value = 0;
            for (var index = 0; index < 10; index++)
            {
                if (offset == bytes.Length) return false;
                var current = bytes[offset++];
                if (index == 9 && (current & 0xfe) != 0) return false;
                value |= (ulong)(current & 0x7f) << (index * 7);
                if ((current & 0x80) == 0) return true;
            }
            return false;
        }
    }
}
